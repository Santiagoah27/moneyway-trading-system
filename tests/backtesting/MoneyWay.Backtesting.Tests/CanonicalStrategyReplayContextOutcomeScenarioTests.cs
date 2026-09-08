using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class CanonicalStrategyReplayContextOutcomeScenarioTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero); private static readonly EvaluateStrategyReplayContextOutcomeUseCase OutcomeUseCase = new();

    [Fact]
    public void PartialCanonicalCoverageNeverProducesReadyAndCompleteCoverageTracksChangingResults()
    {
        var definition = Definition(); var partialRun = Run(definition, [new Fake("A", _ => RuleEvaluationResult.Passed)]);
        var partial = partialRun.StrategyObservations.Select(x => OutcomeUseCase.Execute(definition, x)).ToArray();
        Assert.All(partial, outcome => { Assert.False(outcome.HasCompleteRequiredCoverage); Assert.Equal([new RuleId("B")], outcome.MissingRequiredRuleIds); Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict); Assert.Null(outcome.EvaluationOutcome); });
        var completeRun = Run(definition, [new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", context => context.Step switch { 1 => RuleEvaluationResult.Waiting, 2 => RuleEvaluationResult.Passed, _ => RuleEvaluationResult.Failed })]);
        var complete = completeRun.StrategyObservations.Select(x => OutcomeUseCase.Execute(definition, x)).ToArray();
        Assert.All(complete, outcome => Assert.True(outcome.HasCompleteRequiredCoverage)); Assert.Equal([StrategyVerdict.Wait, StrategyVerdict.Ready, StrategyVerdict.NoTrade], complete.Select(x => x.Verdict));
        var humanValidation = Run(definition, [new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", _ => RuleEvaluationResult.HumanValidationRequired)]);
        Assert.All(humanValidation.StrategyObservations, observation => Assert.Equal(StrategyVerdict.HumanValidationRequired, OutcomeUseCase.Execute(definition, observation).Verdict));
    }

    [Fact]
    public void SimultaneousCloseCreatesOneOutcomeAndRetainedContextRemainsEvaluatorOwned()
    {
        var definition = Definition(singleRule: true); var evaluator = new Fake("A", context =>
        {
            if (context.AsOfUtc == Start.AddMinutes(5)) return context.WasUpdated(Minute) && context.WasUpdated(Five) ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed;
            if (context.AsOfUtc == Start.AddMinutes(6)) return context.IsAvailable(Five) && !context.WasUpdated(Five) ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed;
            return RuleEvaluationResult.Waiting;
        });
        var run = Run(definition, [evaluator], [Series(Minute, 1, 5, 6), Series(Five, 5)]);
        var atFive = Assert.Single(run.StrategyObservations, x => x.AsOfUtc == Start.AddMinutes(5)); var outcome = OutcomeUseCase.Execute(definition, atFive);
        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict); Assert.Single(run.MarketObservations, x => x.AsOfUtc == Start.AddMinutes(5));
        Assert.Equal(StrategyVerdict.Ready, OutcomeUseCase.Execute(definition, run.StrategyObservations.Single(x => x.AsOfUtc == Start.AddMinutes(6))).Verdict);
    }

    [Fact]
    public void ExplicitDataUnavailableDiffersFromMissingCoverageAndFutureDataDoesNotChangeEarlierOutcome()
    {
        var definition = Definition(singleRule: true); var explicitRun = Run(definition, [new Fake("A", context => context.IsAvailable(Five) ? RuleEvaluationResult.Passed : RuleEvaluationResult.DataUnavailable)], [Series(Minute, 1, 5), Series(Five, 5)]);
        var explicitOutcome = OutcomeUseCase.Execute(definition, explicitRun.StrategyObservations[0]);
        Assert.True(explicitOutcome.HasCompleteRequiredCoverage); Assert.NotNull(explicitOutcome.EvaluationOutcome); Assert.Equal(StrategyVerdict.DataUnavailable, explicitOutcome.Verdict);
        var missingOutcome = OutcomeUseCase.Execute(definition, Run(definition, [], [Series(Minute, 1), Series(Five, 5)]).StrategyObservations[0]);
        Assert.False(missingOutcome.HasCompleteRequiredCoverage); Assert.Null(missingOutcome.EvaluationOutcome); Assert.Equal(StrategyVerdict.DataUnavailable, missingOutcome.Verdict);
        var evaluator = new Fake("A", context => { context.TryGetFrame(Minute, out var frame); return frame!.CurrentCandle.Close == 100 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed; });
        var first = Outcomes(definition, [Series(Minute, (1, 100m), (2, 101m))], evaluator)[0]; var second = Outcomes(definition, [Series(Minute, (1, 100m), (2, 999m))], evaluator)[0];
        Assert.Equal((first.Verdict, first.Reason, first.HasCompleteRequiredCoverage), (second.Verdict, second.Reason, second.HasCompleteRequiredCoverage));
    }

    private static StrategyReplayContextOutcome[] Outcomes(StrategyDefinition definition, CandleSeries[] series, Fake evaluator) => Run(definition, [evaluator], series).StrategyObservations.Select(x => OutcomeUseCase.Execute(definition, x)).ToArray();
    private static MultiTimeframeStrategyBacktestRun Run(StrategyDefinition definition, IReplayRuleEvaluator[] evaluators, CandleSeries[]? series = null) => new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(evaluators)).Execute(definition, series ?? [Series(Minute, 1, 2, 3)]);
    private static StrategyDefinition Definition(bool singleRule = false) => new(new("synthetic"), new("v1"), "Synthetic", "test", singleRule ? [Rule("A", 10)] : [Rule("A", 10), Rule("B", 20)]);
    private static StrategyRuleDefinition Rule(string id, int sequence) => new(new(id), id, "stage", sequence, true, RuleDefinitionStatus.Confirmed, "description", "source");
    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => Series(timeframe, closes.Select(x => (x, 100m)).ToArray());
    private static CandleSeries Series(Timeframe timeframe, params (int Close, decimal Price)[] values) => new(Provider, Symbol, timeframe, values.Select(x => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(x.Close - 1), Start.AddMinutes(x.Close), x.Price, x.Price + 1, x.Price - 1, x.Price, null)));
    private sealed class Fake(string ruleId, Func<StrategyReplayContext, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("synthetic"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = new(ruleId);
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(result(context), "Synthetic.", null);
    }
}
