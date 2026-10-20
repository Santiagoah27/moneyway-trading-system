using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed partial class NasdaqHumanWickfillScenario2ReversalEvaluatorTests
{
    private readonly MoneyWayNasdaqHumanWickfillScenario2ReversalEvaluator evaluator = new();
    private static readonly RuleId Rule = new("NQ-WF2-001");
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);

    [Fact]
    public void NoCanonicalSetupWaitsAndASetupWithoutApplicabilityRequiresHumanReview()
    {
        var f = Build();
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(Context(At(13, 30), f.Inputs)).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(Frame(f, f.Owner.AsOfUtc, Observation(f))).Result);
        var decision = Evaluate(f);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Fact]
    public void ConflictingApplicabilityRetainsBothReviewsWithoutChoosingAPath()
    {
        var f = Build();
        var decision = Evaluate(f, Observation(f, source: "review:standard"), Observation(f, NasdaqStrategyPath.WickfillScenario2, source: "review:scenario2"));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("review:standard", decision.EvidenceReference);
        Assert.Contains("review:scenario2", decision.EvidenceReference);
        Assert.Null(decision.Fact);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void ExplicitStandardRequiresNoScenario2ContextLondonSweepOrReversalAndDoesNotEstablishProgression(NasdaqHumanH4PermittedDirection direction)
    {
        var f = Build(direction: direction, endpoint: direction == NasdaqHumanH4PermittedDirection.Sell
            ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow, includeContext: false);
        var context = Frame(f, At(14), Observation(f));
        var decision = evaluator.Evaluate(context);
        Assert.Equal(RuleEvaluationResult.NotApplicable, decision.Result);
        Assert.Null(decision.Fact);
        Assert.Contains("review:path", decision.EvidenceReference);
        var observation = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var progression = new AdvanceStrategyReplayProgressionUseCase().Execute(MoneyWayReplayWorkflowDefinitions.GetAll().Single(), observation,
            f.Run.StrategyObservations.Last(o => o.AsOfUtc < context.AsOfUtc).WorkflowProgression);
        Assert.False(progression.RuleEligibility.Single(e => e.RuleId == Rule).EstablishesProgression);
        Assert.DoesNotContain(MoneyWayReplayWorkflowDefinitions.GetAll().Single().RulePrerequisites, p => p.PrerequisiteRuleIds.Contains(Rule));
        Assert.Equal(StrategyVerdict.Ready, Aggregate(decision.Result));
    }

    [Fact]
    public void Scenario2MissingContextNeedsReviewAndAnInadmissibleOpeningEndpointWaits()
    {
        var missing = Build(includeContext: false);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(missing, Observation(missing, NasdaqStrategyPath.WickfillScenario2)).Result);
        var asia = Build(endpoint: NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh);
        var decision = Evaluate(asia, Observation(asia, NasdaqStrategyPath.WickfillScenario2));
        Assert.Equal(RuleEvaluationResult.Waiting, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextConflictAndUnavailableNamedContextSourcesAreDistinct(bool unavailable)
    {
        var f = Build(includeContext: false);
        var h4 = f.Inputs.OfType<NasdaqHumanH4ContextObservation>().Single();
        var one = new NasdaqHumanWickfillScenario2ContextObservation(h4, h4.Fact.ZoneAnchors.Single(),
            Sources().Where(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4), At(13, 10), At(13, 10), "review:context:a");
        var other = new NasdaqHumanWickfillScenario2ContextObservation(h4, h4.Fact.ZoneAnchors.Single(),
            unavailable ? one.ReviewedSourceCandles.Append(LiquidityFixture.Candle(-4)) : one.ReviewedSourceCandles,
            unavailable ? At(13, 10) : At(13, 11), At(13, 15), "review:context:b");
        var decision = Evaluate(f, unavailable ? [Observation(f, NasdaqStrategyPath.WickfillScenario2), other] : [Observation(f, NasdaqStrategyPath.WickfillScenario2), other, one]);
        Assert.Equal(unavailable ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("review:context:b", decision.EvidenceReference);
    }

    [Fact]
    public void MissingAndConflictingReversalsNeedHumanReviewRatherThanFailing()
    {
        var f = Build(); var app = Observation(f, NasdaqStrategyPath.WickfillScenario2);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(f, app).Result);
        var one = Reversal(f); var two = Reversal(f, eventId: "source:other");
        var decision = Evaluate(f, app, one, two);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("source:other", decision.EvidenceReference);
        Assert.Contains("source:turn", decision.EvidenceReference);
        Assert.Null(decision.Fact);
    }

    [Fact]
    public void ValidHumanReversalProducesOnlyTheCanonicalFactAndRetainsOriginalEffectiveTime()
    {
        var f = Build(); var app = Observation(f, NasdaqStrategyPath.WickfillScenario2); var reversal = Reversal(f);
        var context = Frame(f, At(14), app, reversal);
        var observation = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var result = observation.Evaluations.Single(e => e.RuleId == Rule);
        Assert.Equal(RuleEvaluationResult.Passed, result.Result);
        Assert.Equal(At(14), result.EvaluatedAtUtc);
        var fact = Assert.IsType<NasdaqHumanWickfillScenario2ReversalRuleFact>(observation.RuleFacts.Single(f => f.RuleId == Rule).Fact);
        Assert.Same(app, fact.Applicability.Fact);
        Assert.Same(reversal, fact.Reversal.Fact);
        Assert.Same(f.Owner, fact.Applicability.Fact.InitialTakeObservation);
        Assert.Same(f.Owner, fact.Reversal.Fact.OpeningSweep.TakeObservation);
        Assert.Equal(reversal.EffectiveAtUtc, fact.EffectiveAtUtc);
        Assert.NotEqual(observation.AsOfUtc, fact.EffectiveAtUtc);
        Assert.Empty(typeof(NasdaqHumanWickfillScenario2ReversalRuleFact).GetConstructors());
        Assert.All(typeof(NasdaqHumanWickfillScenario2ReversalRuleFact).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.DoesNotContain(observation.RuleFacts, f => f.RuleId.Value == "NQ-M1-003");
        Assert.Equal(StrategyVerdict.Ready, Aggregate(result.Result));
        Assert.Equal(StrategyVerdict.HumanValidationRequired, Aggregate(RuleEvaluationResult.HumanValidationRequired));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiredMissingReversalSourcesAndUnavailableCompetitionBlockPassing(bool competition)
    {
        var f = Build(); var missing = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(5, TimeframeUnit.Minute), At(13, 30), At(13, 35), 100, 110, 90, 100, null);
        var unavailable = Reversal(f, eventId: "unavailable:other", observed: At(14), sources: [missing]);
        var inputs = new List<IStrategyReplayInputObservation> { Observation(f, NasdaqStrategyPath.WickfillScenario2), unavailable };
        if (competition) inputs.Add(Reversal(f));
        var decision = Evaluate(f, inputs.ToArray());
        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Contains("unavailable:other", decision.EvidenceReference);
        Assert.Null(decision.Fact);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FutureApplicabilityOrReversalCannotRewriteAnEarlierFrame(bool futureApplicability)
    {
        var f = Build(); var app = Observation(f, NasdaqStrategyPath.WickfillScenario2, observed: futureApplicability ? At(14) : At(13, 15));
        var reversal = Reversal(f, observed: futureApplicability ? At(13, 32) : At(14));
        var visible = futureApplicability ? new IStrategyReplayInputObservation[] { reversal } : [app];
        var baseline = evaluator.Evaluate(Frame(f, At(13, 33), visible));
        var withFuture = evaluator.Evaluate(Frame(f, At(13, 33), app, reversal));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, withFuture.Result);
        Assert.Equal((baseline.Result, baseline.EvidenceReference), (withFuture.Result, withFuture.EvidenceReference));
        Assert.Null(withFuture.Fact);
        Assert.Equal(RuleEvaluationResult.Passed, Evaluate(f, app, reversal).Result);
        Assert.Equal(withFuture.EvidenceReference, evaluator.Evaluate(Frame(f, At(13, 33), app, reversal)).EvidenceReference);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnotherInitialTakeInTheSameSessionCannotSupplyApplicabilityOrReversal(bool wrongApplicability)
    {
        var f = Build(); var other = Build(eventId: "source:other setup");
        Assert.Equal(Take(f.Owner).Session, Take(other.Owner).Session);
        var app = Observation(wrongApplicability ? other : f, NasdaqStrategyPath.WickfillScenario2);
        var reversal = Reversal(wrongApplicability ? f : other);
        var decision = Evaluate(f, app, reversal);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Fact]
    public void LateContextAvailabilityIsAllowedWithoutInventingAnAvailabilityBeforeTheSweep()
    {
        var f = Build(includeContext: false); var h4 = f.Inputs.OfType<NasdaqHumanH4ContextObservation>().Single();
        var preOpen = new NasdaqHumanWickfillScenario2ContextObservation(h4, h4.Fact.ZoneAnchors.Single(),
            Sources().Where(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4), At(13, 10), At(13, 32), "review:late context");
        var context = Frame(f, At(14), preOpen);
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(context, h4.Session));
        var sweep = NasdaqWickfillScenario2OpeningSweepContext.From(context, selected, f.Owner);
        var reversal = new NasdaqHumanWickfillScenario2PostSweepReversalObservation(sweep, "source:turn", At(13, 32), At(13, 33), "review:turn");
        Assert.True(preOpen.ObservedAtUtc > sweep.EffectiveAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, Evaluate(f, preOpen, Observation(f, NasdaqStrategyPath.WickfillScenario2), reversal).Result);
    }

    [Fact]
    public void TerminalMisalignmentProtectsTheNewRuleInTheSameFrameAndDirectCalls()
    {
        var f = Build(); var root = Take(f.Owner).Take;
        var upstream = Context(At(13, 5), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream, new NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "review:opposite");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            "source:opposite", reference.ReferencePrice - 1, At(13, 33), "source:opposite"), upstream.AsOfUtc, At(13, 33), "review:opposite");
        var inputs = f.Inputs.Concat([opposite, new NasdaqHumanRelevantLiquidityTakeObservation(opposite, opposite.ObservedAtUtc, "review:opposite selected", root),
            Observation(f, NasdaqStrategyPath.WickfillScenario2), Reversal(f)]).ToArray();
        var run = Run(inputs);
        var terminal = run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33));
        Assert.Equal(RuleEvaluationResult.Failed, terminal.Evaluations.Single(e => e.RuleId == Rule).Result);
        Assert.DoesNotContain(terminal.RuleFacts, f => f.RuleId == Rule);
        Assert.True(Take(terminal).Blocks(Frame(f, At(14)), Rule));
        var context = Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14)));
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(context).Result);
    }

    [Fact]
    public void CanonicalTimeAndLifecycleProtectionCannotBeBypassedByOldPositiveEvidence()
    {
        var f = Build(); var app = Observation(f, NasdaqStrategyPath.WickfillScenario2); var reversal = Reversal(f);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(Frame(f, At(16), app, reversal)).Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(Frame(f, At(17), app, reversal)).Result);
    }

    [Fact]
    public void RegistryMetadataCapabilityAndOtherEvaluatorsRemainTruthful()
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(registry, e => e.RuleId == Rule && e is MoneyWayNasdaqHumanWickfillScenario2ReversalEvaluator);
        var rule = LiquidityFixture.Definition.Rules.Single(r => r.RuleId == Rule);
        Assert.True(rule.IsRequired);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, rule.DefinitionStatus);
        Assert.Equal(90, rule.Sequence);
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(evaluator.StrategyId, evaluator.StrategyVersion)!;
        Assert.Equal((34, 13, 0), (report.TotalRuleCount, report.RequiredRuleCount, report.RequiredEvaluatorGapCount));
        var missing = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry.Where(e => e.RuleId != Rule),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(evaluator.StrategyId, evaluator.StrategyVersion)!;
        Assert.Equal(1, missing.RequiredEvaluatorGapCount);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, report.Rules.Single(r => r.RuleId == Rule).CapabilityStatus);
        var f = Build(); var context = Frame(f, At(14), Observation(f));
        var without = new EvaluateStrategyReplayContextUseCase(registry.Where(e => e.RuleId != Rule)).Execute(LiquidityFixture.Definition, context);
        var with = new EvaluateStrategyReplayContextUseCase(registry).Execute(LiquidityFixture.Definition, context);
        Assert.Equal(without.Evaluations, with.Evaluations.Where(e => e.RuleId != Rule));
        Assert.Equal(JsonSerializer.Serialize(without.RuleFacts), JsonSerializer.Serialize(with.RuleFacts));
    }

    [Fact]
    public void InputOrderDoesNotChooseAReviewAndCanonicalDiagnosticsNameTheRule()
    {
        var f = Build(); var app = Observation(f, NasdaqStrategyPath.WickfillScenario2); var one = Reversal(f); var two = Reversal(f, eventId: "source:alternative");
        var forward = Evaluate(f, app, one, two); var reverse = Evaluate(f, two, app, one);
        Assert.Equal((forward.Result, forward.EvidenceReference), (reverse.Result, reverse.EvidenceReference));
        var definition = new StrategyDefinition(evaluator.StrategyId, evaluator.StrategyVersion, "Test projection", "source", [LiquidityFixture.Definition.Rules.Single(r => r.RuleId == Rule)]);
        var evaluation = new RuleEvaluation(Rule, RuleDefinitionStatus.HumanValidationRequired, forward.Result, 90, true, forward.Reason, At(14), forward.EvidenceReference);
        var observation = new StrategyReplayContextObservation(evaluator.StrategyId, evaluator.StrategyVersion, LiquidityFixture.Provider, LiquidityFixture.Symbol, 1, At(14), [evaluation]);
        var outcome = new EvaluateStrategyReplayContextOutcomeUseCase().Execute(definition, observation);
        Assert.True(outcome.HasCompleteRequiredCoverage);
        Assert.Equal(Rule, outcome.EvaluationOutcome!.BlockingRuleId);
        Assert.Equal(StrategyVerdict.HumanValidationRequired, outcome.Verdict);
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var run = new MultiTimeframeStrategyBacktestRun(evaluator.StrategyId, evaluator.StrategyVersion, LiquidityFixture.Provider, LiquidityFixture.Symbol,
            [minute], [new MultiTimeframeBacktestObservation(1, At(14), [minute], [minute])], [observation]);
        var report = new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase().Execute(definition, new(run, [outcome]));
        Assert.Equal(Rule, Assert.Single(report.BlockingRules).RuleId);
        Assert.Equal(Rule, Assert.Single(report.Frames).BlockingRuleId);
        Assert.Empty(report.MissingRequiredRules);
    }

    private ReplayRuleEvaluationDecision Evaluate(Fixture f, params IStrategyReplayInputObservation[] inputs) => evaluator.Evaluate(Frame(f, At(14), inputs));
    private static StrategyVerdict Aggregate(RuleEvaluationResult result) => new SequentialStrategyEvaluator().Evaluate(
        [new RuleEvaluation(Rule, RuleDefinitionStatus.HumanValidationRequired, result, 90, true, "Audited test result", At(14), null)]).Verdict;
    internal static NasdaqHumanWickfillScenario2PostSweepReversalObservation Reversal(Fixture f, string eventId = "source:turn", DateTimeOffset? observed = null, Candle[]? sources = null)
    {
        var context = Frame(f, At(14));
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(context, Take(f.Owner).Session));
        var sweep = NasdaqWickfillScenario2OpeningSweepContext.From(context, selected, f.Owner);
        return new(sweep, eventId, At(13 + f.Shift, 32), observed ?? At(13 + f.Shift, 32), "review:turn", sources);
    }

    internal static NasdaqHumanBranchApplicabilityObservation Observation(Fixture f, NasdaqStrategyPath path = NasdaqStrategyPath.Standard,
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
            new[] { At(13 + shift, 5), At(13 + shift, 15), At(13 + shift, 30), At(13 + shift, 31), At(13 + shift, 32), At(13 + shift, 33), At(14 + shift), At(16 + shift), At(17 + shift) }
                .Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Minute), t.AddMinutes(-1), t, 100, 110, 90, 100, null)))).ToArray();
    internal static StrategyReplayContext Context(DateTimeOffset time, IStrategyReplayInputObservation[] inputs, int shift = 0)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(Series(shift));
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs);
        throw new InvalidOperationException("Fixture frame missing.");
    }
    internal static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs, int shift = 0)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new())).Execute(LiquidityFixture.Definition, Series(shift), inputs);
    }
    internal static Fixture Build(NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Sell,
        string eventId = "source:initial", NasdaqLiquidityTakeReference.SessionEndpoint? endpoint = null, int shift = 0,
        DateTimeOffset? takeEffective = null, bool includeContext = true, bool extension = false)
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
    internal sealed record Fixture(MultiTimeframeStrategyBacktestRun Run, StrategyReplayContextObservation Owner,
        StrategyReplayContext Context, IStrategyReplayInputObservation[] Inputs, int Shift);
}
