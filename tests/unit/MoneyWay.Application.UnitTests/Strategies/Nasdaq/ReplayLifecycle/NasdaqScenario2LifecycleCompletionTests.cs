using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using F = MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs.NasdaqHumanWickfillScenario2ReversalEvaluatorTests;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayLifecycle;

public sealed class NasdaqScenario2LifecycleCompletionTests
{
    private static readonly RuleId EligibilityRule = new("NQ-WF2-002");
    private static readonly MoneyWayNasdaqReplayLifecyclePolicy Policy = new();
    private static readonly StrategyReplayWorkflowDefinition Workflow = MoneyWayReplayWorkflowDefinitions.GetAll().Single();
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);
    private static IStrategyReplayInputObservation[] Inputs(F.Fixture fixture, DateTimeOffset? observed = null) =>
        [.. fixture.Inputs, F.Observation(fixture, NasdaqStrategyPath.WickfillScenario2), F.Reversal(fixture, observed: observed)];
    private static NasdaqScenario2LifecycleCompletionEvidence Proof(StrategyReplayContextObservation owner) =>
        Assert.IsType<NasdaqScenario2LifecycleCompletionEvidence>(owner.LifecycleProgression!.ActiveInstance!.Evidence.Evidence
            .Single(e => e is NasdaqScenario2LifecycleCompletionEvidence));

    [Fact]
    public void PriorCanonicalCompletionPreservesExactOwnersAndSurvivesCutoffWithoutDownstreamReadiness()
    {
        var f = F.Build(); var inputs = Inputs(f); var run = F.Run(inputs);
        var emitted = run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33));
        Assert.Equal(RuleEvaluationResult.Passed, emitted.Evaluations.Single(e => e.RuleId == EligibilityRule).Result);
        Assert.Null(Proof(emitted).Eligibility); // Current-frame Passed is not a lifecycle prerequisite.
        var next = run.StrategyObservations.Single(o => o.AsOfUtc == At(14));
        var proof = Proof(next);
        Assert.Same(emitted, proof.EligibilityOwner);
        Assert.Same(emitted.RuleFacts.Single(fact => fact.RuleId == EligibilityRule).Fact, proof.Eligibility);
        Assert.Same(run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 31)), proof.StartOwner);
        Assert.Equal(At(13, 32), proof.Eligibility!.EligibilityEffectiveAtUtc);
        Assert.Equal(At(14), proof.ObservedAtUtc);
        var signature = JsonSerializer.Serialize(next.LifecycleProgression);
        foreach (var time in new[] { At(16), At(17) })
        {
            var frame = run.StrategyObservations.Single(o => o.AsOfUtc == time);
            Assert.NotNull(frame.LifecycleProgression!.ActiveInstance);
            Assert.DoesNotContain(frame.LifecycleProgression.TransitionHistory, t => t.Kind == StrategyReplayLifecycleTransitionKind.Expire);
            Assert.DoesNotContain(frame.WorkflowProgression!.EstablishedRuleIds, id => id.Value is "NQ-M1-003" or "NQ-SL-001" or "NQ-RISK-001");
        }
        Assert.Equal(RuleEvaluationResult.Waiting, next.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result);
        Assert.Equal(RuleEvaluationResult.Waiting, next.Evaluations.Single(e => e.RuleId.Value == "NQ-RISK-001").Result);
        Assert.NotEqual(StrategyVerdict.Ready, new EvaluateStrategyReplayContextOutcomeUseCase().Execute(LiquidityFixture.Definition, next).Verdict);
        Assert.Equal(signature, JsonSerializer.Serialize(next.LifecycleProgression));
        Assert.DoesNotContain(next.RuleFacts, fact => fact.Fact is NasdaqPreEntryEligibilityRuleFact or NasdaqHumanStructuralStopLossRuleFact or NasdaqRiskExposureRuleFact);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Waiting)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable)]
    [InlineData(RuleEvaluationResult.NotApplicable)]
    [InlineData(RuleEvaluationResult.Failed)]
    [InlineData(RuleEvaluationResult.Passed)]
    public void RawRuleMembershipOrProgressionWithoutCanonicalEligibilityCannotComplete(RuleEvaluationResult result)
    {
        var f = F.Build(); var run = ControlledRun(Inputs(f), new EligibilityStub(result));
        Assert.Contains(run.StrategyObservations, o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001" && e.Result == RuleEvaluationResult.Passed));
        Assert.DoesNotContain(run.StrategyObservations.SelectMany(o => o.RuleFacts), fact => fact.RuleId == EligibilityRule);
        var cutoff = run.StrategyObservations.Single(o => o.AsOfUtc == At(16));
        Assert.Null(cutoff.LifecycleProgression!.ActiveInstance);
        Assert.Equal(StrategyReplayProgressionTerminationKind.Expired, Assert.Single(cutoff.LifecycleProgression.Instances).TerminationKind);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(17)]
    public void EligibilityFirstObservedAtOrAfterCutoffCannotBackfillOrRevive(int observedHour)
    {
        var f = F.Build(); var baseline = F.Run([.. f.Inputs, F.Observation(f, NasdaqStrategyPath.WickfillScenario2)]); var run = F.Run(Inputs(f, At(observedHour)));
        var cutoff = run.StrategyObservations.Single(o => o.AsOfUtc == At(16));
        Assert.Null(cutoff.LifecycleProgression!.ActiveInstance);
        Assert.Null(run.StrategyObservations.Last().LifecycleProgression!.ActiveInstance);
        Assert.DoesNotContain(run.StrategyObservations.SelectMany(o => o.RuleFacts), fact => fact.RuleId == EligibilityRule);
        foreach (var pair in baseline.StrategyObservations.Where(o => o.AsOfUtc < At(16)).Zip(run.StrategyObservations))
            Assert.Equal(JsonSerializer.Serialize(pair.First), JsonSerializer.Serialize(pair.Second));
    }

    [Theory]
    [InlineData("take")]
    [InlineData("direction")]
    [InlineData("instance")]
    [InlineData("progression")]
    [InlineData("future")]
    public void ForeignOrUnestablishedCanonicalOwnersCannotPreventExpiration(string mismatch)
    {
        var f = F.Build(); var inputs = Inputs(f); var run = F.Run(inputs);
        var history = run.StrategyObservations.Where(o => o.AsOfUtc < At(16)).ToArray();
        var candidate = history.Last();
        if (mismatch is "take" or "direction")
        {
            var other = F.Build(eventId: "source:another-initial-take"); var otherRun = F.Run(Inputs(other));
            var foreign = otherRun.StrategyObservations.Last(o => o.AsOfUtc < At(16)).RuleFacts.Single(fact => fact.RuleId == EligibilityRule);
            history[^1] = new(candidate.StrategyId, candidate.StrategyVersion, candidate.ProviderId, candidate.Symbol,
                candidate.Step, candidate.AsOfUtc, candidate.Evaluations, candidate.WorkflowProgression, candidate.LifecycleProgression,
                ruleFacts: candidate.RuleFacts.Where(fact => fact.RuleId != EligibilityRule).Append(foreign));
            if (mismatch == "direction")
            {
                // BUY Scenario 2 is unrepresentable by the approved human applicability constructor.
                var buy = F.Build(direction: NasdaqHumanH4PermittedDirection.Buy, includeContext: false);
                Assert.Throws<ArgumentException>(() => F.Observation(buy, NasdaqStrategyPath.WickfillScenario2));
            }
        }
        else if (mismatch == "progression")
            history[^1] = new(candidate.StrategyId, candidate.StrategyVersion, candidate.ProviderId, candidate.Symbol,
                candidate.Step, candidate.AsOfUtc, candidate.Evaluations, ruleFacts: candidate.RuleFacts);
        else if (mismatch == "instance")
        {
            var old = candidate.LifecycleProgression!; var instance = old.ActiveInstance!;
            var start = history.Single(o => o.AsOfUtc == instance.StartedAtUtc);
            var ended = new StrategyReplayProgressionInstanceSnapshot(new(1), instance.StartedAtUtc, instance.StateReference,
                start.WorkflowProgression!, At(13, 32), StrategyReplayProgressionTerminationKind.Expired);
            var different = new StrategyReplayProgressionInstanceSnapshot(new(2), At(13, 33), instance.StateReference, instance.WorkflowProgression);
            var lifecycle = new StrategyReplayLifecycleSnapshot(candidate.StrategyId, candidate.StrategyVersion, candidate.ProviderId, candidate.Symbol,
                candidate.Step, candidate.AsOfUtc, [ended, different], []);
            history[^1] = new(candidate.StrategyId, candidate.StrategyVersion, candidate.ProviderId, candidate.Symbol,
                candidate.Step, candidate.AsOfUtc, candidate.Evaluations, candidate.WorkflowProgression, lifecycle, ruleFacts: candidate.RuleFacts);
        }
        else
            Assert.Throws<ArgumentException>(() => F.Context(At(14), inputs).WithPriorObservations(history));
        if (mismatch == "future") return;
        var context = F.Context(At(16), inputs).WithPriorObservations(history);
        var previous = run.StrategyObservations.Last(o => o.AsOfUtc < At(16)).LifecycleProgression!;
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var result = new AdvanceStrategyReplayLifecycleUseCase(new()).Execute(Workflow, Policy, context, raw, previous,
            previous.ActiveInstance!.WorkflowProgression, Policy);
        Assert.Null(result.LifecycleProgression.ActiveInstance);
    }

    [Theory]
    [InlineData("applicability")]
    [InlineData("reversal")]
    public void LaterIncompatibleReviewSupersedesPriorCompletionWithoutRevival(string kind)
    {
        var f = F.Build(); var inputs = Inputs(f);
        IStrategyReplayInputObservation conflict = kind == "applicability"
            ? F.Observation(f, NasdaqStrategyPath.Standard, observed: At(14))
            : F.Reversal(f, eventId: "source:conflicting-turn", observed: At(14));
        var run = F.Run([.. inputs, conflict]);
        Assert.NotNull(run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33)).RuleFacts.SingleOrDefault(fact => fact.RuleId == EligibilityRule));
        Assert.Null(Proof(run.StrategyObservations.Single(o => o.AsOfUtc == At(14))).Eligibility);
        Assert.Null(run.StrategyObservations.Single(o => o.AsOfUtc == At(16)).LifecycleProgression!.ActiveInstance);
        Assert.Null(run.StrategyObservations.Last().LifecycleProgression!.ActiveInstance);
    }

    [Fact]
    public void SameFrameInvalidationCancelsAnAlreadyCompletedScenario2Setup()
    {
        var f = F.Build(); var initial = f.Owner.RuleFacts.Select(fact => fact.Fact).OfType<NasdaqLiquidityTakeRuleFact>().Single().Take;
        var upstream = F.Context(At(13, 5), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream,
            new MoneyWay.Application.Strategies.Nasdaq.Liquidity.NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "review:opposite");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "source:terminal", reference.ReferencePrice - 1, At(15), "source:terminal"),
            upstream.AsOfUtc, At(15), "review:terminal");
        var series = ScenarioSeries().Select(series => series.Timeframe == new Timeframe(1, TimeframeUnit.Minute)
            ? new CandleSeries(series.ProviderId, series.Symbol, series.Timeframe, series.Candles.Append(new Candle(
                series.ProviderId, series.Symbol, series.Timeframe, At(14, 59), At(15), 100, 110, 90, 100, null)).OrderBy(c => c.OpenTimeUtc)) : series).ToArray();
        var run = ControlledRun([.. Inputs(f), opposite, new NasdaqHumanRelevantLiquidityTakeObservation(opposite, At(15), "review:terminal", initial)],
            new MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator(), series);
        Assert.NotNull(Proof(run.StrategyObservations.Single(o => o.AsOfUtc == At(14))).Eligibility);
        var terminal = run.StrategyObservations.Single(o => o.AsOfUtc == At(15));
        Assert.Contains(new RuleId("NQ-WF2-002"), run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33)).WorkflowProgression!.EstablishedRuleIds);
        Assert.Null(terminal.LifecycleProgression!.ActiveInstance);
        Assert.Equal(StrategyReplayProgressionTerminationKind.Cancelled, Assert.Single(terminal.LifecycleProgression.Instances).TerminationKind);
        Assert.Null(run.StrategyObservations.Last().LifecycleProgression!.ActiveInstance);
    }

    [Fact]
    public void ExplicitStandardWithoutM1CompletionStillExpiresAndCannotUseScenario2RawEvidence()
    {
        var f = F.Build(); var run = F.Run([.. f.Inputs, F.Observation(f), F.Reversal(f)]);
        Assert.All(run.StrategyObservations.Where(o => o.AsOfUtc is var time && time > At(13, 32) && time < At(16)),
            o => Assert.Equal(RuleEvaluationResult.NotApplicable, o.Evaluations.Single(e => e.RuleId == EligibilityRule).Result));
        Assert.Null(run.StrategyObservations.Single(o => o.AsOfUtc == At(16)).LifecycleProgression!.ActiveInstance);
    }

    [Fact]
    public void CurrentFrameOnlyPositiveEligibilityCannotPreventCutoffExpiration()
    {
        var f = F.Build(); var run = ControlledRun(Inputs(f), new DelayedEligibility());
        var cutoff = run.StrategyObservations.Single(o => o.AsOfUtc == At(16));
        Assert.Equal(RuleEvaluationResult.Passed, cutoff.Evaluations.Single(e => e.RuleId == EligibilityRule).Result);
        Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(cutoff.RuleFacts.Single(fact => fact.RuleId == EligibilityRule).Fact);
        Assert.Null(cutoff.LifecycleProgression!.ActiveInstance);
        Assert.Null(run.StrategyObservations.Last().LifecycleProgression!.ActiveInstance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerEvidenceCoexistsWithPolicyProofAndCannotMaskForeignIdentity(bool foreignIdentity)
    {
        var f = F.Build(); var inputs = Inputs(f); var run = F.Run(inputs);
        var context = F.Context(At(16), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(16)));
        var previous = context.PriorObservations.Last().LifecycleProgression!;
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var useCase = new AdvanceStrategyReplayLifecycleUseCase(new());
        if (foreignIdentity)
        {
            Assert.Throws<ArgumentException>(() => useCase.Execute(Workflow, Policy, context, raw, previous,
                previous.ActiveInstance!.WorkflowProgression, new CallerProducer(true)));
            return;
        }
        var result = useCase.Execute(Workflow, Policy, context, raw, previous, previous.ActiveInstance!.WorkflowProgression, new CallerProducer(false));
        var active = Assert.IsType<StrategyReplayProgressionInstanceSnapshot>(result.LifecycleProgression.ActiveInstance);
        Assert.True(active.Evidence.TryGet<CallerEvidence>(out _));
        Assert.True(active.Evidence.TryGet<NasdaqScenario2LifecycleCompletionEvidence>(out var proof));
        Assert.NotNull(proof!.Eligibility);
    }

    [Fact]
    public void ForeignCompletionPayloadWithMatchingOrdinalAndTimestampCannotCompleteAnotherStart()
    {
        var f = F.Build(); var inputs = Inputs(f); var run = F.Run(inputs);
        var other = F.Build(eventId: "source:foreign-start"); var foreignInputs = Inputs(other); var foreignRun = F.Run(foreignInputs);
        var context = F.Context(At(16), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(16)));
        var foreignContext = F.Context(At(16), foreignInputs).WithPriorObservations(foreignRun.StrategyObservations.Where(o => o.AsOfUtc < At(16)));
        var previous = context.PriorObservations.Last().LifecycleProgression!;
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var progression = new AdvanceStrategyReplayProgressionUseCase().Execute(Workflow, raw, previous.ActiveInstance!.WorkflowProgression);
        var foreignProof = Policy.Capture(new(foreignContext, raw, progression, foreignContext.PriorObservations.Last().LifecycleProgression));
        Assert.Equal(previous.ActiveInstance.InstanceId, foreignProof.InstanceId);
        Assert.NotNull(Assert.IsType<NasdaqScenario2LifecycleCompletionEvidence>(Assert.Single(foreignProof.Evidence)).Eligibility);
        Assert.Equal(StrategyReplayLifecycleTransitionKind.Expire, Policy.Decide(new(raw, progression, previous, foreignProof)).Kind);
        // EstablishedRuleIds alone cannot substitute for the verified companion proof either.
        Assert.Equal(StrategyReplayLifecycleTransitionKind.Expire, Policy.Decide(new(raw, progression, previous)).Kind);
    }

    private sealed record CallerEvidence(DateTimeOffset ObservedAtUtc) : IStrategyReplayLifecycleEvidence;
    private sealed class CallerProducer(bool foreignIdentity) : IStrategyReplayLifecycleEvidenceProducer
    {
        public StrategyId StrategyId => Policy.StrategyId;
        public StrategyVersion StrategyVersion => Policy.StrategyVersion;
        public StrategyReplayLifecycleEvidenceSnapshot Capture(StrategyReplayLifecycleEvidenceProductionContext production)
        {
            var context = production.ReplayContext;
            return new(context.StrategyId, context.StrategyVersion, foreignIdentity ? new MarketDataProviderId("foreign") : context.ProviderId,
                context.Symbol, context.Step, context.AsOfUtc, StrategyReplayLifecycleEvidenceScope.ActiveInstance,
                production.PreviousLifecycle!.ActiveInstance!.InstanceId, [new CallerEvidence(context.AsOfUtc)]);
        }
    }

    private sealed class DelayedEligibility : IReplayRuleEvaluator
    {
        private readonly MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator original = new();
        private NasdaqWickfillScenario2PreEntryEligibilityRuleFact? retained;
        public StrategyId StrategyId => original.StrategyId;
        public StrategyVersion StrategyVersion => original.StrategyVersion;
        public RuleId RuleId => original.RuleId;
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            // Adversarial delayed canonical emission: the source fact was observable earlier,
            // but no owning-rule Passed/progression was emitted before the actual cutoff.
            if (context.AsOfUtc < At(16))
            {
                retained ??= original.Evaluate(context).Fact as NasdaqWickfillScenario2PreEntryEligibilityRuleFact;
                return new(RuleEvaluationResult.Waiting, "Controlled delayed emission.", null);
            }
            return new(RuleEvaluationResult.Passed, "Current frame only.", null, retained);
        }
    }

    [Fact]
    public void RawApplicabilityAndReversalWithoutCanonicalStartCannotCreateCompletion()
    {
        var f = F.Build();
        var run = F.Run([F.Observation(f, NasdaqStrategyPath.WickfillScenario2), F.Reversal(f)]);
        Assert.All(run.StrategyObservations, observation =>
        {
            Assert.Empty(observation.LifecycleProgression!.Instances);
            Assert.DoesNotContain(observation.RuleFacts, fact => fact.RuleId == EligibilityRule);
        });
    }

    private static MultiTimeframeStrategyBacktestRun ControlledRun(IStrategyReplayInputObservation[] inputs, IReplayRuleEvaluator replacement, CandleSeries[]? series = null)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(),
            new(MoneyWayReplayRuleEvaluators.GetAll().Where(e => e.RuleId != EligibilityRule).Append(replacement)),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, series ?? ScenarioSeries(), inputs);
    }

    private static CandleSeries[] ScenarioSeries()
    {
        var sources = Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h))
            .Concat(new[] { 0, 4, 8 }.Select(h => LiquidityFixture.Candle(h, hours: 4)));
        return sources.GroupBy(c => c.Timeframe).Select(g => new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, g.Key, g.OrderBy(c => c.OpenTimeUtc)))
            .Append(new(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Minute),
                new[] { At(13, 5), At(13, 15), At(13, 30), At(13, 31), At(13, 32), At(13, 33), At(14), At(16), At(17) }
                    .Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(1, TimeframeUnit.Minute), t.AddMinutes(-1), t, 100, 110, 90, 100, null)))).ToArray();
    }

    private sealed class EligibilityStub(RuleEvaluationResult result) : IReplayRuleEvaluator
    {
        private readonly MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator identity = new();
        public StrategyId StrategyId => identity.StrategyId;
        public StrategyVersion StrategyVersion => identity.StrategyVersion;
        public RuleId RuleId => EligibilityRule;
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(result, "Controlled non-completion evidence.", null);
    }
}
