using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>Incrementally merges candle-close and optional market-price observation boundaries.</summary>
public sealed class CanonicalMultiTimeframeReplayCursor
{
    private readonly MultiTimeframeCandleReplayCursor candleCursor;
    private readonly HistoricalMarketPriceObservationSeries? marketPriceSeries;
    private MultiTimeframeReplayFrame? nextCandleFrame;
    private MultiTimeframeReplayFrame? latestCandleFrame;
    private int nextObservationGroupIndex;
    private int visibleObservationCount;
    private readonly HashSet<string> visibleObservationKinds = new(StringComparer.Ordinal);
    private readonly HashSet<string> visibleSourceResolutions = new(StringComparer.Ordinal);
    private int stepsCompleted;

    public CanonicalMultiTimeframeReplayCursor(IEnumerable<CandleSeries> candleSeries)
        : this(candleSeries, null, false)
    {
    }

    public CanonicalMultiTimeframeReplayCursor(
        IEnumerable<CandleSeries> candleSeries,
        HistoricalMarketPriceObservationSeries marketPriceSeries)
        : this(candleSeries, marketPriceSeries ?? throw new ArgumentNullException(nameof(marketPriceSeries)), true)
    {
    }

    private CanonicalMultiTimeframeReplayCursor(
        IEnumerable<CandleSeries> candleSeries,
        HistoricalMarketPriceObservationSeries? marketPriceSeries,
        bool highResolutionInputConfigured)
    {
        if (highResolutionInputConfigured != (marketPriceSeries is not null))
            throw new ArgumentException("High-resolution input configuration is inconsistent.", nameof(highResolutionInputConfigured));
        candleCursor = new(candleSeries);
        this.marketPriceSeries = marketPriceSeries;
        if (marketPriceSeries is not null
            && (marketPriceSeries.ProviderId != candleCursor.ProviderId || marketPriceSeries.Symbol != candleCursor.Symbol))
            throw new ArgumentException("Market-price observations must match candle provider and symbol.", nameof(marketPriceSeries));
        ProviderId = candleCursor.ProviderId;
        Symbol = candleCursor.Symbol;
        ConfiguredTimeframes = candleCursor.ConfiguredTimeframes;
        candleCursor.TryAdvance(out nextCandleFrame);
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public bool HighResolutionInputConfigured => marketPriceSeries is not null;
    public int StepsCompleted => stepsCompleted;

    public bool TryAdvance(out CanonicalMultiTimeframeReplayFrame? frame)
    {
        var nextGroup = marketPriceSeries is not null && nextObservationGroupIndex < marketPriceSeries.Groups.Count
            ? marketPriceSeries.Groups[nextObservationGroupIndex]
            : null;
        if (nextCandleFrame is null && nextGroup is null)
        {
            frame = null;
            return false;
        }

        var asOfUtc = nextCandleFrame is null
            ? nextGroup!.ObservedAtUtc
            : nextGroup is null
                ? nextCandleFrame.AsOfUtc
                : nextCandleFrame.AsOfUtc <= nextGroup.ObservedAtUtc ? nextCandleFrame.AsOfUtc : nextGroup.ObservedAtUtc;
        var candleAtBoundary = nextCandleFrame?.AsOfUtc == asOfUtc ? nextCandleFrame : null;
        var observationsAtBoundary = nextGroup?.ObservedAtUtc == asOfUtc ? nextGroup : null;
        if (candleAtBoundary is not null)
        {
            latestCandleFrame = candleAtBoundary;
            candleCursor.TryAdvance(out nextCandleFrame);
        }
        if (observationsAtBoundary is not null)
        {
            nextObservationGroupIndex++;
            visibleObservationCount += observationsAtBoundary.Observations.Count;
            foreach (var observation in observationsAtBoundary.Observations)
            {
                visibleObservationKinds.Add(observation.ObservationKind);
                visibleSourceResolutions.Add(observation.SourceResolution);
            }
        }

        stepsCompleted++;
        frame = new(
            ProviderId,
            Symbol,
            stepsCompleted,
            asOfUtc,
            ConfiguredTimeframes,
            candleAtBoundary?.UpdatedTimeframes ?? [],
            latestCandleFrame?.FramesByTimeframe ?? new Dictionary<Timeframe, ReplayFrame>(),
            HighResolutionInputConfigured,
            observationsAtBoundary,
            new(ProviderId, Symbol, marketPriceSeries, nextObservationGroupIndex, visibleObservationCount),
            visibleObservationKinds,
            visibleSourceResolutions);
        return true;
    }
}
