using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanStructuralStopLossTests
{
    private static readonly NasdaqHumanStructuralStopLossObservationSelector Selector = new();

    private static Candle Minute(int close) => M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute)
        .Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, close));

    private static IStrategyReplayInputObservation[] Inputs(NasdaqHumanH4PermittedDirection direction,
        params IStrategyReplayInputObservation[] extra)
    {
        var take = M5Fixture.Take(direction == NasdaqHumanH4PermittedDirection.Sell);
        var reference = M5Fixture.Five(M5Fixture.At(14));
        var trigger = new NasdaqHumanM5TriggerObservation(take,
            new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), direction,
                [reference], [reference], 100), M5Fixture.At(14, 10), "trigger:source");
        var fvg = new NasdaqHumanM5FvgObservation(trigger,
            new([M5Fixture.Five(M5Fixture.At(14)), M5Fixture.Five(M5Fixture.At(14, 5)),
                M5Fixture.Five(M5Fixture.At(14, 10))]), M5Fixture.At(14, 10), "fvg:source");
        var quality = new NasdaqHumanM5FvgQualityObservation(new(fvg,
            NasdaqHumanM5FvgQualityDecision.Approved, M5Fixture.At(14, 10), "Approved source review."),
            M5Fixture.At(14, 15), "quality:source");
        var pullback = new NasdaqHumanM1CorrectiveRetracementObservation(quality,
            new([Minute(20)]), M5Fixture.At(14, 20), "review:pullback");
        return M5Fixture.Inputs(direction, take).Concat([trigger, fvg, quality, pullback]).Concat(extra).ToArray();
    }

    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
    }

    private static (NasdaqPreEntryEligibilityRuleFact Fact, NasdaqHumanM1RealignmentObservation Realignment) Setup(
        NasdaqHumanH4PermittedDirection direction, decimal? swingLevel = null)
    {
        var pullback = Run(Inputs(direction)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-001").Select(f => f.Fact)
            .OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().First();
        var realignment = new NasdaqHumanM1RealignmentObservation(pullback,
            new(Minute(20), [Minute(20)], swingLevel ?? (direction == NasdaqHumanH4PermittedDirection.Buy ? 99 : 101), direction),
            M5Fixture.At(14, 20), "review:realignment", pullbackBeforeRealignmentAtSameClose: true);
        var fact = Run(Inputs(direction, realignment)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().First();
        return (fact, realignment);
    }

    private static StrategyReplayContext Frame(NasdaqHumanH4PermittedDirection direction, DateTimeOffset time,
        NasdaqHumanM1RealignmentObservation realignment, params IStrategyReplayInputObservation[] extra)
    {
        var inputs = Inputs(direction, realignment).Concat(extra).ToArray();
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < time).ToArray();
        return M5Fixture.Context(time, inputs).WithPriorObservations(history);
    }

    private static NasdaqHumanStructuralStopLossObservation Stop(NasdaqPreEntryEligibilityRuleFact fact,
        Candle? source = null, decimal? stopPrice = null, DateTimeOffset? observed = null, string reference = "review:stop")
    {
        var anchor = new NasdaqHumanM5ProtectionAnchor(fact.Direction == NasdaqHumanH4PermittedDirection.Buy
            ? NasdaqM5ProtectionAnchorKind.HigherLow : NasdaqM5ProtectionAnchorKind.LowerHigh,
            [source ?? M5Fixture.Five(M5Fixture.At(14, 20))]);
        return new(fact, anchor, stopPrice ?? (fact.Direction == NasdaqHumanH4PermittedDirection.Buy ? 89 : 111),
            M5Fixture.At(14, 30), observed ?? M5Fixture.At(14, 30), reference);
    }

    private static RuleEvaluationResult Evaluation(MultiTimeframeStrategyBacktestRun run, DateTimeOffset time) =>
        run.StrategyObservations.Single(o => o.AsOfUtc == time).Evaluations
            .Single(e => e.RuleId.Value == "NQ-SL-001").Result;

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqM5ProtectionAnchorKind.HigherLow, 90, 89)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqM5ProtectionAnchorKind.LowerHigh, 110, 111)]
    public void CanonicalEvaluatorPassesOnlyExactDocumentedStructuralStop(
        NasdaqHumanH4PermittedDirection direction, NasdaqM5ProtectionAnchorKind kind, decimal anchor, decimal stopPrice)
    {
        var setup = Setup(direction);
        var evidence = Stop(setup.Fact);
        var run = Run(Inputs(direction, setup.Realignment, evidence));
        var at = M5Fixture.At(15);
        var stage = run.StrategyObservations.Single(o => o.AsOfUtc == at);
        Assert.Equal(RuleEvaluationResult.Passed, Evaluation(run, at));
        var fact = Assert.IsType<NasdaqHumanStructuralStopLossRuleFact>(stage.RuleFacts
            .Single(f => f.RuleId.Value == "NQ-SL-001").Fact);
        Assert.Same(evidence, fact.Selection.Fact);
        Assert.Same(stage.RuleFacts.Single(f => f.RuleId.Value == "NQ-SL-001").Fact, fact);
        Assert.Equal(direction, fact.Direction);
        Assert.Equal(kind, fact.ProtectionAnchorKind);
        Assert.Equal(anchor, fact.StructuralAnchorPrice);
        Assert.Equal(stopPrice, fact.StopPrice);
        Assert.Equal(evidence.EffectiveAtUtc, fact.EffectiveAtUtc);
        Assert.NotEqual(at, fact.EffectiveAtUtc);
        Assert.Equal(at, stage.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").EvaluatedAtUtc);
        Assert.Contains("review:stop", stage.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").EvidenceReference);
        Assert.Contains(new RuleId("NQ-SL-001"), stage.WorkflowProgression!.EstablishedRuleIds);
        Assert.Equal([new RuleId("NQ-SL-001")], MoneyWayReplayWorkflowDefinitions.GetAll().Single()
            .GetPrerequisiteRuleIds(new RuleId("NQ-TP-001")));
        Assert.NotEqual(RuleEvaluationResult.Passed, stage.Evaluations.Single(e => e.RuleId.Value == "NQ-TP-001").Result);

        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(evaluators, e => e.RuleId.Value == "NQ-SL-001"
            && e is MoneyWayNasdaqHumanStructuralStopLossEvaluator);
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount,
            report.ImplementedCount, report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
        Assert.DoesNotContain(report.Rules, r => r.IsRequired && r.CapabilityStatus != ReplayRuleEvaluationCapabilityStatus.Implemented);
    }

    [Fact]
    public void EvaluatorMapsMissingConflictUnavailableAndWrongUpstreamWithoutFailure()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var at = M5Fixture.At(15);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Evaluation(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment)), at));
        var first = Stop(setup.Fact);
        var other = Stop(setup.Fact, source: M5Fixture.Five(M5Fixture.At(14, 15)), reference: "other:anchor");
        var conflict = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment, first, other));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Evaluation(conflict, at));
        Assert.Contains("other:anchor", conflict.StrategyObservations.Single(o => o.AsOfUtc == at).Evaluations
            .Single(e => e.RuleId.Value == "NQ-SL-001").EvidenceReference);
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5ProtectionAnchor.M5,
            M5Fixture.At(14, 19).AddSeconds(1), M5Fixture.At(14, 24).AddSeconds(1), 100, 110, 90, 100, null);
        var unavailable = Stop(setup.Fact, source: absent);
        var noSource = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment, unavailable));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, Evaluation(noSource, at));
        Assert.Contains("review:stop", noSource.StrategyObservations.Single(o => o.AsOfUtc == at).Evaluations
            .Single(e => e.RuleId.Value == "NQ-SL-001").EvidenceReference);
        Assert.Equal(RuleEvaluationResult.DataUnavailable,
            Evaluation(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment, first, unavailable)), at));
        var wrong = Stop(Setup(NasdaqHumanH4PermittedDirection.Buy, swingLevel: 98).Fact);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Evaluation(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment, wrong)), at));
    }

    [Fact]
    public void EvaluatorWaitsForPreEntryAndCannotUseFutureOrTerminalEvidence()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var stop = Stop(setup.Fact);
        var withoutPreEntry = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, stop));
        Assert.Equal(RuleEvaluationResult.Waiting, Evaluation(withoutPreEntry, M5Fixture.At(14, 30)));
        var future = Stop(setup.Fact, observed: M5Fixture.At(16));
        var baseline = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment));
        var expanded = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment, future));
        Assert.Equal(Evaluation(baseline, M5Fixture.At(15)), Evaluation(expanded, M5Fixture.At(15)));
        Assert.NotEqual(RuleEvaluationResult.Passed, Evaluation(expanded, M5Fixture.At(16)));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var initiating = setup.Fact.Realignment.Pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake;
        var terminal = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, setup.Realignment, stop, invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal:event", initiating)));
        Assert.NotEqual(RuleEvaluationResult.Passed, Evaluation(terminal, M5Fixture.At(15)));
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqM5ProtectionAnchorKind.HigherLow, 90)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqM5ProtectionAnchorKind.LowerHigh, 110)]
    public void ExactHumanSelectedM5AnchorAndDocumentedStopAreUnique(
        NasdaqHumanH4PermittedDirection direction, NasdaqM5ProtectionAnchorKind kind, decimal anchorPrice)
    {
        var setup = Setup(direction);
        var stop = Stop(setup.Fact);
        var selection = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Unique>(
            Selector.Select(Frame(direction, M5Fixture.At(15), setup.Realignment, stop), setup.Fact));
        Assert.Same(stop, selection.Fact);
        Assert.Equal(kind, selection.Fact.ProtectionAnchor.Kind);
        Assert.Equal(anchorPrice, selection.Fact.ProtectionAnchor.ProtectionAnchorPrice);
        Assert.Equal(M5Fixture.At(14, 30), selection.Fact.EffectiveAtUtc);
        Assert.Equal(M5Fixture.At(14, 30), selection.Fact.ObservedAtUtc);
        Assert.NotEqual(selection.Fact.ProtectionAnchor.ProtectionAnchorPrice, selection.Fact.StopPrice);
        Assert.Equal("review:stop", selection.Fact.SourceReference);
        Assert.Empty(selection.UnavailableSourceObservations);
    }

    [Fact]
    public void WrongKindTimeframeAndStopSideAreRejectedWithoutInferringAFormula()
    {
        var buy = Setup(NasdaqHumanH4PermittedDirection.Buy).Fact;
        var sell = Setup(NasdaqHumanH4PermittedDirection.Sell).Fact;
        var m5 = M5Fixture.Five(M5Fixture.At(14, 20));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralStopLossObservation(buy,
            new(NasdaqM5ProtectionAnchorKind.LowerHigh, [m5]), 111, M5Fixture.At(14, 30),
            M5Fixture.At(14, 30), "wrong:kind"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralStopLossObservation(sell,
            new(NasdaqM5ProtectionAnchorKind.HigherLow, [m5]), 89, M5Fixture.At(14, 30),
            M5Fixture.At(14, 30), "wrong:kind"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5ProtectionAnchor(
            NasdaqM5ProtectionAnchorKind.HigherLow, [Minute(20)]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5ProtectionAnchor(
            NasdaqM5ProtectionAnchorKind.HigherLow, [LiquidityFixture.Candle(9)]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5ProtectionAnchor(
            NasdaqM5ProtectionAnchorKind.HigherLow, [LiquidityFixture.Candle(8, hours: 4)]));
        Assert.Throws<ArgumentException>(() => Stop(buy, stopPrice: 90));
        Assert.Throws<ArgumentException>(() => Stop(sell, stopPrice: 110));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralStopLossObservation(buy,
            new(NasdaqM5ProtectionAnchorKind.HigherLow, [m5]), 89, M5Fixture.At(14, 15),
            M5Fixture.At(14, 30), "early:selection"));
    }

    [Fact]
    public void MissingCompatibleSupportAndCompetingAnchorsKeepAllProvenance()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment), setup.Fact));
        var first = Stop(setup.Fact);
        var duplicate = Stop(setup.Fact, observed: M5Fixture.At(15), reference: "second:review");
        var compatible = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Unique>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, duplicate, first), setup.Fact));
        Assert.Equal(2, compatible.SupportingObservations.Count);
        Assert.Contains(first, compatible.SupportingObservations);
        Assert.Contains(duplicate, compatible.SupportingObservations);
        var other = Stop(setup.Fact, source: M5Fixture.Five(M5Fixture.At(14, 15)), reference: "other:anchor");
        var forward = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Conflict>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, first, other), setup.Fact));
        var reversed = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Conflict>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, other, first), setup.Fact));
        Assert.Equal(forward.Alternatives.Select(o => o.SourceReference), reversed.Alternatives.Select(o => o.SourceReference));
        Assert.Equal(2, forward.Alternatives.Count);
        var differentStop = Stop(setup.Fact, stopPrice: 88, reference: "other:stop");
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Conflict>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, first, differentStop), setup.Fact));
    }

    [Fact]
    public void WrongUpstreamUnavailableSourceAndFutureEvidenceCannotQualify()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var other = Setup(NasdaqHumanH4PermittedDirection.Buy, swingLevel: 98).Fact;
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, Stop(other)), setup.Fact));
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5ProtectionAnchor.M5,
            M5Fixture.At(14, 19).AddSeconds(1), M5Fixture.At(14, 24).AddSeconds(1), 100, 110, 90, 100, null);
        var unresolved = Stop(setup.Fact, source: absent);
        var missing = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, unresolved), setup.Fact));
        Assert.Same(unresolved, Assert.Single(missing.UnavailableSourceObservations));
        var future = Stop(setup.Fact, observed: M5Fixture.At(16));
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, future), setup.Fact));
        var futureSource = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5ProtectionAnchor.M5,
            M5Fixture.At(15), M5Fixture.At(15, 5), 100, 110, 90, 100, null);
        var futureAnchor = new NasdaqHumanM5ProtectionAnchor(NasdaqM5ProtectionAnchorKind.HigherLow, [futureSource]);
        var laterSelection = new NasdaqHumanStructuralStopLossObservation(setup.Fact, futureAnchor, 89,
            M5Fixture.At(15, 5), M5Fixture.At(15, 5), "future:source");
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, laterSelection), setup.Fact));
    }

    [Fact]
    public void TerminalSessionAndCanonicalCutoffDoNotReviveStopEvidence()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var stop = Stop(setup.Fact);
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(16), setup.Realignment, stop), setup.Fact));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var initiating = setup.Fact.Realignment.Pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake;
        var terminal = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, stop,
            invalid, new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal:event", initiating));
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(terminal, setup.Fact));
    }
}
