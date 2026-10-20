using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanBranchApplicabilityTests
{
    private readonly NasdaqHumanBranchApplicabilityObservationSelector selector = new();
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);

    [Theory]
    [InlineData(NasdaqStrategyPath.Standard, NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqStrategyPath.Standard, NasdaqHumanH4PermittedDirection.Sell)]
    [InlineData(NasdaqStrategyPath.WickfillScenario2, NasdaqHumanH4PermittedDirection.Sell)]
    public void PositiveClassificationRetainsExactInitialTakeAndLifecycleWithoutProvingAnyPathRule(
        NasdaqStrategyPath path, NasdaqHumanH4PermittedDirection direction)
    {
        var f = Build(direction: direction); var observation = Observation(f, path);
        Assert.Same(f.Owner, observation.InitialTakeObservation);
        Assert.Same(Take(f.Owner), observation.InitialTake);
        Assert.Same(observation.InitialTake.Take, observation.InitialTake.InitiatingTake);
        Assert.Equal(direction, observation.Direction);
        Assert.Equal(path, observation.Path);
        Assert.Equal(LiquidityFixture.Session(13), observation.Session);
        Assert.Equal(f.Owner.LifecycleProgression!.ActiveInstance!.StartedAtUtc, observation.EffectiveAtUtc);
        Assert.Equal("review:path", observation.SourceReference);
        Assert.Equal("review:take", observation.InitialTake.InitiatingTake.SourceReference);
        Assert.All(typeof(NasdaqHumanBranchApplicabilityObservation).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(f, At(14), observation), f.Owner));
        Assert.DoesNotContain(f.Inputs, o => o is NasdaqHumanWickfillScenario2ContextObservation);
    }

    [Fact]
    public void Scenario2CannotInventBuySemanticsAndUndefinedPathsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Observation(Build(direction: NasdaqHumanH4PermittedDirection.Buy), NasdaqStrategyPath.WickfillScenario2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Observation(Build(), (NasdaqStrategyPath)99));
        Assert.Equal(2, Enum.GetValues<NasdaqStrategyPath>().Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" padded")]
    [InlineData("padded ")]
    [InlineData(null)]
    public void ProvenanceIsRequired(string? source) => Assert.ThrowsAny<ArgumentException>(() => Observation(Build(), source: source!));

    [Theory]
    [InlineData("effective-offset")]
    [InlineData("observed-offset")]
    [InlineData("availability-before-review")]
    public void InvalidReviewTimesAreRejected(string issue)
    {
        var f = Build();
        var effective = issue == "effective-offset" ? At(13, 10).ToOffset(TimeSpan.FromHours(-5)) : At(13, 10);
        var observed = issue == "observed-offset" ? At(13, 15).ToOffset(TimeSpan.FromHours(-5))
            : issue == "availability-before-review" ? At(13, 5) : At(13, 15);
        Assert.Throws<ArgumentException>(() => Observation(f, effective: effective, observed: observed));
    }

    [Fact]
    public void PreOpenReviewIsRetainedButBindingWaitsForActualCanonicalStart()
    {
        var f = Build(); var observation = Observation(f, effective: At(13, 10), observed: At(13, 15));
        Assert.Equal(At(13, 10), observation.ReviewEffectiveAtUtc);
        Assert.Equal(At(13, 15), observation.ReviewObservedAtUtc);
        Assert.Equal(f.Owner.AsOfUtc, observation.EffectiveAtUtc);
        Assert.Equal(f.Owner.AsOfUtc, observation.ObservedAtUtc);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(f, At(13, 30), observation), f.Owner));
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(f, At(13, 32), observation), f.Owner));
        // A previously emitted owner can be joined at its own frame, but no rule is propagated or reevaluated.
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(f, f.Owner.AsOfUtc, observation), f.Owner));
    }

    [Fact]
    public void LateCanonicalEmissionDoesNotBackdateBindingToTheMarketTake()
    {
        var f = Build(takeEffective: At(13, 30)); var observation = Observation(f);
        Assert.Equal(At(13, 30), observation.InitialTake.Take.EffectiveAtUtc);
        Assert.Equal(At(13, 31), observation.EffectiveAtUtc);
        Assert.Equal(At(13, 31), observation.ObservedAtUtc);
    }

    [Fact]
    public void FutureReviewDoesNotRewriteEarlierUniqueOrMissingSelections()
    {
        var f = Build(); var early = Observation(f); var late = Observation(f, NasdaqStrategyPath.WickfillScenario2, observed: At(14));
        var before = Frame(f, At(13, 33), early, late);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(f, At(13, 33), late), f.Owner));
        Assert.Same(early, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(before, f.Owner)).Fact);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Conflict>(selector.Select(Frame(f, At(14), early, late), f.Owner));
        Assert.Same(early, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(before, f.Owner)).Fact);
        Assert.Equal(At(13, 10), late.ReviewEffectiveAtUtc);
        Assert.Equal(At(14), late.ObservedAtUtc);
    }

    [Fact]
    public void NoApplicabilityEvidenceIsMissingEvenWhenScenario2ContextExists()
    {
        var f = Build(includeContext: true);
        Assert.Contains(f.Inputs, o => o is NasdaqHumanWickfillScenario2ContextObservation);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(f, At(14)), f.Owner));
    }

    [Fact]
    public void CompatibleReviewsRetainAllProvenanceAndReviewTimesDeterministically()
    {
        var f = Build();
        var one = Observation(f, source: "z");
        var two = Observation(Build(), effective: At(13, 32), observed: At(13, 33), source: "a");
        var forward = Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(f, At(14), one, two, one), f.Owner));
        var reverse = Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(f, At(14), two, one, one), f.Owner));
        Assert.Equal(forward.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal([one, one, two], forward.SupportingObservations);
        Assert.Equal(At(13, 32), two.ReviewEffectiveAtUtc);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanBranchApplicabilityObservation>)forward.SupportingObservations).Clear());
    }

    [Fact]
    public void StandardAndScenario2ForSameSetupConflictWithoutMajorityOrLatestWinner()
    {
        var f = Build(); var standard = Observation(f); var scenario2 = Observation(f, NasdaqStrategyPath.WickfillScenario2, observed: At(14));
        var forward = Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Conflict>(selector.Select(Frame(f, At(14), standard, standard, scenario2), f.Owner));
        var reverse = Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Conflict>(selector.Select(Frame(f, At(14), scenario2, standard, standard), f.Owner));
        Assert.Equal(3, forward.Alternatives.Count);
        Assert.Equal(forward.Alternatives, reverse.Alternatives);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanBranchApplicabilityObservation>)forward.Alternatives).Clear());
    }

    [Theory]
    [InlineData("event")]
    [InlineData("endpoint")]
    [InlineData("direction")]
    [InlineData("time")]
    public void TwoIndependentlyEstablishedSetupsInSameSessionCannotClassifyEachOther(string difference)
    {
        var first = Build();
        var second = Build(eventId: difference == "event" ? "different:event" : "source:initial",
            endpoint: difference == "endpoint" ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh : null,
            direction: difference == "direction" ? NasdaqHumanH4PermittedDirection.Buy : NasdaqHumanH4PermittedDirection.Sell,
            takeEffective: difference == "time" ? At(13, 30) : null);
        var standard = Observation(first); var scenario2 = Observation(second);
        if (difference != "direction") scenario2 = Observation(second, NasdaqStrategyPath.WickfillScenario2);
        Assert.Equal(standard.Session, scenario2.Session);
        // Replay-local ordinals can coincide across executions. The actual initiating ancestry distinguishes targets.
        Assert.Equal(first.Owner.LifecycleProgression!.ActiveInstance!.InstanceId, second.Owner.LifecycleProgression!.ActiveInstance!.InstanceId);
        Assert.Same(standard, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(first, At(14), standard, scenario2), first.Owner)).Fact);
        Assert.Same(scenario2, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(second, At(14), standard, scenario2), second.Owner)).Fact);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(first, At(14), scenario2), first.Owner));
    }

    [Fact]
    public void WrongSessionAndAnOwnerOutsideBoundedHistoryAreExcluded()
    {
        var first = Build(); var next = Build(shift: 24); var observation = Observation(first);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(next, At(38), observation), next.Owner));
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(next, At(38), observation), first.Owner));
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Context(At(14), [observation]), first.Owner));
    }

    [Theory]
    [InlineData("no-lifecycle")]
    [InlineData("no-progression")]
    [InlineData("not-passed")]
    [InlineData("no-fact")]
    [InlineData("not-start")]
    public void UnestablishedOrNonInitialOwnersCannotCreateAnApplicabilityAssertion(string issue)
    {
        var f = Build(); var owner = f.Owner;
        var evaluations = owner.Evaluations.Select(e => issue == "not-passed" && e.RuleId.Value == "NQ-LIQ-003"
            ? new RuleEvaluation(e.RuleId, e.DefinitionStatus, RuleEvaluationResult.Waiting, e.Sequence, e.IsRequired, e.Reason, e.EvaluatedAtUtc, e.EvidenceReference) : e);
        var invalid = issue == "not-start" ? f.Run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 32))
            : new StrategyReplayContextObservation(owner.StrategyId, owner.StrategyVersion, owner.ProviderId, owner.Symbol, owner.Step, owner.AsOfUtc,
                evaluations, issue is "no-progression" or "not-passed" ? null : owner.WorkflowProgression,
                issue is "no-lifecycle" or "no-progression" or "not-passed" ? null : owner.LifecycleProgression,
                ruleFacts: issue == "no-fact" ? [] : owner.RuleFacts);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanBranchApplicabilityObservation(invalid, NasdaqStrategyPath.Standard, At(13, 10), At(13, 15), "review"));
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(Frame(f, At(14)), invalid));
    }

    [Fact]
    public void SameSideExtensionRetainsClassificationButCannotReplaceTheInitialOwner()
    {
        var f = Build(extension: true); var observation = Observation(f);
        var later = f.Run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33));
        Assert.Same(Take(f.Owner).InitiatingTake, Take(later).InitiatingTake);
        Assert.NotSame(Take(f.Owner).Take, Take(later).Take);
        Assert.Same(observation, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(Frame(f, At(14), observation), f.Owner)).Fact);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanBranchApplicabilityObservation(later, NasdaqStrategyPath.Standard, At(13, 33), At(14), "review"));
    }

    [Fact]
    public void AddingAndSelectingEvidenceCannotChangeOtherCanonicalEvaluationsMetadataWorkflowOrLifecycle()
    {
        var f = Build(); var observation = Observation(f, NasdaqStrategyPath.WickfillScenario2);
        var metadata = JsonSerializer.Serialize(LiquidityFixture.Definition);
        var capabilities = JsonSerializer.Serialize(new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).GetAll());
        var workflow = JsonSerializer.Serialize(MoneyWayReplayWorkflowDefinitions.GetAll());
        var context = Frame(f, At(14), observation); var history = JsonSerializer.Serialize(context.PriorObservations);
        selector.Select(context, f.Owner);
        var withEvidence = Run([.. f.Inputs, observation]);
        foreach (var pair in f.Run.StrategyObservations.Zip(withEvidence.StrategyObservations))
        {
            Assert.Equal(pair.First.Evaluations.Where(e => e.RuleId.Value is not ("NQ-WF2-001" or "NQ-WF2-002")), pair.Second.Evaluations.Where(e => e.RuleId.Value is not ("NQ-WF2-001" or "NQ-WF2-002")));
            Assert.Equal(JsonSerializer.Serialize(pair.First.RuleFacts), JsonSerializer.Serialize(pair.Second.RuleFacts));
            Assert.Equal(JsonSerializer.Serialize(pair.First.WorkflowProgression), JsonSerializer.Serialize(pair.Second.WorkflowProgression));
            Assert.Equal(JsonSerializer.Serialize(pair.First.LifecycleProgression), JsonSerializer.Serialize(pair.Second.LifecycleProgression));
        }
        Assert.Equal(history, JsonSerializer.Serialize(context.PriorObservations));
        Assert.Equal(metadata, JsonSerializer.Serialize(LiquidityFixture.Definition));
        Assert.Equal(workflow, JsonSerializer.Serialize(MoneyWayReplayWorkflowDefinitions.GetAll()));
        Assert.Equal(capabilities, JsonSerializer.Serialize(new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).GetAll()));
        Assert.False((object)observation is IReplayRuleFact);
        Assert.DoesNotContain(typeof(NasdaqHumanBranchApplicabilityObservation).GetProperties(), p => p.PropertyType == typeof(StrategyVerdict));
        Assert.DoesNotContain(withEvidence.StrategyObservations.SelectMany(o => o.RuleFacts), f => f.RuleId.Value == "NQ-M1-003");
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var f = Build();
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanBranchApplicabilityObservation(null!, NasdaqStrategyPath.Standard, At(13), At(13), "review"));
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, f.Owner));
        Assert.Throws<ArgumentNullException>(() => selector.Select(f.Context, null!));
    }

    private static NasdaqHumanBranchApplicabilityObservation Observation(Fixture f, NasdaqStrategyPath path = NasdaqStrategyPath.Standard,
        DateTimeOffset? effective = null, DateTimeOffset? observed = null, string source = "review:path") =>
        new(f.Owner, path, effective ?? At(13 + f.Shift, 10), observed ?? At(13 + f.Shift, 15), source);
    private static NasdaqLiquidityTakeRuleFact Take(StrategyReplayContextObservation owner) =>
        owner.RuleFacts.Select(f => f.Fact).OfType<NasdaqLiquidityTakeRuleFact>().Single();
    private static StrategyReplayContext Frame(Fixture f, DateTimeOffset time, params IStrategyReplayInputObservation[] observations) =>
        Context(time, [.. f.Inputs, .. observations], f.Shift).WithPriorObservations(f.Run.StrategyObservations.Where(o => o.AsOfUtc < time));
    private static Candle[] Sources(int shift = 0) => Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h + shift))
        .Concat(new[] { 0, 4, 8 }.Select(h => LiquidityFixture.Candle(h + shift, hours: 4))).ToArray();
    private static CandleSeries[] Series(int shift = 0) => Sources(shift).GroupBy(c => c.Timeframe)
        .Select(g => new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, g.Key, g.OrderBy(c => c.OpenTimeUtc)))
        .Append(new(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Minute),
            new[] { At(13 + shift, 5), At(13 + shift, 15), At(13 + shift, 30), At(13 + shift, 31), At(13 + shift, 32), At(13 + shift, 33), At(14 + shift) }
                .Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Minute), t.AddMinutes(-1), t, 100, 110, 90, 100, null)))).ToArray();
    private static StrategyReplayContext Context(DateTimeOffset time, IStrategyReplayInputObservation[] inputs, int shift = 0)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(Series(shift));
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs);
        throw new InvalidOperationException("Fixture frame missing.");
    }
    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs, int shift = 0)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new())).Execute(LiquidityFixture.Definition, Series(shift), inputs);
    }
    private static Fixture Build(NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Sell,
        string eventId = "source:initial", NasdaqLiquidityTakeReference.SessionEndpoint? endpoint = null, int shift = 0,
        DateTimeOffset? takeEffective = null, bool includeContext = false, bool extension = false)
    {
        var upstream = Context(At(13 + shift, 5), [], shift);
        var session = LiquidityFixture.Session(At(13 + shift));
        var h4 = new NasdaqHumanH4ContextObservation(session, new(direction, NasdaqHumanH4ContextKind.Wickfill, At(8 + shift), At(12 + shift), [],
            [new(NasdaqHumanH4StructuralRole.LowerHigh, 100, 120, NasdaqHumanH4ZoneBoundaryPrecision.Approximate, [At(8 + shift)])]), upstream.AsOfUtc, "review:h4");
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream, new NasdaqSessionLiquidityCalculator().Calculate(upstream),
            endpoint ?? (direction == NasdaqHumanH4PermittedDirection.Sell ? NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh : NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow),
            upstream.AsOfUtc, "review:endpoint");
        var take = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            eventId, reference.ReferencePrice + (reference.Side == NasdaqStructuralLiquiditySide.High ? 1 : -1), takeEffective ?? At(13 + shift, 31), "source:take"),
            upstream.AsOfUtc, At(13 + shift, 31), "review:take");
        var inputs = new List<IStrategyReplayInputObservation> { h4, take, new NasdaqHumanRelevantLiquidityTakeObservation(take, take.ObservedAtUtc, "review:relevant"),
            new NasdaqPreparationCompletionObservation(session.StrategyId, session.StrategyVersion, session.ProviderId, session.Symbol, session.TradingDay, At(13 + shift, 15), "review:prep") };
        if (includeContext) inputs.Add(new NasdaqHumanWickfillScenario2ContextObservation(h4, h4.Fact.ZoneAnchors.Single(),
            Sources(shift).Where(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4), At(13 + shift, 10), At(13 + shift, 10), "review:context"));
        if (extension)
        {
            var laterReference = new NasdaqLiquidityTakeReference.SessionLevel(upstream, new NasdaqSessionLiquidityCalculator().Calculate(upstream),
                NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, upstream.AsOfUtc, "review:later endpoint");
            var later = new NasdaqHumanLiquidityTakeObservation(laterReference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol,
                "source:extension", laterReference.ReferencePrice + 2, At(13 + shift, 33), "source:extension"), upstream.AsOfUtc, At(13 + shift, 33), "review:extension");
            inputs.Add(later); inputs.Add(new NasdaqHumanRelevantLiquidityTakeObservation(later, later.ObservedAtUtc, "review:later", take));
        }
        var run = Run(inputs.ToArray(), shift);
        return new(run, run.StrategyObservations.Single(o => o.AsOfUtc == At(13 + shift, 31)),
            Context(At(14 + shift), inputs.ToArray(), shift), inputs.ToArray(), shift);
    }
    private sealed record Fixture(MultiTimeframeStrategyBacktestRun Run, StrategyReplayContextObservation Owner,
        StrategyReplayContext Context, IStrategyReplayInputObservation[] Inputs, int Shift);
}
