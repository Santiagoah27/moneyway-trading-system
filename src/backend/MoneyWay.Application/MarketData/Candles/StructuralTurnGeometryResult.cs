namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Represents the separate body-based structural price and wick-based protection anchor of a caller-selected structural turn.
/// </summary>
public sealed record StructuralTurnGeometryResult
{
    internal StructuralTurnGeometryResult(
        StructuralTurnBodyCoordinateResult bodyCoordinate,
        StructuralTurnProtectionAnchorResult protectionAnchor)
    {
        ArgumentNullException.ThrowIfNull(bodyCoordinate);
        ArgumentNullException.ThrowIfNull(protectionAnchor);

        if (bodyCoordinate.Side != ToBodyCoordinateSide(protectionAnchor.Side))
        {
            throw new ArgumentException("The structural turn geometry sides must match.", nameof(protectionAnchor));
        }

        Side = bodyCoordinate.Side;
        StructuralPrice = bodyCoordinate.StructuralPrice;
        ProtectionAnchor = protectionAnchor.ProtectionAnchor;
    }

    public StructuralTurnBodyCoordinateSide Side { get; }

    public decimal StructuralPrice { get; }

    public decimal ProtectionAnchor { get; }

    private static StructuralTurnBodyCoordinateSide ToBodyCoordinateSide(StructuralTurnProtectionSide side) =>
        side switch
        {
            StructuralTurnProtectionSide.Lower => StructuralTurnBodyCoordinateSide.Lower,
            StructuralTurnProtectionSide.Upper => StructuralTurnBodyCoordinateSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn protection side is not supported."),
        };
}
