using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects explicit human realignment for the current canonical pullback without detecting price structure.</summary>
public sealed class NasdaqHumanM1RealignmentObservationSelector
{
    public NasdaqHumanM1RealignmentSelection Select(StrategyReplayContext context,
        NasdaqHumanM1CorrectiveRetracementRuleFact pullback)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pullback);
        var selected = pullback.Selection.Fact;
        if (!selected.Session.Matches(context) || selected.ObservedAtUtc > context.AsOfUtc
            || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc)
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-M1-002")))
            || context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression is { ActiveInstance: null })
            return new NasdaqHumanM1RealignmentSelection.Missing([]);

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-M1-001"));
        if (latest?.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").Result != RuleEvaluationResult.Passed
            || latest.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-M1-001")?.EstablishesProgression != true
            || latest.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-001").Select(f => f.Fact)
                .OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().SingleOrDefault() is not { } currentFact
            || !SamePullback(currentFact, pullback)
            || new NasdaqHumanM1CorrectiveRetracementObservationSelector().Select(context, pullback.ApprovedQuality)
                is not NasdaqHumanM1CorrectiveRetracementSelection.Unique currentSelection
            || !currentSelection.Fact.SameFact(selected))
            return new NasdaqHumanM1RealignmentSelection.Missing([]);

        var valid = new List<NasdaqHumanM1RealignmentObservation>();
        var unavailable = new List<NasdaqHumanM1RealignmentObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanM1RealignmentObservation>())
            .Where(o => SamePullback(o.Pullback, pullback) && o.ObservedAtUtc <= context.AsOfUtc
                && o.RealignmentEffectiveAtUtc >= selected.PullbackEffectiveAtUtc
                && NasdaqHumanM5FvgObservationSelector.InWindow(context, o.RealignmentEffectiveAtUtc)))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.Event.SourcesToResolve)
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
        if (valid.Count == 0) return new NasdaqHumanM1RealignmentSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanM1RealignmentSelection.Unique(valid, unavailable)
            : new NasdaqHumanM1RealignmentSelection.Conflict(valid, unavailable);
    }

    private static bool SamePullback(NasdaqHumanM1CorrectiveRetracementRuleFact one,
        NasdaqHumanM1CorrectiveRetracementRuleFact other) =>
        one.ApprovedQuality.Candidate.Selection.Fact.SameFact(other.ApprovedQuality.Candidate.Selection.Fact)
        && one.ApprovedQuality.Selection.Fact.Fact.SameFact(other.ApprovedQuality.Selection.Fact.Fact)
        && one.Selection.Fact.SameFact(other.Selection.Fact);
}
