using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Derives scoped eligibility from prior canonical reversal only; no downstream branch integration.</summary>
public sealed class MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-WF2-002");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;
        var active = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression?.ActiveInstance;
        var predecessor = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001"));
        if (active is null || predecessor is null || predecessor.AsOfUtc < active.StartedAtUtc
            || predecessor.LifecycleProgression?.ActiveInstance?.InstanceId != active.InstanceId)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-WF2-001");
        var evaluation = predecessor.Evaluations.Single(e => e.RuleId.Value == "NQ-WF2-001");
        // NotApplicable is the upstream canonical explicit Standard selection, never an inferred default.
        if (evaluation.Result == RuleEvaluationResult.NotApplicable)
            return new(RuleEvaluationResult.NotApplicable, "The prior canonical reversal rule classified this exact setup Standard.", evaluation.EvidenceReference);
        if (evaluation.Result != RuleEvaluationResult.Passed)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-WF2-001");

        NasdaqWickfillScenario2PreEntryEligibilityRuleFact fact;
        try { fact = NasdaqWickfillScenario2PreEntryEligibilityRuleFact.Create(context, predecessor); }
        catch (ArgumentException) { return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-WF2-001"); }
        return new(RuleEvaluationResult.Passed,
            "The exact canonical Scenario 2 SELL reversal establishes scoped pre-entry eligibility only; no execution or downstream readiness is established.",
            JsonSerializer.Serialize(new
            {
                RuleId = RuleId.Value,
                ReversalRuleId = "NQ-WF2-001",
                fact.Session,
                fact.Direction,
                fact.EligibilityEffectiveAtUtc,
                ReversalEvaluatedAtUtc = predecessor.AsOfUtc,
                ReversalEvidence = evaluation.EvidenceReference,
            }), fact);
    }
}
