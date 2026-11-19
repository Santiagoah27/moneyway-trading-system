using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Checks documented planned loss against the audited maximum, without deriving size or execution.</summary>
public sealed class MoneyWayNasdaqRiskExposureEvaluator : IReplayRuleEvaluator
{
    private readonly StrategyVersion boundVersion;
    public MoneyWayNasdaqRiskExposureEvaluator() : this(MoneyWayNasdaqStrategyDefinition.Instance.Version) { }
    internal MoneyWayNasdaqRiskExposureEvaluator(StrategyVersion version) { boundVersion = version; }

    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => boundVersion;
    public RuleId RuleId { get; } = new("NQ-RISK-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.CheckVersion(context, new RuleId("NQ-M1-003"), StrategyVersion);
        if (blocked is not null) return blocked;
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        if (latest?.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result == RuleEvaluationResult.Passed
            && NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-SL-001")
            && latest.RuleFacts.SingleOrDefault(f => f.RuleId.Value == "NQ-SL-001")?.Fact is NasdaqHumanWickfillScenario2StopLossRuleFact wf2
            && wf2.Session.Matches(context))
            return EvaluateScenario2(context, wf2);
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

    private static ReplayRuleEvaluationDecision EvaluateScenario2(StrategyReplayContext context, NasdaqHumanWickfillScenario2StopLossRuleFact stop)
    {
        var selection = new NasdaqWickfillScenario2RiskExposureObservationSelector().Select(context, stop);
        NasdaqWickfillScenario2RiskExposureRuleFact? fact = selection.IsUnique && selection.UnavailableSourceObservations.Count == 0 ? new(selection) : null;
        object Evidence(NasdaqWickfillScenario2RiskExposureObservation o) => new
        {
            o.Session,
            o.Exposure,
            o.EffectiveAtUtc,
            o.ObservedAtUtc,
            o.SourceReference,
            StopOwnerStep = o.StopOwner.Step,
            StopOwnerAsOfUtc = o.StopOwner.AsOfUtc,
            o.StopLoss.StopPrice,
            ProtectiveReference = o.StopLoss.Selection.Fact.ProtectiveReference
        };
        var proof = JsonSerializer.Serialize(new
        {
            Path = "WickfillScenario2",
            SlFactReceived = true,
            RiskExposureEvidence = selection.SupportingObservations.Select(Evidence),
            UnavailableSourceEvidence = selection.UnavailableSourceObservations.Select(Evidence),
            fact?.RiskRatio,
            fact?.RatioExceedsDecimalRange,
            Limit = NasdaqRiskExposureRuleFact.MaximumRiskRatio,
            RequiredInputs = new[] { "Documented positive account amount and named basis", "Same-currency documented planned maximum loss",
                "Native planned entry price", "Explicit cost treatment and calculation provenance", "Original assessment/availability times", "Quantity/unit only when supplied" }
        });
        if (selection.IsConflict) return new(RuleEvaluationResult.HumanValidationRequired, "Scenario 2 documented risk exposures conflict; no source or currency is ranked.", proof);
        if (selection.UnavailableSourceObservations.Count > 0)
            return new(RuleEvaluationResult.DataUnavailable, "Scenario 2 risk cannot resolve its exact current native SL sources/ancestry.", proof);
        if (fact is null) return new(RuleEvaluationResult.HumanValidationRequired,
            "Canonical Scenario 2 SL received; native documented account basis/currency, planned maximum loss, planned entry, cost treatment, calculation provenance and original risk availability are required. No sizing or exposure is inferred.", proof);
        return new(fact.IsWithinLimit ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed,
            fact.IsWithinLimit ? "Scenario 2 documented planned maximum loss is at most 1% of the documented account basis."
                : "Scenario 2 documented planned maximum loss exceeds 1% of the documented account basis.", proof, fact);
    }
}
