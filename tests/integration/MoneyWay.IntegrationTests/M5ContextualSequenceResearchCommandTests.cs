using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class M5ContextualSequenceResearchCommandTests
{
    private static string Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src/frontend/moneyway-web/src/research/context/data");
    }

    private static JsonDocument Run(string input, string output, string cutoff)
    {
        const string filename = "contextual-sequence.json";
        var errors = new StringWriter();
        var code = new NasdaqSessionLiquidityResearchCommand().Execute(
            ["nasdaq-session-liquidity", "--input", input, "--output", output, "--compare-context-sequence", "--cutoff-utc", cutoff],
            new StringWriter(), errors);
        Assert.True(code == 0, errors.ToString());
        return JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, filename)));
    }

    [Fact]
    public void ExposedLlE1ReproducesChronologyAndWindowEligibilityWithLaterZoneInvalidation()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-sequence-");
        try
        {
            var input = Path.Combine(Sources(), "june23-context.json");
            using var doc = Run(input, Path.Combine(root.FullName, "full"), "2026-06-23T16:30:00Z");
            var sequence = doc.RootElement.GetProperty("sequence");
            Assert.False(sequence.GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.False(sequence.GetProperty("operationalSignal").GetBoolean());
            var episode = sequence.GetProperty("episodes").EnumerateArray().Single(e =>
                e.GetProperty("selection").GetProperty("liquidityReference").GetProperty("category").GetString() == "LL" &&
                e.GetProperty("selection").GetProperty("policyId").GetString()!.EndsWith("-E1-V1", StringComparison.Ordinal));
            var cases = sequence.GetProperty("results").EnumerateArray().Where(r =>
                r.GetProperty("episodeId").GetString() == episode.GetProperty("id").GetString()).ToArray();
            Assert.Equal(24, cases.Length);
            foreach (var item in cases)
            {
                Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 25, 0, TimeSpan.Zero), item.GetProperty("chronologicalFirstUtc").GetDateTimeOffset());
                switch (item.GetProperty("configuration").GetProperty("windowPolicy").GetString())
                {
                    case "PostSweepObservation":
                        Assert.Equal("IFVGFirst", item.GetProperty("firstOutcome").GetString());
                        Assert.False(item.GetProperty("historicallyCompleted").GetBoolean());
                        break;
                    case "WindowEventOnly":
                        Assert.Equal("SweepOutsideWindow", item.GetProperty("sweepReason").GetString());
                        Assert.False(item.GetProperty("historicallyCompleted").GetBoolean());
                        break;
                    default:
                        Assert.Equal("StructuralChangeFirst", item.GetProperty("firstOutcome").GetString());
                        Assert.True(item.GetProperty("historicallyCompleted").GetBoolean());
                        Assert.Equal(new DateTimeOffset(2026, 6, 23, 13, 55, 0, TimeSpan.Zero), item.GetProperty("completedAtUtc").GetDateTimeOffset());
                        Assert.Contains(item.GetProperty("lifecycle").EnumerateArray(), t => t.GetProperty("state").GetString() == "FVGInvalidated");
                        break;
                }
            }
            Assert.Contains(sequence.GetProperty("confirmationAssociations").EnumerateArray(), a => a.GetProperty("ambiguous").GetBoolean());

            var frozen = Path.Combine(Sources(), "contextual-validation", "context.json");
            var before = SHA256.HashData(File.ReadAllBytes(frozen));
            var target = Path.Combine(root.FullName, "protected");
            Assert.Equal(2, new NasdaqSessionLiquidityResearchCommand().Execute(
                ["nasdaq-session-liquidity", "--input", frozen, "--output", target, "--compare-context-sequence"],
                new StringWriter(), new StringWriter()));
            Assert.False(Directory.Exists(target));
            Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(frozen)));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void SourcePrefixAndFutureMutationHaveIdenticalSequenceAtExactCutoff()
    {
        var root = Directory.CreateTempSubdirectory("moneyway-sequence-prefix-");
        try
        {
            var original = Path.Combine(Sources(), "june23-context.json");
            const string cutoff = "2026-06-23T13:35:00Z";
            var boundary = new DateTimeOffset(2026, 6, 23, 13, 35, 0, TimeSpan.Zero);
            var context = JsonNode.Parse(File.ReadAllBytes(original))!;
            var candles = context["series"]!["M5"]!["candles"]!.AsArray();
            var future = candles.First(c => c!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary)!;
            future["high"] = 99999m;
            future["low"] = 1m;
            var mutated = Path.Combine(root.FullName, "mutated.json");
            File.WriteAllText(mutated, context.ToJsonString());
            for (var i = candles.Count - 1; i >= 0 && candles[i]!["closeTimeUtc"]!.GetValue<DateTimeOffset>() > boundary; i--)
                candles.RemoveAt(i);
            var prefix = Path.Combine(root.FullName, "prefix.json");
            File.WriteAllText(prefix, context.ToJsonString());
            using var baseline = Run(original, Path.Combine(root.FullName, "original"), cutoff);
            using var removed = Run(prefix, Path.Combine(root.FullName, "removed"), cutoff);
            using var changed = Run(mutated, Path.Combine(root.FullName, "changed"), cutoff);
            Assert.Equal(baseline.RootElement.GetProperty("sequence").GetRawText(), removed.RootElement.GetProperty("sequence").GetRawText());
            Assert.Equal(baseline.RootElement.GetProperty("sequence").GetRawText(), changed.RootElement.GetProperty("sequence").GetRawText());
        }
        finally { root.Delete(recursive: true); }
    }
}
