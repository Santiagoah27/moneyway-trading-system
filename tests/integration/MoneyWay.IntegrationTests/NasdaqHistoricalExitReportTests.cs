using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqHistoricalExitReportTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "moneyway-exit-" + Guid.NewGuid().ToString("N") + ".json");
    private static DateTimeOffset At(int m) => LiquidityFixture.At(13).AddMinutes(m);
    private static NasdaqHistoricalExitEvidence Close(NasdaqMentorSessionReplayReport report) => new(report.Session,
        "synthetic:exit", "synthetic:close-review", "synthetic:human", "HumanReview", "ReportedClosure", "synthetic:platform")
    {
        SourceSymbol = "USTEC",
        OriginalTime = "synthetic:around 08:37",
        OriginalClockDomain = "synthetic:America/Bogota",
        TimePrecision = "Approximate",
        NominalEventUtc = At(37),
        TimeMappingReference = "synthetic:explicit-documentary-mapping",
        LinkedEntryReviewId = "synthetic:entry-review",
        AssociationSourceReference = "synthetic:reviewed-association",
        ReportedStopProtection = "synthetic:approximately 1:1",
        StopProtectionSourceReference = "synthetic:separate-protection-review"
    };
    private void Save(NasdaqMentorSessionReplayReport original, params NasdaqHistoricalExitEvidence[] exits)
    {
        var opening = new NasdaqHistoricalEntryEvidence(original.Session, "WickfillScenario2", "Sell", "synthetic:entry-review",
            "synthetic:original-entry", "synthetic:human", "HumanReview", "ReportedExecution", "synthetic:platform", null, "USTEC",
            "synthetic:13:32Z", "SourceReportedSecond", At(32), At(32), "synthetic:explicit-mapping", null, null, 110, "NativePrice", null);
        var input = new NasdaqMentorSessionReplayInput(original.SessionId, original.Session, original.SuppliedCsvFiles, [], [opening], exits);
        var report = new NasdaqMentorSessionReplayReport(input, original.Imports, original.InputDiagnostics, original.Canonical, original.Frames, original.BindingDiagnostics);
        Assert.Same(original.Canonical, report.Canonical); Assert.Equal(original.Frames, report.Frames);
        using var file = File.Create(path); NasdaqMentorSessionReportJson.Write(file, report);
    }
    private WebApplicationFactory<Program> App() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        b.UseEnvironment("Development").UseSetting("NasdaqHistoricalDemo:ReportPath", path).UseSetting("NasdaqHistoricalDemo:ExpectedSha256", ""));
    [Fact]
    public async Task ActualCanonicalReportAndApiExposeRetrospectiveExitWithoutChangingEntryRulesOrMonetaryResults()
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport(null, true);
        Save(report, Close(report)); var bytes = File.ReadAllBytes(path); Save(report, Close(report)); Assert.Equal(bytes, File.ReadAllBytes(path));
        using var reader = new ReadNasdaqHistoricalReport();
        var earlyStep = report.Frames.Single(f => f.Strategy.AsOfUtc == At(33)).Strategy.Step;
        var lateStep = report.Frames.Single(f => f.Strategy.AsOfUtc == LiquidityFixture.At(15)).Strategy.Step;
        var early = await reader.ExecuteAsync(path, null, frameStep: earlyStep); Assert.Null(early.SelectedFrame!.TradeLifecycle!.HistoricalExit);
        using var app = App(); var view = await app.CreateClient().GetFromJsonAsync<NasdaqHistoricalReportSummary>("/api/nasdaq/historical-demo?frameStep=" + lateStep);
        var s = view!.SelectedFrame!.TradeLifecycle!.SupervisedSnapshot!;
        Assert.True(s.HistoricalExitObserved); Assert.False(s.NativeExitIndependentlyVerified); Assert.False(s.EntryExitRelationshipIndependentlyVerified);
        Assert.False(s.CompleteSupervisedTrade); Assert.Null(s.Exit); Assert.Null(s.EconomicOutcome); Assert.Equal("unresolved", s.InstrumentCorrespondence);
        var e = Assert.Single(s.DocumentaryExit!.Observations); Assert.Equal("RetrospectiveOnly", e.Availability);
        Assert.Equal("DocumentaryCandidate_OrderingRequiresValidation", e.EntryAssociation); Assert.Null(e.Evidence.ExitPrice); Assert.Null(e.Evidence.Quantity);
        Assert.False(s.CanonicalEligibilityRecordedAtEntry); Assert.Equal(At(32), s.DocumentaryEntry!.Observations.Single().CanonicalAtEntry!.AsOfUtc);
        var original = report.Frames.Single(f => f.Strategy.Step == lateStep);
        Assert.Equal(original.Strategy.Evaluations.Select(r => r.Result.ToString()), view.SelectedFrame.Rules.Select(r => r.Result));
        Save(report); var without = await reader.ExecuteAsync(path, null, frameStep: earlyStep);
        Assert.Equal(JsonSerializer.Serialize(early.SelectedFrame), JsonSerializer.Serialize(without.SelectedFrame));
        Save(report, Close(report) with { HistoricalAvailableAtUtc = LiquidityFixture.At(16) });
        var future = await reader.ExecuteAsync(path, null, frameStep: lateStep); Assert.Null(future.SelectedFrame!.TradeLifecycle!.HistoricalExit);
    }
    [Theory]
    [InlineData("native")]
    [InlineData("link")]
    [InlineData("complete")]
    [InlineData("outcome")]
    [InlineData("price")]
    public async Task PersistedExitCannotForgeVerificationCompletionOrSourcePrice(string field)
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport(null, true); Save(report, Close(report));
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        var e = root["Frames"]!.AsArray().First(f => f!["TradeLifecycle"]!["HistoricalExit"] is JsonObject)!["TradeLifecycle"]!["HistoricalExit"]!;
        if (field == "price") e["Observations"]![0]!["Evidence"]!["ExitPrice"] = 999;
        else e[field switch { "native" => "NativeExitIndependentlyVerified", "link" => "EntryExitRelationshipIndependentlyVerified", "complete" => "CompleteSupervisedTrade", _ => "EconomicOutcomeSupported" }] = true;
        File.WriteAllText(path, root.ToJsonString()); using var app = App();
        Assert.NotEqual(HttpStatusCode.OK, (await app.CreateClient().GetAsync("/api/nasdaq/historical-demo")).StatusCode);
    }
    public void Dispose() => File.Delete(path);
}
