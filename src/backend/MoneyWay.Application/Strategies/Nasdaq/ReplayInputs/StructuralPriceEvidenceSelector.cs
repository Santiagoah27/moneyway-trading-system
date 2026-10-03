using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Shared exact-price compatibility and provenance ordering for already-visible, identity-matched evidence.</summary>
internal static class StructuralPriceEvidenceSelector
{
    internal static (IReadOnlyList<T> Observations, IReadOnlyList<decimal> Prices) Select<T>(
        IEnumerable<T> observations, Func<T, decimal> price, Func<T, string> sourceReference)
        where T : IStrategyReplayInputObservation
    {
        var supporting = observations.OrderBy(item => item.ObservedAtUtc)
            .ThenBy(sourceReference, StringComparer.Ordinal).ThenBy(price).ToArray();
        var prices = supporting.Select(price).Distinct().OrderBy(item => item).ToArray();
        return (Array.AsReadOnly(supporting), Array.AsReadOnly(prices));
    }
}
