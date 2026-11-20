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

    private static int Run(string input, string output, string cutoff, TextWriter? error = null) =>
        new NasdaqSessionLiquidityResearchCommand().Execute(
            ["nasdaq-session-liquidity", "--input", input, "--output", output, "--compare-context-realignment", "--cutoff-utc", cutoff],
            TextWriter.Null, error ?? TextWriter.Null);

    private static JsonDocument Report(string path) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(path, "contextual-m1-realignment.json")));

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
    public void M1ReportIsIdenticalWhenFutureM1AndM5AreRemovedOrMutated()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-m1-prefix-");
        try
        {
            var original = Path.Combine(Sources(), "june23-context.json");
            const string cutoff = "2026-06-23T13:58:00Z";
            var boundary = new DateTimeOffset(2026, 6, 23, 13, 58, 0, TimeSpan.Zero);
            var context = JsonNode.Parse(File.ReadAllBytes(original))!;
            JsonArray Candles(string tf) => context["series"]![tf]!["candles"]!.AsArray();
            foreach (var tf in new[] { "M1", "M5" })
            {
                var future = Candles(tf).First(c => c!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary)!;
                future["high"] = 99999m;
                future["low"] = 1m;
            }
            var mutated = Path.Combine(root.FullName, "mutated.json");
            File.WriteAllText(mutated, context.ToJsonString());
            foreach (var tf in new[] { "M1", "M5" })
            {
                var rows = Candles(tf);
                while (rows[^1]!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary) rows.RemoveAt(rows.Count - 1);
            }
            var prefix = Path.Combine(root.FullName, "prefix.json");
            File.WriteAllText(prefix, context.ToJsonString());
            var reports = new List<string>();
            foreach (var input in new[] { original, prefix, mutated })
            {
                var output = Path.Combine(root.FullName, "output-" + reports.Count);
                var errors = new StringWriter();
                Assert.True(Run(input, output, cutoff, errors) == 0, errors.ToString());
                using var report = Report(output);
                reports.Add(report.RootElement.GetProperty("realignment").GetRawText());
            }
            Assert.Equal(reports[0], reports[1]);
            Assert.Equal(reports[0], reports[2]);
        }
        finally { root.Delete(recursive: true); }
    }
}
