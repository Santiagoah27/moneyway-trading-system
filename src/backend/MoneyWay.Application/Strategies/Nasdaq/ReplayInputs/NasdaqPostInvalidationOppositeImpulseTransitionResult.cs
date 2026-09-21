using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Preserves the advanced opposite impulse and, if correction began, its first-candle transition.
/// The terminal is frozen for the later strict second break when correction starts.
/// </summary>
public sealed class NasdaqPostInvalidationOppositeImpulseTransitionResult
{
    internal NasdaqPostInvalidationOppositeImpulseTransitionResult(
        NasdaqPostInvalidationOppositeImpulseState resultingState,
        CorrectionStartTransitionResult correctionStart)
    {
        ArgumentNullException.ThrowIfNull(resultingState);
        ArgumentNullException.ThrowIfNull(correctionStart);

        if (resultingState.IsTerminalFrozen != (correctionStart.TransitionKind == CorrectionStartTransitionKind.StartCorrection)
            || correctionStart.TransitionKind == CorrectionStartTransitionKind.StartCorrection
                && !ReferenceEquals(correctionStart.FirstTurnCandle, resultingState.LastProcessedCandle))
        {
            throw new ArgumentException("The correction start must use the last processed candle.", nameof(correctionStart));
        }

        ResultingState = resultingState;
        Kind = correctionStart.TransitionKind == CorrectionStartTransitionKind.StartCorrection
            ? NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted
            : NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse;
        CorrectionStart = Kind == NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted
            ? correctionStart
            : null;
    }

    public NasdaqPostInvalidationOppositeImpulseTransitionKind Kind { get; }

    public NasdaqPostInvalidationOppositeImpulseState ResultingState { get; }

    /// <summary>Present only when this candle starts correction; identifies its sole first member.</summary>
    public CorrectionStartTransitionResult? CorrectionStart { get; }
}
