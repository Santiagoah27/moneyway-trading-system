using System.Text.Json;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Validates explicit human selection, never ranks FVGs or detects their geometry.</summary>
public sealed class NasdaqHumanM5FvgObservationSelector
{
    public NasdaqHumanM5FvgSelection Select(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trigger);
        if (!IsEstablished(context, trigger)) return new NasdaqHumanM5FvgSelection.Missing([]);
        var rejected = NasdaqHumanM5FvgCandidateHistory.Rejections(context, trigger);
        var current = NasdaqHumanM5FvgCandidateHistory.Current(context, trigger, rejected);
        var valid = new List<NasdaqHumanM5FvgObservation>();
        var unavailable = new List<NasdaqHumanM5FvgObservation>();
        foreach (var observation in Ordered(context.InputObservations.OfType<NasdaqHumanM5FvgObservation>())
            .Where(o => o.Trigger.SameFact(trigger.Selection.Fact) && o.ObservedAtUtc <= context.AsOfUtc
                && o.EffectiveAtUtc >= trigger.Selection.Fact.EffectiveAtUtc && InWindow(context, o.EffectiveAtUtc)))
        {
            if (rejected.Any(r => r.Candidate.Selection.Fact.SameFact(observation))) continue;
            if (current is not null && !observation.SameFact(current.Selection.Fact)
                && observation.EffectiveAtUtc != current.Selection.Fact.EffectiveAtUtc) continue;
            if (current is null && rejected.Count > 0 && (observation.EffectiveAtUtc <= rejected[^1].Candidate.Selection.Fact.EffectiveAtUtc
                || observation.ObservedAtUtc < rejected[^1].Selection.Fact.ObservedAtUtc)) continue;
            var source = ResolveSources(context, observation);
            if (source == SourceAvailability.Unavailable) unavailable.Add(observation);
            else if (source == SourceAvailability.Usable) valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanM5FvgSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanM5FvgSelection.Unique(valid, unavailable)
            : new NasdaqHumanM5FvgSelection.Conflict(valid, unavailable);
    }

    internal static bool IsEstablished(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger)
    {
        var fact = trigger.Selection.Fact;
        if (!fact.Session.Matches(context) || fact.ObservedAtUtc > context.AsOfUtc
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-FVG-001")))) return false;
        var lifecycle = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression;
        if (lifecycle is not null && lifecycle.ActiveInstance is null) return false;
        var owner = context.PriorObservations.LastOrDefault(o => o.RuleFacts.Any(f => f.RuleId.Value == "NQ-M5-001"
            && f.Fact is NasdaqHumanM5TriggerRuleFact t && t.Selection.Fact.Session == fact.Session));
        if (owner is null || !owner.RuleFacts.Where(f => f.RuleId.Value == "NQ-M5-001")
            .Select(f => f.Fact).OfType<NasdaqHumanM5TriggerRuleFact>().Single().Selection.Fact.SameFact(fact)) return false;
        var latest = context.PriorObservations.Where(o => o.AsOfUtc >= owner.AsOfUtc)
            .LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-M5-001"));
        return latest?.Evaluations.Single(e => e.RuleId.Value == "NQ-M5-001").Result == RuleEvaluationResult.Passed
            && latest.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-M5-001")?.EstablishesProgression == true
            && InWindow(context, context.AsOfUtc);
    }
    internal static bool InWindow(StrategyReplayContext context, DateTimeOffset time) => context.PriorObservations
        .SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value is "NQ-TIME-001" or "NQ-TIME-002")
        .Select(f => f.Fact).OfType<NasdaqTradingWindowFact>().Any(w => w.Contains(time) && w.Contains(context.AsOfUtc));

    internal enum SourceAvailability { Usable, Unavailable, Unusable }
    internal static SourceAvailability ResolveSources(StrategyReplayContext context, NasdaqHumanM5FvgObservation observation)
    {
        var missing = false;
        foreach (var source in observation.Event.SourceCandles)
        {
            if (source.CloseTimeUtc > context.AsOfUtc) return SourceAvailability.Unusable;
            if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
            var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
            if (actual is null) { missing = true; continue; }
            if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) return SourceAvailability.Unusable;
        }
        return missing ? SourceAvailability.Unavailable : SourceAvailability.Usable;
    }
    internal static IEnumerable<T> Ordered<T>(IEnumerable<T> observations) where T : IStrategyReplayInputObservation => observations
        .OrderBy(o => o.ObservedAtUtc).ThenBy(o => o.SourceReference, StringComparer.Ordinal)
        .ThenBy(o => JsonSerializer.Serialize(o), StringComparer.Ordinal);
}
