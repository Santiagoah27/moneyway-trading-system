using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class CanonicalMultiTimeframeStrategyBacktestDiagnosticsScenarioTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullCanonicalPipelineProjectsEveryVerdictAndRealBlocker()
    {
        var definition = Definition("A", "B");
        var report = Pipeline(definition, [Series(Minute, 1, 2, 3, 4, 5)],
            new Fake("A", _ => RuleEvaluationResult.Passed),
            new Fake("B", context => context.Step switch
            {
                1 => RuleEvaluationResult.Waiting,
                2 => RuleEvaluationResult.Passed,
                3 => RuleEvaluationResult.Failed,
                4 => RuleEvaluationResult.HumanValidationRequired,
                _ => RuleEvaluationResult.DataUnavailable,
            }));

        Assert.Equal(5, report.FrameCount);
        Assert.Equal((1, 1, 1, 1, 1),
            (report.WaitCount, report.ReadyCount, report.NoTradeCount, report.HumanValidationRequiredCount, report.DataUnavailableCount));
        Assert.Equal(5, report.CompleteRequiredCoverageCount);
        Assert.Equal(1, report.CompleteCoverageDataUnavailableCount);
        Assert.Equal([StrategyVerdict.Wait, StrategyVerdict.NoTrade, StrategyVerdict.HumanValidationRequired, StrategyVerdict.DataUnavailable],
            report.BlockingRules.Select(item => item.Verdict));
        Assert.All(report.BlockingRules, item => Assert.Equal(new RuleId("B"), item.RuleId));
    }

    [Fact]
    public void PartialCoverageProducesMissingFrequenciesWithoutInferredBlockers()
    {
        var report = Pipeline(Definition("A", "B", "C"), [Series(Minute, 1, 2, 3)],
            new Fake("A", _ => RuleEvaluationResult.Passed));
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(3, report.IncompleteRequiredCoverageCount);
        Assert.Equal(3, report.DataUnavailableCount);
        Assert.Empty(report.BlockingRules);
        Assert.Equal([("B", 3), ("C", 3)], report.MissingRequiredRules.Select(item => (item.RuleId.Value, item.Count)));
    }

    [Fact]
    public void AtomicClosesExactTimeframesAndFutureIsolationRemainAuditable()
    {
        var definition = Definition("A");
        var evaluator = new Fake("A", context =>
        {
            context.TryGetFrame(Minute, out var frame);
            return frame!.CurrentCandle.Close < 500 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed;
        });
        var atomic = Pipeline(definition,
            [Series(Minute, 1, 5), Series(FiveMinutes, 5), Series(Hour, 5)], evaluator);
        var at = Start.AddMinutes(5);
        var frameAt = Assert.Single(atomic.Frames, frame => frame.AsOfUtc == at);
        Assert.Equal([Minute, FiveMinutes, Hour], frameAt.UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes, Hour], frameAt.AvailableTimeframes);
        Assert.Single(atomic.OutcomeRun.Outcomes, outcome => outcome.AsOfUtc == at);

        var sixtyMinutes = new Timeframe(60, TimeframeUnit.Minute);
        var exact = Pipeline(definition, [Series(Minute, 1), Series(sixtyMinutes, 1), Series(Hour, 1)],
            new Fake("A", _ => RuleEvaluationResult.Passed));
        Assert.Equal([Minute, sixtyMinutes, Hour], exact.ConfiguredTimeframes);
        Assert.Equal([Minute, sixtyMinutes, Hour], Assert.Single(exact.Frames).UpdatedTimeframes);

        var futureA = Pipeline(definition, [Series(Minute, (1, 100m), (5, 100m), (6, 200m))],
            new Fake("A", context => CurrentClose(context) < 500 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed));
        var futureB = Pipeline(definition, [Series(Minute, (1, 100m), (5, 100m), (6, 900m))],
            new Fake("A", context => CurrentClose(context) < 500 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed));
        Assert.Equal(Signatures(futureA).Take(2), Signatures(futureB).Take(2));
        Assert.NotEqual(futureA.Frames[2].Verdict, futureB.Frames[2].Verdict);
    }

    private static decimal CurrentClose(StrategyReplayContext context)
    {
        context.TryGetFrame(Minute, out var frame);
        return frame!.CurrentCandle.Close;
    }

    private static IEnumerable<object> Signatures(MultiTimeframeStrategyBacktestDiagnosticsReport report) =>
        report.Frames.Select(frame => (object)(frame.Step, frame.AsOfUtc, frame.Verdict, frame.HasCompleteRequiredCoverage,
            frame.Reason, Blocker: frame.BlockingRuleId?.Value, frame.BlockingSequence, frame.BlockingResult,
            Missing: string.Join(',', frame.MissingRequiredRuleIds), Updated: string.Join(',', frame.UpdatedTimeframes),
            Available: string.Join(',', frame.AvailableTimeframes)));

    private static MultiTimeframeStrategyBacktestDiagnosticsReport Pipeline(
        StrategyDefinition definition,
        CandleSeries[] series,
        params IReplayRuleEvaluator[] evaluators)
    {
        var outcomeGenerator = new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(
            new(new(), new(), new(evaluators)), new());
        var outcomeRun = outcomeGenerator.Execute(definition, series);
        return new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase().Execute(definition, outcomeRun);
    }

    private static StrategyDefinition Definition(params string[] ruleIds) => new(
        Strategy, Version, "Synthetic", "test",
        ruleIds.Select((id, index) => new StrategyRuleDefinition(new(id), id, "stage", (index + 1) * 10, true,
            RuleDefinitionStatus.Confirmed, "description", "source")));

    private static CandleSeries Series(Timeframe timeframe, params int[] closes) =>
        Series(timeframe, closes.Select(close => (close, 100m)).ToArray());

    private static CandleSeries Series(Timeframe timeframe, params (int Close, decimal Price)[] values) => new(
        Provider, Symbol, timeframe,
        values.Select(value => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(value.Close - 1),
            Start.AddMinutes(value.Close), value.Price, value.Price + 1, value.Price - 1, value.Price, null)));

    private sealed class Fake(string ruleId, Func<StrategyReplayContext, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public RuleId RuleId { get; } = new(ruleId);
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(result(context), "Synthetic.", null);
    }
}
