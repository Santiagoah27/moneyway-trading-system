using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects only source-backed human pullback evidence for the current approved candidate.</summary>
public sealed class NasdaqHumanM1CorrectiveRetracementObservationSelector
{
    public NasdaqHumanM1CorrectiveRetracementSelection Select(StrategyReplayContext context,
        NasdaqHumanM5FvgQualityRuleFact approvedQuality)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(approvedQuality);
        var quality = approvedQuality.Selection.Fact;
        if (quality.Fact.Decision != NasdaqHumanM5FvgQualityDecision.Approved
            || !quality.Session.Matches(context) || quality.ObservedAtUtc > context.AsOfUtc
            || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc)
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-M1-001")))
            || context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression is { ActiveInstance: null })
            return new NasdaqHumanM1CorrectiveRetracementSelection.Missing([]);

        var latestQuality = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-FVG-002"));
        if (latestQuality?.Evaluations.Single(e => e.RuleId.Value == "NQ-FVG-002").Result != RuleEvaluationResult.Passed
            || latestQuality.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-FVG-002")?.EstablishesProgression != true
            || latestQuality.RuleFacts.Where(f => f.RuleId.Value == "NQ-FVG-002").Select(f => f.Fact)
                .OfType<NasdaqHumanM5FvgQualityRuleFact>().SingleOrDefault() is not { } currentFact
            || !currentFact.Candidate.Selection.Fact.SameFact(approvedQuality.Candidate.Selection.Fact)
            || !currentFact.Selection.Fact.Fact.SameFact(quality.Fact)
            || new NasdaqHumanM5FvgQualityObservationSelector().SelectForCandidate(context,
                approvedQuality.Candidate.Trigger, approvedQuality.Candidate.Selection) is not NasdaqHumanM5FvgQualitySelection.Unique currentQuality
            || !currentQuality.Fact.Fact.SameFact(quality.Fact))
            return new NasdaqHumanM1CorrectiveRetracementSelection.Missing([]);

        var valid = new List<NasdaqHumanM1CorrectiveRetracementObservation>();
        var unavailable = new List<NasdaqHumanM1CorrectiveRetracementObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanM1CorrectiveRetracementObservation>())
            .Where(o => o.Session == quality.Session && o.ApprovedQuality.Fact.SameFact(quality.Fact)
                && o.ApprovedQuality.Fact.Fvg.SameFact(approvedQuality.Candidate.Selection.Fact)
                && o.ObservedAtUtc <= context.AsOfUtc && o.PullbackEffectiveAtUtc >= quality.EffectiveAtUtc
                && NasdaqHumanM5FvgObservationSelector.InWindow(context, o.PullbackEffectiveAtUtc)))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.Event.SourceCandles)
            {
                if (source.CloseTimeUtc > context.AsOfUtc) { unusable = true; break; }
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { unusable = true; break; }
            }
            if (unusable) continue;
            if (missing) unavailable.Add(observation);
            else valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanM1CorrectiveRetracementSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanM1CorrectiveRetracementSelection.Unique(valid, unavailable)
            : new NasdaqHumanM1CorrectiveRetracementSelection.Conflict(valid, unavailable);
    }
}
