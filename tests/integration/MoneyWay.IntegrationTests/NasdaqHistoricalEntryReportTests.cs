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

// Entirely synthetic source/price/time records. Historical observations do not become strategy inputs.
public sealed class NasdaqHistoricalEntryReportTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "moneyway-entry-" + Guid.NewGuid().ToString("N") + ".json");
    private static DateTimeOffset At(int m) => LiquidityFixture.At(13).AddMinutes(m);
    private static NasdaqHistoricalEntryEvidence Review(NasdaqMentorSessionReplayReport report) => new(report.Session,
        "WickfillScenario2", "Sell", "synthetic:review", "synthetic:original-material", "synthetic:human",
        "HumanReview", "PositionOpeningField", "synthetic:MT5", null, "USTEC", "synthetic:16:32 server",
        "SourceReportedSecond", At(32), At(32), "synthetic:explicit-session-only-mapping", null, new(2026, 11, 5),
        123.45678901234567890123456789m, null, null);
    private void Save(NasdaqMentorSessionReplayReport original, params NasdaqHistoricalEntryEvidence[] reviews)
    {
        var input = new NasdaqMentorSessionReplayInput(original.SessionId, original.Session, original.SuppliedCsvFiles, [], reviews);
        var annotated = new NasdaqMentorSessionReplayReport(input, original.Imports, original.InputDiagnostics, original.Canonical, original.Frames, original.BindingDiagnostics);
        Assert.Same(original.Canonical, annotated.Canonical); Assert.Equal(original.Frames, annotated.Frames);
        using var file = File.Create(path); NasdaqMentorSessionReportJson.Write(file, annotated);
    }
    private WebApplicationFactory<Program> App() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        b.UseEnvironment("Development").UseSetting("NasdaqHistoricalDemo:ReportPath", path).UseSetting("NasdaqHistoricalDemo:ExpectedSha256", ""));

    [Fact]
    public async Task RetrospectiveEntryUsesOriginalFrozenFrameAndPreservesCanonicalRulesAndSlRiskTp()
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport(null, true); var review = Review(report); Save(report, review);
        var original = report.Frames.Single(f => f.Strategy.AsOfUtc == At(32));
        var later = report.Frames.Single(f => f.Strategy.AsOfUtc == LiquidityFixture.At(15));
        using var reader = new ReadNasdaqHistoricalReport(); var loaded = await reader.ExecuteAsync(path, null, frameStep: later.Strategy.Step);
        var trade = loaded.SelectedFrame!.TradeLifecycle!; var diagnostic = Assert.IsType<NasdaqHistoricalEntryAssessment>(trade.HistoricalEntry);
        var observation = Assert.Single(diagnostic.Observations);
        Assert.Equal("RetrospectiveOnly", diagnostic.SelectionStatus); Assert.Equal("USTEC", observation.Evidence.SourceSymbol);
        Assert.Equal(review.SourceEntryPriceText, observation.Evidence.SourceEntryPriceText);
        Assert.Equal(original.Strategy.Step, observation.CanonicalAtEntry!.Step);
        Assert.Equal("Waiting", observation.CanonicalAtEntry.Rules.Single(r => r.RuleId == "NQ-WF2-002").Result);
        Assert.Equal("Passed", loaded.SelectedFrame.Rules.Single(r => r.RuleId == "NQ-WF2-002").Result);
        Assert.Contains("OriginalInstrumentMismatch_NoMapping", observation.Issues);
        Assert.Null(trade.Entry); Assert.Null(trade.Exit); Assert.False(trade.CompleteSupervisedTrade); Assert.False(diagnostic.NativeExecutionIndependentlyVerified);
        Assert.NotNull(trade.Scenario2StopLoss); Assert.NotNull(trade.Scenario2TakeProfit);
        Assert.Equal("HumanValidationRequired", trade.Stages.Single(s => s.Stage == "Risk").RuleResult);
        using var app = App(); var http = await app.CreateClient().GetFromJsonAsync<NasdaqHistoricalReportSummary>("/api/nasdaq/historical-demo?frameStep=" + later.Strategy.Step);
        Assert.Equal(JsonSerializer.Serialize(diagnostic), JsonSerializer.Serialize(http!.SelectedFrame!.TradeLifecycle!.HistoricalEntry));
        var before = await reader.ExecuteAsync(path, null, frameStep: original.Strategy.Step);
        Assert.Equal(JsonSerializer.Serialize(before.SelectedFrame!.TradeLifecycle!.HistoricalEntry!.Observations), JsonSerializer.Serialize(diagnostic.Observations));
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("native")]
    [InlineData("backdating")]
    [InlineData("source")]
    [InlineData("availability")]
    public async Task ForgedAuthorizationNativeVerificationAndHistoricalEligibilityFailClosed(string corruption)
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport(null); Save(report, Review(report));
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        var diagnostic = root["Frames"]!.AsArray().First(f => f!["TradeLifecycle"]!["HistoricalEntry"] is not null)!["TradeLifecycle"]!["HistoricalEntry"]!;
        if (corruption == "authorization") diagnostic["EntryAuthorization"] = "Approved";
        if (corruption == "native") diagnostic["NativeExecutionIndependentlyVerified"] = true;
        if (corruption == "backdating") diagnostic["Observations"]![0]!["CanonicalAtEntry"]!["Rules"]!.AsArray()
            .Single(r => r!["RuleId"]!.GetValue<string>() == "NQ-WF2-002")!["Result"] = "Passed";
        if (corruption == "source") root["HistoricalEntryEvidence"]![0]!["SourceReference"] = "";
        if (corruption == "availability") root["HistoricalEntryEvidence"]![0]!["HistoricalAvailableAtUtc"] = At(37);
        File.WriteAllText(path, root.ToJsonString()); using var app = App();
        var response = await app.CreateClient().GetAsync("/api/nasdaq/historical-demo");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ActualCanonicalWf2EvaluatorsPopulateSupervisedDocumentaryParametersWithoutExitOrEconomicCompletion()
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport("100", true);
        var instant = LiquidityFixture.At(15); var frame = report.Frames.Single(f => f.Strategy.AsOfUtc == instant);
        var native = Review(report) with
        {
            Origin = "NativeExecutionRecord",
            EventKind = "ReportedExecution",
            SourceSymbol = report.Session.Symbol.Value,
            SourceProviderId = report.Session.ProviderId.Value,
            SourceEntryPrice = 110,
            SourcePriceUnit = "NativeInstrumentPrice",
            ExecutionReference = "synthetic:deal-123",
            OriginalTime = "synthetic:15:00Z",
            EarliestEventUtc = instant,
            LatestEventUtc = instant,
            TimePrecision = "ExactNativeRecord",
            HistoricalAvailableAtUtc = instant
        };
        Save(report, native); var hash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
        Save(report, native); Assert.Equal(hash, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        using var reader = new ReadNasdaqHistoricalReport();
        var view = await reader.ExecuteAsync(path, null, frameStep: frame.Strategy.Step);
        var trade = view.SelectedFrame!.TradeLifecycle!; var snapshot = Assert.IsType<NasdaqSupervisedHistoricalTradeSnapshot>(trade.SupervisedSnapshot);
        Assert.True(snapshot.HistoricalEntryObserved); Assert.True(snapshot.CanonicalEligibilityRecordedAtEntry);
        Assert.True(snapshot.StopLossSupported); Assert.True(snapshot.RiskExposureSupported); Assert.True(snapshot.TakeProfitSupported);
        Assert.True(snapshot.NativeExecutionDocumented); Assert.True(snapshot.CanonicalBindingsConsistent); Assert.True(snapshot.DocumentedParameterSetAvailable);
        Assert.Equal("native_identity_matches", snapshot.InstrumentCorrespondence);
        Assert.Equal("Ready", snapshot.DocumentaryEntry!.Observations.Single().CanonicalAtEntry!.Verdict);
        Assert.Equal(report.Canonical!.Frames.Single(f => f.Step == frame.Strategy.Step).Verdict.ToString(), snapshot.CanonicalVerdict);
        Assert.Same(trade.Scenario2StopLoss, snapshot.StopLoss); Assert.Same(trade.Scenario2RiskExposure, snapshot.Risk); Assert.Same(trade.Scenario2TakeProfit, snapshot.TakeProfit);
        Assert.Equal(116, snapshot.StopLoss!.StopPrice); Assert.Equal(100, snapshot.Risk!.Exposure.ReportedMaximumLoss);
        Assert.Equal(snapshot.TakeProfit!.TargetReferencePrice, snapshot.TakeProfit.TakeProfitPrice);
        Assert.Equal(110, snapshot.DocumentaryEntry.Observations.Single().Evidence.SourceEntryPrice);
        Assert.False(snapshot.CompleteSupervisedTrade); Assert.False(snapshot.NativeExecutionIndependentlyVerified);
        Assert.False(snapshot.ExitSupported); Assert.False(snapshot.EconomicOutcomeSupported); Assert.Null(snapshot.Exit); Assert.Null(snapshot.EconomicOutcome);
        Assert.Null(snapshot.Risk.Exposure.Quantity); Assert.Null(snapshot.Risk.Exposure.QuantityUnit); Assert.Equal("NotGrantedBySnapshot", snapshot.EntryAuthorization);
        using var app = App(); var http = await app.CreateClient().GetFromJsonAsync<NasdaqHistoricalReportSummary>("/api/nasdaq/historical-demo?frameStep=" + frame.Strategy.Step);
        Assert.True(http!.SelectedFrame!.TradeLifecycle!.SupervisedSnapshot!.DocumentedParameterSetAvailable);
        var earlier = await reader.ExecuteAsync(path, null, frameStep: report.Frames.Single(f => f.Strategy.AsOfUtc == At(32)).Strategy.Step);
        Assert.Null(earlier.SelectedFrame!.TradeLifecycle!.HistoricalEntry);
        Assert.False(earlier.SelectedFrame.TradeLifecycle.SupervisedSnapshot!.HistoricalEntryObserved);
        Assert.Null(earlier.SelectedFrame.TradeLifecycle.SupervisedSnapshot.StopLoss);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("authorization")]
    [InlineData("verification")]
    [InlineData("exit")]
    [InlineData("outcome")]
    [InlineData("instrument")]
    [InlineData("price")]
    public async Task SupervisedSnapshotCannotClaimUnsupportedCompletionOrMergeSourcePrices(string corruption)
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport(null, true); Save(report, Review(report));
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        var snapshot = root["Frames"]!.AsArray().Last(f => f!["TradeLifecycle"]!["SupervisedSnapshot"]?["StopLoss"] is not null)!["TradeLifecycle"]!["SupervisedSnapshot"]!;
        if (corruption == "complete") snapshot["CompleteSupervisedTrade"] = true;
        if (corruption == "authorization") snapshot["EntryAuthorization"] = "Approved";
        if (corruption == "verification") snapshot["NativeExecutionIndependentlyVerified"] = true;
        if (corruption == "exit") snapshot["Exit"] = new JsonObject { ["Price"] = 100, ["EffectiveAtUtc"] = At(37) };
        if (corruption == "outcome") snapshot["EconomicOutcome"] = "Profitable";
        if (corruption == "instrument") snapshot["InstrumentCorrespondence"] = "native_identity_matches";
        if (corruption == "price") snapshot["StopLoss"]!["StopPrice"] = 999;
        File.WriteAllText(path, root.ToJsonString()); using var app = App();
        var response = await app.CreateClient().GetAsync("/api/nasdaq/historical-demo"); Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task KnownFutureReviewCannotAlterAnEarlierReportAndSerializationIsDeterministic()
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); var report = fixture.SyntheticRiskReport(null); var first = Review(report) with { HistoricalAvailableAtUtc = At(32) };
        var at = report.Frames.Single(f => f.Strategy.AsOfUtc == At(33)).Strategy.Step;
        Save(report, first); var firstHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
        Save(report, first); Assert.Equal(firstHash, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        using var reader = new ReadNasdaqHistoricalReport(); var before = (await reader.ExecuteAsync(path, null, frameStep: at)).SelectedFrame!;
        Save(report, first, first with { ReviewId = "synthetic:future-review", HistoricalAvailableAtUtc = LiquidityFixture.At(15), SourceEntryPrice = 900 });
        var after = (await reader.ExecuteAsync(path, null, frameStep: at)).SelectedFrame!;
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
    }
    public void Dispose() => File.Delete(path);
}
