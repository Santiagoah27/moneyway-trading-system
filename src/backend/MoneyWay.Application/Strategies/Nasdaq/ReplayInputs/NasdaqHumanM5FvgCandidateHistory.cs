using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Replay-local candidate transitions from owning-rule prior facts, never from favorable input quality.</summary>
internal static class NasdaqHumanM5FvgCandidateHistory
{
    internal static IReadOnlyList<NasdaqHumanM5FvgQualityRuleFact> Rejections(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger)
    {
        var rejected = new List<NasdaqHumanM5FvgQualityRuleFact>();
        foreach (var observation in context.PriorObservations)
        {
            var fact = observation.RuleFacts.Where(f => f.RuleId.Value == "NQ-FVG-002").Select(f => f.Fact)
                .OfType<NasdaqHumanM5FvgQualityRuleFact>().SingleOrDefault();
            if (fact is null || !fact.Candidate.Trigger.Selection.Fact.SameFact(trigger.Selection.Fact)
                || fact.Selection.Fact.Fact.Decision != NasdaqHumanM5FvgQualityDecision.Rejected
                || !observation.Evaluations.Any(e => e.RuleId.Value == "NQ-FVG-002" && e.Result == RuleEvaluationResult.Waiting)
                || observation.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-FVG-002")?.IsEligible != true)
                continue;
            // A newly visible contradictory review reopens the exact candidate, never permits skipping it.
            if (context.InputObservations.OfType<NasdaqHumanM5FvgQualityObservation>().Any(o =>
                o.Fact.Fvg.SameFact(fact.Candidate.Selection.Fact) && !o.Fact.SameFact(fact.Selection.Fact.Fact))) break;
            if (!rejected.Any(f => f.Candidate.Selection.Fact.SameFact(fact.Candidate.Selection.Fact))) rejected.Add(fact);
        }
        return rejected.AsReadOnly();
    }
    internal static NasdaqHumanM5FvgRuleFact? Current(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger,
        IReadOnlyList<NasdaqHumanM5FvgQualityRuleFact> rejected)
    {
        var facts = context.PriorObservations.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-FVG-001")
            .Select(f => f.Fact).OfType<NasdaqHumanM5FvgRuleFact>()
            .Where(f => f.Trigger.Selection.Fact.SameFact(trigger.Selection.Fact)).ToArray();
        // The first unresolved established candidate owns the slot; input timestamps cannot choose a winner.
        return facts.FirstOrDefault(f => !rejected.Any(x => x.Candidate.Selection.Fact.SameFact(f.Selection.Fact)));
    }
}
