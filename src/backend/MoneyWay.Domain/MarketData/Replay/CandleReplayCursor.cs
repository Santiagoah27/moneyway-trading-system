namespace MoneyWay.Domain.MarketData.Replay;

/// <summary>
/// Advances a historical <see cref="CandleSeries"/> one closed candle at a time.
/// This orchestration mechanism should not be supplied directly to strategy rule evaluators;
/// future evaluators should consume <see cref="ReplayFrame"/>.
/// </summary>
public sealed class CandleReplayCursor
{
    private readonly CandleSeries source;
    private int stepsCompleted;

    public CandleReplayCursor(CandleSeries source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.source = source;
    }

    public MarketDataProviderId ProviderId => source.ProviderId;
    public MarketSymbol Symbol => source.Symbol;
    public Timeframe Timeframe => source.Timeframe;
    public int StepsCompleted => stepsCompleted;

    public bool TryAdvance(out ReplayFrame? frame)
    {
        if (stepsCompleted >= source.Count)
        {
            frame = null;
            return false;
        }

        var currentCandle = source.Candles[stepsCompleted];
        stepsCompleted++;
        frame = new ReplayFrame(
            source.ProviderId,
            source.Symbol,
            source.Timeframe,
            stepsCompleted,
            currentCandle.CloseTimeUtc,
            currentCandle,
            source.Candles.Take(stepsCompleted));
        return true;
    }
}
