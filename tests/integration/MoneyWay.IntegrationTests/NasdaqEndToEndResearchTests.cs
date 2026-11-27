using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqEndToEndResearchTests
{
    private const string Cutoff = "2026-06-23T16:30:00Z";

    [Fact]
    public void June23SharesComponentIdentitiesAndReportsBothInvalidationTimesAndGeometry()
    {
        using var run = new ResearchRun();
        var baseline = run.Execute(run.Source, Cutoff, "--compare-structural-tp");
        var integrated = run.Execute(run.Source, Cutoff);
        Assert.Single(Directory.GetFiles(integrated));
        using var evidence = ResearchRun.Report(integrated);
        var envelope = evidence.RootElement;
        var result = envelope.GetProperty("research");
        Assert.Equal("EXP-NQ-END-TO-END-MTF-V1", result.GetProperty("candidateId").GetString());
        Assert.Equal("1.1.0", result.GetProperty("version").GetString());
        Assert.StartsWith("EXP-NQ-END-TO-END-MTF-V1/1.1.0/", result.GetProperty("runId").GetString());
        Assert.Equal("nasdaq-0.1.0-draft", result.GetProperty("replayStrategyVersion").GetString());
        Assert.Equal("EXP-NQ-TRADING-WINDOW-1130-V1", result.GetProperty("window").GetProperty("candidateConfigurationId").GetString());
        Assert.Equal("AtTradingWindowEnd", result.GetProperty("window").GetProperty("candidate").GetProperty("phase").GetString());
        Assert.False(result.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(result.GetProperty("operationalSignal").GetBoolean());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(run.Source))), envelope.GetProperty("inputSha256").GetString());
        Assert.Equal(4, envelope.GetProperty("sourceProvenance").EnumerateObject().Count());
        Assert.All(envelope.GetProperty("sourceProvenance").EnumerateObject(), source =>
            Assert.Equal(64, source.Value.GetProperty("rawSha256").GetString()!.Length));
        var replay = result.GetProperty("replayCutoff");
        Assert.Equal(replay.GetProperty("requestedUtc").GetDateTimeOffset(), replay.GetProperty("canonicalFrameUtc").GetDateTimeOffset());
        Assert.Equal(4, replay.GetProperty("sources").EnumerateArray().Count());
        var prep = result.GetProperty("preparation");
        Assert.Equal(At(13), prep.GetProperty("scheduledUtc").GetDateTimeOffset());
        Assert.Equal(At(13), prep.GetProperty("replayCutoff").GetProperty("canonicalFrameUtc").GetDateTimeOffset());
        Assert.Equal(6, prep.GetProperty("profiles").EnumerateArray().Count());
        Assert.All(prep.GetProperty("profiles").EnumerateArray(), p =>
        {
            Assert.False(p.GetProperty("independentlyValidated").GetBoolean());
            Assert.Equal(At(13), p.GetProperty("evidence").GetProperty("asOfUtc").GetDateTimeOffset());
        });
        Assert.All(result.GetProperty("sessionLiquidity").EnumerateArray(), session =>
        {
            Assert.Equal("Complete", session.GetProperty("coverageStatus").GetString());
            Assert.True(session.GetProperty("endUtc").GetDateTimeOffset() <= At(13));
        });

        foreach (var (filename, property) in new[]
                 {
                     ("contextual-sequence.json", "sequence"),
                     ("contextual-m1-realignment.json", "realignment"),
                     ("contextual-m5-structural-sl.json", "structuralStop"),
                     ("contextual-structural-tp.json", "structuralTakeProfit")
                 })
        {
            using var prior = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(baseline, filename)));
            Assert.True(JsonElement.DeepEquals(prior.RootElement.GetProperty(property), result.GetProperty(property)),
                $"Canonical-prefix integration must preserve {property} evidence.");
        }

        var summary = result.GetProperty("summary");
        Assert.Equal(6, summary.GetProperty("episodesWithObservedLiquidityTake").GetInt32());
        Assert.Equal(4, summary.GetProperty("distinctM1Anchors").GetInt32());
        Assert.Equal(5, summary.GetProperty("distinctRealignmentEvents").GetInt32());
        Assert.Equal(60, summary.GetProperty("distinctStructuralReferenceCombinations").GetInt32());
        Assert.Equal(36, summary.GetProperty("distinctPairsWithPositiveRewardRisk").GetInt32());
        Assert.Equal(2880, summary.GetProperty("evaluatedStructuralAssociations").GetInt32());
        Assert.Equal(2688, summary.GetProperty("geometryCompletedAssociations").GetInt32());
        Assert.Equal(0, summary.GetProperty("completeH4ToM1Associations").GetInt32());
        Assert.Equal(0, summary.GetProperty("completeAssociationsWithInvalidationAtCutoff").GetInt32());
        Assert.Equal("H4ContextUnavailable", result.GetProperty("outcome").GetString());
        var realignmentCutoffs = result.GetProperty("realignmentCutoffs").EnumerateArray().ToArray();
        Assert.Equal(5, realignmentCutoffs.Length);
        var realigned = Assert.Single(realignmentCutoffs, c => c.GetProperty("requestedUtc").GetDateTimeOffset() == At(13, 58));
        Assert.Equal(At(13, 58), realigned.GetProperty("canonicalFrameUtc").GetDateTimeOffset());
        var m5AtRealignment = Assert.Single(realigned.GetProperty("sources").EnumerateArray(),
            s => s.GetProperty("timeframe").GetString() == "5m");
        Assert.Equal(At(13, 55), m5AtRealignment.GetProperty("lastNominalCloseUtc").GetDateTimeOffset());
        var sequence = result.GetProperty("sequence");
        var episode = sequence.GetProperty("episodes").EnumerateArray().Single(e =>
            e.GetProperty("selection").GetProperty("liquidityReference").GetProperty("category").GetString() == "LL" &&
            e.GetProperty("selection").GetProperty("policyId").GetString()!.EndsWith("-E1-V1", StringComparison.Ordinal));
        var ll = result.GetProperty("chains").EnumerateArray().Where(c =>
            c.GetProperty("episodeId").GetString() == episode.GetProperty("id").GetString() &&
            c.GetProperty("realignmentUtc").ValueKind != JsonValueKind.Null).ToArray();
        Assert.NotEmpty(ll);
        Assert.All(ll, chain =>
        {
            Assert.Equal(At(13, 55), chain.GetProperty("directionalFvgUtc").GetDateTimeOffset());
            Assert.Equal(At(13, 58), chain.GetProperty("realignmentUtc").GetDateTimeOffset());
            Assert.All(chain.GetProperty("branches").EnumerateArray(), b =>
            {
                Assert.Equal("Observed", b.GetProperty("stateAtRealignment").GetString());
                Assert.Equal("Invalidated", b.GetProperty("stateAtCutoff").GetString());
                Assert.Equal(At(15, 10), b.GetProperty("fvg").GetProperty("invalidatedAtUtc").GetDateTimeOffset());
            });
            Assert.Contains(chain.GetProperty("geometry").EnumerateArray(), g =>
                g.GetProperty("structuralReferencesCompletedAtUtc").ValueKind == JsonValueKind.String);
        });
        Assert.Contains(result.GetProperty("chains").EnumerateArray(), c => c.GetProperty("branches").EnumerateArray()
            .Any(b => b.GetProperty("stateAtRealignment").GetString() == "Invalidated"));
        var chains = result.GetProperty("chains").EnumerateArray().ToArray();
        Assert.Equal(16, chains.Count(c =>
        {
            var branches = c.GetProperty("branches").EnumerateArray().ToArray();
            return c.GetProperty("realignmentUtc").ValueKind == JsonValueKind.String && branches.Length > 0 &&
                branches.All(b => b.GetProperty("stateAtRealignment").GetString() == "Invalidated");
        }));
        Assert.Equal(chains.Length, chains.Select(c => (c.GetProperty("episodeId").GetString(),
            c.GetProperty("m5ConfigurationId").GetString(), c.GetProperty("m1Profile").GetString(),
            c.GetProperty("realignmentId").GetString())).Distinct().Count());
        Assert.All(chains, c =>
        {
            var rows = c.GetProperty("geometry").EnumerateArray().ToArray();
            Assert.Equal(12, rows.Select(g => g.GetProperty("configurationId").GetString()).Distinct().Count());
        });
    }

    [Fact]
    public void ReplayPrefixStaysIdenticalAcrossFutureMutationAndRemovalInAllFourSources()
    {
        using var run = new ResearchRun();
        const string cutoff = "2026-06-23T13:58:00Z";
        var node = JsonNode.Parse(File.ReadAllBytes(run.Source))!;
        JsonArray Candles(string tf) => node["series"]![tf]!["candles"]!.AsArray();
        var boundary = At(13, 58);
        var h4 = Candles("H4");
        var appended = h4[^1]!.DeepClone();
        appended["openTimeUtc"] = At(13);
        appended["closeTimeUtc"] = At(17);
        h4.Add(appended);
        foreach (var tf in new[] { "H4", "H1", "M5", "M1" })
        {
            var future = Candles(tf).First(c => c!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary)!;
            future["high"] = 99999m;
            future["low"] = 1m;
        }
        var mutation = run.WriteInput("mutation", node);
        foreach (var tf in new[] { "H4", "H1", "M5", "M1" })
        {
            var rows = Candles(tf);
            while (rows[^1]!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary) rows.RemoveAt(rows.Count - 1);
        }
        var prefix = run.WriteInput("prefix", node);
        string AtCutoff(string path)
        {
            using var report = ResearchRun.Report(run.Execute(path, cutoff));
            return report.RootElement.GetProperty("research").GetRawText();
        }
        var original = AtCutoff(run.Source);
        Assert.True(original == AtCutoff(mutation));
        Assert.True(original == AtCutoff(prefix));
    }

    [Fact]
    public void MissingH4DoesNotEraseDownstreamAndRegisteredReviewIsRejected()
    {
        using var run = new ResearchRun();
        var node = JsonNode.Parse(File.ReadAllBytes(run.Source))!;
        Assert.True(node["series"]!.AsObject().Remove("H4"));
        using var evidence = ResearchRun.Report(run.Execute(run.WriteInput("missing-h4", node), Cutoff));
        var result = evidence.RootElement.GetProperty("research");
        Assert.Equal("H4ContextUnavailable", result.GetProperty("outcome").GetString());
        Assert.All(result.GetProperty("preparation").GetProperty("profiles").EnumerateArray(), p =>
        {
            Assert.False(p.GetProperty("contextAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, p.GetProperty("evidence").ValueKind);
        });
        var summary = result.GetProperty("summary");
        Assert.True(summary.GetProperty("geometryCompletedAssociations").GetInt32() > 0);
        Assert.Equal(0, summary.GetProperty("completeH4ToM1Associations").GetInt32());

        var protectedInput = Path.Combine(Path.GetDirectoryName(run.Source)!, "contextual-validation", "context.json");
        var hash = SHA256.HashData(File.ReadAllBytes(protectedInput));
        run.Execute(protectedInput, "2026-06-22T16:30:00Z", expected: 2);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(protectedInput)));
    }

    [Fact]
    public void AvailableH4FixtureCompletesTheChainAndLaterInvalidationKeepsItsOriginalCompletion()
    {
        using var run = new ResearchRun();
        var node = JsonNode.Parse(File.ReadAllBytes(run.Source))!;
        var h4 = node["series"]!["H4"]!.AsObject();
        h4.Remove("rawSha256");
        h4.Remove("normalizedSha256");
        var rows = h4["candles"]!.AsArray();
        (decimal Low, decimal High)[] prices =
            [(12, 20), (14, 30), (10, 24), (13, 22), (17, 33), (12, 27), (16, 25), (19, 38), (14, 31), (18, 28)];
        Assert.Equal(prices.Length, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]!;
            row["low"] = prices[i].Low;
            row["high"] = prices[i].High;
            row["open"] = prices[i].Low + 1;
            row["close"] = prices[i].Low + 1;
        }
        var input = run.WriteInput("synthetic-h4", node);
        using var before = ResearchRun.Report(run.Execute(input, "2026-06-23T13:58:00Z"));
        using var after = ResearchRun.Report(run.Execute(input, Cutoff));
        var original = before.RootElement.GetProperty("research");
        var subsequent = after.RootElement.GetProperty("research");
        Assert.Equal(original.GetProperty("preparation").GetRawText(), subsequent.GetProperty("preparation").GetRawText());
        Assert.Contains(original.GetProperty("preparation").GetProperty("profiles").EnumerateArray(),
            p => p.GetProperty("contextAvailable").GetBoolean());
        Assert.True(subsequent.GetProperty("summary").GetProperty("completeH4ToM1Associations").GetInt32() > 0);

        static IEnumerable<JsonElement> CompletionAt(JsonElement report) => report.GetProperty("chains").EnumerateArray()
            .Where(c => c.GetProperty("realignmentUtc").ValueKind == JsonValueKind.String &&
                        c.GetProperty("realignmentUtc").GetDateTimeOffset() == At(13, 58))
            .SelectMany(c => c.GetProperty("geometry").EnumerateArray())
            .SelectMany(g => g.GetProperty("contextOutcomes").EnumerateArray())
            .Where(o => o.GetProperty("completedAtUtc").ValueKind == JsonValueKind.String);
        var first = CompletionAt(original).ToArray();
        var later = CompletionAt(subsequent).ToArray();
        Assert.NotEmpty(first);
        Assert.Equal(first.Length, later.Length);
        Assert.Contains(first, o => o.GetProperty("outcome").GetString() == "ResearchChainComplete");
        Assert.Contains(later, o => o.GetProperty("outcome").GetString() == "ResearchChainCompleteWithInvalidatedContext");
        Assert.All(later, o => Assert.Equal(At(13, 58), o.GetProperty("completedAtUtc").GetDateTimeOffset()));
    }

    [Fact]
    public void DirectionalFvgAndCountertrendDoNotCreateGeometryBeforeRealignmentCloses()
    {
        using var run = new ResearchRun();
        using var report = ResearchRun.Report(run.Execute(run.Source, "2026-06-23T13:57:00Z"));
        var main = report.RootElement.GetProperty("research");
        var pending = main.GetProperty("chains").EnumerateArray().Where(c =>
            c.GetProperty("directionalFvgUtc").ValueKind == JsonValueKind.String &&
            c.GetProperty("directionalFvgUtc").GetDateTimeOffset() == At(13, 55)).ToArray();
        Assert.NotEmpty(pending);
        Assert.All(pending, c =>
        {
            Assert.Equal(JsonValueKind.Null, c.GetProperty("realignmentUtc").ValueKind);
            Assert.All(c.GetProperty("geometry").EnumerateArray(), g =>
            {
                Assert.Equal("WaitingForM1Realignment", g.GetProperty("downstreamOutcome").GetString());
                Assert.Equal(JsonValueKind.Null, g.GetProperty("stopEvaluationId").ValueKind);
                Assert.Equal(JsonValueKind.Null, g.GetProperty("targetEvaluationId").ValueKind);
            });
        });
    }

    [Fact]
    public void V2ComparesThePinnedJuneAtBothCutoffsWithoutReplacingTheBaseline()
    {
        const string mode = "--compare-h4-causal-context-v2";
        const string filename = "nasdaq-h4-causal-context-v2.json";
        using var run = new ResearchRun();
        var sourceBefore = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(run.Source));
        var output = run.Execute(run.Source, Cutoff, mode);
        var artifact = File.ReadAllBytes(Path.Combine(output, filename));
        Assert.Equal(artifact, File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), filename)));
        Assert.False(File.Exists(Path.Combine(output, "nasdaq-end-to-end-research.json")));
        using var checkedReport = JsonDocument.Parse(artifact);
        Assert.Equal(2, checkedReport.RootElement.GetProperty("schemaVersion").GetInt32());
        var research = checkedReport.RootElement.GetProperty("research");
        Assert.Equal("EXP-NQ-H4-CAUSAL-CONTEXT-V2", research.GetProperty("candidateId").GetString());
        Assert.Equal("2.0.0", research.GetProperty("version").GetString());
        Assert.Equal("1.1.0", research.GetProperty("baselineVersion").GetString());
        Assert.Equal("H4ContextUnavailable", research.GetProperty("v1Outcome").GetString());
        Assert.Equal(At(13), research.GetProperty("preparationUtc").GetDateTimeOffset());
        Assert.Equal(At(16, 30), research.GetProperty("reportCutoff").GetProperty("requestedUtc").GetDateTimeOffset());
        Assert.False(research.GetProperty("canonicalReady").GetBoolean());
        Assert.False(research.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(research.GetProperty("operationalSignal").GetBoolean());
        Assert.False(research.GetProperty("experimentalChainComplete").GetBoolean());
        Assert.Equal(8, research.GetProperty("documentaryM5Events").GetArrayLength());
        Assert.Equal(5, research.GetProperty("documentaryM1Events").GetArrayLength());
        var profiles = research.GetProperty("profiles").EnumerateArray().ToArray();
        Assert.Equal(6, profiles.Length);
        Assert.All(profiles, p =>
        {
            Assert.False(p.GetProperty("v1ContextAvailable").GetBoolean());
            Assert.Equal("InsufficientConfirmedPivots", p.GetProperty("v1GateReason").GetString());
            Assert.True(p.GetProperty("v1CandlesWithReferences").GetInt32() > 0);
            var v2 = p.GetProperty("v2");
            Assert.Equal("Provisional", v2.GetProperty("structureState").GetString());
            Assert.Equal("NoQualifiedEvent", v2.GetProperty("contextState").GetString());
            Assert.Equal(0, p.GetProperty("v2CompleteAssociations").GetInt32());
        });
        Assert.Equal(new[] { 3, 3, 1, 1, 1, 1 },
            profiles.Select(p => p.GetProperty("v2").GetProperty("confirmedReferences").GetArrayLength()));
        using var atPrep = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(run.Execute(run.Source, "2026-06-23T13:00:00Z", mode), filename)));
        Assert.Equal(profiles.Select(p => p.GetProperty("v2").GetRawText()),
            atPrep.RootElement.GetProperty("research").GetProperty("profiles").EnumerateArray().Select(p => p.GetProperty("v2").GetRawText()));
        using var before = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(run.Execute(run.Source, "2026-06-23T12:59:00Z", mode), filename)));
        Assert.All(before.RootElement.GetProperty("research").GetProperty("profiles").EnumerateArray(),
            p => Assert.Equal(JsonValueKind.Null, p.GetProperty("v2").ValueKind));
        Assert.Equal(sourceBefore, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(run.Source)));
    }

    [Theory]
    [InlineData("2026-11-23", "Validation")]
    [InlineData("2026-11-24", "HeldOut")]
    [InlineData("2026-06-23", "Development")]
    public void V2RejectsUnpinnedPackagesWithoutPublishing(string day, string partition)
    {
        using var run = new ResearchRun();
        var stub = new JsonObject { ["schema"] = "moneyway.nasdaq-evidence-registration.v1", ["date"] = day, ["partition"] = partition };
        run.Execute(run.WriteInput("unpinned", stub), Cutoff, "--compare-h4-causal-context-v2", expected: 2);
    }

    [Fact]
    public void V2OnlyCompletesCausallyAlignedChainsInTheSyntheticH4Projection()
    {
        using var run = new ResearchRun();
        using var source = JsonDocument.Parse(File.ReadAllBytes(run.Source));
        var provider = new MarketDataProviderId("oanda-global-markets-mt5-demo");
        var instrument = new MarketSymbol("US100");
        var h4 = new Timeframe(4, TimeframeUnit.Hour);
        var start = At(13).AddHours(-40);
        var original = source.RootElement.GetProperty("series");
        var h1 = Read("H1", new(1, TimeframeUnit.Hour));
        var m5 = Read("M5", new(5, TimeframeUnit.Minute));
        var m1 = Read("M1", new(1, TimeframeUnit.Minute));
        var aligned = Compare(mirror: false, breakout: true);
        var withoutEvent = Compare(mirror: false, breakout: false);
        var opposite = Compare(mirror: true, breakout: true);
        Assert.True(aligned.ExperimentalChainComplete);
        Assert.Equal(H4ResearchDirection.Buy, aligned.Profiles[0].V2!.ContextAlignedDirection);
        Assert.NotEmpty(aligned.Profiles[0].V2CompletedM1Events);
        Assert.True(aligned.Profiles[0].V2CompleteAssociations > aligned.V2DistinctCompletedM1Events);
        Assert.All(aligned.Profiles[0].V2CompletedM1Events,
            e => Assert.Contains(aligned.DocumentaryM1Events, d => d.ConfirmedAtUtc == e.ConfirmedAtUtc && d.Direction == e.Direction));
        Assert.True(withoutEvent.V1DownstreamSummary.CompleteH4ToM1Associations > 0);
        Assert.False(withoutEvent.ExperimentalChainComplete);
        Assert.Equal(H4ResearchDirection.Sell, opposite.Profiles[0].V2!.ContextAlignedDirection);
        Assert.False(opposite.ExperimentalChainComplete);
        Assert.False(aligned.CanonicalReady);

        NasdaqH4CausalContextComparison Compare(bool mirror, bool breakout)
        {
            (decimal Low, decimal High, decimal Open, decimal Close)[] prices =
                [(11, 12, 11, 12), (9, 13, 10, 11), (8, 15, 11, 11), (10, 20, 15, 15), (5, 16, 10, 10),
                 (9, 18, 12, 12), (11, 24, 17, 17), (7, 22, 15, 15), (12, 23, 20, 21),
                 breakout ? (13, 27, 21, 25) : (13, 24, 21, 24)];
            var bars = prices.Select((p, i) => new Candle(provider, instrument, h4, start.AddHours(4 * i),
                start.AddHours(4 * (i + 1)), mirror ? 100 - p.Open : p.Open, mirror ? 100 - p.Low : p.High,
                mirror ? 100 - p.High : p.Low, mirror ? 100 - p.Close : p.Close, null));
            return new CompareNasdaqH4CausalContextUseCase().Execute(new("synthetic-h4-on-june-development-downstream",
                new(2026, 6, 23), At(16, 30), [new(provider, instrument, h4, bars), h1, m5, m1], start, "test fixture"));
        }

        CandleSeries Read(string key, Timeframe timeframe) => new(provider, instrument, timeframe,
            original.GetProperty(key).GetProperty("candles").EnumerateArray().Select(c => new Candle(provider, instrument, timeframe,
                c.GetProperty("openTimeUtc").GetDateTimeOffset(), c.GetProperty("closeTimeUtc").GetDateTimeOffset(),
                c.GetProperty("open").GetDecimal(), c.GetProperty("high").GetDecimal(), c.GetProperty("low").GetDecimal(),
                c.GetProperty("close").GetDecimal(), c.GetProperty("volume").ValueKind == JsonValueKind.Number
                    ? c.GetProperty("volume").GetDecimal() : null)));
    }

    [Fact]
    public void V3PreservesJuneEventContinuityWithoutReclassifyingV2OrAdmittingOtherSources()
    {
        const string mode = "--compare-h4-context-continuity-v3";
        const string filename = "nasdaq-h4-context-continuity-v3.json";
        using var run = new ResearchRun();
        var full = File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), filename));
        Assert.Equal(full, File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), filename)));
        using var reproduced = JsonDocument.Parse(full);
        using var atPreparation = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(run.Execute(run.Source, "2026-06-23T13:00:00Z", mode), filename)));
        var research = reproduced.RootElement.GetProperty("research");
        var profiles = research.GetProperty("profiles").EnumerateArray().ToArray();
        Assert.Equal(atPreparation.RootElement.GetProperty("research").GetProperty("profiles").GetRawText(), research.GetProperty("profiles").GetRawText());
        Assert.Equal(6, profiles.Length);
        Assert.False(research.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(research.GetProperty("operationalSignal").GetBoolean());
        Assert.Equal(0, research.GetProperty("v2DistinctCompletedM1Events").GetInt32());
        Assert.All(profiles, p =>
        {
            Assert.Empty(p.GetProperty("eventsAtLatestClose").EnumerateArray());
            Assert.Equal("Provisional", p.GetProperty("structureState").GetString());
            Assert.Equal(JsonValueKind.Null, p.GetProperty("confirmedStructureDirection").ValueKind);
            Assert.Equal("NoQualifiedEvent", p.GetProperty("v2ContextState").GetString());
        });
        Assert.All(profiles.Take(2), p =>
        {
            Assert.Equal(2, p.GetProperty("eventHistory").GetArrayLength());
            var previous = Assert.Single(p.GetProperty("latestPreviouslyObservedEvents").EnumerateArray());
            Assert.Equal(At(5), previous.GetProperty("event").GetProperty("observedAtUtc").GetDateTimeOffset());
            Assert.Equal(30268m, previous.GetProperty("event").GetProperty("reference").GetProperty("price").GetDecimal());
            Assert.True(previous.GetProperty("humanValidationRequired").GetBoolean());
            Assert.All(previous.GetProperty("subsequentCloses").EnumerateArray(),
                c => Assert.Equal("BelowReference", c.GetProperty("closeRelation").GetString()));
            Assert.Equal("ContextValidityUnresolved", previous.GetProperty("validity").GetString());
        });
        Assert.All(profiles.Skip(2), p => Assert.Empty(p.GetProperty("eventHistory").EnumerateArray()));
        var reserved = run.WriteInput("reserved-fixture", new JsonObject
        { ["tradingDay"] = "2026-11-23", ["partition"] = "Validation" });
        run.Execute(reserved, Cutoff, mode, expected: 2);
    }

    [Fact]
    public void OptionalFvgV2ComparesPinnedDevelopmentWithoutChangingPublishedV1Bytes()
    {
        const string mode = "--compare-m5-to-m1-optional-fvg-v2";
        const string file = "nasdaq-m5-to-m1-optional-fvg-v2.json";
        using var run = new ResearchRun();
        var baseline = File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff), "nasdaq-end-to-end-research.json"));
        Assert.Equal("D19C506BBCE9609A8D3237E9F21B527D4D0150A1ED0339DC3A86F04735B8CB61",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(baseline)));
        var bytes = File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), file));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), file)));
        using var report = JsonDocument.Parse(bytes);
        var research = report.RootElement.GetProperty("research");
        Assert.False(research.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(research.GetProperty("operationalSignal").GetBoolean());
        Assert.Equal(0, research.GetProperty("baselineCompleteH4ToM1Associations").GetInt32());
        var count = research.GetProperty("summary");
        Assert.Equal(4, count.GetProperty("distinctTakes").GetInt32());
        Assert.Equal(5, count.GetProperty("distinctFirstTriggers").GetInt32());
        Assert.Equal(5, count.GetProperty("distinctFvgAnchoredRealignments").GetInt32());
        Assert.Equal(6, count.GetProperty("distinctTriggerAnchoredRealignments").GetInt32());
        Assert.Equal(3, count.GetProperty("distinctSharedRealignments").GetInt32());
        Assert.Equal(3, count.GetProperty("distinctTriggerOnlyRealignments").GetInt32());
        Assert.Equal(16, count.GetProperty("invalidatedIfvgAtTriggerRealignmentAssociations").GetInt32());
        Assert.Contains(research.GetProperty("byPolicy").EnumerateArray(), r => r.GetProperty("preWindowSweep").GetBoolean());
        var altered = run.WriteInput("non-pinned-development", new JsonObject { ["tradingDay"] = "2026-06-23" });
        run.Execute(altered, Cutoff, mode, expected: 2);
    }

    [Fact]
    public void TriggerAnchoredDownstreamUsesPinnedDevelopmentAndPreservesPublishedReports()
    {
        const string mode = "--compare-trigger-anchored-downstream-v1";
        const string filename = "nasdaq-trigger-anchored-downstream-v1.json";
        using var run = new ResearchRun();
        var bytes = File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), filename));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, mode), filename)));
        var baseline = File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff), "nasdaq-end-to-end-research.json"));
        var optional = File.ReadAllBytes(Path.Combine(run.Execute(run.Source, Cutoff, "--compare-m5-to-m1-optional-fvg-v2"),
            "nasdaq-m5-to-m1-optional-fvg-v2.json"));
        Assert.Equal("d19c506bbce9609a8d3237e9f21b527d4d0150a1ed0339dc3a86f04735b8cb61", Convert.ToHexStringLower(SHA256.HashData(baseline)));
        Assert.Equal("2f9687c2dc812f5623b3085d5b012d32c71b5e621c534f12911379c7c4bea345", Convert.ToHexStringLower(SHA256.HashData(optional)));
        using var document = JsonDocument.Parse(bytes);
        using var v2 = JsonDocument.Parse(optional);
        var report = document.RootElement.GetProperty("research");
        Assert.Equal("EXP-NQ-TRIGGER-ANCHORED-DOWNSTREAM-RESEARCH-V1", report.GetProperty("candidateId").GetString());
        Assert.Equal("H4ContextUnavailable", report.GetProperty("baselineOutcome").GetString());
        Assert.True(JsonElement.DeepEquals(v2.RootElement.GetProperty("research").GetProperty("summary"), report.GetProperty("v2Summary")));
        foreach (var flag in new[] { "h4ContextAvailable", "independentHumanValidation", "authenticatedTemporalAvailability",
                     "canonicalAdmissionPermitted", "operationalSignal" })
            Assert.False(report.GetProperty(flag).GetBoolean());
        var summary = report.GetProperty("summary");
        Assert.Equal(6, summary.GetProperty("realignment").GetProperty("distinctM1Realignments").GetInt32());
        Assert.Equal(3, summary.GetProperty("sharedM1WithFvgAnchored").GetArrayLength());
        Assert.Equal(3, summary.GetProperty("triggerOnlyM1").GetArrayLength());
        var geometry = report.GetProperty("geometryByMarketRealignmentId");
        Assert.Equal(6, geometry.EnumerateObject().Count());
        Assert.Equal(3, summary.GetProperty("both").GetProperty("distinctM1Realignments").GetInt32());
        Assert.Equal(40, summary.GetProperty("realignmentWithoutSlAssociations").GetInt32());
        Assert.Equal(0, summary.GetProperty("realignmentWithoutTpAssociations").GetInt32());
        using var prefix = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            run.Execute(run.Source, "2026-06-23T13:58:00Z", mode), filename)));
        Assert.All(prefix.RootElement.GetProperty("research").GetProperty("geometryByMarketRealignmentId").EnumerateObject(),
            snapshot => Assert.True(JsonElement.DeepEquals(snapshot.Value, geometry.GetProperty(snapshot.Name))));
        var paths = report.GetProperty("paths").EnumerateArray().ToArray();
        Assert.Equal(256, paths.Count(row => row.GetProperty("m1RealignmentObserved").GetBoolean()));
        Assert.Equal(16, paths.Count(row => row.GetProperty("m1RealignmentObserved").GetBoolean() &&
            row.GetProperty("triggers").EnumerateArray().Any(t => t.GetProperty("validityAtAssessment").GetString() == "Invalidated")));
        Assert.All(paths.Where(row => row.GetProperty("m1RealignmentObserved").GetBoolean()), row =>
        {
            var key = row.GetProperty("marketRealignmentId").GetString()!;
            var cutoff = row.GetProperty("assessmentUtc").GetDateTimeOffset();
            var snapshot = geometry.GetProperty(key);
            Assert.All(snapshot.GetProperty("stops").EnumerateArray(), stop =>
                Assert.Equal(cutoff, stop.GetProperty("asOfUtc").GetDateTimeOffset()));
            Assert.All(snapshot.GetProperty("targets").EnumerateArray(), target =>
                Assert.Equal(cutoff, target.GetProperty("asOfUtc").GetDateTimeOffset()));
            Assert.All(row.GetProperty("triggers").EnumerateArray().SelectMany(t => t.GetProperty("optionalFvgs").EnumerateArray()),
                fvg => Assert.True(fvg.GetProperty("availableAtUtc").GetDateTimeOffset() <= cutoff));
        });
        Assert.DoesNotContain("strategyVerdict", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Passed\"", document.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("hypotheticalRewardRisk", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        run.Execute(run.WriteInput("non-pinned", new JsonObject { ["tradingDay"] = "2026-06-23" }), Cutoff, mode, expected: 2);
    }

    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 6, 23, hour, minute, 0, TimeSpan.Zero);

    private sealed class ResearchRun : IDisposable
    {
        private readonly DirectoryInfo temp = Directory.CreateTempSubdirectory("moneyway-mtf-research-");
        private int runs;
        public string Source { get; } = FindSource();

        public string Execute(string input, string cutoff, string mode = "--compare-end-to-end", int expected = 0)
        {
            var output = Path.Combine(temp.FullName, "run-" + runs++);
            using var error = new StringWriter();
            var status = new NasdaqSessionLiquidityResearchCommand().Execute(
                ["nasdaq-session-liquidity", "--input", input, "--output", output, mode, "--cutoff-utc", cutoff],
                TextWriter.Null, error);
            Assert.True(status == expected, error.ToString());
            if (expected != 0) Assert.False(Directory.Exists(output));
            return output;
        }

        public string WriteInput(string name, JsonNode node)
        {
            var path = Path.Combine(temp.FullName, name + ".json");
            File.WriteAllText(path, node.ToJsonString());
            return path;
        }

        public static JsonDocument Report(string output) =>
            JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "nasdaq-end-to-end-research.json")));

        private static string FindSource()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) directory = directory.Parent;
            return Path.Combine(directory!.FullName, "src/frontend/moneyway-web/src/research/context/data/june23-context.json");
        }

        public void Dispose() => temp.Delete(recursive: true);
    }
}
