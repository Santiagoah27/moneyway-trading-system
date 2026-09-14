using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Computes a body-based coordinate from candles whose structural-turn membership was selected by the caller.
/// </summary>
public sealed class StructuralTurnBodyCoordinateCalculator
{
    public StructuralTurnBodyCoordinateResult Evaluate(
        IReadOnlyList<Candle> candles,
        StructuralTurnBodyCoordinateSide side)
    {
        ArgumentNullException.ThrowIfNull(candles);

        if (candles.Count == 0)
        {
            throw new ArgumentException("At least one candle is required.", nameof(candles));
        }

        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn body coordinate side is not supported.");
        }

        var coordinate = side == StructuralTurnBodyCoordinateSide.Lower
            ? decimal.MaxValue
            : decimal.MinValue;

        foreach (var candle in candles)
        {
            ArgumentNullException.ThrowIfNull(candle);

            var bodyEdge = side == StructuralTurnBodyCoordinateSide.Lower
                ? decimal.Min(candle.Open, candle.Close)
                : decimal.Max(candle.Open, candle.Close);

            coordinate = side == StructuralTurnBodyCoordinateSide.Lower
                ? decimal.Min(coordinate, bodyEdge)
                : decimal.Max(coordinate, bodyEdge);
        }

        return new StructuralTurnBodyCoordinateResult(coordinate, side);
    }
}
