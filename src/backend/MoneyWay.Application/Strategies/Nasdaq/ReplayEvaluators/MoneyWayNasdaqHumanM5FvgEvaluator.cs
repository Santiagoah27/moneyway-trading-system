using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM5FvgEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-FVG-001");
    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;
        var trigger = context.PriorObservations.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-M5-001")
            .Select(f => f.Fact).OfType<NasdaqHumanM5TriggerRuleFact>().LastOrDefault(f => f.Selection.Fact.Session.Matches(context));
        if (trigger is null || !NasdaqHumanM5FvgObservationSelector.IsEstablished(context, trigger))
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-M5-001");
        var selection = new NasdaqHumanM5FvgObservationSelector().Select(context, trigger);
        var supporting = selection switch
        {
            NasdaqHumanM5FvgSelection.Unique u => u.SupportingObservations,
            NasdaqHumanM5FvgSelection.Conflict c => c.SupportingObservations,
            _ => Array.Empty<NasdaqHumanM5FvgObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            Session = trigger.Selection.Fact.Session,
            Trigger = trigger.Selection,
            FvgEvidence = supporting,
            UnavailableSourceEvidence = selection.UnavailableSourceObservations,
            RejectedCandidates = NasdaqHumanM5FvgCandidateHistory.Rejections(context, trigger)
        });
        if (selection is NasdaqHumanM5FvgSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "Mandatory FVG selections conflict; no ranking is defined.", proof);
        if (selection is NasdaqHumanM5FvgSelection.Unique unique)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(unique.Fact)))
                return new(RuleEvaluationResult.DataUnavailable, "A competing current selection has unavailable required sources.", proof);
            return new(RuleEvaluationResult.Passed, "The exact human-selected mandatory FVG exists; quality remains independent.",
                proof, new NasdaqHumanM5FvgRuleFact(trigger, unique));
        }
        return new(selection.UnavailableSourceObservations.Count > 0 ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable mandatory FVG selection is available for the eligible candidate slot.", proof);
    }
}
