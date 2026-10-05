using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Checks documented planned loss against the audited maximum, without deriving size or execution.</summary>
public sealed class MoneyWayNasdaqRiskExposureEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-RISK-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, new RuleId("NQ-M1-003"));
        if (blocked is not null) return blocked;
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        var stop = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().SingleOrDefault();
        if (stop is null || latest!.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result != RuleEvaluationResult.Passed
            || !NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-SL-001"))
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-SL-001");

        var selection = new NasdaqRiskExposureObservationSelector().Select(context, stop.PreEntryEligibility, stop);
        var support = selection switch
        {
            NasdaqRiskExposureSelection.Unique unique => unique.SupportingObservations,
            NasdaqRiskExposureSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqRiskExposureObservation>(),
        };
        NasdaqRiskExposureRuleFact? fact = selection is NasdaqRiskExposureSelection.Unique selected
            && !selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact))
            ? new(selected) : null;
        var proof = JsonSerializer.Serialize(new
        {
            PreEntryEligibility = stop.PreEntryEligibility,
            StopLoss = stop,
            RiskExposureEvidence = support,
            UnavailableSourceEvidence = selection.UnavailableSourceObservations,
            RiskRatio = fact?.RiskRatio,
            RatioExceedsDecimalRange = fact?.RatioExceedsDecimalRange,
            Limit = NasdaqRiskExposureRuleFact.MaximumRiskRatio,
        });
        if (selection is NasdaqRiskExposureSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "Documented risk exposures conflict; no source priority is defined.", proof);
        if (fact is not null)
            return new(fact.IsWithinLimit ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed,
                fact.IsWithinLimit ? "Documented planned maximum loss is at most 1% of the documented account basis."
                    : "Documented planned maximum loss exceeds 1% of the documented account basis.", proof, fact);
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable documented risk exposure is available for the exact eligible setup and selected SL.", proof);
    }
}
