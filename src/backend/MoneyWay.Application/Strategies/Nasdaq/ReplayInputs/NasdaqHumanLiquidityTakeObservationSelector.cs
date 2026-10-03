using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves exact evidence without discovering prices, choosing a winner or assigning rule outcomes.</summary>
public sealed class NasdaqHumanLiquidityTakeObservationSelector
{
    public NasdaqHumanLiquidityTakeSelection Select(StrategyReplayContext context, NasdaqLiquidityTakeReference reference)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.Session.Matches(context)) return new NasdaqHumanLiquidityTakeSelection.Missing([]);
        var candidates = context.InputObservations.OfType<NasdaqHumanLiquidityTakeObservation>()
            .Where(o => o.Session == reference.Session && o.Reference.SameSlot(reference) && o.ObservedAtUtc <= context.AsOfUtc)
            .OrderBy(o => o.ObservedAtUtc).ThenBy(o => o.SourceReference, StringComparer.Ordinal)
            .ThenBy(o => o.EffectiveAtUtc).ThenBy(o => o.ObservedPrice).ThenBy(o => o.ReferenceEligibleAtUtc)
            .ThenBy(o => o.Event.GetType().Name, StringComparer.Ordinal).ThenBy(o => o.Event.SortKey, StringComparer.Ordinal).ThenBy(o => o.AuditKey, StringComparer.Ordinal)
            .ToArray();
        var valid = new List<NasdaqHumanLiquidityTakeObservation>();
        var unavailable = new List<NasdaqHumanLiquidityTakeObservation>();
        foreach (var observation in candidates)
        {
            var state = Resolve(context, observation);
            if (state == SourceState.Available) valid.Add(observation);
            else if (state == SourceState.Unavailable) unavailable.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanLiquidityTakeSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanLiquidityTakeSelection.Unique(valid, unavailable)
            : new NasdaqHumanLiquidityTakeSelection.Conflict(valid, unavailable);
    }

    private enum SourceState { Available, Unavailable, Unusable }
    private static SourceState Resolve(StrategyReplayContext context, NasdaqHumanLiquidityTakeObservation observation)
    {
        var sources = observation.Reference.Sources;
        if (observation.Event is NasdaqHumanLiquidityTakeEvent.Documented documented)
            sources = sources.Concat(documented.SupportingCandles).ToArray();
        foreach (var source in sources)
        {
            if (!context.TryGetFrame(source.Timeframe, out var frame)) return SourceState.Unavailable;
            var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
            if (actual is null) return SourceState.Unavailable;
            if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) return SourceState.Unusable;
        }
        if (observation.Reference is NasdaqLiquidityTakeReference.Structural structural)
        {
            var selection = new NasdaqHumanStructuralLiquidityObservationSelector().Select(context, structural.Session);
            if (selection is not NasdaqHumanStructuralLiquiditySelection.Unique unique
                || unique.SupportingObservations[0].CompareFact(structural.Selection.SupportingObservations[0]) != 0
                || !unique.References.Contains(structural.Member)
                || !unique.SupportingObservations.Any(o => o.ObservedAtUtc <= observation.ReferenceEligibleAtUtc))
                return SourceState.Unusable;
        }
        if (observation.Event is NasdaqHumanLiquidityTakeEvent.CanonicalPrice price
            && !context.MarketPriceObservations.Groups.Any(g => g.Observations.Contains(price.Observation)))
            return SourceState.Unavailable;
        return SourceState.Available;
    }
}
