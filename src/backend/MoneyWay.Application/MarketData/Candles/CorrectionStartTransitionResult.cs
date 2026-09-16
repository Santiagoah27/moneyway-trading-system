using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports pre-start membership and the observed correction-origin price extreme.
/// The extreme is not a validated structural point.
/// </summary>
public sealed record CorrectionStartTransitionResult
{
    internal CorrectionStartTransitionResult(
        CorrectionStartTransitionKind transitionKind,
        CorrectionBoundaryObservationResult observation,
        Candle? firstTurnCandle)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!Enum.IsDefined(transitionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transitionKind), transitionKind, "The correction-start transition kind is not supported.");
        }

        if ((transitionKind == CorrectionStartTransitionKind.StartCorrection) != (firstTurnCandle is not null))
        {
            throw new ArgumentException("Only a starting correction can have a first turn candle.", nameof(firstTurnCandle));
        }

        TransitionKind = transitionKind;
        Side = observation.Side;
        PreviousExtreme = observation.PreviousExtreme;
        ResultingExtreme = observation.ResultingExtreme;
        WasExtremeUpdated = observation.WasExtremeUpdated;
        CurrentCandleMembership = transitionKind == CorrectionStartTransitionKind.StartCorrection
            ? CorrectionTurnCurrentCandleMembership.NewTurn
            : CorrectionTurnCurrentCandleMembership.NoCorrectionTurn;
        FirstTurnCandle = firstTurnCandle;
    }

    public CorrectionStartTransitionKind TransitionKind { get; }

    public CorrectionOriginExtremeSide Side { get; }

    public decimal PreviousExtreme { get; }

    public decimal ResultingExtreme { get; }

    public bool WasExtremeUpdated { get; }

    public CorrectionTurnCurrentCandleMembership CurrentCandleMembership { get; }

    public Candle? FirstTurnCandle { get; }
}
