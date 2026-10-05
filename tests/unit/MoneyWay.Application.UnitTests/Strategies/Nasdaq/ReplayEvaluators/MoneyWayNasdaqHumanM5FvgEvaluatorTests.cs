using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM5FvgEvaluatorTests
{
    private readonly NasdaqHumanLiquidityTakeObservation take = M5Fixture.Take(false);
    private NasdaqHumanM5TriggerObservation Trigger(bool ifvg = false)
    {
        var reference = M5Fixture.Five(M5Fixture.At(14));
        NasdaqHumanM5TriggerEvent ev = ifvg
            ? new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10)), NasdaqHumanH4PermittedDirection.Buy, [reference], [reference])
            : new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), NasdaqHumanH4PermittedDirection.Buy, [reference], [reference], 100);
        return new(take, ev, M5Fixture.At(14, 10), "trigger source");
    }
    private NasdaqHumanM5FvgObservation Fvg(int close = 10, int? observed = null, bool ifvg = false, bool missing = false, string source = "FVG source") =>
        new(Trigger(ifvg), new([M5Fixture.Five(missing ? M5Fixture.At(13, 20) : M5Fixture.At(14, close).AddMinutes(-10)),
            M5Fixture.Five(M5Fixture.At(14, close).AddMinutes(-5)), M5Fixture.Five(M5Fixture.At(14, close))]),
            M5Fixture.At(14, observed ?? Math.Max(close, 10)), source);
    private static NasdaqHumanM5FvgQualityObservation Quality(NasdaqHumanM5FvgObservation fvg,
        NasdaqHumanM5FvgQualityDecision decision = NasdaqHumanM5FvgQualityDecision.Approved, int? observed = null, string source = "quality source") =>
        new(new(fvg, decision, fvg.EffectiveAtUtc, "Mentor review of exact candidate strength."), M5Fixture.At(14, observed ?? fvg.ObservedAtUtc.Minute), source);
    private IStrategyReplayInputObservation[] Inputs(bool ifvg, params IStrategyReplayInputObservation[] extra) =>
        M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat([Trigger(ifvg)]).Concat(extra).ToArray();
    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs, bool nextStageProbe = false, CandleSeries[]? series = null)
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll().ToList();
        if (nextStageProbe)
        {
            registry.RemoveAll(e => e.RuleId.Value == "NQ-M1-001");
            registry.Add(new NextStageProbe());
        }
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(registry),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(), new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, series ?? M5Fixture.Series(), inputs);
    }
    private static StrategyReplayContextObservation At(MultiTimeframeStrategyBacktestRun run, int minute) =>
        run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, minute));
    private static RuleEvaluationResult Result(StrategyReplayContextObservation observation, string rule) => observation.Evaluations.Single(e => e.RuleId.Value == rule).Result;
    private static NasdaqHumanM5FvgRuleFact Candidate(StrategyReplayContextObservation observation) =>
        Assert.IsType<NasdaqHumanM5FvgRuleFact>(observation.RuleFacts.Single(f => f.RuleId.Value == "NQ-FVG-001").Fact);
    private static NasdaqHumanM5FvgQualityRuleFact Review(StrategyReplayContextObservation observation) =>
        Assert.IsType<NasdaqHumanM5FvgQualityRuleFact>(observation.RuleFacts.Single(f => f.RuleId.Value == "NQ-FVG-002").Fact);

    [Fact]
    public void RegistryCapabilityAndPrematurePrerequisitesAreDerived()
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(registry, e => e.RuleId.Value == "NQ-FVG-001");
        Assert.Single(registry, e => e.RuleId.Value == "NQ-FVG-002");
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 15, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount, report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
        Assert.DoesNotContain(report.Rules, r => r.IsRequired && r.CapabilityStatus != ReplayRuleEvaluationCapabilityStatus.Implemented);
        var run = Run(Inputs(false, Fvg(), Quality(Fvg())));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 10), "NQ-FVG-001"));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 15), "NQ-FVG-002"));
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 10)]
    [InlineData(false, 15)]
    [InlineData(true, 15)]
    public void SameCloseAndLaterIndependentFvgPassWithoutGeometry(bool ifvg, int close)
    {
        var fvg = Fvg(close, ifvg: ifvg); var quality = Quality(fvg);
        var run = Run(Inputs(ifvg, fvg, quality), nextStageProbe: true);
        var existence = At(run, 15); var reviewed = At(run, 20);
        Assert.Equal(RuleEvaluationResult.Passed, Result(existence, "NQ-FVG-001"));
        Assert.Same(fvg, Candidate(existence).Selection.Fact);
        Assert.Equal(RuleEvaluationResult.Passed, Result(reviewed, "NQ-FVG-002"));
        Assert.Same(quality, Review(reviewed).Selection.Fact);
        Assert.Equal(fvg.Event.Candle1.High, fvg.Event.Candle3.High); // no numeric gap detector
        Assert.True(At(run, 25).WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").IsEligible);
        var evaluation = reviewed.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002");
        Assert.Equal(reviewed.AsOfUtc, evaluation.EvaluatedAtUtc);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, evaluation.DefinitionStatus);
        Assert.Contains("QualityEffectiveAtUtc", evaluation.EvidenceReference);
        Assert.Contains("Mentor review", evaluation.EvidenceReference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingFvgIsHumanReviewDueAndIfvgDoesNotProveIt(bool ifvg)
    {
        var run = Run(Inputs(ifvg));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 15), "NQ-FVG-001"));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 20), "NQ-FVG-002"));
    }

    [Fact]
    public void IncompatibleInitialSelectionsConflictRatherThanChooseApproved()
    {
        var a = Fvg(); var b = Fvg(15);
        var run = Run(Inputs(false, a, b, Quality(b)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 15), "NQ-FVG-001"));
        Assert.Contains("FvgEvidence", At(run, 15).Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-001").EvidenceReference);
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 20), "NQ-FVG-002"));
    }

    [Fact]
    public void CurrentUnavailableSourcesCannotBeSkippedForLaterApprovedSelection()
    {
        var missing = Fvg(missing: true); var later = Fvg(15);
        var run = Run(Inputs(false, missing, later, Quality(later)));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, Result(At(run, 15), "NQ-FVG-001"));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, Result(At(run, 20), "NQ-FVG-002"));
    }

    [Fact]
    public void MissingQualityIsNotRejectionAndCannotSkipCurrentCandidate()
    {
        var first = Fvg(); var later = Fvg(25);
        var run = Run(Inputs(false, first, later, Quality(later)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 20), "NQ-FVG-002"));
        Assert.Same(first, Candidate(At(run, 30)).Selection.Fact);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 30), "NQ-FVG-002"));
    }

    [Fact]
    public void RejectedThenDistinctApprovedCandidatePreservesHistoryAndSameSetup()
    {
        var first = Fvg(); var rejected = Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected);
        var later = Fvg(25); var approved = Quality(later);
        var run = Run(Inputs(false, first, rejected, later, approved), nextStageProbe: true);
        var at20 = At(run, 20); var at25 = At(run, 25); var at30 = At(run, 30);
        Assert.Equal(RuleEvaluationResult.Waiting, Result(at20, "NQ-FVG-002"));
        Assert.Same(first, Review(at20).Candidate.Selection.Fact);
        Assert.Equal(NasdaqHumanM5FvgQualityDecision.Rejected, Review(at20).Selection.Fact.Fact.Decision);
        Assert.NotNull(at20.LifecycleProgression!.ActiveInstance);
        Assert.False(at25.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").IsEligible);
        Assert.Same(later, Candidate(at25).Selection.Fact);
        Assert.Equal(RuleEvaluationResult.Passed, Result(at30, "NQ-FVG-002"));
        Assert.Same(later, Review(at30).Candidate.Selection.Fact);
        Assert.Equal(at20.LifecycleProgression.ActiveInstance.InstanceId, at30.LifecycleProgression!.ActiveInstance!.InstanceId);
        Assert.True(At(run, 35).WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").IsEligible);
        Assert.Equal(NasdaqHumanM5FvgQualityDecision.Rejected, Review(at20).Selection.Fact.Fact.Decision);
        var baseline = Run(Inputs(false, first, rejected));
        Assert.Equal(baseline.StrategyObservations.Single(o => o.AsOfUtc == at20.AsOfUtc).Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").EvidenceReference,
            at20.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").EvidenceReference);
    }

    [Fact]
    public void QualityConflictCannotBeSkippedOrHiddenByPriorRejection()
    {
        var first = Fvg(); var rejected = Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected);
        var contradicts = Quality(first, observed: 25, source: "late contradiction"); var later = Fvg(25);
        var run = Run(Inputs(false, first, rejected, contradicts, later, Quality(later)), nextStageProbe: true);
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 20), "NQ-FVG-002"));
        Assert.Same(first, Candidate(At(run, 25)).Selection.Fact);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 25), "NQ-FVG-002"));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 30), "NQ-FVG-002"));
        Assert.False(At(run, 30).WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").IsEligible);
    }

    [Fact]
    public void LateConflictAfterSecondApprovalBlocksNextStageWithoutRewritingHistory()
    {
        var first = Fvg(); var later = Fvg(25);
        var run = Run(Inputs(false, first, Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected), later,
            Quality(later), Quality(first, observed: 35, source: "late incompatible approval")), nextStageProbe: true);
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 20), "NQ-FVG-002"));
        Assert.Equal(RuleEvaluationResult.Passed, Result(At(run, 30), "NQ-FVG-002"));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 35), "NQ-FVG-002"));
        Assert.False(At(run, 40).WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-M1-001").IsEligible);
        Assert.Same(later, Review(At(run, 30)).Candidate.Selection.Fact);
    }

    [Fact]
    public void MultipleNextCandidatesConflictWithoutQualityOrTimestampRanking()
    {
        var first = Fvg(); var another = Fvg(30);
        // Both next selections first become available together; neither was canonically established first.
        var delayedLater = Fvg(25, observed: 30);
        var run = Run(Inputs(false, first, Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected), delayedLater, another, Quality(another)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 30), "NQ-FVG-001"));
    }

    [Fact]
    public void FutureObservationsSourcesAndQualityDoNotChangeHistoricalFrames()
    {
        var fvg = Fvg(observed: 25); var future = Fvg(25);
        var run = Run(Inputs(false, fvg, future));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(run, 15), "NQ-FVG-001"));
        var first = Fvg(); var lateQuality = Quality(first, observed: 30);
        var baseline = Run(Inputs(false, first)); var expanded = Run(Inputs(false, first, lateQuality, Fvg(35)));
        Assert.Equal(At(baseline, 20).Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").EvidenceReference,
            At(expanded, 20).Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").EvidenceReference);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(expanded, 20), "NQ-FVG-002"));
    }

    [Fact]
    public void TerminalAndCutoffDominateRejectedCandidateWaiting()
    {
        var first = Fvg(); var later = Fvg(30);
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 25), observed: M5Fixture.At(14, 25));
        var run = Run(Inputs(false, first, Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected), invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc, "terminal", take), later, Quality(later)));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 20), "NQ-FVG-002"));
        Assert.Equal(RuleEvaluationResult.Failed, Result(At(run, 30), "NQ-FVG-001"));
        Assert.Equal(RuleEvaluationResult.Failed, Result(At(run, 30), "NQ-FVG-002"));
        Assert.Null(At(run, 30).LifecycleProgression!.ActiveInstance);
        var cutoff = Run(Inputs(false, first, Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected)));
        var atCutoff = cutoff.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(16));
        Assert.Equal(RuleEvaluationResult.Failed, Result(atCutoff, "NQ-FVG-002"));
        Assert.Null(atCutoff.LifecycleProgression!.ActiveInstance);
    }

    [Fact]
    public void UnrelatedLaterUnavailableSelectionDoesNotBlockEstablishedCurrentCandidate()
    {
        var first = Fvg(); var missing = Fvg(25, missing: true);
        var run = Run(Inputs(false, first, Quality(first), missing));
        Assert.Equal(RuleEvaluationResult.Passed, Result(At(run, 30), "NQ-FVG-001"));
        Assert.Equal(RuleEvaluationResult.Passed, Result(At(run, 30), "NQ-FVG-002"));
    }

    [Fact]
    public void ReorderedCompatibleInputsKeepIdenticalAuditEvidenceAndRejectionTransitions()
    {
        var first = Fvg(); var duplicate = Fvg(observed: 15, source: "duplicate source"); var later = Fvg(25);
        var inputs = Inputs(false, first, duplicate, Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected), later, Quality(later));
        var a = Run(inputs); var b = Run(inputs.Reverse().ToArray());
        foreach (var minute in new[] { 15, 20, 25, 30 })
            foreach (var rule in new[] { "NQ-FVG-001", "NQ-FVG-002" })
                Assert.Equal(At(a, minute).Evaluations.Single(e => e.RuleId.Value == rule).EvidenceReference,
                    At(b, minute).Evaluations.Single(e => e.RuleId.Value == rule).EvidenceReference);
    }

    [Fact]
    public void UnavailableRetiredCandidateSourceDoesNotBlockLaterCandidate()
    {
        var first = Fvg(); var later = Fvg(25);
        var inputs = Inputs(false, first, Quality(first, NasdaqHumanM5FvgQualityDecision.Rejected), later, Quality(later));
        var baseline = Run(inputs);
        var history = baseline.StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 30)).ToArray();
        var series = M5Fixture.Series().Select(s => s.Timeframe != NasdaqHumanM5TriggerEvent.M5 ? s :
            new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Select(c => c.OpenTimeUtc != first.Event.Candle1.OpenTimeUtc ? c :
                new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc.AddSeconds(1), c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume)))).ToArray();
        var cursor = new MoneyWay.Domain.MarketData.Replay.MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == M5Fixture.At(14, 30))
            {
                var context = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs).WithPriorObservations(history);
                Assert.Equal(RuleEvaluationResult.Passed, new MoneyWayNasdaqHumanM5FvgEvaluator().Evaluate(context).Result);
                var quality = new MoneyWayNasdaqHumanM5FvgQualityEvaluator().Evaluate(context);
                Assert.Equal(RuleEvaluationResult.Passed, quality.Result);
                Assert.Same(later, Assert.IsType<NasdaqHumanM5FvgQualityRuleFact>(quality.Fact).Candidate.Selection.Fact);
                Assert.Equal(NasdaqHumanM5FvgQualityDecision.Rejected, Review(At(baseline, 20)).Selection.Fact.Fact.Decision);
                return;
            }
        Assert.Fail("Fixture boundary not found.");
    }

    [Fact]
    public void QualitySourceUnavailabilityIsNotRejection()
    {
        var first = Fvg(); var inputs = Inputs(false, first, Quality(first)); var run = Run(inputs);
        var history = run.StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 20)).ToArray();
        var series = M5Fixture.Series().Select(s => s.Timeframe != NasdaqHumanM5TriggerEvent.M5 ? s :
            new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Select(c => c.OpenTimeUtc != first.Event.Candle1.OpenTimeUtc ? c :
                new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc.AddSeconds(1), c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume)))).ToArray();
        var cursor = new MoneyWay.Domain.MarketData.Replay.MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == M5Fixture.At(14, 20))
            {
                var context = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs).WithPriorObservations(history);
                var result = new MoneyWayNasdaqHumanM5FvgQualityEvaluator().Evaluate(context);
                Assert.Equal(RuleEvaluationResult.DataUnavailable, result.Result);
                Assert.Null(result.Fact);
                return;
            }
        Assert.Fail("Fixture boundary not found.");
    }

    private sealed class NextStageProbe : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => LiquidityFixture.Definition.StrategyId;
        public StrategyVersion StrategyVersion => LiquidityFixture.Definition.Version;
        public RuleId RuleId { get; } = new("NQ-M1-001");
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(RuleEvaluationResult.Waiting, "Test-only next-stage eligibility probe.", null);
    }
}
