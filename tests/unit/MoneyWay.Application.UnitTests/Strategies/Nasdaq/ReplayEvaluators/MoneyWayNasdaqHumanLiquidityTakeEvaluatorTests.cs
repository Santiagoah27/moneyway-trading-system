using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanLiquidityTakeEvaluatorTests
{
    private static readonly DateTimeOffset Day = LiquidityFixture.At(0);
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset[] Boundaries = [At(13, 15), At(13, 30), At(14, 2), At(14, 37), At(15), At(16), At(37, 15), At(37, 30), At(38, 2)];
    private readonly MoneyWayNasdaqHumanLiquidityTakeEvaluator evaluator = new();
    private static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    [Fact]
    public void MissingIsHumanReviewAndNoUnselectedTakeAutomaticallyPasses()
    {
        var take = Take(false);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context(At(14, 2), [take])).Result);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, false, RuleEvaluationResult.Passed)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, true, RuleEvaluationResult.Passed)]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, true, RuleEvaluationResult.Failed)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, false, RuleEvaluationResult.Failed)]
    public void CanonicalPriorFactsDetermineAlignmentAndPreserveTimestamp(NasdaqHumanH4PermittedDirection direction, bool high, RuleEvaluationResult expected)
    {
        var inputs = Inputs(direction, Take(high));
        var run = Run(inputs, complete: true);
        var atEvent = run.StrategyObservations.Single(o => o.AsOfUtc == At(14, 2));
        var evaluation = Assert.Single(atEvent.Evaluations, e => e.RuleId == evaluator.RuleId);
        Assert.Equal(expected, evaluation.Result);
        Assert.Equal(atEvent.AsOfUtc, evaluation.EvaluatedAtUtc);
        Assert.Equal(LiquidityFixture.Definition.Rules.Single(r => r.RuleId == evaluator.RuleId).DefinitionStatus, evaluation.DefinitionStatus);
        Assert.Contains("TakeEffectiveAtUtc", evaluation.EvidenceReference);
        Assert.Contains("review:relevant", evaluation.EvidenceReference);
        var fact = Assert.IsType<NasdaqLiquidityTakeRuleFact>(Assert.Single(atEvent.RuleFacts, f => f.RuleId == evaluator.RuleId).Fact);
        Assert.Equal(expected == RuleEvaluationResult.Failed, fact.IsSessionInvalidated);
        var outcome = new EvaluateStrategyReplayContextOutcomeUseCase().Execute(LiquidityFixture.Definition, atEvent);
        if (expected == RuleEvaluationResult.Failed) Assert.Equal(StrategyVerdict.NoTrade, outcome.Verdict);
        else Assert.NotNull(atEvent.LifecycleProgression!.ActiveInstance);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, true)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, false)]
    public void LaterAlignedTakeCannotReviveTerminalSessionAndDownstreamEvaluatorIsNotCalled(NasdaqHumanH4PermittedDirection direction, bool firstHigh)
    {
        var first = Take(firstHigh);
        var later = Take(!firstHigh, effective: At(14, 37), observed: At(14, 37), eventId: "later");
        var counter = new CountingTrigger();
        var run = Run(Inputs(direction, first).Concat([later, Relevant(later)]).ToArray(), trigger: counter);
        var failed = run.StrategyObservations.Where(o => o.AsOfUtc >= At(14, 2) && o.AsOfUtc < At(16)).ToArray();
        Assert.All(failed, o => Assert.Equal(RuleEvaluationResult.Failed, o.Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result));
        Assert.All(failed, o => Assert.Null(o.LifecycleProgression!.ActiveInstance));
        Assert.All(failed, o => Assert.False(o.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M5-001").EstablishesProgression));
        Assert.Equal(2, counter.CallsWithinFirstSession);
    }

    [Fact]
    public void TerminalSessionDoesNotContaminateNextBogotaDay()
    {
        var first = Inputs(NasdaqHumanH4PermittedDirection.Sell, Take(false));
        var next = Inputs(NasdaqHumanH4PermittedDirection.Sell, Take(true, shift: 24), shift: 24);
        var run = Run(first.Concat(next).ToArray());
        var evaluation = run.StrategyObservations.Single(o => o.AsOfUtc == At(38, 2)).Evaluations.Single(e => e.RuleId == evaluator.RuleId);
        Assert.Equal(RuleEvaluationResult.Passed, evaluation.Result);
    }

    [Fact]
    public void FutureInvalidationDoesNotChangeEarlierCanonicalFrames()
    {
        var take = Take(false);
        var baseline = Run(Inputs(NasdaqHumanH4PermittedDirection.Sell, null));
        var future = Run(Inputs(NasdaqHumanH4PermittedDirection.Sell, take));
        foreach (var earlier in baseline.StrategyObservations.Where(o => o.AsOfUtc < take.ObservedAtUtc))
        {
            var a = earlier.Evaluations.Single(e => e.RuleId == evaluator.RuleId);
            var b = future.StrategyObservations.Single(o => o.AsOfUtc == earlier.AsOfUtc).Evaluations.Single(e => e.RuleId == evaluator.RuleId);
            Assert.Equal((a.Result, a.EvidenceReference), (b.Result, b.EvidenceReference));
        }
    }

    [Fact]
    public void ConflictingEventsPreserveAllAlternativesAndNoWinner()
    {
        var one = Take(false);
        var two = Take(false, eventId: "other");
        var inputs = Inputs(NasdaqHumanH4PermittedDirection.Buy, one).Concat([two, Relevant(two)]).ToArray();
        var decision = EvaluateWithPrior(inputs);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("other", decision.EvidenceReference);
        Assert.Contains("source:event", decision.EvidenceReference);
        Assert.Equal(decision.EvidenceReference, EvaluateWithPrior(inputs.Reverse().ToArray()).EvidenceReference);
    }

    [Fact]
    public void SeparateRelevantReferenceSelectionsConflictWithoutAutonomousRanking()
    {
        var one = Take(false);
        var two = Take(true);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            EvaluateWithPrior(Inputs(NasdaqHumanH4PermittedDirection.Buy, one).Concat([two, Relevant(two)]).ToArray()).Result);
    }

    [Fact]
    public void RequiredUnavailableClaimBlocksUniqueButUnselectedOtherReferenceDoesNot()
    {
        var take = Take(false);
        var other = Take(true);
        var quote = new HistoricalMarketPriceObservation(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            At(14, 2), 89, "quote", "missing source", 1);
        var unavailable = new NasdaqHumanLiquidityTakeObservation(take.Reference, new NasdaqHumanLiquidityTakeEvent.CanonicalPrice(quote), At(13, 15), At(14, 2), "unavailable claim");
        Assert.Equal(RuleEvaluationResult.DataUnavailable,
            EvaluateWithPrior(Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat([unavailable]).ToArray()).Result);
        var unrelated = new NasdaqHumanLiquidityTakeObservation(other.Reference,
            new NasdaqHumanLiquidityTakeEvent.CanonicalPrice(new(LiquidityFixture.Provider, LiquidityFixture.Symbol, At(14, 2), 111, "quote", "other missing", 1)),
            At(13, 15), At(14, 2), "unrelated missing");
        Assert.Equal(RuleEvaluationResult.Passed,
            EvaluateWithPrior(Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat([unrelated]).ToArray()).Result);
    }

    [Fact]
    public void UnresolvedH4AndMissingCanonicalHistoryCannotPass()
    {
        var inputs = Inputs(NasdaqHumanH4PermittedDirection.Unresolved, Take(false));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, EvaluateWithPrior(inputs).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(Context(At(14, 2), Inputs(NasdaqHumanH4PermittedDirection.Buy, Take(false)))).Result);
    }

    [Fact]
    public void H4EvidenceArrivingAfterTakeCannotRetroactivelyValidateIt()
    {
        var inputs = Inputs(NasdaqHumanH4PermittedDirection.Buy, Take(false));
        inputs = inputs.Where(o => o is not NasdaqHumanH4ContextObservation).Append(H4(NasdaqHumanH4PermittedDirection.Buy, observed: At(14, 37))).ToArray();
        var run = Run(inputs);
        Assert.NotEqual(RuleEvaluationResult.Passed, run.StrategyObservations.Single(o => o.AsOfUtc == At(15)).Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result);
    }

    [Fact]
    public void PreparationMissingAndPreWindowEventCannotPassOrInventMisalignmentFailure()
    {
        var take = Take(true);
        var inputs = Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Where(o => o is not NasdaqPreparationCompletionObservation).ToArray();
        Assert.Equal(RuleEvaluationResult.Failed, EvaluateWithPrior(inputs).Result); // actual canonical preparation failure
        Assert.Null(EvaluateWithPrior(inputs).Fact); // not a terminal misalignment
        var early = Take(true, effective: At(13, 20), observed: At(14, 2));
        Assert.NotEqual(RuleEvaluationResult.Passed, EvaluateWithPrior(Inputs(NasdaqHumanH4PermittedDirection.Buy, early)).Result);
    }

    [Fact]
    public void HistoryRejectsCurrentFutureReorderedAndForeignIdentityAndIsImmutable()
    {
        var context = Context(At(14, 2), []);
        var useCase = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var prior = useCase.Execute(LiquidityFixture.Definition, Context(At(13, 15), []));
        var current = useCase.Execute(LiquidityFixture.Definition, context);
        Assert.Throws<ArgumentException>(() => context.WithPriorObservations([current]));
        Assert.Throws<ArgumentException>(() => context.WithPriorObservations([prior, prior]));
        var foreign = new StrategyReplayContextObservation(prior.StrategyId, prior.StrategyVersion, new("other-provider"), prior.Symbol,
            prior.Step, prior.AsOfUtc, prior.Evaluations);
        Assert.Throws<ArgumentException>(() => context.WithPriorObservations([foreign]));
        var input = new List<StrategyReplayContextObservation> { prior };
        var bounded = context.WithPriorObservations(input);
        input.Clear();
        Assert.Single(bounded.PriorObservations);
        Assert.Empty(context.PriorObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<StrategyReplayContextObservation>)bounded.PriorObservations).Clear());
    }

    [Theory]
    [InlineData(RuleEvaluationResult.DataUnavailable, RuleEvaluationResult.DataUnavailable)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired, RuleEvaluationResult.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.Waiting, RuleEvaluationResult.Waiting)]
    [InlineData(RuleEvaluationResult.Failed, RuleEvaluationResult.Failed)]
    [InlineData(RuleEvaluationResult.NotApplicable, RuleEvaluationResult.Waiting)]
    public void RequiredCanonicalPrerequisiteProjectsWithoutFabricatingTerminality(RuleEvaluationResult upstream, RuleEvaluationResult expected)
    {
        var decision = EvaluateWithPrior(Inputs(NasdaqHumanH4PermittedDirection.Buy, Take(false)), new FixedPrerequisite(upstream));
        Assert.Equal(expected, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, false)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, true)]
    public void RelevantMisalignedEventBoundToPendingSetupCancelsItAndEndsSession(NasdaqHumanH4PermittedDirection direction, bool initialHigh)
    {
        var first = Take(initialHigh);
        var later = Take(!initialHigh, effective: At(14, 37), observed: At(14, 37), eventId: "pre-entry target consumed");
        var inputs = Inputs(direction, first).Concat([later, new NasdaqHumanRelevantLiquidityTakeObservation(later, later.ObservedAtUtc, "review:later pre-entry", first)]).ToArray();
        var run = Run(inputs);
        Assert.NotNull(run.StrategyObservations.Single(o => o.AsOfUtc == At(14, 2)).LifecycleProgression!.ActiveInstance);
        var invalid = run.StrategyObservations.Single(o => o.AsOfUtc == At(14, 37));
        Assert.Equal(RuleEvaluationResult.Failed, invalid.Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result);
        Assert.Null(invalid.LifecycleProgression!.ActiveInstance);
        Assert.Equal(StrategyReplayLifecycleTransitionKind.Cancel, invalid.LifecycleProgression.TransitionHistory.Last().Kind);
        Assert.Equal(RuleEvaluationResult.Failed, run.StrategyObservations.Single(o => o.AsOfUtc == At(15)).Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result);
    }

    [Fact]
    public void ConflictAndUnavailableDiagnosticsBothSurviveTheLocalHumanReviewProjection()
    {
        var first = Take(false);
        var second = Take(false, eventId: "conflicting event");
        var quote = new HistoricalMarketPriceObservation(LiquidityFixture.Provider, LiquidityFixture.Symbol, At(14, 2), 89, "quote", "unavailable", 1);
        var missing = new NasdaqHumanLiquidityTakeObservation(first.Reference, new NasdaqHumanLiquidityTakeEvent.CanonicalPrice(quote), At(13, 15), At(14, 2), "unavailable source");
        var decision = EvaluateWithPrior(Inputs(NasdaqHumanH4PermittedDirection.Buy, first).Concat([second, Relevant(second), missing]).ToArray());
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("conflicting event", decision.EvidenceReference);
        Assert.Contains("unavailable source", decision.EvidenceReference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactStructuralReferenceKeepsFormulaOrHumanCoordinateOwnership(bool human)
    {
        var candle = Sources().First(c => c.Timeframe == new Timeframe(1, TimeframeUnit.Hour) && c.OpenTimeUtc == At(8));
        var member = human ? LiquidityFixture.Human(candle, low: true) : NasdaqStructuralLiquidityReference.OrdinaryTurn(
            NasdaqHumanH4StructuralRole.HigherLow, [candle], "review:formula members");
        var upstream = new NasdaqHumanStructuralLiquidityObservation(LiquidityFixture.Session(13), [member], At(12), At(13, 5), "review:exact structural set");
        var selection = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(new NasdaqHumanStructuralLiquidityObservationSelector()
            .Select(Context(At(13, 15), [upstream]), upstream.Session));
        var reference = new NasdaqLiquidityTakeReference.Structural(selection, member);
        var take = new NasdaqHumanLiquidityTakeObservation(reference,
            new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol, "structural event", member.StructuralPrice - 1,
                At(14, 2), "retained:structural event"), At(13, 15), At(14, 2), "review:structural take");
        var inputs = Inputs(NasdaqHumanH4PermittedDirection.Buy, null).Where(o => o is not NasdaqHumanStructuralLiquidityObservation)
            .Concat([upstream, take, Relevant(take)]).ToArray();
        var decision = EvaluateWithPrior(inputs);
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Contains("PriceOwnership", decision.EvidenceReference);
        Assert.Same(reference, Assert.IsType<NasdaqLiquidityTakeRuleFact>(decision.Fact).Take.Reference);
    }

    [Fact]
    public void CanonicalCutoffStopsAlignedTakeWithoutCreatingMisalignmentTerminality()
    {
        var run = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, Take(false)));
        var cutoff = run.StrategyObservations.Single(o => o.AsOfUtc == At(16));
        Assert.Equal(RuleEvaluationResult.Failed, cutoff.Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result);
        Assert.DoesNotContain(cutoff.RuleFacts, f => f.Fact is NasdaqLiquidityTakeRuleFact { IsSessionInvalidated: true });
        Assert.Null(cutoff.LifecycleProgression!.ActiveInstance);
    }

    private sealed class FixedPrerequisite(RuleEvaluationResult result) : AlwaysPassed(new("NQ-LIQ-002"))
    {
        public override ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(result, "Controlled canonical prerequisite.", null);
    }

    private ReplayRuleEvaluationDecision EvaluateWithPrior(IStrategyReplayInputObservation[] inputs, IReplayRuleEvaluator? prerequisite = null)
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll().Where(e => e.RuleId != prerequisite?.RuleId).ToList();
        if (prerequisite is not null) registry.Add(prerequisite);
        var useCase = new EvaluateStrategyReplayContextUseCase(registry);
        var history = new List<StrategyReplayContextObservation>();
        foreach (var time in Boundaries.Where(t => t < At(14, 2)))
            history.Add(useCase.Execute(LiquidityFixture.Definition, Context(time, inputs).WithPriorObservations(history)));
        return evaluator.Evaluate(Context(At(14, 2), inputs).WithPriorObservations(history));
    }

    private static NasdaqHumanRelevantLiquidityTakeObservation Relevant(NasdaqHumanLiquidityTakeObservation take) => new(take, take.ObservedAtUtc, "review:relevant");
    private static NasdaqHumanLiquidityTakeObservation Take(bool high, int shift = 0, DateTimeOffset? effective = null, DateTimeOffset? observed = null, string eventId = "source:event")
    {
        var context = Context(At(13 + shift, 15), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            high ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow,
            context.AsOfUtc, "review:endpoint");
        return new(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol, eventId,
            high ? 111 : 89, effective ?? At(14 + shift, 2), "source:exact event"), context.AsOfUtc, observed ?? At(14 + shift, 2), "review:take");
    }
    private static NasdaqHumanH4ContextObservation H4(NasdaqHumanH4PermittedDirection direction, int shift = 0, DateTimeOffset? observed = null)
    {
        var session = LiquidityFixture.Session(At(13 + shift));
        var fact = new NasdaqHumanH4ContextFact(direction, NasdaqHumanH4ContextKind.Breakout, At(8 + shift), At(12 + shift),
            [new(NasdaqHumanH4StructuralRole.HigherHigh, 110, [At(0 + shift)], At(8 + shift))]);
        return new(session, fact, observed ?? At(13 + shift, 5), "review:h4");
    }
    private static IStrategyReplayInputObservation[] Inputs(NasdaqHumanH4PermittedDirection direction, NasdaqHumanLiquidityTakeObservation? take, int shift = 0)
    {
        var source = Sources().First(c => c.Timeframe == new Timeframe(1, TimeframeUnit.Hour) && c.OpenTimeUtc == At(8 + shift));
        var upstream = new NasdaqHumanStructuralLiquidityObservation(LiquidityFixture.Session(At(13 + shift)),
            [LiquidityFixture.Human(source)], At(12 + shift), At(13 + shift, 5), "review:structural");
        var preparation = new NasdaqPreparationCompletionObservation(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version,
            LiquidityFixture.Provider, LiquidityFixture.Symbol, LiquidityFixture.Session(At(13 + shift)).TradingDay, At(13 + shift, 15), "review:prep");
        return take is null ? [H4(direction, shift), upstream, preparation] : [H4(direction, shift), upstream, preparation, take, Relevant(take)];
    }
    private static Candle[] Sources() => new[] { 0, 24 }.SelectMany(shift =>
        Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h + shift)).Concat(
            new[] { 0, 4, 8 }.Select(h => LiquidityFixture.Candle(h + shift, hours: 4)))).ToArray();
    private static CandleSeries[] Series() => Sources().GroupBy(c => c.Timeframe).Select(g => new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, g.Key, g.OrderBy(c => c.OpenTimeUtc)))
        .Append(new(LiquidityFixture.Provider, LiquidityFixture.Symbol, Minute, Boundaries.Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, Minute,
            t.AddMinutes(-1), t, 100, 110, 90, 100, null)))).ToArray();
    private static StrategyReplayContext Context(DateTimeOffset time, IStrategyReplayInputObservation[] inputs)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(Series());
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs);
        throw new InvalidOperationException("Fixture missing.");
    }
    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs, bool complete = false, CountingTrigger? trigger = null)
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll().Where(e => e.RuleId != trigger?.RuleId).ToList();
        if (complete) registry.AddRange(LiquidityFixture.Definition.Rules.Where(r => !registry.Any(e => e.RuleId == r.RuleId)).Select(r => new AlwaysPassed(r.RuleId)));
        if (trigger is not null) registry.Add(trigger);
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(registry),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new())).Execute(LiquidityFixture.Definition, Series(), inputs);
    }
    private class AlwaysPassed(RuleId id) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => LiquidityFixture.Definition.StrategyId;
        public StrategyVersion StrategyVersion => LiquidityFixture.Definition.Version;
        public RuleId RuleId => id;
        public virtual ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(RuleEvaluationResult.Passed, "Test-only independent rule.", null);
    }
    private sealed class CountingTrigger() : AlwaysPassed(new("NQ-M5-001"))
    {
        public int CallsWithinFirstSession { get; private set; }
        public override ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            if (context.AsOfUtc >= At(13, 15) && context.AsOfUtc < At(16)) CallsWithinFirstSession++;
            return base.Evaluate(context);
        }
    }
}
