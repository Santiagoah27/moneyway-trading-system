using System.Collections;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Exposes an immutable prefix of a historical observation series. The private source may contain later data, but the
/// public view cannot enumerate or index beyond the replay boundary captured by this snapshot.
/// </summary>
public sealed class HistoricalMarketPriceObservationSnapshot
{
    internal HistoricalMarketPriceObservationSnapshot(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        HistoricalMarketPriceObservationSeries? source,
        int visibleGroupCount,
        int visibleObservationCount)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (source is not null && (source.ProviderId != providerId || source.Symbol != symbol))
            throw new ArgumentException("Observation source must match the snapshot identity.", nameof(source));
        if (visibleGroupCount < 0 || visibleGroupCount > (source?.Groups.Count ?? 0))
            throw new ArgumentOutOfRangeException(nameof(visibleGroupCount));
        if (visibleObservationCount < 0 || visibleObservationCount > (source?.Count ?? 0))
            throw new ArgumentOutOfRangeException(nameof(visibleObservationCount));
        if ((visibleGroupCount == 0) != (visibleObservationCount == 0))
            throw new ArgumentException("Visible group and observation counts must both be empty or non-empty.");

        InputConfigured = source is not null;
        Groups = new PrefixReadOnlyList<HistoricalMarketPriceObservationGroup>(source?.Groups ?? [], visibleGroupCount);
        ObservationCount = visibleObservationCount;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public bool InputConfigured { get; }
    public IReadOnlyList<HistoricalMarketPriceObservationGroup> Groups { get; }
    public int ObservationCount { get; }

    private sealed class PrefixReadOnlyList<T>(IReadOnlyList<T> source, int count) : IReadOnlyList<T>
    {
        public int Count { get; } = count;
        public T this[int index] => index >= 0 && index < Count
            ? source[index]
            : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<T> GetEnumerator()
        {
            for (var index = 0; index < Count; index++) yield return source[index];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
