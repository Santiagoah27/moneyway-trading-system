using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqContextResearchTests
{
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MoneyWay.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository unavailable.");
    }
    private static string Data => Path.Combine(Root(), "src/frontend/moneyway-web/src/research/context/data");
    private static JsonDocument Context() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Data, "june23-context.json")));
    private static JsonDocument Evidence() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Data, "development-review.json")));
    private static string Text(JsonElement e, string key) => e.GetProperty(key).GetString()!;
    private static string Sha(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    [Fact]
    public void OriginalTimeframesRetainSourceIdentityNativeIntervalsOhlcAndDocumentaryScope()
    {
        using var context = Context(); var c = context.RootElement;
        using var package = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), "docs/data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json")));
        Assert.Equal("America/Bogota", Text(c, "timeZone")); Assert.Equal("US100", Text(c, "symbol"));
        Assert.False(c.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(c.GetProperty("operationalSignal").GetBoolean());
        foreach (var (key, count, amount, unit) in new[] { ("H4", 10, 4, TimeframeUnit.Hour), ("H1", 41, 1, TimeframeUnit.Hour), ("M5", 498, 5, TimeframeUnit.Minute), ("M1", 2490, 1, TimeframeUnit.Minute) })
        {
            var frame = c.GetProperty("series").GetProperty(key); var tf = new Timeframe(amount, unit);
            Assert.False(frame.GetProperty("derived").GetBoolean()); Assert.Equal(count, frame.GetProperty("count").GetInt32());
            var provider = new MarketDataProviderId(Text(c, "providerId")); var symbol = new MarketSymbol("US100");
            var candles = frame.GetProperty("candles").EnumerateArray().Select(bar =>
            {
                var open = bar.GetProperty("openTimeUtc").GetDateTimeOffset(); var close = bar.GetProperty("closeTimeUtc").GetDateTimeOffset();
                Assert.Equal(TimeSpan.FromMinutes(frame.GetProperty("intervalMinutes").GetInt32()), close - open);
                Assert.Equal(provider.Value, Text(bar.GetProperty("providerId"), "value")); Assert.Equal(symbol.Value, Text(bar.GetProperty("symbol"), "value"));
                Assert.Equal(open.UtcDateTime.AddHours(3).ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture), Text(bar, "nativeOpenTime"));
                Assert.True(close <= c.GetProperty("maximumCutoffUtc").GetDateTimeOffset());
                return new Candle(provider, symbol, tf, open, close, bar.GetProperty("open").GetDecimal(), bar.GetProperty("high").GetDecimal(), bar.GetProperty("low").GetDecimal(), bar.GetProperty("close").GetDecimal(), bar.GetProperty("volume").GetDecimal());
            }).ToArray();
            var series = new CandleSeries(provider, symbol, tf, candles); Assert.Equal(count, series.Count);
            foreach (var kind in new[] { "raw", "normalized" })
            {
                var artifact = package.RootElement.GetProperty("artifacts").EnumerateArray().Single(a => Text(a, "id") == kind + "-" + Text(frame, "sourceTimeframe"));
                Assert.Equal(Text(artifact, "sha256"), Text(frame, kind + "Sha256")); Assert.Equal(Text(artifact, "relativePath"), Text(frame, kind + "RelativePath"));
            }
        }
    }

    [Fact]
    public void MissingSourceRangesAreRetainedWithoutInventedSessionsOrUnfinishedH4Candles()
    {
        using var context = Context(); var c = context.RootElement;
        Assert.Equal("UnresolvedForContextualResearch_NoAsiaLondonExtrema", Text(c, "sessionBoundariesStatus"));
        Assert.Equal("08:30–11:30 America/Bogota", Text(c, "windowInstruction")); Assert.Equal("[08:30,11:00) America/Bogota", Text(c, "windowRuntime"));
        Assert.Empty(c.GetProperty("series").GetProperty("H4").GetProperty("sourceGaps").EnumerateArray());
        Assert.Equal("2026-06-23T13:00:00Z", Text(c.GetProperty("series").GetProperty("H4"), "lastClosedUtc"));
        foreach (var key in new[] { "H1", "M5", "M1" })
        {
            var gaps = c.GetProperty("series").GetProperty(key).GetProperty("sourceGaps").EnumerateArray().ToArray(); Assert.Equal(2, gaps.Length);
            Assert.Equal("2026-06-21T21:00:00Z", Text(gaps[0], "startUtc")); Assert.Equal("2026-06-21T22:00:00Z", Text(gaps[0], "endUtc"));
            Assert.Equal("2026-06-22T21:00:00Z", Text(gaps[1], "startUtc")); Assert.Equal("2026-06-22T22:00:00Z", Text(gaps[1], "endUtc"));
        }
    }

    [Fact]
    public void ActualOriginalHumanLabelsMatchImmutableReportsAndExplainTheConfirmingCandleDifference()
    {
        using var evidence = Evidence(); using var context = Context();
        Assert.Equal("8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861", Text(evidence.RootElement, "sourceSha256"));
        var scores = new Dictionary<string, int>(); var reviews = evidence.RootElement.GetProperty("reviews").EnumerateArray().ToArray(); Assert.Equal(2, reviews.Length);
        foreach (var r in reviews)
        {
            var id = Text(r, "episodeId"); var reportPath = Path.Combine(Root(), "src/frontend/moneyway-web/src/research/blind/data/reports", id + ".json");
            Assert.Equal(Text(r, "reportSha256"), Sha(reportPath)); Assert.Equal("SelfDeclaredIndependent", Text(r, "originalExposure"));
            var label = r.GetProperty("label"); Assert.Equal(1, label.GetProperty("revision").GetInt32()); Assert.Equal("IndependentRetrospectiveResearch", Text(label, "classification"));
            Assert.Equal(JsonValueKind.Null, label.GetProperty("confirmationCloseUtc").ValueKind); Assert.Equal(JsonValueKind.Null, label.GetProperty("confirmationOpenUtc").ValueKind);
            using var report = JsonDocument.Parse(File.ReadAllBytes(reportPath)); Assert.Equal(Text(r, "inputSha256"), Text(report.RootElement, "inputSha256"));
            var selected = label.GetProperty("selectedOpens").EnumerateArray().Select(t => t.GetDateTimeOffset()).ToArray();
            var originalBar = context.RootElement.GetProperty("series").GetProperty("M5").GetProperty("candles").EnumerateArray().Single(b => b.GetProperty("openTimeUtc").GetDateTimeOffset() == selected[0]);
            Assert.Equal(label.GetProperty("price").GetDecimal(), originalBar.GetProperty(Text(label, "extreme") == "Low" ? "low" : "high").GetDecimal());
            foreach (var algorithm in report.RootElement.GetProperty("comparison").GetProperty("results").EnumerateArray())
            {
                var algorithmId = Text(algorithm.GetProperty("algorithm"), "algorithmId"); scores.TryAdd(algorithmId, 0);
                var refs = algorithm.GetProperty("referenceCandles").EnumerateArray().Select(b => b.GetProperty("openTimeUtc").GetDateTimeOffset()).ToArray();
                var delta = algorithm.GetProperty("proposedReferencePrice").GetDecimal() - label.GetProperty("price").GetDecimal();
                if (delta == 0 && refs.SequenceEqual(selected)) scores[algorithmId]++;
                if (Text(label, "extreme") == "Low")
                {
                    Assert.Equal(59.5m, Math.Abs(delta)); Assert.Equal(29441.2m, algorithm.GetProperty("proposedReferencePrice").GetDecimal());
                    Assert.Equal(selected[0], algorithm.GetProperty("continuationCandle").GetProperty("openTimeUtc").GetDateTimeOffset());
                    Assert.DoesNotContain(selected[0], refs); Assert.Equal(DateTimeOffset.Parse("2026-06-23T13:35:00Z"), algorithm.GetProperty("confirmationAtUtc").GetDateTimeOffset());
                }
                else { Assert.Equal(0, delta); Assert.Equal(DateTimeOffset.Parse("2026-06-23T14:40:00Z"), algorithm.GetProperty("confirmationAtUtc").GetDateTimeOffset()); }
            }
        }
        Assert.Equal(4, scores.Count); Assert.All(scores.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public void PublicProjectionExcludesPrivateIdentityAndContextExcludesValidationAndHeldOutDates()
    {
        var projected = Path.Combine(Data, "development-review.json");
        Assert.Equal(File.ReadAllBytes(Path.Combine(Root(), "docs/data/strategy-lab/context-v1/development-review.json")), File.ReadAllBytes(projected));
        using var evidence = Evidence(); Assert.False(evidence.RootElement.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        foreach (var r in evidence.RootElement.GetProperty("reviews").EnumerateArray())
        {
            Assert.Equal("human-reviewer-1", Text(r, "reviewerAlias")); var label = r.GetProperty("label");
            Assert.False(label.TryGetProperty("reviewer", out _)); Assert.False(label.TryGetProperty("reviewedAtDeviceUtc", out _)); Assert.False(r.TryGetProperty("revealedAtDeviceUtc", out _));
        }
        var data = File.ReadAllText(Path.Combine(Data, "june23-context.json"));
        Assert.DoesNotContain("2026-06-25T", data); Assert.DoesNotContain("2026-06-30T", data); Assert.DoesNotContain("2026-06-23T17:00:00Z", data);
    }
}
