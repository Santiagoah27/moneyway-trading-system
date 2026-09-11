using MoneyWay.Application.MarketData.PriceLevels;

namespace MoneyWay.Application.Strategies.Nasdaq.Targets;

/// <summary>
/// Selects the first MoneyWay Nasdaq session target encountered in an already-established expected price direction.
/// The caller must supply both Asia and London prices as eligible, future-valid session candidates for this selection
/// case. This primitive does not establish that eligibility, inspect prior touches, or select a structural fallback.
/// </summary>
public sealed class NasdaqSessionTargetSelector
{
    public NasdaqSessionTargetSelection Select(
        decimal eligibleAsiaTargetPrice,
        decimal eligibleLondonTargetPrice,
        PriceLevelDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));

        if (eligibleAsiaTargetPrice == eligibleLondonTargetPrice)
        {
            return new(
                eligibleAsiaTargetPrice,
                direction,
                NasdaqSessionTargetOrigin.CoincidentAsiaAndLondon);
        }

        var asiaIsFirst = direction == PriceLevelDirection.Upper
            ? eligibleAsiaTargetPrice < eligibleLondonTargetPrice
            : eligibleAsiaTargetPrice > eligibleLondonTargetPrice;

        return asiaIsFirst
            ? new(eligibleAsiaTargetPrice, direction, NasdaqSessionTargetOrigin.Asia)
            : new(eligibleLondonTargetPrice, direction, NasdaqSessionTargetOrigin.London);
    }
}
