using System.Collections.ObjectModel;

namespace MoneyWay.Domain.MarketData.Replay;

/// <summary>
/// Represents the market data observable at one closed-candle step of a historical replay.
/// It intentionally excludes future candles.
/// </summary>
public sealed class ReplayFrame
{
    internal ReplayFrame(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        int step,
        DateTimeOffset asOfUtc,
        Candle currentCandle,
        IEnumerable<Candle> availableCandles)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(currentCandle);
        ArgumentNullException.ThrowIfNull(availableCandles);

        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Step must be greater than zero.");
        }

        var snapshot = availableCandles.ToArray();
        if (snapshot.Length != step)
        {
            throw new ArgumentException("Available candle count must equal the replay step.", nameof(availableCandles));
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            var candle = snapshot[index]
                ?? throw new ArgumentException("Available candles cannot contain null elements.", nameof(availableCandles));

            if (candle.ProviderId != providerId || candle.Symbol != symbol || candle.Timeframe != timeframe)
            {
                throw new ArgumentException("Available candles must match the replay metadata.", nameof(availableCandles));
            }

            if (candle.CloseTimeUtc > asOfUtc)
            {
                throw new ArgumentException("Available candles cannot close after the replay time.", nameof(availableCandles));
            }

            if (index > 0 && candle.OpenTimeUtc <= snapshot[index - 1].OpenTimeUtc)
            {
                throw new ArgumentException(
                    "Available candles must have unique open timestamps in ascending order.",
                    nameof(availableCandles));
            }
        }

        if (!ReferenceEquals(snapshot[^1], currentCandle))
        {
            throw new ArgumentException("Current candle must be the last available candle.", nameof(currentCandle));
        }

        if (asOfUtc != currentCandle.CloseTimeUtc)
        {
            throw new ArgumentException("Replay time must equal the current candle close time.", nameof(asOfUtc));
        }

        ProviderId = providerId;
        Symbol = symbol;
        Timeframe = timeframe;
        Step = step;
        AsOfUtc = asOfUtc;
        CurrentCandle = currentCandle;
        AvailableCandles = new ReadOnlyCollection<Candle>(snapshot);
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public Candle CurrentCandle { get; }
    public IReadOnlyList<Candle> AvailableCandles { get; }
}
