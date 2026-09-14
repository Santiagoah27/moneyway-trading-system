namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports observable candle facts relative to a supplied correction-origin floor or ceiling.
/// It does not represent a correction-state transition or structural validation.
/// </summary>
public sealed record CorrectionBoundaryObservationResult
{
    internal CorrectionBoundaryObservationResult(
        CorrectionOriginExtremeResult extremeObservation,
        CandleBodyDirection bodyDirection)
    {
        ArgumentNullException.ThrowIfNull(extremeObservation);

        if (!Enum.IsDefined(bodyDirection))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyDirection), bodyDirection, "The candle body direction is not supported.");
        }

        Side = extremeObservation.Side;
        PreviousExtreme = extremeObservation.PreviousExtreme;
        ResultingExtreme = extremeObservation.ResultingExtreme;
        WasExtremeUpdated = extremeObservation.WasUpdated;
        BodyDirection = bodyDirection;
    }

    public CorrectionOriginExtremeSide Side { get; }

    public decimal PreviousExtreme { get; }

    public decimal ResultingExtreme { get; }

    public bool WasExtremeUpdated { get; }

    public CandleBodyDirection BodyDirection { get; }
}
