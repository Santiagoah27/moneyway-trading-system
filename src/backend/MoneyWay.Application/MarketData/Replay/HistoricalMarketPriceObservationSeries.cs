using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>Represents one immutable chronological input of normalized historical market-price evidence.</summary>
public sealed class HistoricalMarketPriceObservationSeries
{
    public HistoricalMarketPriceObservationSeries(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        IEnumerable<HistoricalMarketPriceObservation> observations)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        ArgumentNullException.ThrowIfNull(observations);
        var snapshot = observations.ToArray();
        if (snapshot.Any(item => item is null))
            throw new ArgumentException("Observations cannot contain null elements.", nameof(observations));
        if (snapshot.Any(item => item.ProviderId != providerId || item.Symbol != symbol))
            throw new ArgumentException("Every observation must match the series provider and symbol.", nameof(observations));
        if (snapshot.Where((item, index) => index > 0 && item.ObservedAtUtc < snapshot[index - 1].ObservedAtUtc).Any())
            throw new ArgumentException("Observations must be chronological by source timestamp.", nameof(observations));

        var groups = snapshot
            .GroupBy(item => item.ObservedAtUtc)
            .Select(group => new HistoricalMarketPriceObservationGroup(group))
            .ToArray();
        Observations = new ReadOnlyCollection<HistoricalMarketPriceObservation>(snapshot);
        Groups = new ReadOnlyCollection<HistoricalMarketPriceObservationGroup>(groups);
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public IReadOnlyList<HistoricalMarketPriceObservation> Observations { get; }
    public IReadOnlyList<HistoricalMarketPriceObservationGroup> Groups { get; }
    public int Count => Observations.Count;
}
