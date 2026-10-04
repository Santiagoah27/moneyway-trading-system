using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM5FvgQualityEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-FVG-002");
    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;
        var candidate = context.PriorObservations.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-FVG-001")
            .Select(f => f.Fact).OfType<NasdaqHumanM5FvgRuleFact>().LastOrDefault(f => f.Selection.Fact.Session.Matches(context));
        if (candidate is null || NasdaqFvgReplayPrerequisites.Latest(context, "NQ-FVG-001")?.Result != RuleEvaluationResult.Passed
            || !NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-FVG-001"))
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-FVG-001");
        var rejections = NasdaqHumanM5FvgCandidateHistory.Rejections(context, candidate.Trigger);
        var current = NasdaqHumanM5FvgCandidateHistory.Current(context, candidate.Trigger, rejections);
        if (current is not null && !current.Selection.Fact.SameFact(candidate.Selection.Fact))
            candidate = current; // A contradictory late review reopens the exact earlier candidate; it cannot be skipped.
        var selection = new NasdaqHumanM5FvgQualityObservationSelector().SelectForCandidate(context, candidate.Trigger, candidate.Selection);
        var supporting = selection switch
        {
            NasdaqHumanM5FvgQualitySelection.Unique u => u.SupportingObservations,
            NasdaqHumanM5FvgQualitySelection.Conflict c => c.SupportingObservations,
            _ => Array.Empty<NasdaqHumanM5FvgQualityObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            Candidate = candidate,
            QualityEvidence = supporting.Select(o => new
            {
                Decision = o.Fact.Decision.ToString(),
                QualityEffectiveAtUtc = o.EffectiveAtUtc,
                Observation = o
            }),
            UnavailableSourceEvidence = selection.UnavailableSourceObservations
        });
        if (selection is NasdaqHumanM5FvgQualitySelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "Exact candidate quality reviews conflict; no winner is defined.", proof);
        if (selection is NasdaqHumanM5FvgQualitySelection.Unique unique)
            return new(unique.Fact.Fact.Decision == NasdaqHumanM5FvgQualityDecision.Approved ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting,
                "Human quality review applies only to this exact candidate; rejection waits without terminating the setup.",
                proof, new NasdaqHumanM5FvgQualityRuleFact(candidate, unique));
        return new(selection.UnavailableSourceObservations.Count > 0 ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "The exact candidate has no usable human quality review.", proof);
    }
}
