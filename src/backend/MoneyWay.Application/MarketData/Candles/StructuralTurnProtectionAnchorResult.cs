namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Represents a wick-based protection anchor computed from a caller-selected structural turn.
/// </summary>
public sealed record StructuralTurnProtectionAnchorResult
{
    internal StructuralTurnProtectionAnchorResult(
        decimal protectionAnchor,
        StructuralTurnProtectionSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn protection side is not supported.");
        }

        ProtectionAnchor = protectionAnchor;
        Side = side;
    }

    public decimal ProtectionAnchor { get; }

    public StructuralTurnProtectionSide Side { get; }
}
