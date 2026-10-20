using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

// Share only this synthetic Scenario 2 fixture; no real-session binding or constants.
public sealed partial class NasdaqHumanWickfillScenario2ReversalEvaluatorTests
{
    private static readonly RuleId EligibilityRule = new("NQ-WF2-002");
    private readonly MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator eligibilityEvaluator = new();

    private static (StrategyReplayContext Context, MultiTimeframeStrategyBacktestRun Run) EligibilityHistory(
        Fixture f, NasdaqStrategyPath path = NasdaqStrategyPath.WickfillScenario2, bool includeReversal = true)
    {
        var inputs = f.Inputs.Concat(new IStrategyReplayInputObservation[] { Observation(f, path) })
            .Concat(includeReversal && path == NasdaqStrategyPath.WickfillScenario2 ? [Reversal(f)] : Array.Empty<IStrategyReplayInputObservation>()).ToArray();
        var run = Run(inputs);
        return (Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14))), run);
    }

    [Fact]
    public void EligibilityPreservesExactImmutableCanonicalReversalAndOnlyProjectsItsIdentityAndTime()
    {
        var (context, _) = EligibilityHistory(Build());
        var decision = eligibilityEvaluator.Evaluate(context);
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        var fact = Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(decision.Fact);
        var predecessor = context.PriorObservations.Last(o => o.Evaluations.Any(e => e.RuleId == Rule));
        Assert.Same(predecessor.RuleFacts.Single(f => f.RuleId == Rule).Fact, fact.Reversal);
        Assert.Equal(fact.Reversal.Applicability.Fact.Session, fact.Session);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, fact.Direction);
        Assert.Equal(NasdaqStrategyPath.WickfillScenario2, fact.Reversal.Applicability.Fact.Path);
        Assert.Same(fact.Reversal.Applicability.Fact.InitialTakeObservation,
            fact.Reversal.Reversal.Fact.OpeningSweep.TakeObservation);
        Assert.Equal(fact.Reversal.EffectiveAtUtc, fact.EligibilityEffectiveAtUtc);
        Assert.True(fact.EligibilityEffectiveAtUtc < context.AsOfUtc);
        Assert.Empty(fact.GetType().GetConstructors());
        Assert.All(fact.GetType().GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(new[] { "Direction", "EligibilityEffectiveAtUtc", "Reversal", "Session" },
            fact.GetType().GetProperties().Select(p => p.Name).OrderBy(n => n));
        var repeat = eligibilityEvaluator.Evaluate(context);
        Assert.Equal((decision.Result, decision.EvidenceReference), (repeat.Result, repeat.EvidenceReference));
        Assert.Same(fact.Reversal, Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(repeat.Fact).Reversal);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void EligibilityUsesCanonicalStandardNotApplicableWithoutCreatingAStandardOrScenario2Fact(NasdaqHumanH4PermittedDirection direction)
    {
        var f = Build(direction: direction, includeContext: false);
        var (context, _) = EligibilityHistory(f, NasdaqStrategyPath.Standard);
        var decision = eligibilityEvaluator.Evaluate(context);
        Assert.Equal(RuleEvaluationResult.NotApplicable, decision.Result);
        Assert.Null(decision.Fact);
        Assert.Equal(StrategyVerdict.Ready, EligibilityAggregate(decision.Result));
    }

    [Fact]
    public void EligibilityWaitsForSetupAndForCanonicalReversalWithoutReinterpretingRawEvidence()
    {
        var f = Build();
        Assert.Equal(RuleEvaluationResult.Waiting, eligibilityEvaluator.Evaluate(Context(At(13, 30), f.Inputs)).Result);
        var (context, _) = EligibilityHistory(f, includeReversal: false);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, LatestReversalResult(context));
        Assert.Equal(RuleEvaluationResult.Waiting, eligibilityEvaluator.Evaluate(context).Result);
        Assert.Null(eligibilityEvaluator.Evaluate(context).Fact);
        var missing = Frame(f, At(14));
        Assert.Equal(RuleEvaluationResult.Waiting, eligibilityEvaluator.Evaluate(missing).Result);
    }

    private static RuleEvaluationResult LatestReversalResult(StrategyReplayContext context) => context.PriorObservations
        .Last(o => o.Evaluations.Any(e => e.RuleId == Rule)).Evaluations.Single(e => e.RuleId == Rule).Result;

    [Fact]
    public void EligibilityHasNoSameFramePositiveTransportAndPreservesEffectiveTimeAtTheNextExistingBoundary()
    {
        var f = Build();
        var (_, run) = EligibilityHistory(f);
        var emitted = run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 32));
        Assert.Equal(RuleEvaluationResult.Passed, emitted.Evaluations.Single(e => e.RuleId == Rule).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, emitted.Evaluations.Single(e => e.RuleId == EligibilityRule).Result);
        Assert.DoesNotContain(emitted.RuleFacts, f => f.RuleId == EligibilityRule);
        var next = run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33));
        var fact = Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(next.RuleFacts.Single(f => f.RuleId == EligibilityRule).Fact);
        Assert.Same(emitted.RuleFacts.Single(f => f.RuleId == Rule).Fact, fact.Reversal);
        Assert.Equal(At(13, 32), fact.EligibilityEffectiveAtUtc);
        Assert.Equal(At(13, 33), next.Evaluations.Single(e => e.RuleId == EligibilityRule).EvaluatedAtUtc);
        Assert.Throws<ArgumentException>(() => Context(emitted.AsOfUtc, []).WithPriorObservations([emitted]));
        Assert.Equal(StrategyVerdict.Ready, EligibilityAggregate(RuleEvaluationResult.Passed));
        Assert.Equal(StrategyVerdict.Wait, EligibilityAggregate(RuleEvaluationResult.Waiting));
        Assert.DoesNotContain(next.RuleFacts, f => f.Fact is NasdaqPreEntryEligibilityRuleFact);
        Assert.Equal(RuleEvaluationResult.Waiting, next.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result);
        Assert.False(next.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-SL-001").EstablishesProgression);
        Assert.DoesNotContain(new RuleId("NQ-M1-003"), next.WorkflowProgression.EstablishedRuleIds);
        Assert.DoesNotContain(MoneyWayReplayWorkflowDefinitions.GetAll().Single().RulePrerequisites,
            p => p.PrerequisiteRuleIds.Contains(EligibilityRule));
    }

    [Fact]
    public void EligibilityPropagatesUnavailableCanonicalReversalSourcesWithoutAnEligibilityFact()
    {
        var f = Build();
        var missing = LiquidityFixture.Candle(-4);
        var inputs = f.Inputs.Concat([Observation(f, NasdaqStrategyPath.WickfillScenario2), Reversal(f, sources: [missing])]).ToArray();
        var run = Run(inputs);
        var context = Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14)));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, LatestReversalResult(context));
        var decision = eligibilityEvaluator.Evaluate(context);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Theory]
    [InlineData("source:other initial take")]
    [InlineData("source:another setup")]
    public void EligibilityRejectsAReversalFromAnotherInitialTakeInTheSameSession(string eventId)
    {
        var (context, _) = EligibilityHistory(Build());
        var (other, _) = EligibilityHistory(Build(eventId: eventId));
        var owner = context.PriorObservations.Last();
        var foreign = other.PriorObservations.Last();
        var replacement = new StrategyReplayContextObservation(owner.StrategyId, owner.StrategyVersion, owner.ProviderId, owner.Symbol,
            owner.Step, owner.AsOfUtc, owner.Evaluations, owner.WorkflowProgression, owner.LifecycleProgression,
            ruleFacts: owner.RuleFacts.Where(f => f.RuleId != Rule).Concat(foreign.RuleFacts.Where(f => f.RuleId == Rule)));
        var malformed = context.WithPriorObservations(context.PriorObservations.Take(context.PriorObservations.Count - 1).Append(replacement));
        var decision = eligibilityEvaluator.Evaluate(malformed);
        Assert.Equal(RuleEvaluationResult.Waiting, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Fact]
    public void EligibilityCannotFallBackToAnOlderPassedReversalAfterNewConflictOrFutureEvidence()
    {
        var f = Build();
        var app = Observation(f, NasdaqStrategyPath.WickfillScenario2);
        var inputs = f.Inputs.Concat([app, Reversal(f), Reversal(f, eventId: "source:conflict", observed: At(13, 33))]).ToArray();
        var run = Run(inputs);
        var context = Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, LatestReversalResult(context));
        Assert.Equal(RuleEvaluationResult.Waiting, eligibilityEvaluator.Evaluate(context).Result);
        var future = Reversal(f, observed: At(14));
        var rawInputs = f.Inputs.Concat([app, future]).ToArray();
        var futureRun = Run(rawInputs);
        Assert.DoesNotContain(futureRun.StrategyObservations.Where(o => o.AsOfUtc <= At(14)).SelectMany(o => o.RuleFacts),
            fact => fact.RuleId == EligibilityRule);
    }

    [Fact]
    public void EligibilitySharesSameFrameAndPriorTerminalInvalidationProtection()
    {
        var f = Build(); var root = Take(f.Owner).Take;
        var upstream = Context(At(13, 5), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream,
            new MoneyWay.Application.Strategies.Nasdaq.Liquidity.NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "review:opposite");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "source:opposite", reference.ReferencePrice - 1, At(13, 33), "source:opposite"),
            upstream.AsOfUtc, At(13, 33), "review:opposite");
        var inputs = f.Inputs.Concat([Observation(f, NasdaqStrategyPath.WickfillScenario2), Reversal(f), opposite,
            new NasdaqHumanRelevantLiquidityTakeObservation(opposite, opposite.ObservedAtUtc, "review:opposite selected", root)]).ToArray();
        var run = Run(inputs);
        var terminal = run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33));
        Assert.Equal(RuleEvaluationResult.Failed, terminal.Evaluations.Single(e => e.RuleId == EligibilityRule).Result);
        Assert.DoesNotContain(terminal.RuleFacts, f => f.RuleId == EligibilityRule);
        Assert.True(Take(terminal).Blocks(Frame(f, At(14)), EligibilityRule));
        var context = Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14)));
        Assert.Equal(RuleEvaluationResult.Failed, eligibilityEvaluator.Evaluate(context).Result);
    }

    [Fact]
    public void EligibilityCannotReviveAnExpiredSetupOrMoveTheTimeCutoff()
    {
        var f = Build();
        var (context, run) = EligibilityHistory(f);
        var atCutoff = Context(At(16), context.InputObservations.ToArray()).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(16)));
        Assert.Equal(RuleEvaluationResult.Failed, eligibilityEvaluator.Evaluate(atCutoff).Result);
        Assert.Null(run.StrategyObservations.Single(o => o.AsOfUtc == At(16)).LifecycleProgression!.ActiveInstance);
        Assert.DoesNotContain(run.StrategyObservations.Where(o => o.AsOfUtc >= At(16)).SelectMany(o => o.RuleFacts), f => f.RuleId == EligibilityRule);
    }

    [Fact]
    public void EligibilityDoesNotReuseAReversalWithoutASurvivingLifecycleInstance()
    {
        var (context, _) = EligibilityHistory(Build());
        var owner = context.PriorObservations.Last();
        var ended = new StrategyReplayLifecycleSnapshot(owner.StrategyId, owner.StrategyVersion, owner.ProviderId, owner.Symbol,
            owner.Step, owner.AsOfUtc, [], []);
        var replacement = new StrategyReplayContextObservation(owner.StrategyId, owner.StrategyVersion, owner.ProviderId, owner.Symbol,
            owner.Step, owner.AsOfUtc, owner.Evaluations, owner.WorkflowProgression, ended, ruleFacts: owner.RuleFacts);
        var noActive = context.WithPriorObservations(context.PriorObservations.Take(context.PriorObservations.Count - 1).Append(replacement));
        var decision = eligibilityEvaluator.Evaluate(noActive);
        Assert.Equal(RuleEvaluationResult.Waiting, decision.Result);
        Assert.Null(decision.Fact);
    }

    [Fact]
    public void EligibilityRegistryMetadataGapAndStandardOutputsRemainTruthful()
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(registry, e => e.RuleId == EligibilityRule && e is MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator);
        var rule = LiquidityFixture.Definition.Rules.Single(r => r.RuleId == EligibilityRule);
        Assert.True(rule.IsRequired);
        Assert.Equal(100, rule.Sequence);
        Assert.Equal(RuleDefinitionStatus.Confirmed, rule.DefinitionStatus);
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(eligibilityEvaluator.StrategyId, eligibilityEvaluator.StrategyVersion)!;
        Assert.Equal((34, 13, 18, 0), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount, report.RequiredEvaluatorGapCount));
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, report.Rules.Single(r => r.RuleId == EligibilityRule).CapabilityStatus);
        var missing = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry.Where(e => e.RuleId != EligibilityRule),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(eligibilityEvaluator.StrategyId, eligibilityEvaluator.StrategyVersion)!;
        Assert.Equal(1, missing.RequiredEvaluatorGapCount);
        var (context, _) = EligibilityHistory(Build(), NasdaqStrategyPath.Standard);
        var without = new EvaluateStrategyReplayContextUseCase(registry.Where(e => e.RuleId != EligibilityRule)).Execute(LiquidityFixture.Definition, context);
        var with = new EvaluateStrategyReplayContextUseCase(registry).Execute(LiquidityFixture.Definition, context);
        Assert.Equal(without.Evaluations, with.Evaluations.Where(e => e.RuleId != EligibilityRule));
        Assert.Equal(JsonSerializer.Serialize(without.RuleFacts), JsonSerializer.Serialize(with.RuleFacts));
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Waiting, StrategyVerdict.Wait)]
    [InlineData(RuleEvaluationResult.DataUnavailable, StrategyVerdict.DataUnavailable)]
    [InlineData(RuleEvaluationResult.Failed, StrategyVerdict.NoTrade)]
    [InlineData(RuleEvaluationResult.NotApplicable, StrategyVerdict.Ready)]
    [InlineData(RuleEvaluationResult.Passed, StrategyVerdict.Ready)]
    public void EligibilityUsesGenericCanonicalOutcomesAndDiagnostics(RuleEvaluationResult result, StrategyVerdict verdict)
    {
        var rule = LiquidityFixture.Definition.Rules.Single(r => r.RuleId == EligibilityRule);
        var definition = new StrategyDefinition(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version,
            "Scoped eligibility projection", "source", [rule]);
        var evaluation = new RuleEvaluation(EligibilityRule, rule.DefinitionStatus, result, rule.Sequence, rule.IsRequired, "Test projection", At(14), null);
        var observation = new StrategyReplayContextObservation(definition.StrategyId, definition.Version,
            LiquidityFixture.Provider, LiquidityFixture.Symbol, 1, At(14), [evaluation]);
        var outcome = new EvaluateStrategyReplayContextOutcomeUseCase().Execute(definition, observation);
        Assert.True(outcome.HasCompleteRequiredCoverage);
        Assert.Equal(verdict, outcome.Verdict);
        var minute = new MoneyWay.Domain.MarketData.Timeframe(1, MoneyWay.Domain.MarketData.TimeframeUnit.Minute);
        var run = new MultiTimeframeStrategyBacktestRun(definition.StrategyId, definition.Version, LiquidityFixture.Provider, LiquidityFixture.Symbol,
            [minute], [new MultiTimeframeBacktestObservation(1, At(14), [minute], [minute])], [observation]);
        var report = new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase().Execute(definition, new(run, [outcome]));
        Assert.Equal(verdict, Assert.Single(report.Frames).Verdict);
        Assert.Empty(report.MissingRequiredRules);
    }

    private static StrategyVerdict EligibilityAggregate(RuleEvaluationResult result) => new SequentialStrategyEvaluator().Evaluate(
        [new RuleEvaluation(EligibilityRule, RuleDefinitionStatus.Confirmed, result, 100, true, "Scoped eligibility", At(14), null)]).Verdict;
}
