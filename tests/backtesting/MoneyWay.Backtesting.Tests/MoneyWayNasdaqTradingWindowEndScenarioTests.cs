using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class MoneyWayNasdaqTradingWindowEndScenarioTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);

    [Theory]
    [InlineData(12, 30, 0, RuleEvaluationResult.Waiting, RuleEvaluationResult.Passed)]
    [InlineData(13, 0, 0, RuleEvaluationResult.Waiting, RuleEvaluationResult.Passed)]
    [InlineData(13, 29, 59, RuleEvaluationResult.Waiting, RuleEvaluationResult.Passed)]
    [InlineData(13, 30, 0, RuleEvaluationResult.Passed, RuleEvaluationResult.Passed)]
    [InlineData(15, 0, 0, RuleEvaluationResult.Passed, RuleEvaluationResult.Passed)]
    [InlineData(16, 29, 59, RuleEvaluationResult.Passed, RuleEvaluationResult.Passed)]
    [InlineData(16, 30, 0, RuleEvaluationResult.Passed, RuleEvaluationResult.Passed)]
    [InlineData(16, 30, 1, RuleEvaluationResult.Passed, RuleEvaluationResult.Failed)]
    [InlineData(17, 0, 0, RuleEvaluationResult.Passed, RuleEvaluationResult.Failed)]
    public void RealTimingEvaluatorsComposeWithoutSharingResponsibilities(
        int utcHour,
        int utcMinute,
        int utcSecond,
        RuleEvaluationResult expectedStart,
        RuleEvaluationResult expectedEnd)
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var context = Context(definition, AtUtc(utcHour, utcMinute, utcSecond));

        var observation = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll())
            .Execute(definition, context);

        Assert.Equal([new RuleId("NQ-TIME-001"), new RuleId("NQ-TIME-002")], observation.Evaluations.Select(item => item.RuleId));
        Assert.Equal([100, 260], observation.Evaluations.Select(item => item.Sequence));
        Assert.Equal([expectedStart, expectedEnd], observation.Evaluations.Select(item => item.Result));
    }

    [Fact]
    public void CanonicalBacktestPreservesBothTimingEvaluationsWhileOtherRequiredCoverageIsMissing()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var startRuleId = new RuleId("NQ-TIME-001");
        var endRuleId = new RuleId("NQ-TIME-002");
        var report = Facade().Execute(definition,
        [
            new CandleSeries(Provider, Symbol, Minute,
            [
                Candle(AtUtc(13, 28, 59), AtUtc(13, 29, 59), 100),
                Candle(AtUtc(13, 29, 59), AtUtc(13, 30), 101),
                Candle(AtUtc(16, 29), AtUtc(16, 30), 102),
                Candle(AtUtc(16, 30), AtUtc(16, 30, 1), 103),
            ]),
        ]);

        Assert.Equal(4, report.FrameCount);
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(4, report.DataUnavailableCount);
        Assert.Equal(4, report.IncompleteRequiredCoverageCount);
        Assert.Equal(11, report.MissingRequiredRules.Count);
        Assert.DoesNotContain(report.MissingRequiredRules, item => item.RuleId == startRuleId || item.RuleId == endRuleId);
        Assert.All(report.OutcomeRun.Outcomes, outcome =>
        {
            Assert.False(outcome.HasCompleteRequiredCoverage);
            Assert.DoesNotContain(startRuleId, outcome.MissingRequiredRuleIds);
            Assert.DoesNotContain(endRuleId, outcome.MissingRequiredRuleIds);
            Assert.Equal(11, outcome.MissingRequiredRuleIds.Count);
        });

        var results = report.OutcomeRun.StrategyRun.StrategyObservations
            .Select(observation => observation.Evaluations.Select(evaluation => evaluation.Result).ToArray())
            .ToArray();
        Assert.Equal([RuleEvaluationResult.Waiting, RuleEvaluationResult.Passed], results[0]);
        Assert.Equal([RuleEvaluationResult.Passed, RuleEvaluationResult.Passed], results[1]);
        Assert.Equal([RuleEvaluationResult.Passed, RuleEvaluationResult.Passed], results[2]);
        Assert.Equal([RuleEvaluationResult.Passed, RuleEvaluationResult.Failed], results[3]);
    }

    private static GenerateCanonicalMultiTimeframeBacktestUseCase Facade() =>
        new(new(new(new RunMultiTimeframeReplayUseCase(), new(), new(MoneyWayReplayRuleEvaluators.GetAll())), new()),
            new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase());

    private static StrategyReplayContext Context(StrategyDefinition definition, DateTimeOffset at)
    {
        StrategyReplayContext? context = null;
        new RunMultiTimeframeReplayUseCase().Execute(
            [new CandleSeries(Provider, Symbol, Minute, [Candle(at.AddMinutes(-1), at, 100)])],
            frame => context = new CreateStrategyReplayContextUseCase().Execute(definition, frame));
        return context!;
    }

    private static DateTimeOffset AtUtc(int hour, int minute = 0, int second = 0) =>
        new(2026, 1, 15, hour, minute, second, TimeSpan.Zero);

    private static Candle Candle(DateTimeOffset open, DateTimeOffset close, decimal value) =>
        new(Provider, Symbol, Minute, open, close, value, value + 1, value - 1, value, null);
}
