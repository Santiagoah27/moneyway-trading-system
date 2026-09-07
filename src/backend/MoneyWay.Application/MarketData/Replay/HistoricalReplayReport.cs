using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Neutral summary of a completed historical candle replay.
/// Contains no strategy or trading outcome.
/// </summary>
public sealed class HistoricalReplayReport
{
    public HistoricalReplayReport(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        int candleCount,
        DateTimeOffset? datasetStartTimeUtc,
        DateTimeOffset? datasetEndTimeUtc,
        int framesProcessed,
        DateTimeOffset? firstFrameAsOfUtc,
        DateTimeOffset? lastFrameAsOfUtc)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(timeframe);

        if (candleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candleCount), candleCount, "Candle count cannot be negative.");
        }

        if (framesProcessed < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(framesProcessed),
                framesProcessed,
                "Frames processed cannot be negative.");
        }

        if (framesProcessed != candleCount)
        {
            throw new ArgumentException("Frames processed must equal candle count.", nameof(framesProcessed));
        }

        if (candleCount == 0
            && (datasetStartTimeUtc is not null
                || datasetEndTimeUtc is not null
                || firstFrameAsOfUtc is not null
                || lastFrameAsOfUtc is not null))
        {
            throw new ArgumentException("An empty replay report cannot contain timestamps.", nameof(candleCount));
        }

        if (candleCount > 0
            && (datasetStartTimeUtc is null
                || datasetEndTimeUtc is null
                || firstFrameAsOfUtc is null
                || lastFrameAsOfUtc is null))
        {
            throw new ArgumentException("A non-empty replay report requires all timestamps.", nameof(candleCount));
        }

        EnsureUtc(datasetStartTimeUtc, nameof(datasetStartTimeUtc));
        EnsureUtc(datasetEndTimeUtc, nameof(datasetEndTimeUtc));
        EnsureUtc(firstFrameAsOfUtc, nameof(firstFrameAsOfUtc));
        EnsureUtc(lastFrameAsOfUtc, nameof(lastFrameAsOfUtc));

        if (datasetEndTimeUtc <= datasetStartTimeUtc)
        {
            throw new ArgumentException("Dataset end must be after dataset start.", nameof(datasetEndTimeUtc));
        }

        if (lastFrameAsOfUtc < firstFrameAsOfUtc)
        {
            throw new ArgumentException("Last frame timestamp cannot be before the first.", nameof(lastFrameAsOfUtc));
        }

        ProviderId = providerId;
        Symbol = symbol;
        Timeframe = timeframe;
        CandleCount = candleCount;
        DatasetStartTimeUtc = datasetStartTimeUtc;
        DatasetEndTimeUtc = datasetEndTimeUtc;
        FramesProcessed = framesProcessed;
        FirstFrameAsOfUtc = firstFrameAsOfUtc;
        LastFrameAsOfUtc = lastFrameAsOfUtc;
    }

    public MarketDataProviderId ProviderId { get; }

    public MarketSymbol Symbol { get; }

    public Timeframe Timeframe { get; }

    public int CandleCount { get; }

    public DateTimeOffset? DatasetStartTimeUtc { get; }

    public DateTimeOffset? DatasetEndTimeUtc { get; }

    public int FramesProcessed { get; }

    public DateTimeOffset? FirstFrameAsOfUtc { get; }

    public DateTimeOffset? LastFrameAsOfUtc { get; }

    private static void EnsureUtc(DateTimeOffset? timestamp, string parameterName)
    {
        if (timestamp is { Offset: var offset } && offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Replay report timestamps must have a UTC offset.", parameterName);
        }
    }
}
