using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Computes a wick-based protection anchor from candles whose structural-turn membership was selected by the caller.
/// </summary>
public sealed class StructuralTurnProtectionAnchorCalculator
{
    public StructuralTurnProtectionAnchorResult Evaluate(
        IReadOnlyList<Candle> candles,
        StructuralTurnProtectionSide side)
    {
        ArgumentNullException.ThrowIfNull(candles);

        if (candles.Count == 0)
        {
            throw new ArgumentException("At least one candle is required.", nameof(candles));
        }

        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn protection side is not supported.");
        }

        var protectionAnchor = side == StructuralTurnProtectionSide.Lower
            ? decimal.MaxValue
            : decimal.MinValue;

        foreach (var candle in candles)
        {
            ArgumentNullException.ThrowIfNull(candle);

            protectionAnchor = side == StructuralTurnProtectionSide.Lower
                ? decimal.Min(protectionAnchor, candle.Low)
                : decimal.Max(protectionAnchor, candle.High);
        }

        return new StructuralTurnProtectionAnchorResult(protectionAnchor, side);
    }
}
