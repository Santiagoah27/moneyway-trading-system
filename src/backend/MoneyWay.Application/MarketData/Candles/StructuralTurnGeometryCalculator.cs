using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Composes body-coordinate and protection-anchor primitives for a structural turn selected by the caller.
/// It does not detect turns or calculate executable stop-loss prices.
/// </summary>
public sealed class StructuralTurnGeometryCalculator
{
    private readonly StructuralTurnBodyCoordinateCalculator bodyCoordinateCalculator = new();
    private readonly StructuralTurnProtectionAnchorCalculator protectionAnchorCalculator = new();

    public StructuralTurnGeometryResult Evaluate(
        IReadOnlyList<Candle> candles,
        StructuralTurnBodyCoordinateSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn body coordinate side is not supported.");
        }

        var bodyCoordinate = bodyCoordinateCalculator.Evaluate(candles, side);
        var protectionAnchor = protectionAnchorCalculator.Evaluate(candles, ToProtectionSide(side));

        return new StructuralTurnGeometryResult(bodyCoordinate, protectionAnchor);
    }

    private static StructuralTurnProtectionSide ToProtectionSide(StructuralTurnBodyCoordinateSide side) =>
        side switch
        {
            StructuralTurnBodyCoordinateSide.Lower => StructuralTurnProtectionSide.Lower,
            StructuralTurnBodyCoordinateSide.Upper => StructuralTurnProtectionSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn body coordinate side is not supported."),
        };
}
