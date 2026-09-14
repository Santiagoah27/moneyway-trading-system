namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Represents a body-based coordinate computed from a caller-selected structural turn.
/// </summary>
public sealed record StructuralTurnBodyCoordinateResult
{
    internal StructuralTurnBodyCoordinateResult(
        decimal structuralPrice,
        StructuralTurnBodyCoordinateSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn body coordinate side is not supported.");
        }

        StructuralPrice = structuralPrice;
        Side = side;
    }

    public decimal StructuralPrice { get; }

    public StructuralTurnBodyCoordinateSide Side { get; }
}
