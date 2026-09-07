using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Runs an in-memory closed-candle replay and exposes each step only as a <see cref="ReplayFrame"/>.
/// The consumer receives no direct access to future candles or the source <see cref="CandleSeries"/>.
/// </summary>
public sealed class RunCandleReplayUseCase
{
    public CandleReplayRunResult Execute(CandleSeries series, Action<ReplayFrame> consumeFrame)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(consumeFrame);

        var cursor = new CandleReplayCursor(series);
        DateTimeOffset? firstAsOfUtc = null;
        DateTimeOffset? lastAsOfUtc = null;

        while (cursor.TryAdvance(out var frame))
        {
            firstAsOfUtc ??= frame!.AsOfUtc;
            lastAsOfUtc = frame!.AsOfUtc;
            consumeFrame(frame);
        }

        return new CandleReplayRunResult(
            series.ProviderId,
            series.Symbol,
            series.Timeframe,
            cursor.StepsCompleted,
            firstAsOfUtc,
            lastAsOfUtc);
    }
}
