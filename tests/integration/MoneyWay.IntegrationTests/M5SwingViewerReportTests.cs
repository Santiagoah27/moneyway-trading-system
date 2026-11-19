using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class M5SwingViewerReportTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository unavailable.");
    }

    [Theory]
    [InlineData("docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json", "synthetic-v1", "Synthetic")]
    [InlineData("docs/data/strategy-lab/m5-swing-v2/june29-documentary.json", "june29-v1", "RetrospectiveDocumentary")]
    public void BundledViewerReportsAreExactExistingRunnerOutputs(string fixture, string name, string origin)
    {
        var root = Root(); var input = Path.Combine(root, fixture);
        var target = Path.Combine(Path.GetTempPath(), "moneyway-viewer-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(0, new M5SwingLabCommand().Execute(["m5-swing-lab", "--input", input, "--output", target], output, error));
            var actual = File.ReadAllBytes(Path.Combine(target, "comparison.json"));
            Assert.DoesNotContain((byte)'\r', actual); // Stable generated resources on Windows and Linux.
            var committed = File.ReadAllBytes(Path.Combine(root, $"src/frontend/moneyway-web/src/research/reports/{name}.json"));
            Assert.Equal(committed, actual);
            using var json = JsonDocument.Parse(actual); var report = json.RootElement.GetProperty("comparison");
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input))), json.RootElement.GetProperty("inputSha256").GetString());
            Assert.Equal(origin, report.GetProperty("input").GetProperty("dataOrigin").GetString());
            Assert.False(report.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(report.GetProperty("operationalSignal").GetBoolean());
            Assert.All(report.GetProperty("results").EnumerateArray(), r => Assert.False(r.GetProperty("mentorRuleConfirmed").GetBoolean()));
            if (origin == "RetrospectiveDocumentary")
            {
                Assert.Equal(24, report.GetProperty("evidenceCandles").GetArrayLength());
                Assert.All(report.GetProperty("results").EnumerateArray(), r =>
                {
                    Assert.Equal("AbstainedNoPullbackBeforeContinuation", r.GetProperty("status").GetString());
                    Assert.Equal(JsonValueKind.Null, r.GetProperty("confirmationAtUtc").ValueKind);
                });
            }
        }
        finally { if (Directory.Exists(target)) Directory.Delete(target, true); }
    }

    [Fact]
    public void HistoricalFixturePreservesOriginalNativeRowsAndExplicitDocumentaryMappingExactly()
    {
        var root = Root();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/data/strategy-lab/m5-swing-v2/june29-documentary.json")));
        var rows = File.ReadAllLines(Path.Combine(root, "docs/data/strategy-lab/m5-swing-v2/june29-native-source.tsv")).Skip(1).ToArray();
        Assert.Equal(File.ReadAllBytes(Path.Combine(root, "docs/data/strategy-lab/m5-swing-v2/june29-native-source.tsv")),
            File.ReadAllBytes(Path.Combine(root, "src/frontend/moneyway-web/src/research/reports/june29-native-source.tsv")));
        var candles = json.RootElement.GetProperty("candles").EnumerateArray().ToArray();
        Assert.Equal(24, rows.Length); Assert.Equal(rows.Length, candles.Length);
        DateTimeOffset? previousClose = null;
        for (var i = 0; i < rows.Length; i++)
        {
            var raw = rows[i].Split('\t'); var candle = candles[i];
            var native = DateTime.ParseExact(raw[0] + " " + raw[1], "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
            var open = candle.GetProperty("openTimeUtc").GetDateTimeOffset(); var close = candle.GetProperty("closeTimeUtc").GetDateTimeOffset();
            Assert.Equal(native, open.UtcDateTime.AddHours(3)); // documentary mapping, not authenticated UTC availability
            Assert.Equal(TimeSpan.FromMinutes(5), close - open);
            if (previousClose is { } previous) Assert.Equal(previous, open);
            previousClose = close;
            foreach (var (property, position) in new[] { ("open", 2), ("high", 3), ("low", 4), ("close", 5), ("volume", 6) })
                Assert.Equal(decimal.Parse(raw[position], CultureInfo.InvariantCulture), candle.GetProperty(property).GetDecimal());
        }
    }

    [Fact]
    public void HistoricalEarlierCutoffsRemainIdenticalWithLaterEpisodeCandlesPresent()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs/data/strategy-lab/m5-swing-v2/june29-documentary.json")));
        var request = json.RootElement; var provider = new MarketDataProviderId(request.GetProperty("providerId").GetString()!); var symbol = new MarketSymbol("US100");
        var tf = CompareM5SwingHypothesesUseCase.M5;
        var candles = request.GetProperty("candles").EnumerateArray().Select(c => new Candle(provider, symbol, tf,
            c.GetProperty("openTimeUtc").GetDateTimeOffset(), c.GetProperty("closeTimeUtc").GetDateTimeOffset(),
            c.GetProperty("open").GetDecimal(), c.GetProperty("high").GetDecimal(), c.GetProperty("low").GetDecimal(), c.GetProperty("close").GetDecimal(), c.GetProperty("volume").GetDecimal())).ToArray();
        var input = new M5SwingExperimentInput("documentary-prefix-test", new(provider, symbol, tf, candles), M5ImpulseDirection.Ascending,
            candles[^1].CloseTimeUtc, new("explicit-research-anchor", provider, symbol, tf, candles[0].OpenTimeUtc, candles[0].CloseTimeUtc, "Research assumption; not authenticated historical availability"), M5ResearchDataOrigin.RetrospectiveDocumentary, "Real documentary episode; not causal admission");
        var runner = new CompareM5SwingHypothesesUseCase();
        foreach (var bar in candles)
        {
            var full = input with { CutoffUtc = bar.CloseTimeUtc };
            var prefix = full with { Series = new(provider, symbol, tf, candles.Where(c => c.CloseTimeUtc <= bar.CloseTimeUtc)) };
            Assert.Equal(JsonSerializer.Serialize(runner.Execute(prefix)), JsonSerializer.Serialize(runner.Execute(full)));
        }
    }
}
