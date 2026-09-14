using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Classifies a supplied candle body using exact decimal Open and Close comparison.
/// It does not interpret wicks, tolerances, correction boundaries, or strategy outcomes.
/// </summary>
public sealed class CandleBodyDirectionCalculator
{
    public CandleBodyDirection Evaluate(Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candle);

        return candle.Close > candle.Open
            ? CandleBodyDirection.Bullish
            : candle.Close < candle.Open
                ? CandleBodyDirection.Bearish
                : CandleBodyDirection.Neutral;
    }
}
