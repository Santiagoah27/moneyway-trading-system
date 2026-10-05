using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Evaluates only documented structural stop evidence for an established pre-entry setup.</summary>
public sealed class MoneyWayNasdaqHumanStructuralStopLossEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-SL-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;

        var established = context.PriorObservations.LastOrDefault(o => o.WorkflowProgression?.RuleEligibility
            .SingleOrDefault(e => e.RuleId.Value == "NQ-M1-003")?.EstablishesProgression == true);
        var preEntry = established?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().SingleOrDefault();
        if (preEntry is null || !preEntry.Session.Matches(context)
            || established!.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-003").Result != RuleEvaluationResult.Passed)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-M1-003");

        var selection = new NasdaqHumanStructuralStopLossObservationSelector().Select(context, preEntry);
        var support = selection switch
        {
            NasdaqHumanStructuralStopLossSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanStructuralStopLossSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanStructuralStopLossObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            PreEntryEligibility = preEntry,
            StructuralStopLossEvidence = support,
            UnavailableSourceEvidence = selection.UnavailableSourceObservations,
        });
        if (selection is NasdaqHumanStructuralStopLossSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired,
                "Selected structural Stop Loss observations conflict; no anchor or price ranking is defined.", proof);
        if (selection is NasdaqHumanStructuralStopLossSelection.Unique selected)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable,
                    "A competing selected structural Stop Loss has unavailable exact 5M sources.", proof);
            return new(RuleEvaluationResult.Passed,
                "The documented 5M structural protection anchor and distinct Stop Price are established for the exact pre-entry setup.",
                proof, new NasdaqHumanStructuralStopLossRuleFact(preEntry, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human structural Stop Loss is available for the exact pre-entry setup.", proof);
    }
}
