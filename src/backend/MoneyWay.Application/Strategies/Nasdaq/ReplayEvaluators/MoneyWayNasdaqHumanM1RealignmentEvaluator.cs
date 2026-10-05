using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM1RealignmentEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-M1-002");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-M1-001"));
        var pullback = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-001").Select(f => f.Fact)
            .OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().SingleOrDefault();
        if (pullback is null || !pullback.Selection.Fact.Session.Matches(context)
            || latest!.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").Result != RuleEvaluationResult.Passed
            || latest.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-M1-001")?.EstablishesProgression != true)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-M1-001");

        var selection = new NasdaqHumanM1RealignmentObservationSelector().Select(context, pullback);
        var support = selection switch
        {
            NasdaqHumanM1RealignmentSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanM1RealignmentSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanM1RealignmentObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            Pullback = pullback,
            RealignmentEvidence = support,
            UnavailableSourceEvidence = selection.UnavailableSourceObservations,
        });
        if (selection is NasdaqHumanM1RealignmentSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired,
                "Selected realignment events conflict; no ranking is defined.", proof);
        if (selection is NasdaqHumanM1RealignmentSelection.Unique selected)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable,
                    "A competing selected realignment has unavailable exact 1M sources.", proof);
            return new(RuleEvaluationResult.Passed,
                "The source-backed 1M realignment is established for the exact corrective retracement.", proof,
                new NasdaqHumanM1RealignmentRuleFact(pullback, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human realignment is available for the exact corrective retracement.", proof);
    }
}
