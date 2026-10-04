using System.Text.Json;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Orders annotated market events only. It never discovers SC/IFVG, invokes an evaluator or assigns a rule result.</summary>
public sealed class NasdaqHumanM5TriggerObservationSelector
{
    public NasdaqHumanM5TriggerSelection Select(StrategyReplayContext context, NasdaqLiquidityTakeRuleFact decisiveTake)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(decisiveTake);
        if (!decisiveTake.Session.Matches(context) || decisiveTake.IsSessionInvalidated)
            return new NasdaqHumanM5TriggerSelection.Missing([]);
        var history = context.PriorObservations.SelectMany(o => o.RuleFacts
            .Where(f => f.RuleId.Value == "NQ-LIQ-003" && f.Fact is NasdaqLiquidityTakeRuleFact)
            .Select(f => (Observation: o, Fact: (NasdaqLiquidityTakeRuleFact)f.Fact)))
            .Where(x => x.Fact.Session == decisiveTake.Session).ToArray();
        // Only an owning-rule canonical prior positive fact establishes the take. Any terminal fact blocks the whole session.
        if (history.Any(x => x.Fact.IsSessionInvalidated)) return new NasdaqHumanM5TriggerSelection.Missing([]);
        var latest = history.LastOrDefault();
        if (latest.Fact is null || !latest.Fact.Take.SameFact(decisiveTake.Take)
            || !latest.Fact.InitiatingTake.SameFact(decisiveTake.InitiatingTake) || latest.Fact.Direction != decisiveTake.Direction
            || !latest.Observation.Evaluations.Any(e => e.RuleId.Value == "NQ-LIQ-003" && e.Result == RuleEvaluationResult.Passed))
            return new NasdaqHumanM5TriggerSelection.Missing([]);

        // Reuse the TIME owner's boundaries; do not calculate a clock or extend an expired setup.
        var window = context.PriorObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value is "NQ-TIME-001" or "NQ-TIME-002")
            .Select(f => f.Fact).OfType<NasdaqTradingWindowFact>()
            .LastOrDefault(w => w.Contains(decisiveTake.Take.EffectiveAtUtc));
        if (window is null || !window.Contains(context.AsOfUtc)) return new NasdaqHumanM5TriggerSelection.Missing([]);

        var valid = new List<NasdaqHumanM5TriggerObservation>();
        var unavailable = new List<NasdaqHumanM5TriggerObservation>();
        foreach (var observation in context.InputObservations.OfType<NasdaqHumanM5TriggerObservation>()
            .Where(o => o.Session == decisiveTake.Session && o.DecisiveTake.SameFact(decisiveTake.Take)
                && o.Event.Direction == decisiveTake.Direction && o.ObservedAtUtc <= context.AsOfUtc
                && o.EffectiveAtUtc >= decisiveTake.Take.EffectiveAtUtc && window.Contains(o.EffectiveAtUtc))
            .OrderBy(o => o.EffectiveAtUtc).ThenBy(o => o.ObservedAtUtc)
            .ThenBy(o => o.SourceReference, StringComparer.Ordinal).ThenBy(o => JsonSerializer.Serialize(o), StringComparer.Ordinal))
        {
            if (observation.EffectiveAtUtc == observation.DecisiveTake.EffectiveAtUtc
                && observation.SameCandleOrder != NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation)
                continue;
            if (observation.SameCandleOrder == NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation && !SameTakeCandle(observation))
                continue;
            bool missing = false, unusable = false;
            foreach (var source in observation.Event.SourcesToResolve)
            {
                if (source.CloseTimeUtc > observation.EffectiveAtUtc || source.CloseTimeUtc > context.AsOfUtc) { unusable = true; break; }
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { unusable = true; break; }
            }
            if (unusable) continue;
            if (missing) unavailable.Add(observation); else valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanM5TriggerSelection.Missing(unavailable);
        // Effective market time owns selection; the remaining ordering only stabilizes retained diagnostics.
        var first = valid.Where(o => o.EffectiveAtUtc == valid[0].EffectiveAtUtc).ToArray();
        return first.All(o => o.SameFact(first[0]))
            ? new NasdaqHumanM5TriggerSelection.Unique(first, unavailable)
            : new NasdaqHumanM5TriggerSelection.Conflict(first, unavailable);
    }
    private static bool SameTakeCandle(NasdaqHumanM5TriggerObservation observation) => observation.DecisiveTake.Event switch
    {
        NasdaqHumanLiquidityTakeEvent.Documented d => d.SupportingCandles.Any(c =>
            NasdaqStructuralLiquidityReference.SameCandle(c, observation.Event.ConfirmationCandle)),
        NasdaqHumanLiquidityTakeEvent.CanonicalPrice p => p.EffectiveAtUtc >= observation.Event.ConfirmationCandle.OpenTimeUtc
            && p.EffectiveAtUtc <= observation.Event.ConfirmationCandle.CloseTimeUtc,
        _ => false,
    };
}
