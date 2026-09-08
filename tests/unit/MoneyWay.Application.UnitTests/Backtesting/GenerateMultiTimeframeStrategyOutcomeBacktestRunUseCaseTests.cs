using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCaseTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorAndExecuteRejectNullInputs()
    {
        var backtest = Backtest([]); var outcome = new EvaluateStrategyReplayContextOutcomeUseCase();
        Assert.Throws<ArgumentNullException>(() => new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(null!, outcome));
        Assert.Throws<ArgumentNullException>(() => new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(backtest, null!));
        var useCase = new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(backtest, outcome);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, [Series(Minute)])); Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition(), null!));
    }

    [Fact]
    public void EmptySourcesPreserveConfigurationWithoutOutcomes()
    {
        var result = UseCase([]).Execute(Definition(), [Series(Five), Series(Minute)]);
        Assert.Equal([Minute, Five], result.ConfiguredTimeframes); Assert.Equal(0, result.OutcomeCount); Assert.Empty(result.Outcomes); Assert.Null(result.FirstAsOfUtc); Assert.Null(result.LastAsOfUtc);
    }

    [Fact]
    public void ZeroAndPartialEvaluatorCoverageProduceOnlyIncompleteDataUnavailableOutcomes()
    {
        var definition = Definition(Rule("A", 10, true), Rule("B", 20, true), Rule("C", 30, true));
        foreach (var evaluators in new IReplayRuleEvaluator[][] { [], [new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", _ => RuleEvaluationResult.Passed)] })
        {
            var result = UseCase(evaluators).Execute(definition, [Series(Minute, 1, 2, 3)]);
            Assert.Equal(3, result.OutcomeCount); Assert.Equal(0, result.ReadyCount); Assert.Equal(3, result.DataUnavailableCount); Assert.Equal(3, result.IncompleteRequiredCoverageCount); Assert.Equal(0, result.CompleteCoverageDataUnavailableCount);
        }
    }

    [Fact]
    public void CompleteCoverageProducesEveryVerdictAndAccurateCounts()
    {
        var definition = Definition(Rule("A", 10, true), Rule("B", 20, true));
        var result = UseCase([new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", context => context.Step switch { 1 => RuleEvaluationResult.Waiting, 2 => RuleEvaluationResult.Passed, 3 => RuleEvaluationResult.Failed, 4 => RuleEvaluationResult.HumanValidationRequired, _ => RuleEvaluationResult.DataUnavailable })]).Execute(definition, [Series(Minute, 1, 2, 3, 4, 5)]);
        Assert.Equal([StrategyVerdict.Wait, StrategyVerdict.Ready, StrategyVerdict.NoTrade, StrategyVerdict.HumanValidationRequired, StrategyVerdict.DataUnavailable], result.Outcomes.Select(x => x.Verdict));
        Assert.Equal(1, result.WaitCount); Assert.Equal(1, result.ReadyCount); Assert.Equal(1, result.NoTradeCount); Assert.Equal(1, result.HumanValidationRequiredCount); Assert.Equal(1, result.DataUnavailableCount);
        Assert.Equal(5, result.CompleteRequiredCoverageCount); Assert.Equal(0, result.IncompleteRequiredCoverageCount); Assert.Equal(1, result.CompleteCoverageDataUnavailableCount);
    }

    [Fact]
    public void OptionalMissingOrFailedDoesNotBlockReady()
    {
        var definition = Definition(Rule("A", 10, true), Rule("B", 20, false), Rule("C", 30, true));
        var missing = UseCase([new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("C", _ => RuleEvaluationResult.Passed)]).Execute(definition, [Series(Minute, 1)]);
        var failed = UseCase([new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", _ => RuleEvaluationResult.Failed), new Fake("C", _ => RuleEvaluationResult.Passed)]).Execute(definition, [Series(Minute, 1)]);
        Assert.Equal(StrategyVerdict.Ready, Assert.Single(missing.Outcomes).Verdict); Assert.Equal(StrategyVerdict.Ready, Assert.Single(failed.Outcomes).Verdict);
    }

    [Fact]
    public void GapsDifferentBoundsInputOrderAndRepeatedRunsRemainAlignedWithoutMutatingSources()
    {
        var minute = Series(Minute, 1, 3, 8); var five = Series(Five, 5, 10); var minuteSnapshot = minute.Candles.ToArray(); var fiveSnapshot = five.Candles.ToArray();
        var first = UseCase([new Fake("A", _ => RuleEvaluationResult.Passed)]).Execute(Definition(), [minute, five]);
        var repeated = UseCase([new Fake("A", _ => RuleEvaluationResult.Passed)]).Execute(Definition(), [minute, five]);
        var reordered = UseCase([new Fake("A", _ => RuleEvaluationResult.Passed)]).Execute(Definition(), [five, minute]);
        static IEnumerable<object> Signatures(MultiTimeframeStrategyOutcomeBacktestRun run) => run.Outcomes.Select(x => (object)(x.Step, x.AsOfUtc, x.StrategyId, x.StrategyVersion, x.ProviderId, x.Symbol, x.Verdict, x.Reason));
        Assert.Equal(5, first.OutcomeCount); Assert.Equal(Signatures(first), Signatures(repeated)); Assert.Equal(Signatures(first), Signatures(reordered));
        Assert.All(Enumerable.Range(0, first.OutcomeCount), index => Assert.Same(first.StrategyRun.StrategyObservations[index], first.Outcomes[index].Observation));
        Assert.Equal(minuteSnapshot, minute.Candles); Assert.Equal(fiveSnapshot, five.Candles);
    }

    [Fact]
    public void OutcomeFailurePropagatesWithoutReturningPartialRun()
    {
        var invalid = (RuleEvaluationResult)999; var evaluator = new Fake("A", context => context.Step == 2 ? invalid : RuleEvaluationResult.Passed);
        Assert.Throws<InvalidOperationException>(() => UseCase([evaluator]).Execute(Definition(), [Series(Minute, 1, 2, 3)])); Assert.Equal(3, evaluator.Invocations);
    }

    private static GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase UseCase(IEnumerable<IReplayRuleEvaluator> evaluators) => new(Backtest(evaluators), new());
    private static GenerateMultiTimeframeStrategyBacktestRunUseCase Backtest(IEnumerable<IReplayRuleEvaluator> evaluators) => new(new(), new(), new(evaluators));
    private static StrategyDefinition Definition(params StrategyRuleDefinition[] rules) => new(new("synthetic"), new("v1"), "Synthetic", "test", rules.Length == 0 ? [Rule("A", 10, true)] : rules);
    private static StrategyRuleDefinition Rule(string id, int sequence, bool required) => new(new(id), id, "stage", sequence, required, RuleDefinitionStatus.Confirmed, "description", "source");
    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => new(Provider, Symbol, timeframe, closes.Select(x => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(x - 1), Start.AddMinutes(x), 100, 101, 99, 100, null)));
    private sealed class Fake(string ruleId, Func<StrategyReplayContext, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("synthetic"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = new(ruleId); public int Invocations { get; private set; }
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) { Invocations++; return new(result(context), "Synthetic.", null); }
    }
}
