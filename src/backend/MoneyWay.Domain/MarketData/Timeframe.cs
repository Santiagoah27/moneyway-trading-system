using System.Globalization;

namespace MoneyWay.Domain.MarketData;

/// <summary>
/// Represents the logical candle timeframe without inferring session alignment.
/// </summary>
public sealed record Timeframe
{
    public Timeframe(int amount, TimeframeUnit unit)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }

        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Timeframe unit is not supported.");
        }

        Amount = amount;
        Unit = unit;
    }

    public int Amount { get; }

    public TimeframeUnit Unit { get; }

    public override string ToString()
    {
        var suffix = Unit switch
        {
            TimeframeUnit.Minute => "m",
            TimeframeUnit.Hour => "h",
            TimeframeUnit.Day => "d",
            TimeframeUnit.Week => "w",
            _ => throw new InvalidOperationException("Unsupported timeframe unit."),
        };

        return string.Concat(Amount.ToString(CultureInfo.InvariantCulture), suffix);
    }
}
