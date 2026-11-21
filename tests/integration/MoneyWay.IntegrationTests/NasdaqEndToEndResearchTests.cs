using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
