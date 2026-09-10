using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>Combines retained closed-candle frames with an optional market-price observation boundary.</summary>
public sealed class CanonicalMultiTimeframeReplayFrame
{
    internal CanonicalMultiTimeframeReplayFrame(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        int step,
        DateTimeOffset asOfUtc,
        IEnumerable<Timeframe> configuredTimeframes,
        IEnumerable<Timeframe> updatedTimeframes,
        IReadOnlyDictionary<Timeframe, ReplayFrame> framesByTimeframe,
        bool highResolutionInputConfigured,
        HistoricalMarketPriceObservationGroup? currentMarketPriceObservations,
        HistoricalMarketPriceObservationSnapshot marketPriceObservations,
        IEnumerable<string> observationKinds,
        IEnumerable<string> sourceResolutions)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Replay timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(configuredTimeframes);
        ArgumentNullException.ThrowIfNull(updatedTimeframes);
        ArgumentNullException.ThrowIfNull(framesByTimeframe);
        ArgumentNullException.ThrowIfNull(marketPriceObservations);
        ArgumentNullException.ThrowIfNull(observationKinds);
        ArgumentNullException.ThrowIfNull(sourceResolutions);
        var configured = configuredTimeframes.ToArray();
        var updated = updatedTimeframes.ToArray();
        var frames = framesByTimeframe.ToArray();
        if (configured.Length == 0 || configured.Any(item => item is null) || configured.Distinct().Count() != configured.Length
            || !configured.SequenceEqual(Order(configured)))
            throw new ArgumentException("Configured timeframes must be non-null, unique, and ordered.", nameof(configuredTimeframes));
        if (updated.Any(item => item is null) || updated.Distinct().Count() != updated.Length
            || !updated.SequenceEqual(Order(updated)) || updated.Any(item => !configured.Contains(item)))
            throw new ArgumentException("Updated timeframes must be unique ordered configured timeframes.", nameof(updatedTimeframes));
        if (frames.Any(pair => pair.Key is null || pair.Value is null || !configured.Contains(pair.Key)
            || pair.Value.ProviderId != providerId || pair.Value.Symbol != symbol || pair.Value.Timeframe != pair.Key
            || pair.Value.AsOfUtc > asOfUtc))
            throw new ArgumentException("Candle frames must be visible and match replay identity.", nameof(framesByTimeframe));
        if (updated.Any(timeframe => !framesByTimeframe.TryGetValue(timeframe, out var frame) || frame.AsOfUtc != asOfUtc))
            throw new ArgumentException("Updated candle frames must close at the canonical boundary.", nameof(updatedTimeframes));
        if (currentMarketPriceObservations is not null
            && (currentMarketPriceObservations.ObservedAtUtc != asOfUtc
                || currentMarketPriceObservations.Observations.Any(item => item.ProviderId != providerId || item.Symbol != symbol)))
            throw new ArgumentException("Market-price observations must match the canonical boundary and identity.", nameof(currentMarketPriceObservations));
        if (marketPriceObservations.ProviderId != providerId || marketPriceObservations.Symbol != symbol
            || marketPriceObservations.InputConfigured != highResolutionInputConfigured
            || marketPriceObservations.Groups.Any(group => group.ObservedAtUtc > asOfUtc))
            throw new ArgumentException("Market-price snapshot must be bounded and match the canonical identity.", nameof(marketPriceObservations));

        Step = step;
        AsOfUtc = asOfUtc;
        ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(configured);
        UpdatedTimeframes = new ReadOnlyCollection<Timeframe>(updated);
        FramesByTimeframe = new ReadOnlyDictionary<Timeframe, ReplayFrame>(frames.ToDictionary(pair => pair.Key, pair => pair.Value));
        CurrentMarketPriceObservations = currentMarketPriceObservations;
        MarketPriceObservations = marketPriceObservations;
        MarketDataAvailability = new(
            highResolutionInputConfigured,
            currentMarketPriceObservations,
            marketPriceObservations.ObservationCount,
            observationKinds,
            sourceResolutions);
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public IReadOnlyList<Timeframe> UpdatedTimeframes { get; }
    public IReadOnlyDictionary<Timeframe, ReplayFrame> FramesByTimeframe { get; }
    public HistoricalMarketPriceObservationGroup? CurrentMarketPriceObservations { get; }
    public HistoricalMarketPriceObservationSnapshot MarketPriceObservations { get; }
    public ReplayMarketDataAvailability MarketDataAvailability { get; }

    private static IOrderedEnumerable<Timeframe> Order(IEnumerable<Timeframe> values) =>
        values.OrderBy(item => item.Unit).ThenBy(item => item.Amount);
}
