using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class M1RealignmentResearchCommandTests
{
    private static string Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src/frontend/moneyway-web/src/research/context/data");
    }

    private static int Run(string input, string output, string cutoff, TextWriter? error = null,
        string mode = "--compare-context-realignment") =>
        new NasdaqSessionLiquidityResearchCommand().Execute(
            ["nasdaq-session-liquidity", "--input", input, "--output", output, mode, "--cutoff-utc", cutoff],
            TextWriter.Null, error ?? TextWriter.Null);

    private static JsonDocument Report(string path) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(path, "contextual-m1-realignment.json")));

    [Fact]
    public void StructuralStopComposesJune23AndRetainsByteIdenticalM5AndM1Evidence()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-structural-sl-");
        try
        {
            var input = Path.Combine(Sources(), "june23-context.json");
            var legacy = Path.Combine(root.FullName, "legacy");
            var research = Path.Combine(root.FullName, "research");
            const string cutoff = "2026-06-23T16:30:00Z";
            Assert.Equal(0, Run(input, legacy, cutoff));
            Assert.Equal(0, Run(input, research, cutoff, mode: "--compare-structural-sl"));
            using var evidence = StructuralStopReport(research);
            var wrapper = evidence.RootElement;
            foreach (var property in new[] { "m5EvidenceFile", "m1EvidenceFile" })
            {
                var companion = wrapper.GetProperty(property).GetString()!;
                Assert.Equal(File.ReadAllBytes(Path.Combine(legacy, companion)), File.ReadAllBytes(Path.Combine(research, companion)));
            }
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input))), wrapper.GetProperty("inputSha256").GetString());
            Assert.Equal("4f468275b5644d4298ba0c085802f04574c765f7dfa662df1ee99456eb9fcf46",
                wrapper.GetProperty("sourceProvenance").GetProperty("M5").GetProperty("rawSha256").GetString());
            var result = wrapper.GetProperty("structuralStop");
            Assert.Equal("EXP-NQ-M5-STRUCTURAL-SL-V1", result.GetProperty("candidateId").GetString());
            Assert.False(result.GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.False(result.GetProperty("operationalSignal").GetBoolean());
            Assert.Equal(6, result.GetProperty("distinctEpisodes").GetInt32());
            Assert.Equal(4, result.GetProperty("distinctM5Anchors").GetInt32());
            Assert.Equal(5, result.GetProperty("distinctM1RealignmentAnchors").GetInt32());
            Assert.Equal(240, result.GetProperty("m5M1ConfigurationCombinations").GetInt32());
            Assert.Equal(720, result.GetProperty("structuralConfigurationCombinations").GetInt32());
            var evaluations = result.GetProperty("evaluations").EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
            Assert.Equal(15, evaluations.Count);
            Assert.Equal(9, evaluations.Values.Count(e => e.GetProperty("distancePoints").ValueKind == JsonValueKind.Number));
            Assert.All(evaluations.Values, e => Assert.Single(e.GetProperty("sourceGaps").EnumerateArray()));
            var associations = result.GetProperty("associations").EnumerateArray().ToArray();
            Assert.All(associations, association =>
            {
                Assert.Equal("EXP-NQ-TRADING-WINDOW-1130-V1", association.GetProperty("windowConfigurationId").GetString());
                var at = association.GetProperty("realignmentConfirmedAtUtc").GetDateTimeOffset();
                var observation = evaluations[association.GetProperty("evaluationId").GetString()!];
                Assert.Equal(at, observation.GetProperty("asOfUtc").GetDateTimeOffset());
                Assert.NotEmpty(association.GetProperty("realignmentId").GetString()!);
                Assert.All(association.GetProperty("bindings").EnumerateArray(), binding =>
                {
                    var source = binding.GetProperty("source");
                    Assert.NotEmpty(source.GetProperty("fvgId").GetString()!);
                    Assert.NotEmpty(source.GetProperty("confirmationId").GetString()!);
                });
            });
            Assert.Equal(48, associations.Count(a => a.GetProperty("bindings").EnumerateArray().All(b =>
                b.GetProperty("contextInvalidatedAtRealignment").ValueKind == JsonValueKind.True)));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void StructuralStopWaitsForObservableM1AndRejectsFrozenReview()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-structural-wait-");
        try
        {
            var output = Path.Combine(root.FullName, "waiting");
            Assert.Equal(0, Run(Path.Combine(Sources(), "june23-context.json"), output, "2026-06-23T13:29:00Z",
                mode: "--compare-structural-sl"));
            using var evidence = StructuralStopReport(output);
            var result = evidence.RootElement.GetProperty("structuralStop");
            Assert.Empty(result.GetProperty("evaluations").EnumerateObject());
            var associations = result.GetProperty("associations").EnumerateArray().ToArray();
            Assert.NotEmpty(associations);
            Assert.All(associations, a =>
            {
                Assert.Equal("WaitingForM1Realignment", a.GetProperty("outcome").GetString());
                Assert.Equal(JsonValueKind.Null, a.GetProperty("realignmentId").ValueKind);
            });
            var frozen = Path.Combine(Sources(), "contextual-validation", "context.json");
            var hash = SHA256.HashData(File.ReadAllBytes(frozen));
            var rejected = Path.Combine(root.FullName, "rejected");
            Assert.Equal(2, Run(frozen, rejected, "2026-06-22T16:30:00Z", mode: "--compare-structural-sl"));
            Assert.False(Directory.Exists(rejected));
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(frozen)));
        }
        finally { root.Delete(recursive: true); }
    }

    private static JsonDocument StructuralStopReport(string path) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(path, "contextual-m5-structural-sl.json")));

    [Fact]
    public void StructuralTargetSharesCausalEvaluationsAndPreservesEveryCompanionReport()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-structural-target-");
        try
        {
            var input = Path.Combine(Sources(), "june23-context.json");
            var legacy = Path.Combine(root.FullName, "stop");
            var output = Path.Combine(root.FullName, "target");
            const string cutoff = "2026-06-23T16:30:00Z";
            Assert.Equal(0, Run(input, legacy, cutoff, mode: "--compare-structural-sl"));
            Assert.False(File.Exists(Path.Combine(legacy, "contextual-structural-tp.json")));
            Assert.Equal(0, Run(input, output, cutoff, mode: "--compare-structural-tp"));
            using var evidence = StructuralTargetReport(output);
            var wrapper = evidence.RootElement;
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input))),
                wrapper.GetProperty("inputSha256").GetString());
            foreach (var property in new[] { "m5EvidenceFile", "m1EvidenceFile", "stopEvidenceFile" })
            {
                var companion = wrapper.GetProperty(property).GetString()!;
                Assert.Equal(File.ReadAllBytes(Path.Combine(legacy, companion)), File.ReadAllBytes(Path.Combine(output, companion)));
            }

            var result = wrapper.GetProperty("structuralTakeProfit");
            Assert.Equal("EXP-NQ-STRUCTURAL-TP-V1", result.GetProperty("candidateId").GetString());
            Assert.False(result.GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.False(result.GetProperty("operationalSignal").GetBoolean());
            Assert.Equal(6, result.GetProperty("distinctEpisodes").GetInt32());
            Assert.Equal(4, result.GetProperty("distinctM5Anchors").GetInt32());
            Assert.Equal(5, result.GetProperty("distinctM1References").GetInt32());
            Assert.Equal(240, result.GetProperty("m5M1ConfigurationCombinations").GetInt32());
            Assert.Equal(2880, result.GetProperty("structuralConfigurationCombinations").GetInt32());
            var evaluations = result.GetProperty("evaluations").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            Assert.Equal(20, evaluations.Count);
            Assert.Equal(5, evaluations.Values.Count(e => e.GetProperty("profile").GetProperty("policy").GetString() ==
                "NearestAuditedSessionExtreme"));

            using var stopEvidence = StructuralStopReport(output);
            var stops = stopEvidence.RootElement.GetProperty("structuralStop").GetProperty("evaluations");
            var comparisons = result.GetProperty("rewardRiskComparisons").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
            Assert.Equal(60, comparisons.Count);
            var ratios = 0;
            foreach (var comparison in comparisons.Values)
            {
                var target = evaluations[comparison.GetProperty("targetEvaluationId").GetString()!];
                var stop = stops.GetProperty(comparison.GetProperty("stopEvaluationId").GetString()!);
                Assert.Equal(stop.GetProperty("closeReference").GetRawText(), target.GetProperty("closeReference").GetRawText());
                var pair = Assert.Single(comparison.GetProperty("pairs").EnumerateArray());
                Assert.Equal(target.GetProperty("targetPrice").GetDecimal(), pair.GetProperty("targetPrice").GetDecimal());
                var ratio = pair.GetProperty("hypotheticalRewardRisk");
                if (ratio.ValueKind == JsonValueKind.Number)
                {
                    ratios++;
                    Assert.Equal(pair.GetProperty("rewardDistancePoints").GetDecimal() /
                        stop.GetProperty("distancePoints").GetDecimal(), ratio.GetDecimal());
                    Assert.Equal(stop.GetProperty("selected").GetProperty("pivot").GetProperty("id").GetString(),
                        pair.GetProperty("stopPivotId").GetString());
                }
                else
                    Assert.Equal("ReferenceNotProtective", pair.GetProperty("reasonCode").GetString());
            }
            Assert.Equal(36, ratios);

            var associations = result.GetProperty("associations").EnumerateArray().ToArray();
            Assert.Equal(2880, associations.Length);
            Assert.All(associations, a =>
            {
                var source = a.GetProperty("source");
                Assert.Equal("EXP-NQ-TRADING-WINDOW-1130-V1", source.GetProperty("windowConfigurationId").GetString());
                Assert.Equal(source.GetProperty("realignmentConfirmedAtUtc").GetDateTimeOffset(),
                    evaluations[a.GetProperty("targetEvaluationId").GetString()!].GetProperty("asOfUtc").GetDateTimeOffset());
            });
            Assert.Equal(192, associations.Count(a => a.GetProperty("source").GetProperty("bindings").EnumerateArray()
                .All(b => b.GetProperty("contextInvalidatedAtRealignment").ValueKind == JsonValueKind.True)));
            Assert.Equal(2880, associations.Select(a =>
            {
                var source = a.GetProperty("source");
                return (source.GetProperty("episodeId").GetString(), source.GetProperty("m5Configuration").GetProperty("id").GetString(),
                    source.GetProperty("realignmentId").GetString(), a.GetProperty("rewardRiskComparisonId").GetString());
            }).Distinct().Count());
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void StructuralTargetRequiresRealignmentAndRejectsProtectedReview()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-tp-wait-");
        try
        {
            var output = Path.Combine(root.FullName, "waiting");
            Assert.Equal(0, Run(Path.Combine(Sources(), "june23-context.json"), output, "2026-06-23T13:29:00Z",
                mode: "--compare-structural-tp"));
            using var evidence = StructuralTargetReport(output);
            var result = evidence.RootElement.GetProperty("structuralTakeProfit");
            Assert.Empty(result.GetProperty("evaluations").EnumerateObject());
            Assert.Empty(result.GetProperty("rewardRiskComparisons").EnumerateObject());
            var associations = result.GetProperty("associations").EnumerateArray().ToArray();
            Assert.NotEmpty(associations);
            Assert.All(associations, a => Assert.Equal("WaitingForM1Realignment", a.GetProperty("outcome").GetString()));

            var frozen = Path.Combine(Sources(), "contextual-validation", "context.json");
            var hash = SHA256.HashData(File.ReadAllBytes(frozen));
            var rejected = Path.Combine(root.FullName, "rejected");
            Assert.Equal(2, Run(frozen, rejected, "2026-06-22T16:30:00Z", mode: "--compare-structural-tp"));
            Assert.False(Directory.Exists(rejected));
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(frozen)));
        }
        finally { root.Delete(recursive: true); }
    }

    private static JsonDocument StructuralTargetReport(string path) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(path, "contextual-structural-tp.json")));

    [Fact]
    public void ExposedContextPreservesLlE1CompanionEvidenceAndSharedScans()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-m1-realignment-");
        try
        {
            var input = Path.Combine(Sources(), "june23-context.json");
            var output = Path.Combine(root.FullName, "development");
            Assert.Equal(0, Run(input, output, "2026-06-23T16:30:00Z"));
            using var document = Report(output);
            Assert.Equal("contextual-sequence.json", document.RootElement.GetProperty("m5EvidenceFile").GetString());
            using var m5 = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "contextual-sequence.json")));
            var sequence = m5.RootElement.GetProperty("sequence");
            var episode = sequence.GetProperty("episodes").EnumerateArray().Single(e =>
                e.GetProperty("selection").GetProperty("liquidityReference").GetProperty("category").GetString() == "LL" &&
                e.GetProperty("selection").GetProperty("policyId").GetString()!.EndsWith("-E1-V1", StringComparison.Ordinal));
            var result = document.RootElement.GetProperty("realignment");
            Assert.False(result.GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.False(result.GetProperty("operationalSignal").GetBoolean());
            Assert.Equal(4, result.GetProperty("distinctAnchorTimestamps").GetInt32());
            Assert.Equal(120, result.GetProperty("completedM5ConfigurationCombinations").GetInt32());
            var cases = result.GetProperty("associations").EnumerateArray().Where(a =>
                a.GetProperty("episodeId").GetString() == episode.GetProperty("id").GetString()).ToArray();
            Assert.Equal(48, cases.Length);
            var scans = result.GetProperty("scans").EnumerateArray().ToDictionary(s => s.GetProperty("id").GetString()!);
            var complete = cases.Where(a => a.GetProperty("scanId").ValueKind == JsonValueKind.String).ToArray();
            Assert.Equal(16, complete.Length);
            Assert.Equal(2, complete.Select(a => a.GetProperty("scanId").GetString()).Distinct().Count());
            foreach (var association in complete)
            {
                var scan = scans[association.GetProperty("scanId").GetString()!];
                var anchor = scan.GetProperty("anchor");
                Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 30, 0, TimeSpan.Zero), anchor.GetProperty("windowOpenUtc").GetDateTimeOffset());
                Assert.Equal(new DateTimeOffset(2026, 6, 23, 16, 30, 0, TimeSpan.Zero), anchor.GetProperty("windowCloseUtc").GetDateTimeOffset());
                Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 55, 0, TimeSpan.Zero), scan.GetProperty("anchor").GetProperty("atUtc").GetDateTimeOffset());
                Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 58, 0, TimeSpan.Zero), scan.GetProperty("firstRealignmentUtc").GetDateTimeOffset());
                Assert.Equal(155, scan.GetProperty("closedM1Count").GetInt32());
                Assert.Empty(scan.GetProperty("sourceGaps").EnumerateArray());
            }
            var frozen = Path.Combine(Sources(), "contextual-validation", "context.json");
            var hash = SHA256.HashData(File.ReadAllBytes(frozen));
            var protectedDestination = Path.Combine(root.FullName, "protected");
            Assert.Equal(2, Run(frozen, protectedDestination, "2026-06-22T16:30:00Z"));
            Assert.False(Directory.Exists(protectedDestination));
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(frozen)));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void ResearchReportsAreIdenticalWhenFutureM1M5AndH1AreRemovedOrMutated()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-m1-prefix-");
        try
        {
            var original = Path.Combine(Sources(), "june23-context.json");
            const string cutoff = "2026-06-23T13:58:00Z";
            var boundary = new DateTimeOffset(2026, 6, 23, 13, 58, 0, TimeSpan.Zero);
            var context = JsonNode.Parse(File.ReadAllBytes(original))!;
            JsonArray Candles(string tf) => context["series"]![tf]!["candles"]!.AsArray();
            foreach (var tf in new[] { "M1", "M5", "H1" })
            {
                var future = Candles(tf).First(c => c!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary)!;
                future["high"] = 99999m;
                future["low"] = 1m;
            }
            var mutated = Path.Combine(root.FullName, "mutated.json");
            File.WriteAllText(mutated, context.ToJsonString());
            foreach (var tf in new[] { "M1", "M5", "H1" })
            {
                var rows = Candles(tf);
                while (rows[^1]!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary) rows.RemoveAt(rows.Count - 1);
            }
            var prefix = Path.Combine(root.FullName, "prefix.json");
            File.WriteAllText(prefix, context.ToJsonString());
            var reports = new List<string>();
            var structuralReports = new List<string>();
            var targetReports = new List<string>();
            foreach (var input in new[] { original, prefix, mutated })
            {
                var output = Path.Combine(root.FullName, "output-" + reports.Count);
                var errors = new StringWriter();
                Assert.True(Run(input, output, cutoff, errors, "--compare-structural-tp") == 0, errors.ToString());
                using var report = Report(output);
                reports.Add(report.RootElement.GetProperty("realignment").GetRawText());
                using var structural = StructuralStopReport(output);
                structuralReports.Add(structural.RootElement.GetProperty("structuralStop").GetRawText());
                using var target = StructuralTargetReport(output);
                targetReports.Add(target.RootElement.GetProperty("structuralTakeProfit").GetRawText());
            }
            Assert.Equal(reports[0], reports[1]);
            Assert.Equal(reports[0], reports[2]);
            Assert.Equal(structuralReports[0], structuralReports[1]);
            Assert.Equal(structuralReports[0], structuralReports[2]);
            Assert.Equal(targetReports[0], targetReports[1]);
            Assert.Equal(targetReports[0], targetReports[2]);
        }
        finally { root.Delete(recursive: true); }
    }
}
