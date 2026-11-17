using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using Xunit.Abstractions;

namespace MoneyWay.IntegrationTests;

// Opt-in read of the already produced real canonical run. No new market data, inferred execution or strategy evaluation.
public sealed class Replay2TradeLifecycleDemoTests(ITestOutputHelper output)
{
    [LocalMentorSessionFact]
    public async Task ExportAuthenticReplay2TradeLifecycleThroughExistingDemoApi()
    {
        const string path = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0d69f182a0b456ba4662896192dc242.json";
        const string hash = "41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2";
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development")
            .UseSetting("NasdaqHistoricalDemo:ReportPath", path).UseSetting("NasdaqHistoricalDemo:ExpectedSha256", hash));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/nasdaq/historical-demo?frameStep=7596&reportSha256={hash}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var report = (await response.Content.ReadFromJsonAsync<NasdaqHistoricalReportSummary>())!;
        Assert.Equal(new DateOnly(2026, 9, 8), report.TradingDay); Assert.Equal("US100", report.Symbol);
        Assert.Equal("BoundedCanonicalDiagnostic", report.ReportScope);
        Assert.Equal("NotRevalidated", report.EvidenceValidation);
        var frame = report.SelectedFrame!; var trade = frame.TradeLifecycle!;
        Assert.Equal(DateTimeOffset.Parse("2026-09-08T13:36:00Z"), frame.AsOfUtc);
        Assert.Equal("Wait", frame.Verdict); Assert.Equal("NQ-SL-001", frame.BlockingRuleId);
        Assert.Equal("WickfillScenario2", trade.Path);
        Assert.Equal("Passed", frame.Rules.Single(r => r.RuleId == "NQ-WF2-001").Result);
        Assert.Equal("Passed", frame.Rules.Single(r => r.RuleId == "NQ-WF2-002").Result);
        Assert.Equal("Waiting", trade.Stages.Single(s => s.Stage == "StopLoss").RuleResult);
        Assert.Equal("MissingOrBlocked", trade.Stages.Single(s => s.Stage == "StopLoss").Status);
        Assert.Null(trade.Entry); Assert.Null(trade.StopPrice); Assert.Null(trade.TakeProfitPrice); Assert.Null(trade.Exit);
        Assert.Equal("inconclusive", trade.FailureClassification); Assert.False(trade.CompleteSupervisedTrade);
        Assert.Contains(trade.Sources, s => s.SourceReference.Contains("human:S13", StringComparison.Ordinal));
        var earlier = await client.GetFromJsonAsync<NasdaqHistoricalReportSummary>($"/api/nasdaq/historical-demo?frameStep=7592&reportSha256={hash}");
        Assert.DoesNotContain(earlier!.SelectedFrame!.TradeLifecycle!.Sources, s => s.SourceReference.Contains("human:S13", StringComparison.Ordinal));
        Assert.All(earlier.SelectedFrame.TradeLifecycle.Sources, s => Assert.True(s.ObservedAtUtc <= earlier.SelectedFrame.AsOfUtc));
        using var json = JsonDocument.Parse(body);
        var artifact = new
        {
            Schema = "moneyway.replay2-supervised-trade-view",
            Version = 1,
            SourceCanonicalReportSha256 = hash,
            Summary = json.RootElement.Clone(),
            EarlierStep = earlier.SelectedFrame.Step,
            NoFutureHumanEvidenceTransported = true,
            OriginalEvidenceUnchanged = hash == Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
        };
        var destination = Path.Combine(Path.GetDirectoryName(path)!, $"replay2-trade-lifecycle-{Guid.NewGuid():N}.json");
        await using (var stream = new FileStream(destination, FileMode.CreateNew))
            await JsonSerializer.SerializeAsync(stream, artifact, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine($"ARTIFACT: {destination}\nSHA256: {Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(destination)))}\nBYTES: {new FileInfo(destination).Length}");
        Assert.True(artifact.OriginalEvidenceUnchanged);
    }
}
