using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class H4StructureResearchCommandTests
{
    [Fact]
    public void ExplicitProfilesProduceIsolatedReportWithoutChangingCanonicalSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "moneyway-h4-structure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "original-h4-normalized.csv");
            const string header = "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume\n";
            var highs = new[] { 10, 15, 11, 12, 16, 11, 12, 17, 11, 12 };
            var lows = new[] { 5, 4, 2, 5, 6, 3, 6, 7, 4, 8 };
            var origin = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero);
            var rows = highs.Select((high, index) =>
                $"oanda-global-markets-mt5-demo,US100,4,Hour,{origin.AddHours(index * 4):yyyy-MM-ddTHH:mm:ssZ},{origin.AddHours((index + 1) * 4):yyyy-MM-ddTHH:mm:ssZ},8,{high},{lows[index]},8,1");
            var bytes = Encoding.UTF8.GetBytes(header + string.Join("\n", rows) + "\n");
            File.WriteAllBytes(input, bytes);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var destination = Path.Combine(root, "report");
            var args = new[] { "h4-structure-research", "--input", input, "--sha256", hash,
                "--requested-start-utc", "2026-06-22T00:00:00Z", "--cutoff-utc", "2026-06-23T16:00:00Z",
                "--provenance", "synthetic-test-source", "--output", destination,
                "--profile", "EXP-NQ-H4-STRICT-1-1-V1:1:1", "--profile", "EXP-NQ-H4-STRICT-2-2-V1:2:2",
                "--profile", "EXP-NQ-H4-STRICT-3-3-V1:3:3" };
            var stdout = new StringWriter(); var stderr = new StringWriter();
            Assert.Equal(0, new H4StructureResearchCommand().Execute(args, stdout, stderr));
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.Equal(bytes, File.ReadAllBytes(input));
            using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(destination, "observations.json")));
            Assert.False(report.RootElement.GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.False(report.RootElement.GetProperty("operationalSignal").GetBoolean());
            var results = report.RootElement.GetProperty("observations");
            Assert.Equal(3, results.GetArrayLength());
            Assert.Equal("Bullish", results[0].GetProperty("classification").GetString());
            Assert.Equal("EXP-NQ-H4-STRICT-3-3-V1", results[2].GetProperty("profile").GetProperty("id").GetString());
            Assert.False(File.Exists(Path.Combine(destination, "context-observations.json")));
            Assert.Equal(2, new H4StructureResearchCommand().Execute(args, new StringWriter(), new StringWriter()));

            var contextDestination = Path.Combine(root, "context");
            args[12] = contextDestination;
            Assert.Equal(2, new H4StructureResearchCommand().Execute(
                [.. args, "--context-horizon", "3"], new StringWriter(), new StringWriter()));
            Assert.False(Directory.Exists(contextDestination));
            Assert.Equal(0, new H4StructureResearchCommand().Execute(
                [.. args, "--context-horizon", "1", "--context-horizon", "2"], new StringWriter(), new StringWriter()));
            Assert.Equal(File.ReadAllBytes(Path.Combine(destination, "observations.json")),
                File.ReadAllBytes(Path.Combine(contextDestination, "observations.json")));
            using var context = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(contextDestination, "context-observations.json")));
            var experiments = context.RootElement.GetProperty("observations");
            Assert.Equal(6, experiments.GetArrayLength());
            Assert.Equal(1, experiments[0].GetProperty("horizon").GetInt32());
            Assert.Equal(2, experiments[1].GetProperty("horizon").GetInt32());
            Assert.Equal("EXP-NQ-H4-CONTEXT-V1", experiments[0].GetProperty("candidateId").GetString());
            Assert.Equal(10, experiments[0].GetProperty("candleHistory").GetArrayLength());
            Assert.False(context.RootElement.GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.Equal(bytes, File.ReadAllBytes(input));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
