namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Converts terminal facts into the structural transition of an already-active bullish correction.
/// It does not inspect OHLC, infer intrabar chronology, validate structural points, or map strategy workflow gates.
/// </summary>
public sealed class BullishCorrectionTerminalTransitionCalculator
{
    public BullishCorrectionTerminalTransitionResult Evaluate(
        BullishCorrectionTerminalObservationResult observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var transitionKind = observation.WasPriorHlInvalidationObserved
            ? BullishCorrectionTerminalTransitionKind.InvalidateBullishStructure
            : observation.WasNewHighResetObserved
                ? TransitionForReset(observation.BodyDirection)
                : BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection;

        return new(
            transitionKind,
            observation.PreviousCeiling,
            observation.ResultingCeiling);
    }

    private static BullishCorrectionTerminalTransitionKind TransitionForReset(
        CandleBodyDirection? bodyDirection) =>
        bodyDirection switch
        {
            CandleBodyDirection.Bearish => BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection,
            CandleBodyDirection.Neutral or CandleBodyDirection.Bullish =>
                BullishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyDirection),
                bodyDirection,
                "A reset observation requires a supported candle body direction."),
        };
}
