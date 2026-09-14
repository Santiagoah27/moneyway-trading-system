namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Determines whether a supplied candle-body direction points toward the expected correction direction.
/// It does not decide whether a correction started or interpret candle price geometry.
/// </summary>
public sealed class CorrectionBodyDirectionCalculator
{
    public bool Evaluate(
        CorrectionOriginExtremeSide side,
        CandleBodyDirection bodyDirection)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The correction-origin extreme side is not supported.");
        }

        if (!Enum.IsDefined(bodyDirection))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyDirection), bodyDirection, "The candle body direction is not supported.");
        }

        return side == CorrectionOriginExtremeSide.Floor
            ? bodyDirection == CandleBodyDirection.Bullish
            : bodyDirection == CandleBodyDirection.Bearish;
    }
}
