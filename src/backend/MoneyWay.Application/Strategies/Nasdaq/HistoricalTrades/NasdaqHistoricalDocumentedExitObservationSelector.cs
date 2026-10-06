using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Selects a single documented exit for an exact canonical snapshot; never consumes contacts or creates fills.</summary>
public sealed class NasdaqHistoricalDocumentedExitObservationSelector
{
    public NasdaqHistoricalDocumentedExitSelection Select(StrategyReplayContext context, NasdaqHistoricalTradeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.Session.Matches(context) || snapshot.AsOfUtc > context.AsOfUtc)
            return new NasdaqHistoricalDocumentedExitSelection.Missing(snapshot, context.AsOfUtc, [], []);
        var valid = new List<NasdaqHistoricalDocumentedExitObservation>();
        var unavailable = new List<NasdaqHistoricalDocumentedExitObservation>();
        var unresolved = new List<NasdaqHistoricalDocumentedExitObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHistoricalDocumentedExitObservation>())
            .Where(o => ReferenceEquals(o.Snapshot, snapshot) && o.ObservedAtUtc <= context.AsOfUtc))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.Exit.SupportingCandles)
            {
                if (source.CloseTimeUtc > context.AsOfUtc) { unusable = true; break; }
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { unusable = true; break; }
            }
            if (unusable) continue;
            var unknownOrder = observation.Exit.ExitEffectiveAtUtc == snapshot.EntryEffectiveAtUtc
                && observation.Exit.EntryBeforeExitEvidence is null;
            if (unknownOrder) unresolved.Add(observation);
            if (missing) unavailable.Add(observation);
            else if (!unknownOrder) valid.Add(observation);
        }
        if (valid.Count == 0)
            return unresolved.Count > 1 && !unresolved.All(o => o.Exit.SameFact(unresolved[0].Exit))
                ? new NasdaqHistoricalDocumentedExitSelection.Conflict(snapshot, context.AsOfUtc, unresolved, unavailable, unresolved)
                : new NasdaqHistoricalDocumentedExitSelection.Missing(snapshot, context.AsOfUtc, unavailable, unresolved);
        // An unresolved alternative cannot be silently displaced by a resolved execution assertion.
        if (unresolved.Count > 0)
            return new NasdaqHistoricalDocumentedExitSelection.Conflict(snapshot, context.AsOfUtc,
                NasdaqHumanM5FvgObservationSelector.Ordered(valid.Concat(unresolved)), unavailable, unresolved);
        return valid.All(o => o.Exit.SameFact(valid[0].Exit))
            ? new NasdaqHistoricalDocumentedExitSelection.Unique(snapshot, context.AsOfUtc, valid, unavailable)
            : new NasdaqHistoricalDocumentedExitSelection.Conflict(snapshot, context.AsOfUtc, valid, unavailable, unresolved);
    }
}
