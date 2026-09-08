using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class CanonicalMultiTimeframeStrategyOutcomeBacktestScenarioTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute); private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ThreeSimultaneousClosesRemainOneGlobalObservationAndOneOutcome()
    {
        var evaluator = new Fake("A", context => context.WasUpdated(Minute) && context.WasUpdated(Five) && context.WasUpdated(Hour) ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting);
        var run = Execute(Definition("A"), [evaluator], [Series(Minute, 1, 5, 6), Series(Five, 5), Series(Hour, 5)]);
        Assert.Equal(3, run.OutcomeCount); Assert.Single(run.StrategyRun.MarketObservations, x => x.AsOfUtc == Start.AddMinutes(5)); Assert.Single(run.StrategyRun.StrategyObservations, x => x.AsOfUtc == Start.AddMinutes(5)); Assert.Single(run.Outcomes, x => x.AsOfUtc == Start.AddMinutes(5));
        Assert.Equal(3, evaluator.Invocations); Assert.Equal(StrategyVerdict.Ready, run.Outcomes.Single(x => x.AsOfUtc == Start.AddMinutes(5)).Verdict);
    }

    [Fact]
    public void PartialAndCompleteRunsExposeDescriptiveCoverageAndVerdictCounts()
    {
        var definition = Definition("A", "B"); var series = new[] { Series(Minute, 1, 2, 3, 4, 5) };
        var partial = Execute(definition, [new Fake("A", _ => RuleEvaluationResult.Passed)], series);
        Assert.Equal(0, partial.ReadyCount); Assert.Equal(partial.OutcomeCount, partial.IncompleteRequiredCoverageCount); Assert.Equal(partial.OutcomeCount, partial.DataUnavailableCount);
        var complete = Execute(definition, [new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", context => context.Step switch { 1 => RuleEvaluationResult.Waiting, 2 => RuleEvaluationResult.Passed, 3 => RuleEvaluationResult.Failed, 4 => RuleEvaluationResult.HumanValidationRequired, _ => RuleEvaluationResult.DataUnavailable })], series);
        Assert.Equal((1, 1, 1, 1, 1), (complete.WaitCount, complete.ReadyCount, complete.NoTradeCount, complete.HumanValidationRequiredCount, complete.DataUnavailableCount)); Assert.Equal(5, complete.CompleteRequiredCoverageCount); Assert.Equal(1, complete.CompleteCoverageDataUnavailableCount);
    }

    [Fact]
    public void InputOrderExactTimeframeIdentityAndFutureIsolationArePreserved()
    {
        var sixty = new Timeframe(60, TimeframeUnit.Minute); var evaluator = new Fake("A", context => { context.TryGetFrame(Minute, out var frame); return frame?.CurrentCandle.Close == 100 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed; });
        var common = new[] { Series(Minute, (1, 100m), (5, 100m)), Series(Hour, (5, 100m)), Series(sixty, (5, 100m)) };
        var first = Execute(Definition("A"), [evaluator], common); var reordered = Execute(Definition("A"), [evaluator], [common[2], common[0], common[1]]);
        Assert.Equal([Minute, sixty, Hour], first.ConfiguredTimeframes); Assert.Equal(Signatures(first), Signatures(reordered));
        var futureChanged = Execute(Definition("A"), [evaluator], [Series(Minute, (1, 100m), (5, 100m), (6, 999m)), Series(Hour, (5, 100m)), Series(sixty, (5, 100m))]);
        Assert.Equal(Signatures(first), Signatures(futureChanged).Take(first.OutcomeCount));
    }

    private static IEnumerable<object> Signatures(MultiTimeframeStrategyOutcomeBacktestRun run) => run.Outcomes.Select(x => (object)(x.Step, x.AsOfUtc, x.Verdict, x.HasCompleteRequiredCoverage, Missing: string.Join(',', x.MissingRequiredRuleIds), x.Reason));
    private static MultiTimeframeStrategyOutcomeBacktestRun Execute(StrategyDefinition definition, IReplayRuleEvaluator[] evaluators, CandleSeries[] series) => new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(new(new(), new(), new(evaluators)), new()).Execute(definition, series);
    private static StrategyDefinition Definition(params string[] rules) => new(new("synthetic"), new("v1"), "Synthetic", "test", rules.Select((id, index) => new StrategyRuleDefinition(new(id), id, "stage", (index + 1) * 10, true, RuleDefinitionStatus.Confirmed, "description", "source")));
    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => Series(timeframe, closes.Select(x => (x, 100m)).ToArray());
    private static CandleSeries Series(Timeframe timeframe, params (int Close, decimal Price)[] values) => new(Provider, Symbol, timeframe, values.Select(x => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(x.Close - 1), Start.AddMinutes(x.Close), x.Price, x.Price + 1, x.Price - 1, x.Price, null)));
    private sealed class Fake(string ruleId, Func<StrategyReplayContext, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("synthetic"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = new(ruleId); public int Invocations { get; private set; }
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) { Invocations++; return new(result(context), "Synthetic.", null); }
    }
}
