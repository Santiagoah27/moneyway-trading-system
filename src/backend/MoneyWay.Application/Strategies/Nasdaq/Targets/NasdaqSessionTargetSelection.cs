using MoneyWay.Application.MarketData.PriceLevels;

namespace MoneyWay.Application.Strategies.Nasdaq.Targets;

/// <summary>
/// Represents one selected MoneyWay Nasdaq session target without Take Profit, Break-Even, lifecycle, or order semantics.
/// </summary>
public sealed record NasdaqSessionTargetSelection
{
    internal NasdaqSessionTargetSelection(
        decimal price,
        PriceLevelDirection direction,
        NasdaqSessionTargetOrigin origin)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(origin)) throw new ArgumentOutOfRangeException(nameof(origin));

        Price = price;
        Direction = direction;
        Origin = origin;
    }

    public decimal Price { get; }

    public PriceLevelDirection Direction { get; }

    public NasdaqSessionTargetOrigin Origin { get; }
}
