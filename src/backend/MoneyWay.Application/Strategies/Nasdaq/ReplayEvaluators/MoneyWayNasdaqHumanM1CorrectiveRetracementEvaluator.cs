using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM1CorrectiveRetracementEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-M1-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-M5-001"));
        var trigger = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M5-001").Select(f => f.Fact)
            .OfType<NasdaqHumanM5TriggerRuleFact>().SingleOrDefault();
        if (trigger is null || !NasdaqHumanM5FvgObservationSelector.IsEstablished(context, trigger))
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-M5-001");
        var confirmation = NasdaqM1FvgSupportResolver.Resolve(context, trigger);
        if (confirmation.Blocker is { } result)
            return new(result, confirmation.Reason, JsonSerializer.Serialize(new { Trigger = trigger, FvgSupport = confirmation }));
        var quality = confirmation.ApprovedQuality;
        var selection = new NasdaqHumanM1CorrectiveRetracementObservationSelector().Select(context, trigger);
        var support = selection switch
        {
            NasdaqHumanM1CorrectiveRetracementSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanM1CorrectiveRetracementSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanM1CorrectiveRetracementObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            Trigger = trigger,
            ApprovedFvg = quality,
            PullbackEvidence = support,
            UnavailableSourceEvidence = selection.UnavailableSourceObservations,
        });
        if (selection is NasdaqHumanM1CorrectiveRetracementSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired,
                "Selected corrective retracement events conflict; no ranking is defined.", proof);
        if (selection is NasdaqHumanM1CorrectiveRetracementSelection.Unique selected)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable,
                    "A competing selected pullback has unavailable exact 1M sources.", proof);
            return new(RuleEvaluationResult.Passed,
                "The source-backed 1M corrective retracement is established for the exact canonical M5 trigger, retaining optional FVG confirmation.", proof,
                new NasdaqHumanM1CorrectiveRetracementRuleFact(trigger,
                    quality is not null && quality.Selection.Fact.EffectiveAtUtc <= selected.Fact.PullbackEffectiveAtUtc
                        && quality.Selection.Fact.ObservedAtUtc <= selected.Fact.ObservedAtUtc ? quality : null, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human corrective retracement is available for the exact canonical M5 trigger, retaining optional FVG confirmation.", proof);
    }
}
