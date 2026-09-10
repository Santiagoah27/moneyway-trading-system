using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Records compact temporal and source-resolution metadata for one canonical backtest step without copying candle or
/// high-resolution observation history.
/// </summary>
public sealed class MultiTimeframeBacktestObservation
{
    public MultiTimeframeBacktestObservation(
        int step,
        DateTimeOffset asOfUtc,
        IEnumerable<Timeframe> updatedTimeframes,
        IEnumerable<Timeframe> availableTimeframes,
        ReplayMarketDataAvailability? marketDataAvailability = null)
    {
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(updatedTimeframes);
        ArgumentNullException.ThrowIfNull(availableTimeframes);
        var updated = updatedTimeframes.ToArray(); var available = availableTimeframes.ToArray();
        Validate(updated, nameof(updatedTimeframes)); Validate(available, nameof(availableTimeframes));
        if (updated.Any(x => !available.Contains(x))) throw new ArgumentException("Updated timeframes must be available.", nameof(updatedTimeframes));
        Step = step;
        AsOfUtc = asOfUtc;
        UpdatedTimeframes = new ReadOnlyCollection<Timeframe>(updated);
        AvailableTimeframes = new ReadOnlyCollection<Timeframe>(available);
        MarketDataAvailability = marketDataAvailability ?? ReplayMarketDataAvailability.CandleOnly;
    }

    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<Timeframe> UpdatedTimeframes { get; }
    public IReadOnlyList<Timeframe> AvailableTimeframes { get; }
    public ReplayMarketDataAvailability MarketDataAvailability { get; }

    private static void Validate(IReadOnlyList<Timeframe> values, string parameterName)
    {
        if (values.Any(x => x is null) || values.Distinct().Count() != values.Count || !values.SequenceEqual(values.OrderBy(x => x.Unit).ThenBy(x => x.Amount)))
            throw new ArgumentException("Timeframes must be non-null, unique, and ordered.", parameterName);
    }
}
