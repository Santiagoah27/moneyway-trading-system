using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class StrategyOutcomeBacktestRunScenarioTests
{
    private static readonly StrategyId Strategy = new("synthetic-strategy");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Frame = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullFiveFramePipelinePreservesEveryFrameAndCountsVerdicts()
    {
        var definition = Definition(Rule("A", 1, true), Rule("B", 2, true));
        var run = Execute(definition, Series(100, 101, 102, 103, 104),
            new Fake("A", _ => RuleEvaluationResult.Passed),
            new Fake("B", frame => frame.Step switch
            {
                1 => RuleEvaluationResult.Waiting,
                2 => RuleEvaluationResult.HumanValidationRequired,
                3 => RuleEvaluationResult.DataUnavailable,
                4 => RuleEvaluationResult.Failed,
                _ => RuleEvaluationResult.Passed,
            }));

        Assert.Equal(5, run.StrategyRun.MarketReplay.ObservationCount);
        Assert.Equal(5, run.StrategyRun.StrategyObservations.Count);
        Assert.Equal(5, run.OutcomeCount);
        Assert.Equal([1, 2, 3, 4, 5], run.Outcomes.Select(item => item.Step));
        Assert.Equal(1, run.ReadyCount); Assert.Equal(1, run.WaitCount); Assert.Equal(1, run.NoTradeCount);
        Assert.Equal(1, run.HumanValidationRequiredCount); Assert.Equal(1, run.DataUnavailableCount);
        Assert.Equal(5, run.CompleteRequiredCoverageCount); Assert.Equal(1, run.CompleteCoverageDataUnavailableCount);
    }

    [Fact]
    public void PartialCoverageIsDataUnavailableWhileMissingOptionalOrFailedOptionalDoesNotBlockReady()
    {
        var definition = Definition(Rule("A", 1, true), Rule("B", 2, true), Rule("optional", 3, false));
        var partial = Execute(definition, Series(100, 101), new Fake("A", _ => RuleEvaluationResult.Passed));
        Assert.Equal(2, partial.IncompleteRequiredCoverageCount); Assert.Equal(2, partial.DataUnavailableCount); Assert.Equal(0, partial.ReadyCount);

        var missingOptional = Execute(definition, Series(100), new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", _ => RuleEvaluationResult.Passed));
        Assert.Equal(1, missingOptional.ReadyCount); Assert.Equal(1, missingOptional.CompleteRequiredCoverageCount);

        var failedOptional = Execute(definition, Series(100),
            new Fake("A", _ => RuleEvaluationResult.Passed),
            new Fake("B", _ => RuleEvaluationResult.Passed),
            new Fake("optional", _ => RuleEvaluationResult.Failed));
        Assert.Equal(1, failedOptional.ReadyCount);
    }

    [Fact]
    public void FutureCandleChangesCannotAffectEarlierOutcomes()
    {
        var definition = Definition(Rule("A", 1, true));
        var evaluator = new Fake("A", frame => frame.AvailableCandles[^1].Close >= 500
            ? RuleEvaluationResult.Failed
            : RuleEvaluationResult.Passed);
        var first = Execute(definition, Series(100, 101, 200), evaluator);
        var second = Execute(definition, Series(100, 101, 500), evaluator);

        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(first.Outcomes[index].Verdict, second.Outcomes[index].Verdict);
            Assert.Equal(first.Outcomes[index].HasCompleteRequiredCoverage, second.Outcomes[index].HasCompleteRequiredCoverage);
            Assert.Equal(first.Outcomes[index].Observation.Evaluations, second.Outcomes[index].Observation.Evaluations);
        }

        Assert.NotEqual(first.Outcomes[2].Verdict, second.Outcomes[2].Verdict);
    }

    private static StrategyOutcomeBacktestRun Execute(StrategyDefinition definition, CandleSeries series, params IReplayRuleEvaluator[] evaluators)
    {
        var strategyRun = new GenerateStrategyBacktestRunUseCase(new RunCandleReplayUseCase(), new(evaluators));
        return new GenerateStrategyOutcomeBacktestRunUseCase(strategyRun, new()).Execute(definition, series);
    }

    private static StrategyDefinition Definition(params StrategyRuleDefinition[] rules) => new(Strategy, Version, "Synthetic", "test", rules);
    private static StrategyRuleDefinition Rule(string id, int sequence, bool required) => new(new(id), id, "stage", sequence, required, RuleDefinitionStatus.Confirmed, "d", "s");
    private static CandleSeries Series(params decimal[] closes) => new(Provider, Symbol, Frame, closes.Select((close, index) =>
    {
        var open = Start.AddMinutes(index * 5);
        return new Candle(Provider, Symbol, Frame, open, open.AddMinutes(5), 100, Math.Max(101, close), Math.Min(99, close), close, null);
    }));

    private sealed class Fake(string ruleId, Func<ReplayFrame, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public RuleId RuleId { get; } = new(ruleId);
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(result(frame), $"Observed {frame.AvailableCandles.Count} candle(s).", null);
    }
}
