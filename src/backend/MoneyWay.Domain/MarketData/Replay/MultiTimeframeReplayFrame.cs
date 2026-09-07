using System.Collections.ObjectModel;

namespace MoneyWay.Domain.MarketData.Replay;

/// <summary>
/// Represents the market data observable across multiple configured timeframes at one historical close-time event.
/// Candles sharing the same close time become observable atomically. It intentionally exposes no future candles.
/// </summary>
public sealed class MultiTimeframeReplayFrame
{
    public MultiTimeframeReplayFrame(MarketDataProviderId providerId, MarketSymbol symbol, int step, DateTimeOffset asOfUtc,
        IEnumerable<Timeframe> configuredTimeframes, IEnumerable<Timeframe> updatedTimeframes,
        IReadOnlyDictionary<Timeframe, ReplayFrame> framesByTimeframe)
    {
        ArgumentNullException.ThrowIfNull(providerId); ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(configuredTimeframes); ArgumentNullException.ThrowIfNull(updatedTimeframes); ArgumentNullException.ThrowIfNull(framesByTimeframe);
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Replay timestamp must be UTC.", nameof(asOfUtc));
        var configured = configuredTimeframes.ToArray(); var updated = updatedTimeframes.ToArray();
        if (configured.Any(x => x is null) || configured.Distinct().Count() != configured.Length || !IsOrdered(configured)) throw new ArgumentException("Configured timeframes must be non-null, unique, and canonically ordered.", nameof(configuredTimeframes));
        if (updated.Any(x => x is null) || updated.Distinct().Count() != updated.Length || !IsOrdered(updated) || updated.Any(x => !configured.Contains(x))) throw new ArgumentException("Updated timeframes must be unique, ordered configured timeframes.", nameof(updatedTimeframes));
        var frames = framesByTimeframe.ToArray();
        if (frames.Any(x => x.Key is null || x.Value is null || !configured.Contains(x.Key) || x.Value.ProviderId != providerId || x.Value.Symbol != symbol || x.Value.Timeframe != x.Key || x.Value.AsOfUtc > asOfUtc)) throw new ArgumentException("Child frames must be observable and match configured metadata.", nameof(framesByTimeframe));
        if (updated.Any(timeframe => !framesByTimeframe.TryGetValue(timeframe, out var child) || child.AsOfUtc != asOfUtc)) throw new ArgumentException("Every updated timeframe must have a child frame at the global timestamp.", nameof(updatedTimeframes));
        ProviderId = providerId; Symbol = symbol; Step = step; AsOfUtc = asOfUtc;
        ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(configured); UpdatedTimeframes = new ReadOnlyCollection<Timeframe>(updated);
        FramesByTimeframe = new ReadOnlyDictionary<Timeframe, ReplayFrame>(frames.ToDictionary(x => x.Key, x => x.Value));
    }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public IReadOnlyList<Timeframe> UpdatedTimeframes { get; }
    public IReadOnlyDictionary<Timeframe, ReplayFrame> FramesByTimeframe { get; }
    internal static IOrderedEnumerable<Timeframe> Order(IEnumerable<Timeframe> values) => values.OrderBy(x => x.Unit).ThenBy(x => x.Amount);
    private static bool IsOrdered(IReadOnlyList<Timeframe> values) => values.SequenceEqual(Order(values));
}
