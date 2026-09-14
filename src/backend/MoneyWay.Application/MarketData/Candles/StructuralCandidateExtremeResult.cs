namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports the replacement of a caller-supplied candidate structural extreme without identifying a candle or turn.
/// </summary>
public sealed record StructuralCandidateExtremeResult
{
    internal StructuralCandidateExtremeResult(
        decimal previousExtreme,
        decimal resultingExtreme,
        StructuralCandidateExtremeSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The structural candidate extreme side is not supported.");
        }

        PreviousExtreme = previousExtreme;
        ResultingExtreme = resultingExtreme;
        Side = side;
    }

    public decimal PreviousExtreme { get; }

    public decimal ResultingExtreme { get; }

    public StructuralCandidateExtremeSide Side { get; }

    public bool WasReplaced => ResultingExtreme != PreviousExtreme;
}
