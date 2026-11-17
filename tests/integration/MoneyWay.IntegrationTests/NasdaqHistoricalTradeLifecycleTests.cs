using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Infrastructure.Strategies.Nasdaq;
using Microsoft.AspNetCore.Hosting;

namespace MoneyWay.IntegrationTests;

// Synthetic complete Standard artifacts exercise presentation only. They are not Replay #2 trades.
public sealed class NasdaqHistoricalTradeLifecycleTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"moneyway-trade-lifecycle-{Guid.NewGuid():N}.json");
    private static NasdaqMentorSessionReplayReport Run(DateTimeOffset? end = null, bool evidence = true)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var input = new NasdaqMentorSessionReplayInput("synthetic-trade-lifecycle-only", LiquidityFixture.Session(M5Fixture.At(13)),
            new Dictionary<Timeframe, string>(), evidence ? SyntheticNasdaqMentorSessionFixture.Records() : []);
        var runner = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(), new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()));
        var link = new NasdaqMentorSessionReplay(input);
        var series = M5Fixture.Series().Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Where(c => c.CloseTimeUtc <= (end ?? M5Fixture.At(15, 10))))).ToArray();
        var canonical = new GenerateCanonicalMultiTimeframeBacktestUseCase(new(runner, new()), new())
            .ExecuteMentorSession(MoneyWayNasdaqStrategyDefinition.Instance, series, link);
        return new(input, [], [], canonical, link.Frames, link.Diagnostics);
    }
    private void Save(NasdaqMentorSessionReplayReport report)
    { using var file = File.Create(path); NasdaqMentorSessionReportJson.Write(file, report); }

    [Fact]
    public async Task CoherentRecordedEntryStopTargetRiskAndExitSurviveWriterAndReader()
    {
        var report = Run(); var frame = report.Frames.Last();
        var snapshot = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(frame.Snapshot).Snapshot;
        var factual = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(frame.FactualEvaluation);
        Save(report); var hash = SHA256.HashData(File.ReadAllBytes(path));
        using var reader = new ReadNasdaqHistoricalReport(); var result = await reader.ExecuteAsync(path, Convert.ToHexString(hash));
        var trade = Assert.IsType<NasdaqHistoricalTradeLifecycleSummary>(result.SelectedFrame!.TradeLifecycle);
        Assert.Equal("Standard", trade.Path);
        Assert.Equal(snapshot.EntryPrice, trade.Entry!.Price); Assert.Equal(snapshot.EntryEffectiveAtUtc, trade.Entry.EffectiveAtUtc);
        Assert.Equal(snapshot.StopPrice, trade.StopPrice); Assert.Equal(snapshot.TakeProfitPrice, trade.TakeProfitPrice);
        Assert.Equal(snapshot.Risk.RiskRatio, trade.RiskRatio); Assert.Equal(snapshot.Risk.Exposure.AccountBasisAmount, trade.AccountBasisAmount);
        Assert.Equal(factual.DocumentedExit.ExitPrice, trade.Exit!.Price);
        Assert.Equal(factual.DocumentedExit.ExitEffectiveAtUtc, trade.Exit.EffectiveAtUtc);
        Assert.Equal(factual.DocumentedExit.DocumentedScope?.ToString(), trade.ExitScope);
        Assert.Equal(factual.DocumentedExit.DocumentedReason, trade.DocumentedExitReason);
        Assert.Equal("inconclusive", trade.FailureClassification); Assert.False(trade.CompleteSupervisedTrade);
        Assert.Contains(trade.Stages, s => s.Stage == "EconomicOutcome" && s.Status == "NotImplemented");
        Assert.Contains(trade.Stages, s => s.Stage == "SessionAttempts" && s.Status == "BlockedByUnresolvedSpecification");
        Assert.All(trade.Sources, s => Assert.True(s.ObservedAtUtc <= frame.Strategy.AsOfUtc));
        Assert.Same(frame.Strategy, report.Canonical!.OutcomeRun.StrategyRun.StrategyObservations.Last());
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Fact]
    public async Task AppendedExitAndPricesNeverAppearInEarlierFrameSelection()
    {
        var report = Run(); var early = report.Frames.First(f => f.Strategy.AsOfUtc >= M5Fixture.At(13)); Save(report);
        using var reader = new ReadNasdaqHistoricalReport();
        var selected = (await reader.ExecuteAsync(path, null, frameStep: early.Strategy.Step)).SelectedFrame!;
        var trade = selected.TradeLifecycle!; Assert.Null(trade.Entry); Assert.Null(trade.Exit);
        Assert.Null(trade.StopPrice); Assert.Null(trade.TakeProfitPrice); Assert.Null(trade.RiskRatio);
        Assert.All(trade.Sources, s => Assert.True(s.ObservedAtUtc <= selected.AsOfUtc));
        var prefix = Run(early.Strategy.AsOfUtc);
        Assert.Equal(JsonSerializer.Serialize(NasdaqHistoricalTradeLifecycleSummary.FromFrame(prefix.Frames.Last())), JsonSerializer.Serialize(trade));
    }

    [Fact]
    public void FailedRulesDoNotInventAnExecutedViolationOrAValidStrategyLoss()
    {
        var report = Run(evidence: false);
        var frame = report.Frames.First(f => f.Strategy.Evaluations.Any(e => e.Result == MoneyWay.Domain.Strategies.RuleEvaluationResult.Failed));
        var trade = NasdaqHistoricalTradeLifecycleSummary.FromFrame(frame);
        Assert.NotEmpty(trade.FailedRuleIds); Assert.Null(trade.Entry); Assert.Null(trade.Exit);
        Assert.Equal("inconclusive", trade.FailureClassification); Assert.False(trade.CompleteSupervisedTrade);
    }

    [Theory]
    [InlineData("future-source")]
    [InlineData("invented-stop")]
    [InlineData("invented-completion")]
    [InlineData("null-sources")]
    [InlineData("invented-rule-result")]
    [InlineData("fabricated-economic-outcome")]
    [InlineData("unowned-scenario2-stop")]
    [InlineData("unowned-scenario2-risk")]
    public async Task MalformedOrFuturePresentationEvidenceFailsClosed(string corruption)
    {
        Save(Run()); var root = JsonNode.Parse(File.ReadAllText(path))!;
        var frames = root["Frames"]!.AsArray();
        var frame = corruption == "future-source"
            ? frames.First(f => f!["TradeLifecycle"]!["Sources"]!.AsArray().Count > 0)!
            : frames.First()!;
        var trade = frame["TradeLifecycle"]!;
        if (corruption == "future-source") trade["Sources"]![0]!["ObservedAtUtc"] = frame["AsOfUtc"]!.GetValue<DateTimeOffset>().AddMinutes(1);
        if (corruption == "invented-stop") trade["StopPrice"] = 29662.25m; // forbidden cross-feed substitution, synthetic adversarial payload only
        if (corruption == "invented-completion") trade["CompleteSupervisedTrade"] = true;
        if (corruption == "null-sources") trade["Sources"] = null;
        if (corruption == "invented-rule-result") trade["Stages"]![0]!["RuleResult"] = "Passed";
        if (corruption == "fabricated-economic-outcome") trade["Stages"]![6]!["Status"] = "RecordedArtifact";
        if (corruption == "unowned-scenario2-stop")
        {
            var at = frame["AsOfUtc"]!.GetValue<DateTimeOffset>();
            var source = NasdaqHistoricalTradeLifecycleSummary.Source("SyntheticInput", at, "synthetic:unowned");
            trade["Scenario2StopLoss"] = JsonSerializer.SerializeToNode(new NasdaqHistoricalScenario2Stop("synthetic:unowned", 115, 116, at, at, source, source));
        }
        if (corruption == "unowned-scenario2-risk")
        {
            var at = frame["AsOfUtc"]!.GetValue<DateTimeOffset>();
            trade["Scenario2RiskExposure"] = JsonSerializer.SerializeToNode(new NasdaqHistoricalScenario2Risk(
                new NasdaqRiskExposure(10000, "USD", "synthetic:basis", 100, 110, "synthetic:costs", "synthetic:calculation"),
                116, .01m, false, .01m, true, at, at, NasdaqHistoricalTradeLifecycleSummary.Source("SyntheticRisk", at, "synthetic:unowned")));
        }
        File.WriteAllText(path, root.ToJsonString());
        using var reader = new ReadNasdaqHistoricalReport();
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ExecuteAsync(path, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("100")]
    [InlineData("101")]
    public async Task Scenario2CanonicalRiskSurvivesWriterReaderAndApiWithNoInventedTrade(string? loss)
    {
        using var fixture = new NasdaqScenario2MentorSessionTests();
        var report = fixture.SyntheticRiskReport(loss); Save(report);
        var step = report.Frames.Single(f => f.Strategy.AsOfUtc == LiquidityFixture.At(15)).Strategy.Step;
        using var reader = new ReadNasdaqHistoricalReport();
        var summary = await reader.ExecuteAsync(path, null, frameStep: step);
        var trade = summary.SelectedFrame!.TradeLifecycle!;
        Assert.Equal("WickfillScenario2", trade.Path); Assert.NotNull(trade.Scenario2StopLoss);
        Assert.Equal(loss is null, trade.Scenario2RiskExposure is null);
        if (loss is not null)
        {
            Assert.Equal(loss == "100", trade.Scenario2RiskExposure!.IsWithinLimit);
            Assert.Equal(10000m, trade.Scenario2RiskExposure.Exposure.AccountBasisAmount);
            Assert.Equal("USD", trade.Scenario2RiskExposure.Exposure.Currency);
            Assert.Equal("synthetic:approved-risk", trade.Scenario2RiskExposure.Source.SourceReference);
        }
        Assert.Null(trade.Entry); Assert.Null(trade.Exit); Assert.Null(trade.TakeProfitPrice); Assert.False(trade.CompleteSupervisedTrade);
        using var app = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development").UseSetting("NasdaqHistoricalDemo:ReportPath", path)
                .UseSetting("NasdaqHistoricalDemo:ExpectedSha256", ""));
        var response = await app.CreateClient().GetAsync("/api/nasdaq/historical-demo?frameStep=" + step);
        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(loss is null, json["selectedFrame"]!["tradeLifecycle"]!["scenario2RiskExposure"] is null);
        if (loss is not null)
        {
            var risk = json["selectedFrame"]!["tradeLifecycle"]!["scenario2RiskExposure"]!;
            Assert.Equal("10000", risk["accountBasisAmountText"]!.GetValue<string>());
            Assert.Equal(loss, risk["reportedMaximumLossText"]!.GetValue<string>());
            Assert.Equal(loss == "100", risk["isWithinLimit"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task OlderScenario2SlReportRetainsItsHistoricalUnimplementedRiskProjection()
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); Save(fixture.SyntheticRiskReport(null));
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var f in root["Frames"]!.AsArray())
            if (f!["TradeLifecycle"]!["Path"]!.GetValue<string>() == "WickfillScenario2")
                f["TradeLifecycle"]!["Stages"]!.AsArray().Single(s => s!["Stage"]!.GetValue<string>() == "Risk")!["Status"] = "NotImplemented";
        File.WriteAllText(path, root.ToJsonString());
        using var reader = new ReadNasdaqHistoricalReport(); await reader.ExecuteAsync(path, null);
    }

    [Theory]
    [InlineData("future")]
    [InlineData("mismatch")]
    [InlineData("limit")]
    [InlineData("stop")]
    public async Task Scenario2RiskProjectionCannotContradictItsPublishedOwner(string corruption)
    {
        using var fixture = new NasdaqScenario2MentorSessionTests(); Save(fixture.SyntheticRiskReport("100"));
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        var frame = root["Frames"]!.AsArray().First(f => f!["TradeLifecycle"]!["Scenario2RiskExposure"] is not null)!;
        var risk = frame["TradeLifecycle"]!["Scenario2RiskExposure"]!;
        if (corruption == "future") risk["ObservedAtUtc"] = frame["AsOfUtc"]!.GetValue<DateTimeOffset>().AddMinutes(1);
        if (corruption == "mismatch") risk["IsWithinLimit"] = false;
        if (corruption == "limit") risk["Limit"] = .02m;
        if (corruption == "stop") risk["StopPrice"] = 117;
        File.WriteAllText(path, root.ToJsonString());
        using var reader = new ReadNasdaqHistoricalReport();
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ExecuteAsync(path, null));
    }

    [Fact]
    public async Task LegacyReportRemainsReadableWithoutInventingTradeDetails()
    {
        Save(Run()); var root = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var frame in root["Frames"]!.AsArray()) frame!.AsObject().Remove("TradeLifecycle");
        File.WriteAllText(path, root.ToJsonString());
        using var reader = new ReadNasdaqHistoricalReport(); var summary = await reader.ExecuteAsync(path, null);
        Assert.Equal("PublishedArtifactsWithoutDetails", summary.SelectedFrame!.TradeLifecycle!.EvidenceScope);
        Assert.Equal("RecordedArtifactWithoutDetails", summary.SelectedFrame.TradeLifecycle.Stages.Single(s => s.Stage == "Entry").Status);
        Assert.Null(summary.SelectedFrame.TradeLifecycle.Entry); Assert.Null(summary.SelectedFrame.TradeLifecycle.Exit);
        Assert.False(summary.SelectedFrame.TradeLifecycle.CompleteSupervisedTrade);
    }

    [Fact]
    public void SourcePreviewRetainsDigestAndMarksTruncationWithoutChangingOriginal()
    {
        var original = "human:approved-source:" + new string('x', 1000);
        var source = NasdaqHistoricalTradeLifecycleSummary.Source("SyntheticInput", M5Fixture.At(13), original);
        Assert.True(source.Truncated); Assert.Equal(512, source.SourceReference.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original))), source.SourceReferenceSha256);
        Assert.Equal(1022, original.Length);
    }
    public void Dispose() => File.Delete(path);
}
