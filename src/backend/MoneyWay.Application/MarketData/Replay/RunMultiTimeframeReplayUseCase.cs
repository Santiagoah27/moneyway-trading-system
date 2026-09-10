using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Runs synchronized historical replay for multiple candle series and, through its additive overload, optional
/// chronological market-price observations belonging to the same provider and symbol. It performs no strategy evaluation.
/// </summary>
public sealed class RunMultiTimeframeReplayUseCase
{
    public MultiTimeframeReplayRunResult Execute(IEnumerable<CandleSeries> series, Action<MultiTimeframeReplayFrame> consumeFrame)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(consumeFrame);
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        DateTimeOffset? firstAsOfUtc = null;
        DateTimeOffset? lastAsOfUtc = null;
        while (cursor.TryAdvance(out var frame))
        {
            var current = frame ?? throw new InvalidOperationException("Replay cursor returned a null frame.");
            firstAsOfUtc ??= current.AsOfUtc;
            lastAsOfUtc = current.AsOfUtc;
            consumeFrame(current);
        }
        return new(cursor.ProviderId, cursor.Symbol, cursor.ConfiguredTimeframes, cursor.StepsCompleted, firstAsOfUtc, lastAsOfUtc);
    }

    public MultiTimeframeReplayRunResult Execute(
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations,
        Action<CanonicalMultiTimeframeReplayFrame> consumeFrame)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations);
        ArgumentNullException.ThrowIfNull(consumeFrame);
        var cursor = new CanonicalMultiTimeframeReplayCursor(series, marketPriceObservations);
        DateTimeOffset? firstAsOfUtc = null;
        DateTimeOffset? lastAsOfUtc = null;
        while (cursor.TryAdvance(out var frame))
        {
            var current = frame ?? throw new InvalidOperationException("Replay cursor returned a null frame.");
            firstAsOfUtc ??= current.AsOfUtc;
            lastAsOfUtc = current.AsOfUtc;
            consumeFrame(current);
        }
        return new(cursor.ProviderId, cursor.Symbol, cursor.ConfiguredTimeframes, cursor.StepsCompleted, firstAsOfUtc, lastAsOfUtc);
    }
}
