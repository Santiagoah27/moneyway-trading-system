using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed partial class NasdaqHumanWickfillScenario2ReversalEvaluatorTests
{
    public static IEnumerable<object[]> StandardRules() => new[] { "NQ-M5-001", "NQ-M1-001", "NQ-M1-002", "NQ-M1-003" }
        .Select(id => new object[] { id });
    private static IReplayRuleEvaluator StandardEvaluator(string id) => MoneyWayReplayRuleEvaluators.GetAll().Single(e => e.RuleId.Value == id);

    [Theory]
    [MemberData(nameof(StandardRules))]
    public void StandardRulesExcludeOnlyTheExactScenario2SetupWithoutStandardFacts(string id)
    {
        var f = Build();
        var inputs = f.Inputs.Concat([Observation(f, NasdaqStrategyPath.WickfillScenario2), Reversal(f)]).ToArray();
        var run = Run(inputs);
        var after = run.StrategyObservations.Single(o => o.AsOfUtc == At(14));
        var result = after.Evaluations.Single(e => e.RuleId.Value == id);
        Assert.Equal(RuleEvaluationResult.NotApplicable, result.Result);
        Assert.DoesNotContain(after.RuleFacts, fact => fact.RuleId.Value == id);
        Assert.DoesNotContain(new RuleId(id), after.WorkflowProgression!.EstablishedRuleIds);
        Assert.False(after.WorkflowProgression.RuleEligibility.Single(e => e.RuleId.Value == id).EstablishesProgression);
        Assert.Equal(RuleEvaluationResult.Waiting, after.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result);
        Assert.False(after.WorkflowProgression.RuleEligibility.Single(e => e.RuleId.Value == "NQ-SL-001").EstablishesProgression);
        var wf = Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(after.RuleFacts.Single(fact => fact.RuleId == EligibilityRule).Fact);
        Assert.Equal(At(13, 32), wf.EligibilityEffectiveAtUtc);
        Assert.Equal(RuleEvaluationResult.Waiting, after.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-001").Result);
        Assert.Equal(RuleEvaluationResult.Waiting, after.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").Result);
        var aggregate = new SequentialStrategyEvaluator().Evaluate(after.Evaluations);
        Assert.NotEqual(StrategyVerdict.Ready, aggregate.Verdict);
        Assert.Equal(new RuleId("NQ-SL-001"), aggregate.BlockingRuleId);
        var direct = StandardEvaluator(id).Evaluate(Frame(f, At(14), Observation(f, NasdaqStrategyPath.WickfillScenario2)));
        Assert.Equal(RuleEvaluationResult.NotApplicable, direct.Result);
        Assert.Null(direct.Fact);
        var wfOnly = new MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator().Evaluate(
            Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14))));
        Assert.Equal(JsonSerializer.Serialize(wf.Reversal), JsonSerializer.Serialize(Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(wfOnly.Fact).Reversal));
    }

    [Theory]
    [MemberData(nameof(StandardRules))]
    public void StandardRulesRequireExplicitUnambiguousClassificationAndExcludeForeignOrFutureReviews(string id)
    {
        var f = Build(); var evaluator = StandardEvaluator(id);
        var missing = evaluator.Evaluate(Frame(f, At(14)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, missing.Result);
        Assert.Null(missing.Fact);
        var standard = Observation(f); var scenario2 = Observation(f, NasdaqStrategyPath.WickfillScenario2);
        var forward = evaluator.Evaluate(Frame(f, At(14), standard, scenario2));
        var reverse = evaluator.Evaluate(Frame(f, At(14), scenario2, standard));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, forward.Result);
        Assert.Equal(forward.EvidenceReference, reverse.EvidenceReference);
        Assert.Contains("Alternatives", forward.EvidenceReference);
        Assert.Null(forward.Fact);
        var late = Observation(f, NasdaqStrategyPath.WickfillScenario2, observed: At(14));
        var earlier = Frame(f, At(13, 33), late);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(earlier).Result);
        Assert.Equal(RuleEvaluationResult.NotApplicable, evaluator.Evaluate(Frame(f, At(14), late)).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(earlier).Result);
        foreach (var other in new[] { Build(eventId: "other:take"), Build(endpoint: NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh), Build(direction: NasdaqHumanH4PermittedDirection.Buy, includeContext: false) })
        {
            Assert.Equal(f.Owner.LifecycleProgression!.ActiveInstance!.InstanceId, other.Owner.LifecycleProgression!.ActiveInstance!.InstanceId);
            var review = Observation(other);
            Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Frame(f, At(14), review)).Result);
        }
        Assert.Equal(id == "NQ-M5-001" ? RuleEvaluationResult.HumanValidationRequired : RuleEvaluationResult.Waiting,
            evaluator.Evaluate(Frame(f, At(14), standard)).Result);
    }

    [Theory]
    [MemberData(nameof(StandardRules))]
    public void StandardApplicabilityCannotInventAnActiveSetupOrReviveAnExpiredOne(string id)
    {
        var f = Build(); var evaluator = StandardEvaluator(id);
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(Context(At(14), [Observation(f)])).Result);
        var inputs = f.Inputs.Concat([Observation(f, NasdaqStrategyPath.WickfillScenario2)]).ToArray();
        var run = Run(inputs);
        var expired = Context(At(17), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(17)));
        var result = evaluator.Evaluate(expired);
        Assert.NotEqual(RuleEvaluationResult.NotApplicable, result.Result);
        Assert.Null(result.Fact);
        var cutoff = Context(At(16), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(16)));
        Assert.Equal(id == "NQ-M5-001" ? RuleEvaluationResult.HumanValidationRequired : RuleEvaluationResult.Failed,
            evaluator.Evaluate(cutoff).Result);
    }

    [Fact]
    public void LateBranchReviewCannotRewriteEarlierCanonicalObservations()
    {
        var f = Build(); var late = Observation(f, NasdaqStrategyPath.WickfillScenario2, observed: At(14));
        var baseline = Run(f.Inputs); var extended = Run(f.Inputs.Concat([late]).ToArray());
        foreach (var pair in baseline.StrategyObservations.Where(o => o.AsOfUtc < At(14)).Zip(extended.StrategyObservations))
            Assert.Equal(JsonSerializer.Serialize(pair.First), JsonSerializer.Serialize(pair.Second));
        var atReview = extended.StrategyObservations.Single(o => o.AsOfUtc == At(14));
        Assert.All(atReview.Evaluations.Where(e => StandardRules().Any(row => (string)row[0] == e.RuleId.Value)), e => Assert.Equal(RuleEvaluationResult.NotApplicable, e.Result));
    }
    [Theory]
    [MemberData(nameof(StandardRules))]
    public void TerminalLiquidityDominatesScenario2ExclusionInTheSameFrameAndAfterCancellation(string id)
    {
        var f = Build(); var root = Take(f.Owner).Take;
        var upstream = Context(At(13, 5), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream,
            new MoneyWay.Application.Strategies.Nasdaq.Liquidity.NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "review:opposite");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "source:opposite", reference.ReferencePrice - 1, At(13, 33), "source:opposite"),
            upstream.AsOfUtc, At(13, 33), "review:opposite");
        var inputs = f.Inputs.Concat([Observation(f, NasdaqStrategyPath.WickfillScenario2), opposite,
            new NasdaqHumanRelevantLiquidityTakeObservation(opposite, opposite.ObservedAtUtc, "review:opposite-selected", root)]).ToArray();
        var run = Run(inputs);
        var terminal = run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33));
        Assert.Equal(RuleEvaluationResult.Failed, terminal.Evaluations.Single(e => e.RuleId.Value == id).Result);
        Assert.DoesNotContain(terminal.RuleFacts, fact => fact.RuleId.Value == id);
        Assert.Null(terminal.LifecycleProgression!.ActiveInstance);
        var later = Context(At(14), inputs).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14)));
        Assert.Equal(RuleEvaluationResult.Failed, StandardEvaluator(id).Evaluate(later).Result);
        Assert.Null(StandardEvaluator(id).Evaluate(later).Fact);
    }

    [Theory]
    [MemberData(nameof(StandardRules))]
    public void ExplicitStandardPreservesUnavailablePrerequisiteMapping(string id)
    {
        var upstream = id switch
        {
            "NQ-M5-001" => "NQ-LIQ-003",
            "NQ-M1-001" => "NQ-M5-001",
            "NQ-M1-002" => "NQ-M1-001",
            _ => "NQ-M1-002",
        };
        var f = Build(); var context = Frame(f, At(14), Observation(f));
        var last = context.PriorObservations.Last();
        var evaluations = last.Evaluations.Select(e => e.RuleId.Value == upstream
            ? new RuleEvaluation(e.RuleId, e.DefinitionStatus, RuleEvaluationResult.DataUnavailable, e.Sequence,
                e.IsRequired, "Missing exact upstream sources", e.EvaluatedAtUtc, "source:unavailable") : e);
        var previous = last.WorkflowProgression!;
        var progression = new StrategyReplayProgressionSnapshot(last.StrategyId, last.StrategyVersion, last.ProviderId, last.Symbol,
            last.Step, last.AsOfUtc, previous.RuleEligibility.Select(e => e.RuleId.Value == upstream
                ? new StrategyReplayRuleEligibility(e.RuleId, e.AsOfUtc, e.IsEligible, e.PrerequisiteRuleIds, e.MissingPrerequisiteRuleIds, false) : e),
            previous.EstablishedRuleIds);
        var replacement = new StrategyReplayContextObservation(last.StrategyId, last.StrategyVersion, last.ProviderId, last.Symbol,
            last.Step, last.AsOfUtc, evaluations, progression, last.LifecycleProgression,
            ruleFacts: last.RuleFacts.Where(fact => fact.RuleId.Value != upstream));
        var unavailable = context.WithPriorObservations(context.PriorObservations.Take(context.PriorObservations.Count - 1).Append(replacement));
        var decision = StandardEvaluator(id).Evaluate(unavailable);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, decision.Result);
        Assert.Null(decision.Fact);
        Assert.Contains("source:unavailable", decision.EvidenceReference);
    }

    [Theory]
    [MemberData(nameof(StandardRules))]
    public void LaterSameSideTakeCannotReplaceTheInitialApplicabilityOwner(string id)
    {
        var f = Build(extension: true);
        Assert.NotSame(Take(f.Owner).Take, Take(f.Run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33))).Take);
        var decision = StandardEvaluator(id).Evaluate(Frame(f, At(14), Observation(f, NasdaqStrategyPath.WickfillScenario2)));
        Assert.Equal(RuleEvaluationResult.NotApplicable, decision.Result);
        Assert.Null(decision.Fact);
    }

}
