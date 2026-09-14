namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports a mechanical correction-origin price-extreme update.
/// It does not represent structural swing validation or a correction-state transition.
/// </summary>
public sealed record CorrectionOriginExtremeResult
{
    internal CorrectionOriginExtremeResult(
        decimal previousExtreme,
        decimal resultingExtreme,
        CorrectionOriginExtremeSide side)
    {
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));

        PreviousExtreme = previousExtreme;
        ResultingExtreme = resultingExtreme;
        Side = side;
    }

    public decimal PreviousExtreme { get; }
    public decimal ResultingExtreme { get; }
    public CorrectionOriginExtremeSide Side { get; }
    public bool WasUpdated => ResultingExtreme != PreviousExtreme;
}
