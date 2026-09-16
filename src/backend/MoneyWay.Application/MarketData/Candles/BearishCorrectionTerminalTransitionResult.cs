namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Reports the structural transition of an already-active bearish correction from one terminal observation.
/// The floor remains a price extreme and is not a validated structural point or executable risk level.
/// </summary>
public sealed record BearishCorrectionTerminalTransitionResult
{
    internal BearishCorrectionTerminalTransitionResult(
        BearishCorrectionTerminalTransitionKind transitionKind,
        decimal previousFloor,
        decimal resultingFloor)
    {
        if (!Enum.IsDefined(transitionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transitionKind),
                transitionKind,
                "The bearish correction terminal transition kind is not supported.");
        }

        if (resultingFloor > previousFloor)
        {
            throw new ArgumentException("The correction-origin floor cannot increase.", nameof(resultingFloor));
        }

        if (transitionKind == BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection
            && resultingFloor != previousFloor)
        {
            throw new ArgumentException("Continuation cannot change the correction-origin floor.", nameof(resultingFloor));
        }

        if (transitionKind is BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection
                or BearishCorrectionTerminalTransitionKind.ResetAndAwaitCorrectionStart
            && resultingFloor == previousFloor)
        {
            throw new ArgumentException("Reset transitions require a lower correction-origin floor.", nameof(resultingFloor));
        }

        TransitionKind = transitionKind;
        PreviousFloor = previousFloor;
        ResultingFloor = resultingFloor;
    }

    public BearishCorrectionTerminalTransitionKind TransitionKind { get; }

    public decimal PreviousFloor { get; }

    public decimal ResultingFloor { get; }

    public bool WasPreviousCandidateDiscarded =>
        TransitionKind != BearishCorrectionTerminalTransitionKind.ContinueExistingCorrection;

    public bool DoesCurrentCandleStartNewBearishCorrection =>
        TransitionKind == BearishCorrectionTerminalTransitionKind.ResetAndStartNewCorrection;
}
