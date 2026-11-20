using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqSessionLiquidityResearchCommandTests : IDisposable
{
    private readonly string owned = Path.Combine(Path.GetTempPath(), "moneyway-session-tests-" + Guid.NewGuid().ToString("N"));
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) directory = directory.Parent;
        return directory!.FullName;
    }
    private static string ContextPath => Path.Combine(Root(), "src/frontend/moneyway-web/src/research/context/data/june23-context.json");
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static int Run(string input, string output) => new NasdaqSessionLiquidityResearchCommand().Execute(["nasdaq-session-liquidity", "--input", input, "--output", output], TextWriter.Null, TextWriter.Null);
    private static int Interactions(string input, string output) => new NasdaqSessionLiquidityResearchCommand().Execute(["nasdaq-session-liquidity", "--input", input, "--output", output, "--analyze-interaction"], TextWriter.Null, TextWriter.Null);

    [Fact]
    public void AuthenticInteractionReportReproducesWithFirstOhlcObservationAndNoRetroactiveFinalClassification()
    {
        var hash = Hash(ContextPath); var target = Path.Combine(owned, "interaction"); Assert.Equal(0, Interactions(ContextPath, target));
        var bytes = File.ReadAllBytes(Path.Combine(target, "london-asia-interaction.json"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root(), "src/frontend/moneyway-web/src/research/context/data/june23-london-asia-interaction.json")), bytes);
        Assert.False(File.Exists(Path.Combine(target, "session-liquidity.json"))); Assert.DoesNotContain((byte)'\r', bytes);
        using var json = JsonDocument.Parse(bytes); var root = json.RootElement;
        Assert.Equal(hash, root.GetProperty("inputSha256").GetString()); Assert.False(root.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(root.GetProperty("operationalSignal").GetBoolean());
        var frames = root.GetProperty("frames").EnumerateArray().ToArray();
        JsonElement At(int hour) => frames.Single(f => f.GetProperty("cutoffUtc").GetDateTimeOffset() == new DateTimeOffset(2026, 6, 23, hour, 0, 0, TimeSpan.Zero)).GetProperty("interaction");
        var first = At(8); Assert.Equal("Partial", first.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, first.GetProperty("completedClassification").ValueKind);
        Assert.Single(first.GetProperty("observations").EnumerateArray()); Assert.Equal(61.4m, first.GetProperty("observedLowerMagnitude").GetDecimal());
        var completed = At(13); Assert.Equal("AsiaLowOnlyPenetrated", completed.GetProperty("completedClassification").GetString());
        Assert.Equal(0m, completed.GetProperty("completedUpperMagnitude").GetDecimal()); Assert.Equal(242.5m, completed.GetProperty("completedLowerMagnitude").GetDecimal());
        Assert.Equal(6, completed.GetProperty("observations").GetArrayLength()); Assert.Equal(JsonValueKind.Null, completed.GetProperty("firstUpperPenetration").ValueKind);
        foreach (var field in new[] { "firstLowerPenetration", "firstLowerCloseBeyond" })
        {
            var candle = completed.GetProperty(field).GetProperty("candle");
            Assert.Equal(new DateTimeOffset(2026, 6, 23, 7, 0, 0, TimeSpan.Zero), candle.GetProperty("openTimeUtc").GetDateTimeOffset());
            Assert.Equal(new DateTimeOffset(2026, 6, 23, 8, 0, 0, TimeSpan.Zero), candle.GetProperty("closeTimeUtc").GetDateTimeOffset());
            Assert.Equal(29734m, candle.GetProperty("open").GetDecimal()); Assert.Equal(29818.6m, candle.GetProperty("high").GetDecimal());
            Assert.Equal(29653.8m, candle.GetProperty("low").GetDecimal()); Assert.Equal(29672.6m, candle.GetProperty("close").GetDecimal());
        }
        Assert.Equal("AsiaUnavailable", At(6).GetProperty("status").GetString()); Assert.Empty(At(7).GetProperty("observations").EnumerateArray());
        var second = Path.Combine(owned, "interaction-repeat"); Assert.Equal(0, Interactions(ContextPath, second)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(second, "london-asia-interaction.json")));
        Assert.Equal(2, Interactions(ContextPath, target)); Assert.Equal(hash, Hash(ContextPath));
        Assert.DoesNotContain("2026-06-25T", System.Text.Encoding.UTF8.GetString(bytes)); Assert.DoesNotContain("2026-06-30T", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("instrument")]
    [InlineData("timeframe")]
    public void InteractionModeRejectsSourceSubstitution(string issue)
    {
        Directory.CreateDirectory(owned); var root = JsonNode.Parse(File.ReadAllText(ContextPath))!; var candle = root["series"]!["H1"]!["candles"]![0]!;
        if (issue == "provider") candle["providerId"]!["value"] = "other";
        if (issue == "instrument") candle["symbol"]!["value"] = "USTEC";
        if (issue == "timeframe") candle["timeframe"]!["amount"] = 4;
        var input = Path.Combine(owned, "wrong.json"); File.WriteAllText(input, root.ToJsonString()); var target = Path.Combine(owned, "none");
        Assert.Equal(2, Interactions(input, target)); Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void AuthenticDocumentaryCalculationReproducesExactArtifactAndCompatibleExplicitSources()
    {
        var original = Hash(ContextPath); var target = Path.Combine(owned, "first"); Assert.Equal(0, Run(ContextPath, target));
        var actual = File.ReadAllBytes(Path.Combine(target, "session-liquidity.json")); Assert.DoesNotContain((byte)'\r', actual);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root(), "src/frontend/moneyway-web/src/research/context/data/june23-session-liquidity.json")), actual);
        using var doc = JsonDocument.Parse(actual); var root = doc.RootElement;
        Assert.Equal(original, root.GetProperty("inputSha256").GetString()); Assert.False(root.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(root.GetProperty("operationalSignal").GetBoolean());
        var atEight = root.GetProperty("frames").EnumerateArray().Single(f => f.GetProperty("cutoffUtc").GetDateTimeOffset() == new DateTimeOffset(2026, 6, 23, 13, 0, 0, TimeSpan.Zero));
        var sessions = atEight.GetProperty("sessions").EnumerateArray().ToArray();
        Assert.Equal(new decimal[] { 30463.9m, 29818.6m }, sessions.Select(s => s.GetProperty("high").GetDecimal()));
        Assert.Equal(new decimal[] { 29715.2m, 29472.7m }, sessions.Select(s => s.GetProperty("low").GetDecimal()));
        Assert.Equal(new[] { 9, 6 }, sessions.Select(s => s.GetProperty("sourceCount").GetInt32()));
        Assert.All(sessions, s => { Assert.Equal("Complete", s.GetProperty("coverageStatus").GetString()); Assert.Equal("AuditedExactH1", s.GetProperty("sourceAuthority").GetString()); });
        foreach (var comparison in root.GetProperty("nonAuthoritativeComparisons").EnumerateArray())
        {
            var refs = comparison.GetProperty("sessions").EnumerateArray().ToArray();
            Assert.Equal(sessions.Select(s => s.GetProperty("high").GetDecimal()), refs.Select(s => s.GetProperty("high").GetDecimal()));
            Assert.Equal(sessions.Select(s => s.GetProperty("low").GetDecimal()), refs.Select(s => s.GetProperty("low").GetDecimal()));
            Assert.All(refs, s => Assert.Equal("ExplicitNonAuthoritativeComparison", s.GetProperty("sourceAuthority").GetString()));
        }
        var second = Path.Combine(owned, "second"); Assert.Equal(0, Run(ContextPath, second)); Assert.Equal(actual, File.ReadAllBytes(Path.Combine(second, "session-liquidity.json")));
        Assert.Equal(2, Run(ContextPath, target)); Assert.Equal(original, Hash(ContextPath));
        Assert.DoesNotContain("2026-06-25T", System.Text.Encoding.UTF8.GetString(actual)); Assert.DoesNotContain("2026-06-30T", System.Text.Encoding.UTF8.GetString(actual));
    }

    [Theory]
    [InlineData("instrument")]
    [InlineData("provider")]
    [InlineData("duplicate")]
    [InlineData("ohlc")]
    [InlineData("scope")]
    public void InvalidDocumentarySourcesAreNotPublished(string invalid)
    {
        Directory.CreateDirectory(owned); var node = JsonNode.Parse(File.ReadAllText(ContextPath))!;
        var bars = node["series"]!["H1"]!["candles"]!.AsArray();
        if (invalid == "instrument") bars[0]!["symbol"]!["value"] = "USTEC";
        if (invalid == "provider") bars[0]!["providerId"]!["value"] = "unrelated";
        if (invalid == "duplicate") bars.Add(bars[0]!.DeepClone());
        if (invalid == "ohlc") bars[0]!["low"] = 999999;
        if (invalid == "scope") node["operationalSignal"] = true;
        var path = Path.Combine(owned, "invalid.json"); File.WriteAllText(path, node.ToJsonString()); var target = Path.Combine(owned, "unpublished");
        Assert.Equal(2, Run(path, target)); Assert.False(Directory.Exists(target));
    }
    public void Dispose() { if (Directory.Exists(owned)) Directory.Delete(owned, true); }
}
