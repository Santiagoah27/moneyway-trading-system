using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class EvaluateStrategyReplayContextUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyRegistryProducesMetadataOnlyObservation()
    {
        var context = Context(Definition(), Series(Minute, Candle(Minute, 0, 1)));
        var result = new EvaluateStrategyReplayContextUseCase([]).Execute(Definition(), context);
        Assert.Equal(Strategy, result.StrategyId); Assert.Equal(Version, result.StrategyVersion);
        Assert.Equal(Provider, result.ProviderId); Assert.Equal(Symbol, result.Symbol);
        Assert.Equal(context.Step, result.Step); Assert.Equal(context.AsOfUtc, result.AsOfUtc); Assert.Empty(result.Evaluations);
    }

    [Fact]
    public void ExactEvaluatorReceivesSameContextAndDefinitionOwnsStructuralMetadata()
    {
        var definition = Definition(); var context = Context(definition, Series(Minute, Candle(Minute, 0, 1)));
        var evaluator = new Fake(Strategy, Version, new("R-1"), new ReplayRuleEvaluationDecision(RuleEvaluationResult.Waiting, "Observed.", "fixture:1"));
        var evaluation = Assert.Single(new EvaluateStrategyReplayContextUseCase([evaluator]).Execute(definition, context).Evaluations);
        Assert.Same(context, evaluator.Context); Assert.Equal(new RuleId("R-1"), evaluation.RuleId);
        Assert.Equal(RuleDefinitionStatus.Candidate, evaluation.DefinitionStatus); Assert.Equal(10, evaluation.Sequence); Assert.True(evaluation.IsRequired);
        Assert.Equal(RuleEvaluationResult.Waiting, evaluation.Result); Assert.Equal("Observed.", evaluation.Reason);
        Assert.Equal("fixture:1", evaluation.EvidenceReference); Assert.Equal(context.AsOfUtc, evaluation.EvaluatedAtUtc);
    }

    [Fact]
    public void ExactIdentityIsRequiredAndOtherRegistrationsAreIgnored()
    {
        var definition = Definition(); var context = Context(definition, Series(Minute, Candle(Minute, 0, 1)));
        var otherStrategy = new Fake(new("other"), Version, new("R-1")); var otherVersion = new Fake(Strategy, new("v2"), new("R-1"));
        Assert.Empty(new EvaluateStrategyReplayContextUseCase([otherStrategy, otherVersion]).Execute(definition, context).Evaluations);
        Assert.Equal(0, otherStrategy.Invocations); Assert.Equal(0, otherVersion.Invocations);
        Assert.Throws<InvalidOperationException>(() => new EvaluateStrategyReplayContextUseCase([]).Execute(definition, Context(Definition("other", "v1"), Series(Minute, Candle(Minute, 0, 1)))));
        Assert.Throws<InvalidOperationException>(() => new EvaluateStrategyReplayContextUseCase([]).Execute(definition, Context(Definition("synthetic", "v2"), Series(Minute, Candle(Minute, 0, 1)))));
    }

    [Fact]
    public void DuplicateUnknownAndNullConfigurationAreRejectedBeforeEvaluation()
    {
        var definition = Definition(); var context = Context(definition, Series(Minute, Candle(Minute, 0, 1))); var fake = new Fake(Strategy, Version, new("R-1"));
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyReplayContextUseCase(null!));
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyReplayContextUseCase(new IReplayRuleEvaluator[] { null! }));
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyReplayContextUseCase([fake, fake]));
        var unknown = new Fake(Strategy, Version, new("UNKNOWN"));
        Assert.Throws<InvalidOperationException>(() => new EvaluateStrategyReplayContextUseCase([unknown]).Execute(definition, context));
        Assert.Equal(0, unknown.Invocations);
    }

    [Fact]
    public void DefinitionSequenceAndStatusesArePreservedWhileMissingRequiredRulesRemainAbsent()
    {
        var rules = new[] { Rule("R-30", 30, RuleDefinitionStatus.Unresolved), Rule("R-10", 10, RuleDefinitionStatus.VisualOnly), Rule("R-20", 20, RuleDefinitionStatus.HumanValidationRequired) };
        var definition = Definition(rules: rules); var context = Context(definition, Series(Minute, Candle(Minute, 0, 1))); var order = new List<int>();
        var e30 = new Fake(Strategy, Version, new("R-30"), callback: () => order.Add(30)); var e10 = new Fake(Strategy, Version, new("R-10"), callback: () => order.Add(10));
        var result = new EvaluateStrategyReplayContextUseCase([e30, e10]).Execute(definition, context);
        Assert.Equal([10, 30], order); Assert.Equal([10, 30], result.Evaluations.Select(x => x.Sequence));
        Assert.Equal([RuleDefinitionStatus.VisualOnly, RuleDefinitionStatus.Unresolved], result.Evaluations.Select(x => x.DefinitionStatus));
    }

    [Fact]
    public void MultiTimeframeStatesAreVisibleAtomicallyIncludingRetainedAndUnavailableFrames()
    {
        var definition = Definition();
        var frames = Frames(Series(Minute, Candle(Minute, 0, 5), Candle(Minute, 5, 6)), Series(Five, Candle(Five, 0, 5)), Series(Hour));
        var factory = new CreateStrategyReplayContextUseCase(); var atomic = factory.Execute(definition, frames[0]); var retained = factory.Execute(definition, frames[1]);
        var evaluator = new Fake(Strategy, Version, new("R-1"), context =>
        {
            Assert.True(context.WasUpdated(Minute)); Assert.True(context.WasUpdated(Five));
            Assert.True(context.TryGetFrame(Minute, out var one)); Assert.True(context.TryGetFrame(Five, out var five));
            Assert.Equal(context.AsOfUtc, one!.AsOfUtc); Assert.Equal(context.AsOfUtc, five!.AsOfUtc);
            return new(RuleEvaluationResult.Passed, $"{one.AvailableCandles.Count}/{five.AvailableCandles.Count}", null);
        });
        Assert.Equal("1/1", Assert.Single(new EvaluateStrategyReplayContextUseCase([evaluator]).Execute(definition, atomic).Evaluations).Reason);
        Assert.False(retained.WasUpdated(Five)); Assert.True(retained.IsAvailable(Five)); Assert.True(retained.TryGetFrame(Five, out var old)); Assert.Equal(Start.AddMinutes(5), old!.AsOfUtc);
        Assert.True(retained.IsConfigured(Hour)); Assert.False(retained.IsAvailable(Hour)); Assert.False(retained.TryGetFrame(Hour, out _));
    }

    [Fact]
    public void FutureDataDoesNotChangeEvaluationAtSameReplayInstant()
    {
        var definition = Definition();
        var a = Context(definition, Series(Minute, Candle(Minute, 0, 1, 100), Candle(Minute, 1, 2, 101)));
        var b = Context(definition, Series(Minute, Candle(Minute, 0, 1, 100), Candle(Minute, 1, 2, 999)));
        static ReplayRuleEvaluationDecision Observe(StrategyReplayContext context) { context.TryGetFrame(Minute, out var frame); return new(RuleEvaluationResult.Passed, frame!.CurrentCandle.Close.ToString(), $"count:{frame.AvailableCandles.Count}"); }
        var useCase = new EvaluateStrategyReplayContextUseCase([new Fake(Strategy, Version, new("R-1"), Observe)]);
        var first = Assert.Single(useCase.Execute(definition, a).Evaluations); var second = Assert.Single(useCase.Execute(definition, b).Evaluations);
        Assert.Equal(first.Result, second.Result); Assert.Equal(first.Reason, second.Reason); Assert.Equal(first.EvidenceReference, second.EvidenceReference);
    }

    [Fact]
    public void NullDecisionAndExceptionStopExecution()
    {
        var definition = Definition(rules: [Rule("R-10", 10), Rule("R-20", 20), Rule("R-30", 30)]); var context = Context(definition, Series(Minute, Candle(Minute, 0, 1)));
        Assert.Throws<InvalidOperationException>(() => new EvaluateStrategyReplayContextUseCase([new Fake(Strategy, Version, new("R-10"), returnNull: true)]).Execute(definition, context));
        var invoked30 = false; var error = new InvalidDataException("failure");
        var evaluators = new[] { new Fake(Strategy, Version, new("R-10")), new Fake(Strategy, Version, new("R-20"), exception: error), new Fake(Strategy, Version, new("R-30"), callback: () => invoked30 = true) };
        Assert.Same(error, Assert.Throws<InvalidDataException>(() => new EvaluateStrategyReplayContextUseCase(evaluators).Execute(definition, context))); Assert.False(invoked30);
    }

    private static StrategyRuleDefinition Rule(string id, int sequence, RuleDefinitionStatus status = RuleDefinitionStatus.Confirmed) => new(new(id), id, "stage", sequence, true, status, "description", "source");
    private static StrategyDefinition Definition(string id = "synthetic", string version = "v1", StrategyRuleDefinition[]? rules = null) => new(new(id), new(version), "Synthetic", "test", rules ?? [Rule("R-1", 10, RuleDefinitionStatus.Candidate)]);
    private static StrategyReplayContext Context(StrategyDefinition definition, params CandleSeries[] series) => new CreateStrategyReplayContextUseCase().Execute(definition, Frames(series)[0]);
    private static List<MultiTimeframeReplayFrame> Frames(params CandleSeries[] series) { var cursor = new MultiTimeframeCandleReplayCursor(series); var result = new List<MultiTimeframeReplayFrame>(); while (cursor.TryAdvance(out var frame)) result.Add(frame!); return result; }
    private static CandleSeries Series(Timeframe timeframe, params Candle[] candles) => new(Provider, Symbol, timeframe, candles);
    private static Candle Candle(Timeframe timeframe, int open, int close, decimal price = 100) => new(Provider, Symbol, timeframe, Start.AddMinutes(open), Start.AddMinutes(close), price, price + 1, price - 1, price, null);

    private sealed class Fake : IReplayRuleEvaluator
    {
        private readonly Func<StrategyReplayContext, ReplayRuleEvaluationDecision>? evaluate;
        private readonly ReplayRuleEvaluationDecision decision;
        private readonly Action? callback; private readonly Exception? exception; private readonly bool returnNull;
        public Fake(StrategyId strategy, StrategyVersion version, RuleId rule, ReplayRuleEvaluationDecision? decision = null, Action? callback = null, Exception? exception = null, bool returnNull = false)
        { StrategyId = strategy; StrategyVersion = version; RuleId = rule; this.decision = decision ?? new(RuleEvaluationResult.Passed, "Synthetic.", null); this.callback = callback; this.exception = exception; this.returnNull = returnNull; }
        public Fake(StrategyId strategy, StrategyVersion version, RuleId rule, Func<StrategyReplayContext, ReplayRuleEvaluationDecision> evaluate) : this(strategy, version, rule) => this.evaluate = evaluate;
        public StrategyId StrategyId { get; }
        public StrategyVersion StrategyVersion { get; }
        public RuleId RuleId { get; }
        public StrategyReplayContext? Context { get; private set; }
        public int Invocations { get; private set; }
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) { Invocations++; Context = context; callback?.Invoke(); if (exception is not null) throw exception; if (returnNull) return null!; return evaluate?.Invoke(context) ?? decision; }
    }
}
