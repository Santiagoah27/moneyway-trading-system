using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Derives only pre-entry eligibility from the exact prior canonical realignment fact.</summary>
public sealed class MoneyWayNasdaqPreEntryEligibilityEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-M1-003");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-M1-002"));
        var realignment = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-002").Select(f => f.Fact)
            .OfType<NasdaqHumanM1RealignmentRuleFact>().SingleOrDefault();
        if (realignment is null || !realignment.Selection.Fact.Session.Matches(context)
            || latest!.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-002").Result != RuleEvaluationResult.Passed
            || latest.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-M1-002")?.EstablishesProgression != true)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-M1-002");

        var fact = new NasdaqPreEntryEligibilityRuleFact(realignment);
        return new(RuleEvaluationResult.Passed,
            "The exact canonical 1M realignment establishes bounded pre-entry eligibility; no entry or order is created.",
            JsonSerializer.Serialize(new
            {
                RuleId = RuleId.Value,
                RealignmentRuleId = "NQ-M1-002",
                fact.Session,
                fact.Direction,
                fact.EligibilityEffectiveAtUtc,
                RealignmentEffectiveAtUtc = realignment.Selection.Fact.RealignmentEffectiveAtUtc,
                RealignmentEvidence = realignment.Selection.SupportingObservations,
            }), fact);
    }
}
