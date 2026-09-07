using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Summarizes a completed replay orchestration run without containing a trading or strategy result.
/// </summary>
public sealed class CandleReplayRunResult
{
    public CandleReplayRunResult(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        int framesProcessed,
        DateTimeOffset? firstAsOfUtc,
        DateTimeOffset? lastAsOfUtc)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(timeframe);

        if (framesProcessed < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(framesProcessed),
                framesProcessed,
                "Frames processed cannot be negative.");
        }

        if (framesProcessed == 0 && (firstAsOfUtc is not null || lastAsOfUtc is not null))
        {
            throw new ArgumentException("An empty replay cannot contain frame timestamps.", nameof(framesProcessed));
        }

        if (framesProcessed > 0 && (firstAsOfUtc is null || lastAsOfUtc is null))
        {
            throw new ArgumentException("A non-empty replay requires first and last frame timestamps.", nameof(framesProcessed));
        }

        if (firstAsOfUtc is { Offset: var firstOffset } && firstOffset != TimeSpan.Zero)
        {
            throw new ArgumentException("First replay timestamp must have a UTC offset.", nameof(firstAsOfUtc));
        }

        if (lastAsOfUtc is { Offset: var lastOffset } && lastOffset != TimeSpan.Zero)
        {
            throw new ArgumentException("Last replay timestamp must have a UTC offset.", nameof(lastAsOfUtc));
        }

        if (lastAsOfUtc < firstAsOfUtc)
        {
            throw new ArgumentException("Last replay timestamp cannot be before the first.", nameof(lastAsOfUtc));
        }

        ProviderId = providerId;
        Symbol = symbol;
        Timeframe = timeframe;
        FramesProcessed = framesProcessed;
        FirstAsOfUtc = firstAsOfUtc;
        LastAsOfUtc = lastAsOfUtc;
    }

    public MarketDataProviderId ProviderId { get; }

    public MarketSymbol Symbol { get; }

    public Timeframe Timeframe { get; }

    public int FramesProcessed { get; }

    public DateTimeOffset? FirstAsOfUtc { get; }

    public DateTimeOffset? LastAsOfUtc { get; }
}
