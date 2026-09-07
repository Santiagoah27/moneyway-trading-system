using System.Collections.ObjectModel;

namespace MoneyWay.Domain.MarketData;

/// <summary>
/// Represents an immutable chronological snapshot of homogeneous candles without trading logic.
/// </summary>
public sealed class CandleSeries
{
    public CandleSeries(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        IEnumerable<Candle> candles)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(candles);

        var snapshot = candles.ToArray();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var candle = snapshot[index]
                ?? throw new ArgumentException("Candles cannot contain null elements.", nameof(candles));

            if (candle.ProviderId != providerId)
            {
                throw new ArgumentException("Every candle must match the series provider.", nameof(candles));
            }

            if (candle.Symbol != symbol)
            {
                throw new ArgumentException("Every candle must match the series symbol.", nameof(candles));
            }

            if (candle.Timeframe != timeframe)
            {
                throw new ArgumentException("Every candle must match the series timeframe.", nameof(candles));
            }

            if (index == 0)
            {
                continue;
            }

            var previous = snapshot[index - 1];
            if (candle.OpenTimeUtc <= previous.OpenTimeUtc)
            {
                throw new ArgumentException(
                    "Candles must have unique open timestamps in ascending order.",
                    nameof(candles));
            }

            if (candle.OpenTimeUtc < previous.CloseTimeUtc)
            {
                throw new ArgumentException("Consecutive candles cannot overlap.", nameof(candles));
            }
        }

        ProviderId = providerId;
        Symbol = symbol;
        Timeframe = timeframe;
        Candles = new ReadOnlyCollection<Candle>(snapshot);
    }

    public MarketDataProviderId ProviderId { get; }

    public MarketSymbol Symbol { get; }

    public Timeframe Timeframe { get; }

    public IReadOnlyList<Candle> Candles { get; }

    public int Count => Candles.Count;

    public DateTimeOffset? StartTimeUtc => Count == 0 ? null : Candles[0].OpenTimeUtc;

    public DateTimeOffset? EndTimeUtc => Count == 0 ? null : Candles[^1].CloseTimeUtc;
}
