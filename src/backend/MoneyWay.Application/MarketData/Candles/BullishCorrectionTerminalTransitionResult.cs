namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports the structural transition of an already-active bullish correction from one terminal observation.
/// The ceiling remains a price extreme and is not a validated structural point or executable risk level.
/// </summary>
public sealed record BullishCorrectionTerminalTransitionResult
{
    internal BullishCorrectionTerminalTransitionResult(
        BullishCorrectionTerminalTransitionKind transitionKind,
        decimal previousCeiling,
        decimal resultingCeiling)
    {
        if (!Enum.IsDefined(transitionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transitionKind),
                transitionKind,
                "The bullish correction terminal transition kind is not supported.");
        }

        if (resultingCeiling < previousCeiling)
        {
            throw new ArgumentException("The correction-origin ceiling cannot decrease.", nameof(resultingCeiling));
        }

        if (transitionKind == BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection
            && resultingCeiling != previousCeiling)
        {
            throw new ArgumentException("Continuation cannot change the correction-origin ceiling.", nameof(resultingCeiling));
        }

        if (transitionKind is BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection
                or BullishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart
            && resultingCeiling == previousCeiling)
        {
            throw new ArgumentException("Reset transitions require a higher correction-origin ceiling.", nameof(resultingCeiling));
        }

        TransitionKind = transitionKind;
        PreviousCeiling = previousCeiling;
        ResultingCeiling = resultingCeiling;
    }

    public BullishCorrectionTerminalTransitionKind TransitionKind { get; }

    public decimal PreviousCeiling { get; }

    public decimal ResultingCeiling { get; }

    public bool WasPreviousCandidateDiscarded =>
        TransitionKind != BullishCorrectionTerminalTransitionKind.ContinueExistingCorrection;

    public bool DoesCurrentCandleStartNewBullishCorrection =>
        TransitionKind == BullishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection;
}
