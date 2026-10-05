using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM5TriggerEvaluatorTests
{
    private readonly MoneyWayNasdaqHumanM5TriggerEvaluator evaluator = new();
    private readonly NasdaqHumanLiquidityTakeObservation take = M5Fixture.Take(false);
    private NasdaqHumanM5TriggerObservation Trigger(bool ifvg = false, int minute = 10, int? observed = null,
        string source = "review:trigger", NasdaqHumanM5TakeTriggerOrder order = NasdaqHumanM5TakeTriggerOrder.Unspecified,
        NasdaqHumanLiquidityTakeObservation? otherTake = null, bool missingSource = false)
    {
        var prior = M5Fixture.Five(M5Fixture.At(missingSource ? 13 : 14, missingSource ? 20 : 0));
        NasdaqHumanM5TriggerEvent marketEvent = ifvg
            ? new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, minute)), NasdaqHumanH4PermittedDirection.Buy, [prior], [prior])
            : new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, minute)), NasdaqHumanH4PermittedDirection.Buy, [prior], [prior], 100);
        return new(otherTake ?? take, marketEvent, M5Fixture.At(14, observed ?? minute), source, order);
    }
    private IStrategyReplayInputObservation[] Inputs(params IStrategyReplayInputObservation[] additional) =>
        M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat(additional).ToArray();
    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs, IReplayRuleEvaluator? stub = null, CandleSeries[]? series = null)
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll().ToList();
        if (stub is not null) { registry.RemoveAll(e => e.RuleId == stub.RuleId); registry.Add(stub); }
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(registry),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new())).Execute(LiquidityFixture.Definition, series ?? M5Fixture.Series(), inputs);
    }
    private StrategyReplayContext Context(int minute, params IStrategyReplayInputObservation[] additional)
    {
        var inputs = Inputs(additional);
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, minute)).ToArray();
        return M5Fixture.Context(M5Fixture.At(14, minute), inputs).WithPriorObservations(history);
    }
    private ReplayRuleEvaluationDecision Evaluate(int minute, params IStrategyReplayInputObservation[] additional) => evaluator.Evaluate(Context(minute, additional));

    [Fact]
    public void IdentityRegistrationAndPrematureWaitingAreTruthful()
    {
        Assert.Equal("NQ-M5-001", evaluator.RuleId.Value);
        Assert.Single(MoneyWayReplayRuleEvaluators.GetAll(), e => e.RuleId == evaluator.RuleId);
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(M5Fixture.Context(M5Fixture.At(14, 10), Inputs(Trigger()))).Result);
        var atTake = Run(Inputs(Trigger(minute: 5, order: NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation)))
            .StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 5));
        Assert.Equal(RuleEvaluationResult.Waiting, atTake.Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result);
    }

    [Fact]
    public void EligibleMissingRequiresHumanReviewRatherThanNegativePatternInference()
    {
        var decision = Evaluate(10);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    [InlineData(false, 10)]
    [InlineData(true, 10)]
    public void UniqueScIfvgAndSameCandlePassOnlyStepFour(bool ifvg, int minute)
    {
        var trigger = Trigger(ifvg, minute, order: minute == 5 ? NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation : NasdaqHumanM5TakeTriggerOrder.Unspecified);
        var decision = Evaluate(10, trigger);
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Same(trigger, Assert.IsType<NasdaqHumanM5TriggerRuleFact>(decision.Fact).Selection.Fact);
        Assert.Contains(ifvg ? "Ifvg" : "StructuralChange", decision.EvidenceReference);
        Assert.Contains("TriggerEffectiveAtUtc", decision.EvidenceReference);
        Assert.Contains("review:trigger", decision.EvidenceReference);
        Assert.Contains("source:event", decision.EvidenceReference);
        if (minute == 5) Assert.Contains("TakeBeforeTriggerConfirmation", decision.EvidenceReference);
    }

    [Fact]
    public void SameCandleTriggerDoesNotRequireAnotherFiveMinuteClose()
    {
        var oneMinuteBoundary = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute,
            M5Fixture.At(14, 5), M5Fixture.At(14, 6), 100, 110, 90, 100, null);
        var series = M5Fixture.Series().Select(s => s.Timeframe == M5Fixture.Minute
            ? new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Append(oneMinuteBoundary).OrderBy(c => c.OpenTimeUtc)) : s).ToArray();
        var trigger = Trigger(minute: 5, order: NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation);
        var observation = Run(Inputs(trigger), series: series).StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 6));
        Assert.Equal(RuleEvaluationResult.Passed, observation.Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result);
        Assert.Equal(M5Fixture.At(14, 5), Assert.IsType<NasdaqHumanM5TriggerRuleFact>(observation.RuleFacts.Single(f => f.RuleId == evaluator.RuleId).Fact).Selection.Fact.EffectiveAtUtc);
    }

    [Fact]
    public void EqualTimeWithoutOrderAndPreTakeTriggerCannotPass()
    {
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(10, Trigger(minute: 5)).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(10, Trigger(minute: 0, observed: 10)).Result);
    }

    [Fact]
    public void SameEarliestScIfvgConflictPreservesBothAlternativesAndNoWinner()
    {
        var sc = Trigger(source: "sc reviewer"); var ifvg = Trigger(true, source: "ifvg reviewer");
        var a = Evaluate(10, sc, ifvg); var b = Evaluate(10, ifvg, sc);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, a.Result);
        Assert.Equal(a.EvidenceReference, b.EvidenceReference);
        Assert.Contains("sc reviewer", a.EvidenceReference);
        Assert.Contains("ifvg reviewer", a.EvidenceReference);
        Assert.Null(a.Fact);
    }

    [Fact]
    public void RequiredMissingMarketSourceIsDataUnavailable()
    {
        var decision = Evaluate(10, Trigger(missingSource: true));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Contains("UnavailableSourceEvidence", decision.EvidenceReference);
        Assert.Contains("13:15:00", decision.EvidenceReference); // exact absent source opens at 13:15
    }

    [Theory]
    [InlineData(5, RuleEvaluationResult.DataUnavailable)]
    [InlineData(10, RuleEvaluationResult.DataUnavailable)]
    [InlineData(20, RuleEvaluationResult.Passed)]
    public void UnavailableCompetitionOnlyBlocksWhenItCouldBeFirst(int missingMinute, RuleEvaluationResult expected)
    {
        var order = missingMinute == 5 ? NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation : NasdaqHumanM5TakeTriggerOrder.Unspecified;
        var missing = Trigger(true, missingMinute, source: "unavailable alternative", order: order, missingSource: true);
        var result = Evaluate(30, Trigger(), missing);
        Assert.Equal(expected, result.Result);
        Assert.Contains("unavailable alternative", result.EvidenceReference);
    }

    [Fact]
    public void ConflictRetainsUnavailableDiagnosticsWithoutMissingDataChoosingWinner()
    {
        var decision = Evaluate(10, Trigger(), Trigger(true), Trigger(true, missingSource: true, source: "missing competing source"));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("missing competing source", decision.EvidenceReference);
    }

    [Fact]
    public void WrongTakeAndFutureSourcesCannotEstablishThisSetup()
    {
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(10, Trigger(otherTake: M5Fixture.Take(false, eventId: "wrong take"))).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(10, Trigger(minute: 15)).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluate(10, Trigger(observed: 20)).Result);
        Assert.Equal(RuleEvaluationResult.Passed, Evaluate(20, Trigger(observed: 20)).Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterDistinctTriggersDoNotSupersedeFirst(bool ifvgFirst)
    {
        var first = Trigger(ifvgFirst, source: "first source"); var later = Trigger(!ifvgFirst, 20, source: "later source");
        var decision = Evaluate(30, later, first);
        Assert.Same(first, Assert.IsType<NasdaqHumanM5TriggerRuleFact>(decision.Fact).Selection.Fact);
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
    }

    [Fact]
    public void LateEarlierMarketFactAffectsOnlyLaterFrameAndNeverRewritesEarlierStoredDecision()
    {
        var later = Trigger(true, 20); var earlier = Trigger(false, 10, 30, "earlier market event annotated later");
        var run = Run(Inputs(later, earlier));
        var at20 = run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 20));
        var at30 = run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 30));
        Assert.Same(later, Assert.IsType<NasdaqHumanM5TriggerRuleFact>(at20.RuleFacts.Single(f => f.RuleId == evaluator.RuleId).Fact).Selection.Fact);
        Assert.Same(earlier, Assert.IsType<NasdaqHumanM5TriggerRuleFact>(at30.RuleFacts.Single(f => f.RuleId == evaluator.RuleId).Fact).Selection.Fact);
        var baseline = Run(Inputs(later)).StrategyObservations.Single(o => o.AsOfUtc == at20.AsOfUtc);
        Assert.Equal(baseline.Evaluations.Single(e => e.RuleId == evaluator.RuleId).EvidenceReference,
            at20.Evaluations.Single(e => e.RuleId == evaluator.RuleId).EvidenceReference);
    }

    [Fact]
    public void CanonicalTimestampMetadataEvidenceAndNextStageEligibilityArePreserved()
    {
        var run = Run(Inputs(Trigger()), new FvgWaiting());
        var at10 = run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 10));
        var at15 = run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 15));
        var evaluated = at10.Evaluations.Single(e => e.RuleId == evaluator.RuleId);
        Assert.Equal(RuleEvaluationResult.Passed, evaluated.Result);
        Assert.Equal(at10.AsOfUtc, evaluated.EvaluatedAtUtc);
        Assert.True(evaluated.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Confirmed, evaluated.DefinitionStatus);
        Assert.Contains(evaluator.RuleId, at10.WorkflowProgression!.EstablishedRuleIds);
        Assert.True(at15.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-FVG-001").IsEligible);
        Assert.Equal(RuleEvaluationResult.Waiting, at15.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-001").Result);
        Assert.DoesNotContain(at15.WorkflowProgression.EstablishedRuleIds, id => id.Value is "NQ-FVG-001" or "NQ-FVG-002");
        Assert.Equal(2, MoneyWayReplayRuleEvaluators.GetAll().Count(e => e.RuleId.Value.StartsWith("NQ-FVG-", StringComparison.Ordinal)));
        var outcome = new EvaluateStrategyReplayContextOutcomeUseCase().Execute(LiquidityFixture.Definition, at10);
        Assert.Equal(StrategyVerdict.Wait, outcome.Verdict); // Required coverage is complete; later prerequisites are still pending.
    }

    [Fact]
    public void TerminalCanonicalGateBlocksSameFrameAndFutureFramesWithoutRevival()
    {
        var misaligned = M5Fixture.Take(true);
        var inputs = M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, misaligned).Concat([Trigger(otherTake: misaligned)]).ToArray();
        var run = Run(inputs);
        var failed = run.StrategyObservations.Where(o => o.AsOfUtc >= M5Fixture.At(14, 5) && o.AsOfUtc < M5Fixture.At(16)).ToArray();
        Assert.All(failed, o => Assert.Equal(RuleEvaluationResult.Failed, o.Evaluations.Single(e => e.RuleId == evaluator.RuleId).Result));
        Assert.All(failed, o => Assert.Null(o.LifecycleProgression!.ActiveInstance));
        Assert.All(failed, o => Assert.DoesNotContain(evaluator.RuleId, o.WorkflowProgression!.EstablishedRuleIds));
        var context = M5Fixture.Context(M5Fixture.At(14, 10), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 10)));
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(context).Result);
    }

    [Fact]
    public void EquivalentInputsGiveEquivalentStatelessDecisionsWithoutMutation()
    {
        var trigger = Trigger();
        var context = Context(10, trigger);
        var inputs = context.InputObservations.ToArray();
        var a = evaluator.Evaluate(context); var b = evaluator.Evaluate(context);
        Assert.Equal((a.Result, a.Reason, a.EvidenceReference), (b.Result, b.Reason, b.EvidenceReference));
        Assert.Equal(inputs, context.InputObservations);
        Assert.Equal(a.EvidenceReference, Evaluate(10, trigger).EvidenceReference);
    }

    private sealed class FvgWaiting : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => LiquidityFixture.Definition.StrategyId;
        public StrategyVersion StrategyVersion => LiquidityFixture.Definition.Version;
        public RuleId RuleId { get; } = new("NQ-FVG-001");
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(RuleEvaluationResult.Waiting, "Test-only next-stage eligibility probe.", null);
    }
}
