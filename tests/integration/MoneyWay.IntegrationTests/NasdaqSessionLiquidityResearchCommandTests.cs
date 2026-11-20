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
    private static int Tracking(string input, string output) => new NasdaqSessionLiquidityResearchCommand().Execute(["nasdaq-session-liquidity", "--input", input, "--output", output, "--track-context"], TextWriter.Null, TextWriter.Null);
    private static int ContextSc(string input, string output) => new NasdaqSessionLiquidityResearchCommand().Execute(["nasdaq-session-liquidity", "--input", input, "--output", output, "--compare-context-sc"], TextWriter.Null, TextWriter.Null);

    [Fact]
    public void FrozenIndependentReviewSourceHasCompleteSessionsAndPreservesNativeIdentityWithoutLabels()
    {
        var data = Path.Combine(Root(), "src/frontend/moneyway-web/src/research/context/data/contextual-validation");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(data, "manifest.json")));
        using var context = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(data, "context.json")));
        var m = manifest.RootElement; var c = context.RootElement;
        Assert.Equal("2026-06-22", m.GetProperty("selectedDay").GetString());
        Assert.Equal(Hash(Path.Combine(data, "context.json")), m.GetProperty("inputSha256").GetString());
        Assert.Equal("US100", c.GetProperty("symbol").GetString()); Assert.Equal("America/Bogota", c.GetProperty("timeZone").GetString());
        Assert.Equal(new[] { "2026-06-22", "2026-06-24", "2026-06-26" }, m.GetProperty("inventory").EnumerateArray().Where(i => i.GetProperty("eligible").GetBoolean()).Select(i => i.GetProperty("day").GetString()));
        Assert.Equal("Validation", m.GetProperty("excluded").GetProperty("2026-06-25").GetString()); Assert.Equal("HeldOut", m.GetProperty("excluded").GetProperty("2026-06-30").GetString());
        foreach (var (tf, count) in new[] { ("H4", 4), ("H1", 18), ("M5", 222), ("M1", 1110) })
        {
            var series = c.GetProperty("series").GetProperty(tf); Assert.Equal(count, series.GetProperty("count").GetInt32()); Assert.False(series.GetProperty("derived").GetBoolean());
            foreach (var row in series.GetProperty("candles").EnumerateArray())
            {
                Assert.Equal("oanda-global-markets-mt5-demo", row.GetProperty("providerId").GetProperty("value").GetString());
                var open = row.GetProperty("openTimeUtc").GetDateTimeOffset(); Assert.True(open < new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero));
                Assert.Equal(open.AddHours(3).ToString("yyyy.MM.dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), row.GetProperty("nativeOpenTime").GetString());
            }
        }
        Assert.False(c.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(c.GetProperty("operationalSignal").GetBoolean());
        Assert.False(c.TryGetProperty("labels", out _));
    }

    [Fact]
    public void FrozenReviewUsesUnchangedPipelineAndReproducesEveryBoundReportWithoutReservedDateAdmission()
    {
        var data = Path.Combine(Root(), "src/frontend/moneyway-web/src/research/context/data/contextual-validation");
        var input = Path.Combine(data, "context.json"); using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(data, "manifest.json")));
        foreach (var (mode, expected, actual) in new[] { ("sc", "comparison", "contextual-swing-sc"), ("sessions", "sessions", "session-liquidity"), ("interaction", "interaction", "london-asia-interaction") })
        {
            var target = Path.Combine(owned, "review-" + mode);
            Assert.Equal(0, mode == "sc" ? ContextSc(input, target) : mode == "sessions" ? Run(input, target) : Interactions(input, target));
            Assert.Equal(File.ReadAllBytes(Path.Combine(data, expected + ".json")), File.ReadAllBytes(Path.Combine(target, actual + ".json")));
            Assert.Equal(Hash(Path.Combine(data, expected + ".json")), manifest.RootElement.GetProperty("reportHashes").GetProperty(expected).GetString());
        }
        Directory.CreateDirectory(owned); var altered = JsonNode.Parse(File.ReadAllText(input))!;
        foreach (var day in new[] { "2026-06-25", "2026-06-30" })
        {
            altered["tradingDay"] = day; altered["id"] = "nasdaq-context-" + day.Replace("-", "") + "-review-v1";
            var path = Path.Combine(owned, "reserved.json"); File.WriteAllText(path, altered.ToJsonString());
            Assert.Equal(2, ContextSc(path, Path.Combine(owned, "must-not-publish")));
        }
    }

    [Fact]
    public void ContextScComparisonReproducesActualEventsFrozenReferencesAndConservativeOutcomes()
    {
        var originalHash = Hash(ContextPath); var target = Path.Combine(owned, "context-sc"); Assert.Equal(0, ContextSc(ContextPath, target));
        var golden = Path.Combine(Root(), "docs/data/strategy-lab/contextual-sc-v1"); var bytes = File.ReadAllBytes(Path.Combine(target, "contextual-swing-sc.json"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(golden, "june23.json")), bytes);
        Assert.Equal(File.ReadAllBytes(Path.Combine(golden, "june23.md")), File.ReadAllBytes(Path.Combine(target, "contextual-swing-sc.md")));
        using var json = JsonDocument.Parse(bytes); var comparison = json.RootElement.GetProperty("comparison");
        using var tracking = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), "docs/data/strategy-lab/liquidity-context-v1/june23.json")));
        Assert.True(JsonElement.DeepEquals(tracking.RootElement.GetProperty("tracking"), comparison.GetProperty("tracking")));
        Assert.Equal(originalHash, json.RootElement.GetProperty("inputSha256").GetString()); Assert.False(comparison.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(comparison.GetProperty("operationalSignal").GetBoolean());
        var rows = comparison.GetProperty("comparisons").EnumerateArray().ToArray(); Assert.Equal(72, rows.Length);
        Assert.Equal(3, rows.Count(r => r.GetProperty("status").GetString() == "ExperimentalScObserved"));
        Assert.Equal(8, rows.Count(r => r.GetProperty("status").GetString() == "AbstainedSameCandleBreak"));
        Assert.Equal(24, rows.Count(r => r.GetProperty("status").GetString() == "AbstainedConfirmedStructure"));
        Assert.Equal(36, rows.Count(r => r.GetProperty("status").GetString() == "NoQualifyingEventObserved"));
        Assert.Equal(1, rows.Count(r => r.GetProperty("status").GetString() == "AbstainedPreparationRangeUnavailable"));
        var ll = rows.Where(r => r.GetProperty("eventSelection").GetProperty("liquidityReference").GetProperty("category").GetString() == "LL").ToArray();
        var detected = ll.Where(r => r.GetProperty("status").GetString() == "ExperimentalScObserved").ToArray(); Assert.Equal(2, detected.Length);
        foreach (var row in detected)
        {
            Assert.Equal(29510.2m, row.GetProperty("structuralReference").GetProperty("price").GetDecimal());
            Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 35, 0, TimeSpan.Zero), row.GetProperty("subsequentSc").GetProperty("asOfUtc").GetDateTimeOffset());
            Assert.Equal(29532.7m, row.GetProperty("subsequentSc").GetProperty("candle").GetProperty("close").GetDecimal());
        }
        var events = comparison.GetProperty("eventSelections").EnumerateArray().Where(e => e.GetProperty("event").ValueKind != JsonValueKind.Null).ToArray(); Assert.Equal(6, events.Length);
        foreach (var e in events.Where(e => !e.GetProperty("policyId").GetString()!.EndsWith("E1-V1")))
            Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 30, 0, TimeSpan.Zero), e.GetProperty("event").GetProperty("candle").GetProperty("openTimeUtc").GetDateTimeOffset());
        var groupIds = comparison.GetProperty("outcomeGroups").EnumerateArray().SelectMany(g => g.GetProperty("comparisonIds").EnumerateArray().Select(v => v.GetString())).ToArray();
        Assert.Equal(72, groupIds.Length); Assert.Equal(72, groupIds.Distinct().Count());
        Assert.All(rows, r => { Assert.False(r.GetProperty("operationalSignal").GetBoolean()); Assert.False(r.GetProperty("canonicalAdmissionPermitted").GetBoolean()); });
        var repeat = Path.Combine(owned, "context-sc-repeat"); Assert.Equal(0, ContextSc(ContextPath, repeat)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(repeat, "contextual-swing-sc.json")));
        Assert.Equal(2, ContextSc(ContextPath, target)); Assert.Equal(originalHash, Hash(ContextPath));
        Assert.DoesNotContain("2026-06-25T", System.Text.Encoding.UTF8.GetString(bytes)); Assert.DoesNotContain("2026-06-30T", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void ContextScRowsAreStableAtEarlierCutoffAndMarkdownIsCultureIndependent()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
            var target = Path.Combine(owned, "context-sc-culture"); Assert.Equal(0, ContextSc(ContextPath, target));
            Assert.Equal(File.ReadAllBytes(Path.Combine(Root(), "docs/data/strategy-lab/contextual-sc-v1/june23.md")), File.ReadAllBytes(Path.Combine(target, "contextual-swing-sc.md")));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        Directory.CreateDirectory(owned); var root = JsonNode.Parse(File.ReadAllText(ContextPath))!; root["maximumCutoffUtc"] = "2026-06-23T13:30:00Z";
        var input = Path.Combine(owned, "cutoff.json"); File.WriteAllText(input, root.ToJsonString()); var first = Path.Combine(owned, "before"); Assert.Equal(0, ContextSc(input, first));
        using var before = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(first, "contextual-swing-sc.json")));
        var future = root["series"]!["M5"]!["candles"]!.AsArray().First(c => c!["openTimeUtc"]!.GetValue<string>().StartsWith("2026-06-23T15:40"))!; future["low"] = 1;
        File.WriteAllText(input, root.ToJsonString()); var second = Path.Combine(owned, "after"); Assert.Equal(0, ContextSc(input, second));
        using var after = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(second, "contextual-swing-sc.json")));
        Assert.True(JsonElement.DeepEquals(before.RootElement.GetProperty("comparison"), after.RootElement.GetProperty("comparison")));
    }

    [Fact]
    public void ContextTrackingReproducesRealInventoryFullSequenceAndExactOpenBoundaryWithoutApproval()
    {
        var hash = Hash(ContextPath); var target = Path.Combine(owned, "tracking"); Assert.Equal(0, Tracking(ContextPath, target));
        var golden = Path.Combine(Root(), "docs/data/strategy-lab/liquidity-context-v1");
        var bytes = File.ReadAllBytes(Path.Combine(target, "liquidity-context.json"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(golden, "june23.json")), bytes);
        Assert.Equal(File.ReadAllBytes(Path.Combine(golden, "june23.md")), File.ReadAllBytes(Path.Combine(target, "liquidity-context.md")));
        using var json = JsonDocument.Parse(bytes); var root = json.RootElement; var r = root.GetProperty("tracking");
        Assert.Equal(hash, root.GetProperty("inputSha256").GetString()); Assert.False(r.GetProperty("canonicalAdmissionPermitted").GetBoolean()); Assert.False(r.GetProperty("operationalSignal").GetBoolean());
        Assert.Equal("EXP-NQ-LIQ-CONTEXT-V1", r.GetProperty("algorithmId").GetString());
        var refs = r.GetProperty("references").EnumerateArray().ToArray(); Assert.Equal(4, refs.Length);
        Assert.Equal(new decimal[] { 30463.9m, 29715.2m, 29818.6m, 29472.7m }, refs.Select(t => t.GetProperty("reference").GetProperty("price").GetDecimal()));
        Assert.Equal(new[] { 114, 114, 42, 42 }, refs.Select(t => t.GetProperty("observations").GetArrayLength()));
        Assert.Equal(4, refs.Select(t => t.GetProperty("reference").GetProperty("id").GetString()).Distinct().Count());
        Assert.All(refs, t => Assert.Equal("CompleteThroughLastClosedInterval", t.GetProperty("observationCoverage").GetString()));
        Assert.Equal("AsiaLowOnlyPenetrated", r.GetProperty("londonHistoricalContext").GetProperty("completedClassification").GetString());
        var ll = refs.Single(t => t.GetProperty("reference").GetProperty("category").GetString() == "LL");
        Assert.True(ll.GetProperty("penetrationObservedBeforeOpen").GetBoolean()); Assert.Equal(4, ll.GetProperty("penetrationsAvailableAtOrAfterOpen").GetInt32());
        var first = ll.GetProperty("firstObservedPenetration"); var c = first.GetProperty("candle");
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 5, 0, TimeSpan.Zero), c.GetProperty("openTimeUtc").GetDateTimeOffset());
        Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 10, 0, TimeSpan.Zero), first.GetProperty("availableAtUtc").GetDateTimeOffset());
        Assert.Equal(29483.9m, c.GetProperty("open").GetDecimal()); Assert.Equal(29494.4m, c.GetProperty("high").GetDecimal());
        Assert.Equal(29467.1m, c.GetProperty("low").GetDecimal()); Assert.Equal(29477.4m, c.GetProperty("close").GetDecimal());
        var observations = ll.GetProperty("observations").EnumerateArray().ToArray();
        var boundary = observations.Single(o => o.GetProperty("candle").GetProperty("openTimeUtc").GetDateTimeOffset().Minute == 25 && o.GetProperty("candle").GetProperty("openTimeUtc").GetDateTimeOffset().Hour == 13);
        Assert.Equal("Preparation", boundary.GetProperty("intervalPhase").GetString()); Assert.Equal("Observation", boundary.GetProperty("availabilityPhase").GetString());
        Assert.Equal(3, boundary.GetProperty("priorPenetrationCount").GetInt32());
        var subsequent = observations.Single(o => o.GetProperty("candle").GetProperty("openTimeUtc").GetDateTimeOffset() == new DateTimeOffset(2026, 6, 23, 13, 30, 0, TimeSpan.Zero));
        Assert.True(subsequent.GetProperty("priorPenetrationBeforeOpen").GetBoolean()); Assert.Equal(4, subsequent.GetProperty("priorPenetrationCount").GetInt32());
        var repeat = Path.Combine(owned, "repeat"); Assert.Equal(0, Tracking(ContextPath, repeat)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(repeat, "liquidity-context.json")));
        Assert.Equal(2, Tracking(ContextPath, target)); Assert.Equal(hash, Hash(ContextPath));
        Assert.DoesNotContain("2026-06-25T", System.Text.Encoding.UTF8.GetString(bytes)); Assert.DoesNotContain("2026-06-30T", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void TrackingMarkdownAndReferenceIdentitiesAreCultureIndependent()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
            var target = Path.Combine(owned, "spanish"); Assert.Equal(0, Tracking(ContextPath, target));
            Assert.Equal(File.ReadAllBytes(Path.Combine(Root(), "docs/data/strategy-lab/liquidity-context-v1/june23.md")), File.ReadAllBytes(Path.Combine(target, "liquidity-context.md")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(Root(), "docs/data/strategy-lab/liquidity-context-v1/june23.json")), File.ReadAllBytes(Path.Combine(target, "liquidity-context.json")));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void ExplicitEarlierTrackingCutoffCannotConsumeLaterSourceRowsOrTheirPrices()
    {
        Directory.CreateDirectory(owned); var root = JsonNode.Parse(File.ReadAllText(ContextPath))!;
        root["maximumCutoffUtc"] = "2026-06-23T13:25:00Z";
        var input = Path.Combine(owned, "earlier.json"); File.WriteAllText(input, root.ToJsonString());
        var target = Path.Combine(owned, "earlier"); Assert.Equal(0, Tracking(input, target));
        using var before = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(target, "liquidity-context.json")));
        var later = root["series"]!["M5"]!["candles"]!.AsArray().First(c => c!["openTimeUtc"]!.GetValue<string>().StartsWith("2026-06-23T15:40"))!;
        later["low"] = 1; File.WriteAllText(input, root.ToJsonString()); var after = Path.Combine(owned, "changed-future"); Assert.Equal(0, Tracking(input, after));
        using var changed = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(after, "liquidity-context.json")));
        Assert.True(JsonElement.DeepEquals(before.RootElement.GetProperty("tracking"), changed.RootElement.GetProperty("tracking")));
    }

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
