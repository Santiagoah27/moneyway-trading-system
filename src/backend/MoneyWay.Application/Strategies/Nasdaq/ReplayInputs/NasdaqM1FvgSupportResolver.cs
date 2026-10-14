using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Distinguishes missing optional confirmation from unsafe selected evidence; discovers no FVG.</summary>
internal static class NasdaqM1FvgSupportResolver
{
    internal sealed record Resolution(NasdaqHumanM5FvgQualityRuleFact? ApprovedQuality,
        RuleEvaluationResult? Blocker, string Reason);

    internal static Resolution Resolve(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger)
    {
        var selection = new NasdaqHumanM5FvgObservationSelector().Select(context, trigger);
        if (selection is NasdaqHumanM5FvgSelection.Conflict)
            return new(null, RuleEvaluationResult.HumanValidationRequired, "Optional FVG selections conflict; conflict is not absence.");
        if (selection.UnavailableSourceObservations.Count > 0)
            return new(null, RuleEvaluationResult.DataUnavailable, "Selected optional FVG sources are unavailable.");
        if (selection is not NasdaqHumanM5FvgSelection.Unique unique)
        {
            var claims = context.InputObservations.OfType<NasdaqHumanM5FvgObservation>()
                .Any(o => o.ObservedAtUtc <= context.AsOfUtc && o.Trigger.SameFact(trigger.Selection.Fact));
            var reviews = context.InputObservations.OfType<NasdaqHumanM5FvgQualityObservation>()
                .Where(o => o.ObservedAtUtc <= context.AsOfUtc && o.Fact.Fvg.Trigger.SameFact(trigger.Selection.Fact)).ToArray();
            if (NasdaqHumanM5FvgCandidateHistory.Rejections(context, trigger).Count > 0
                || reviews.Any(o => o.Fact.Decision == NasdaqHumanM5FvgQualityDecision.Rejected))
                return new(null, RuleEvaluationResult.Waiting, "Rejected FVG evidence cannot use the clean-absence path.");
            return claims || reviews.Length > 0
                ? new(null, RuleEvaluationResult.HumanValidationRequired, "Unusable selected FVG evidence is not clean absence.")
                : new(null, null, "No optional FVG confirmation is selected.");
        }
        var quality = new NasdaqHumanM5FvgQualityObservationSelector().SelectForCandidate(context, trigger, unique);
        if (quality is NasdaqHumanM5FvgQualitySelection.Conflict)
            return new(null, RuleEvaluationResult.HumanValidationRequired, "Optional FVG quality conflicts; no winner is defined.");
        if (quality.UnavailableSourceObservations.Count > 0)
            return new(null, RuleEvaluationResult.DataUnavailable, "Optional FVG quality sources are unavailable.");
        if (quality is not NasdaqHumanM5FvgQualitySelection.Unique approved)
        {
            if (NasdaqHumanM5FvgCandidateHistory.Rejections(context, trigger).Count > 0)
                return new(null, RuleEvaluationResult.Waiting, "A replacement after rejection still awaits its own valid review.");
            var claims = context.InputObservations.OfType<NasdaqHumanM5FvgQualityObservation>()
                .Any(o => o.ObservedAtUtc <= context.AsOfUtc && o.Fact.Fvg.SameFact(unique.Fact));
            return claims ? new(null, RuleEvaluationResult.HumanValidationRequired, "Unusable FVG review is not clean absence.")
                : new(null, null, "No optional FVG quality confirmation is available.");
        }
        if (approved.Fact.Fact.Decision == NasdaqHumanM5FvgQualityDecision.Rejected)
            return new(null, RuleEvaluationResult.Waiting, "Explicit FVG rejection retains candidate-local waiting.");
        var owner = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-FVG-002"));
        var fact = owner?.RuleFacts.Where(f => f.RuleId.Value == "NQ-FVG-002").Select(f => f.Fact)
            .OfType<NasdaqHumanM5FvgQualityRuleFact>().SingleOrDefault();
        if (owner?.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").Result != RuleEvaluationResult.Passed
            || owner.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-FVG-002")?.EstablishesProgression != true
            || fact is null || !fact.Candidate.Selection.Fact.SameFact(unique.Fact)
            || !fact.Selection.Fact.Fact.SameFact(approved.Fact.Fact))
            return new(null, RuleEvaluationResult.Waiting, "Visible approved FVG support awaits its canonical owning fact.");
        return new(fact, null, "Canonical approved FVG confirmation is retained as optional support.");
    }
}
