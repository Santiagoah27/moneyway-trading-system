using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class GenerateMultiTimeframeStrategyBacktestRunUseCaseTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorAndExecuteRejectNullInputs()
    {
        var replay = new RunMultiTimeframeReplayUseCase(); var context = new CreateStrategyReplayContextUseCase(); var evaluation = new EvaluateStrategyReplayContextUseCase([]);
        Assert.Throws<ArgumentNullException>(() => new GenerateMultiTimeframeStrategyBacktestRunUseCase(null!, context, evaluation));
        Assert.Throws<ArgumentNullException>(() => new GenerateMultiTimeframeStrategyBacktestRunUseCase(replay, null!, evaluation));
        Assert.Throws<ArgumentNullException>(() => new GenerateMultiTimeframeStrategyBacktestRunUseCase(replay, context, null!));
        var useCase = new GenerateMultiTimeframeStrategyBacktestRunUseCase(replay, context, evaluation);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, [Series(Minute)])); Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition(), null!));
    }

    [Fact]
    public void EmptySourcesProduceEmptyRunWithConfiguration()
    {
        var result = UseCase([]).Execute(Definition(), [Series(Five), Series(Minute)]);
        Assert.Equal([Minute, Five], result.ConfiguredTimeframes); Assert.Equal(0, result.ObservationCount); Assert.Empty(result.MarketObservations); Assert.Empty(result.StrategyObservations); Assert.Null(result.FirstAsOfUtc); Assert.Null(result.LastAsOfUtc);
    }

    [Fact]
    public void ZeroEvaluatorsStillProduceOneEmptyStrategyObservationPerGlobalEvent()
    {
        var result = UseCase([]).Execute(Definition(), [Series(Minute, 1, 2, 3), Series(Five, 3)]);
        Assert.Equal(3, result.ObservationCount); Assert.All(result.StrategyObservations, x => Assert.Empty(x.Evaluations));
        Assert.Equal(result.MarketObservations.Select(x => (x.Step, x.AsOfUtc)), result.StrategyObservations.Select(x => (x.Step, x.AsOfUtc)));
    }

    [Fact]
    public void EvaluatorsRunInDefinitionOrderOncePerGlobalEventAndMissingRequiredIsAllowed()
    {
        var order = new List<string>(); var definition = Definition([Rule("A", 10), Rule("B", 20), Rule("C", 30)]);
        var evaluators = new[] { new Fake("C", _ => { order.Add("C"); return Decision(); }), new Fake("A", _ => { order.Add("A"); return Decision(); }) };
        var result = UseCase(evaluators).Execute(definition, [Series(Minute, 1, 2)]);
        Assert.Equal(["A", "C", "A", "C"], order); Assert.All(result.StrategyObservations, x => Assert.Equal([10, 30], x.Evaluations.Select(y => y.Sequence)));
    }

    [Fact]
    public void ContextMetadataCapturesSimultaneousRetainedAndUnavailableTimeframes()
    {
        var hour = new Timeframe(1, TimeframeUnit.Hour); var captured = new List<StrategyReplayContext>();
        var evaluator = new Fake("A", context => { captured.Add(context); return Decision($"{context.UpdatedTimeframes.Count}/{context.AvailableTimeframes.Count}"); });
        var result = UseCase([evaluator]).Execute(Definition(), [Series(Minute, 1, 5, 6), Series(Five, 5), Series(hour, 60)]);
        Assert.Equal(4, result.ObservationCount); Assert.Equal([Minute, Five, hour], result.ConfiguredTimeframes);
        var simultaneous = result.MarketObservations[1]; Assert.Equal(Start.AddMinutes(5), simultaneous.AsOfUtc); Assert.Equal([Minute, Five], simultaneous.UpdatedTimeframes); Assert.Equal([Minute, Five], simultaneous.AvailableTimeframes);
        var retained = result.MarketObservations[2]; Assert.Equal([Minute], retained.UpdatedTimeframes); Assert.Equal([Minute, Five], retained.AvailableTimeframes);
        Assert.True(captured[0].IsConfigured(hour)); Assert.False(captured[0].IsAvailable(hour)); Assert.True(captured[2].TryGetFrame(Five, out var five)); Assert.Equal(Start.AddMinutes(5), five!.AsOfUtc);
    }

    [Fact]
    public void EvaluatorExceptionAbortsBeforeLaterGlobalSteps()
    {
        var calls = 0; var error = new InvalidDataException("stop"); var evaluator = new Fake("A", context => { calls++; if (context.Step == 2) throw error; return Decision(); });
        Assert.Same(error, Assert.Throws<InvalidDataException>(() => UseCase([evaluator]).Execute(Definition(), [Series(Minute, 1, 2, 3)]))); Assert.Equal(2, calls);
    }

    private static GenerateMultiTimeframeStrategyBacktestRunUseCase UseCase(IEnumerable<IReplayRuleEvaluator> evaluators) => new(new(), new(), new(evaluators));
    private static StrategyDefinition Definition(StrategyRuleDefinition[]? rules = null) => new(new("synthetic"), new("v1"), "Synthetic", "test", rules ?? [Rule("A", 10)]);
    private static StrategyRuleDefinition Rule(string id, int sequence) => new(new(id), id, "stage", sequence, true, RuleDefinitionStatus.Confirmed, "description", "source");
    private static ReplayRuleEvaluationDecision Decision(string reason = "Synthetic.") => new(RuleEvaluationResult.Passed, reason, null);
    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => new(Provider, Symbol, timeframe, closes.Select(close => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(close - 1), Start.AddMinutes(close), 100, 101, 99, 100, null)));
    private sealed class Fake(string ruleId, Func<StrategyReplayContext, ReplayRuleEvaluationDecision> evaluate) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("synthetic"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = new(ruleId);
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => evaluate(context);
    }
}
