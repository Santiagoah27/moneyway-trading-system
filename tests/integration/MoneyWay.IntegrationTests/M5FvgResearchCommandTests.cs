using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class M5FvgResearchCommandTests
{
    [Fact]
    public void PublishesExplicitProfilesFromVerifiedOriginalM5Csv()
    {
        var path = Path.GetTempFileName();
        try
        {
            const string header = "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume";
            const string prefix = "oanda-global-markets-mt5-demo,US100,5,Minute,";
            var bytes = Encoding.UTF8.GetBytes(string.Join('\n', header,
                prefix + "2026-01-05T00:00:00Z,2026-01-05T00:05:00Z,95,100,90,97,1",
                prefix + "2026-01-05T00:05:00Z,2026-01-05T00:10:00Z,97,115,95,100,1",
                prefix + "2026-01-05T00:10:00Z,2026-01-05T00:15:00Z,112,117,110,115,1",
                prefix + "2026-01-05T00:15:00Z,2026-01-05T00:20:00Z,95,97,92,96,1"));
            File.WriteAllBytes(path, bytes);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var args = new[] { "m5-fvg-research", "--input", path, "--sha256", hash,
                "--start-utc", "2026-01-05T00:00:00Z", "--cutoff-utc", "2026-01-05T00:20:00Z",
                "--provenance", "SyntheticFixture", "--profile", "GeometryOnly", "--profile", "MiddleCloseConfirmation" };
            var output = new StringWriter();
            var error = new StringWriter();
            var command = new M5FvgResearchCommand();
            Assert.Equal(0, command.Execute(args, output, error));
            Assert.Empty(error.ToString());
            using var document = JsonDocument.Parse(output.ToString());
            Assert.Equal(hash, document.RootElement.GetProperty("inputSha256").GetString());
            var reports = document.RootElement.GetProperty("reports");
            Assert.Equal(2, reports.GetArrayLength());
            var zones = reports[0].GetProperty("zones");
            Assert.Equal(1, zones.GetArrayLength());
            Assert.Equal("Bearish", zones[0].GetProperty("invertedDirection").GetString());
            Assert.Equal(0, reports[1].GetProperty("zones").GetArrayLength());
            Assert.False(reports[0].GetProperty("operationalSignal").GetBoolean());
            Assert.False(reports[0].GetProperty("canonicalAdmissionPermitted").GetBoolean());
            Assert.Equal(bytes, File.ReadAllBytes(path));
            args[4] = new string('0', 64);
            var rejected = new StringWriter();
            Assert.Equal(2, command.Execute(args, rejected, error));
            Assert.Empty(rejected.ToString());
            Assert.Contains("SHA256 mismatch", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
