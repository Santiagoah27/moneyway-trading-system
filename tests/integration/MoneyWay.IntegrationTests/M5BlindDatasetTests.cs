using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class M5BlindDatasetTests
{
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MoneyWay.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository unavailable.");
    }
    private static string Dataset => Path.Combine(Root(), "docs/data/strategy-lab/m5-blind-v1");
    private static string Frontend => Path.Combine(Root(), "src/frontend/moneyway-web/src/research/blind/data");
    private static JsonDocument Manifest() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Dataset, "dataset.json")));
    private static string Text(JsonElement e, string property) => e.GetProperty(property).GetString()!;
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public void ManifestPreservesUniqueSourceCandlesChronologicalGroupsAndUnlabelledResearchStatus()
    {
        using var manifest = Manifest(); var entries = manifest.RootElement.GetProperty("episodes").EnumerateArray().ToArray();
        Assert.Equal(8, entries.Length); Assert.Equal(8, entries.Select(e => Text(e, "id")).Distinct().Count());
        var identities = new HashSet<string>(); var count = 0;
        foreach (var e in entries)
        {
            var path = Path.Combine(Dataset, Text(e, "inputFile")); Assert.Equal(Text(e, "inputSha256"), Sha(path));
            using var input = JsonDocument.Parse(File.ReadAllBytes(path)); var request = input.RootElement;
            Assert.Equal("Unlabelled", Text(e, "labelAvailability")); Assert.False(e.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(e.GetProperty("operationalSignal").GetBoolean());
            Assert.Equal(Text(e, "seriesId"), Text(request, "seriesId")); Assert.Equal(Text(e, "direction"), Text(request, "direction"));
            var candles = request.GetProperty("candles").EnumerateArray().ToArray(); Assert.Equal(e.GetProperty("candleCount").GetInt32(), candles.Length);
            var anchor = candles.Single(c => c.GetProperty("openTimeUtc").GetDateTimeOffset() == e.GetProperty("anchorOpenUtc").GetDateTimeOffset());
            Assert.Equal(e.GetProperty("anchorPrice").GetDecimal(), anchor.GetProperty(Text(e, "direction") == "Ascending" ? "high" : "low").GetDecimal());
            DateTimeOffset? previous = null; var native = e.GetProperty("nativeOpenIdentities").EnumerateArray().ToArray();
            for (var i = 0; i < candles.Length; i++)
            {
                var c = candles[i]; var open = c.GetProperty("openTimeUtc").GetDateTimeOffset(); var close = c.GetProperty("closeTimeUtc").GetDateTimeOffset();
                Assert.Equal(TimeSpan.FromMinutes(5), close - open); if (previous is { } prior) Assert.Equal(prior, open); previous = close;
                Assert.True(close <= e.GetProperty("cutoffUtc").GetDateTimeOffset());
                if (Text(e, "dataOrigin") == "RetrospectiveDocumentary")
                {
                    Assert.True(identities.Add(Text(e, "providerId") + "/" + Text(e, "symbol") + "/" + open.ToString("O")));
                    Assert.Equal(open.UtcDateTime.AddHours(3).ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture), native[i].GetString()); count++;
                }
            }
        }
        Assert.Equal(96, count); Assert.Equal(count, identities.Count);
        DateTimeOffset End(string p) => entries.Where(e => Text(e, "partition") == p).Max(e => e.GetProperty("cutoffUtc").GetDateTimeOffset());
        DateTimeOffset Start(string p) => entries.Where(e => Text(e, "partition") == p).Min(e => e.GetProperty("startUtc").GetDateTimeOffset());
        Assert.True(End("Development") < Start("Validation")); Assert.True(End("Validation") < Start("HeldOut"));
        foreach (var p in new[] { "Development", "Validation", "HeldOut" }) Assert.Equal(new[] { "Ascending", "Descending" }, entries.Where(e => Text(e, "partition") == p).Select(e => Text(e, "direction")));
        Assert.Equal("InsufficientForIndependentGeneralizationClaims", Text(manifest.RootElement, "splitAssessment"));
    }

    [Fact]
    public void HeldOutInputsHaveNoComputedResultsAndAreNotIncludedInFrontendPayloads()
    {
        using var manifest = Manifest(); using var publicManifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Frontend, "manifest.json")));
        Assert.Equal(Sha(Path.Combine(Dataset, "dataset.json")), Text(publicManifest.RootElement, "datasetContentSha256"));
        Assert.Equal(6, publicManifest.RootElement.GetProperty("episodes").GetArrayLength());
        var allFrontend = string.Join("\n", Directory.GetFiles(Frontend, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var held in manifest.RootElement.GetProperty("episodes").EnumerateArray().Where(e => Text(e, "partition") == "HeldOut"))
        {
            Assert.Equal(JsonValueKind.Null, held.GetProperty("reportSha256").ValueKind);
            Assert.DoesNotContain(Text(held, "id"), allFrontend);
            Assert.DoesNotContain(Text(held, "startUtc"), allFrontend);
            Assert.False(File.Exists(Path.Combine(Frontend, "reports", Text(held, "id") + ".json")));
        }
        using var charts = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Frontend, "candles.json")));
        Assert.All(charts.RootElement.EnumerateObject(), e => Assert.Equal(new[] { "input", "evidenceCandles" }, e.Value.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(Dataset, "held-out"), "*.json").Length);
    }

    [Theory]
    [InlineData("m5-20260623-1300-ascending-v1")]
    [InlineData("m5-20260623-1400-descending-v1")]
    [InlineData("m5-20260625-1300-ascending-v1")]
    [InlineData("m5-20260625-1400-descending-v1")]
    [InlineData("previously-exposed-june29")]
    [InlineData("software-synthetic")]
    public void RevealResourcesReproduceUnchangedV1RunnerAndExactInputContext(string id)
    {
        using var manifest = Manifest(); var e = manifest.RootElement.GetProperty("episodes").EnumerateArray().Single(e => Text(e, "id") == id);
        var target = Path.Combine(Path.GetTempPath(), "moneyway-blind-report-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(0, new M5SwingLabCommand().Execute(["m5-swing-lab", "--input", Path.Combine(Dataset, Text(e, "inputFile")), "--output", target], output, error));
            var generated = Path.Combine(target, "comparison.json"); var resource = Path.Combine(Frontend, "reports", id + ".json");
            Assert.Equal(File.ReadAllBytes(resource), File.ReadAllBytes(generated)); Assert.Equal(Text(e, "reportSha256"), Sha(generated));
            using var report = JsonDocument.Parse(File.ReadAllBytes(generated)); using var charts = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Frontend, "candles.json")));
            var comparison = report.RootElement.GetProperty("comparison"); var chart = charts.RootElement.GetProperty(id);
            Assert.True(JsonElement.DeepEquals(comparison.GetProperty("input"), chart.GetProperty("input")));
            Assert.True(JsonElement.DeepEquals(comparison.GetProperty("evidenceCandles"), chart.GetProperty("evidenceCandles")));
            Assert.False(comparison.GetProperty("operationalSignal").GetBoolean()); Assert.False(comparison.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        }
        finally { if (Directory.Exists(target)) Directory.Delete(target, true); }
    }
}
