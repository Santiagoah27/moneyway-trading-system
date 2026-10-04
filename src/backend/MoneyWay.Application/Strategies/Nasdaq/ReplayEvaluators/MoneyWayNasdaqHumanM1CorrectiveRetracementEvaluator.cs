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

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-FVG-002"));
        var quality = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-FVG-002").Select(f => f.Fact)
            .OfType<NasdaqHumanM5FvgQualityRuleFact>().SingleOrDefault();
        if (quality is null || quality.Selection.Fact.Fact.Decision != NasdaqHumanM5FvgQualityDecision.Approved
            || !quality.Selection.Fact.Session.Matches(context)
            || NasdaqFvgReplayPrerequisites.Latest(context, "NQ-FVG-001")?.Result != RuleEvaluationResult.Passed
            || !NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-FVG-001")
            || NasdaqFvgReplayPrerequisites.Latest(context, "NQ-FVG-002")?.Result != RuleEvaluationResult.Passed
            || !NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-FVG-002"))
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-FVG-002");

        var selection = new NasdaqHumanM1CorrectiveRetracementObservationSelector().Select(context, quality);
        var support = selection switch
        {
            NasdaqHumanM1CorrectiveRetracementSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanM1CorrectiveRetracementSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanM1CorrectiveRetracementObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
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
                "The source-backed 1M corrective retracement is established for the exact approved FVG.", proof,
                new NasdaqHumanM1CorrectiveRetracementRuleFact(quality, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human corrective retracement is available for the exact approved FVG.", proof);
    }
}
