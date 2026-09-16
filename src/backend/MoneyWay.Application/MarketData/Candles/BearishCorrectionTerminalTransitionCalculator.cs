namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Converts terminal facts into the structural transition of an already-active bearish correction.
/// It does not inspect OHLC, infer intrabar chronology, validate structural points, or map strategy workflow gates.
/// </summary>
public sealed class BearishCorrectionTerminalTransitionCalculator
{
    public BearishCorrectionTerminalTransitionResult Evaluate(
        BearishCorrectionTerminalObservationResult observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var transitionKind = observation.WasPriorLhInvalidationObserved
            ? BearishCorrectionTerminalTransitionKind.InvalidateBearishStructure
            : observation.WasNewLowResetObserved
                ? TransitionForReset(observation.BodyDirection)
                : BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection;

        return new(
            transitionKind,
            observation.PreviousFloor,
            observation.ResultingFloor);
    }

    private static BearishCorrectionTerminalTransitionKind TransitionForReset(
        CandleBodyDirection? bodyDirection) =>
        bodyDirection switch
        {
            CandleBodyDirection.Bullish => BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection,
            CandleBodyDirection.Neutral or CandleBodyDirection.Bearish =>
                BearishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyDirection),
                bodyDirection,
                "A reset observation requires a supported candle body direction."),
        };
}
