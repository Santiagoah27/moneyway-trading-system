using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Summarizes a completed synchronized multi-timeframe replay. <see cref="GlobalFramesProcessed"/> counts synchronized
/// close-time events rather than the sum of source candles.
/// </summary>
public sealed class MultiTimeframeReplayRunResult
{
    public MultiTimeframeReplayRunResult(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        IEnumerable<Timeframe> configuredTimeframes,
        int globalFramesProcessed,
        DateTimeOffset? firstAsOfUtc,
        DateTimeOffset? lastAsOfUtc)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(configuredTimeframes);
        var configured = configuredTimeframes.ToArray();
        if (configured.Length == 0 || configured.Any(x => x is null) || configured.Distinct().Count() != configured.Length || !IsOrdered(configured))
            throw new ArgumentException("Configured timeframes must be non-empty, non-null, unique, and ordered.", nameof(configuredTimeframes));
        if (globalFramesProcessed < 0) throw new ArgumentOutOfRangeException(nameof(globalFramesProcessed));
        if (globalFramesProcessed == 0 && (firstAsOfUtc is not null || lastAsOfUtc is not null))
            throw new ArgumentException("An empty replay cannot contain timestamps.", nameof(globalFramesProcessed));
        if (globalFramesProcessed > 0 && (firstAsOfUtc is null || lastAsOfUtc is null))
            throw new ArgumentException("A non-empty replay requires first and last timestamps.", nameof(globalFramesProcessed));
        if (firstAsOfUtc is { Offset: var firstOffset } && firstOffset != TimeSpan.Zero)
            throw new ArgumentException("First timestamp must be UTC.", nameof(firstAsOfUtc));
        if (lastAsOfUtc is { Offset: var lastOffset } && lastOffset != TimeSpan.Zero)
            throw new ArgumentException("Last timestamp must be UTC.", nameof(lastAsOfUtc));
        if (lastAsOfUtc < firstAsOfUtc) throw new ArgumentException("Last timestamp cannot precede first timestamp.", nameof(lastAsOfUtc));
        ProviderId = providerId;
        Symbol = symbol;
        ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(configured);
        GlobalFramesProcessed = globalFramesProcessed;
        FirstAsOfUtc = firstAsOfUtc;
        LastAsOfUtc = lastAsOfUtc;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public int GlobalFramesProcessed { get; }
    public DateTimeOffset? FirstAsOfUtc { get; }
    public DateTimeOffset? LastAsOfUtc { get; }

    private static bool IsOrdered(IReadOnlyList<Timeframe> values) => values.SequenceEqual(values.OrderBy(x => x.Unit).ThenBy(x => x.Amount));
}
