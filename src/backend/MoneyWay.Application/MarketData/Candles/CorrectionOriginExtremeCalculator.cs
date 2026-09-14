using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Mechanically updates a supplied correction-origin floor or ceiling from one supplied candle range.
/// It does not infer correction state, gaps, candle membership, structural validation, or strategy outcomes.
/// </summary>
public sealed class CorrectionOriginExtremeCalculator
{
    public CorrectionOriginExtremeResult Evaluate(
        decimal currentExtreme,
        Candle candle,
        CorrectionOriginExtremeSide side)
    {
        ArgumentNullException.ThrowIfNull(candle);
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));

        var resultingExtreme = side == CorrectionOriginExtremeSide.Floor
            ? decimal.Min(currentExtreme, candle.Low)
            : decimal.Max(currentExtreme, candle.High);

        return new(currentExtreme, resultingExtreme, side);
    }
}
