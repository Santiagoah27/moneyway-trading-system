using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Creates an active opposite correction from exactly one CorrectionStarted transition.</summary>
public sealed class NasdaqPostInvalidationCorrectionInitializer
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public NasdaqPostInvalidationCorrectionState Initialize(
        NasdaqPostInvalidationOppositeImpulseTransitionResult transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        if (transition.Kind != NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted
            || transition.CorrectionStart is null
            || !transition.ResultingState.IsTerminalFrozen)
        {
            throw new ArgumentException("An active opposite correction requires a frozen CorrectionStarted transition.", nameof(transition));
        }

        var impulse = transition.ResultingState;
        var startCandle = transition.CorrectionStart.FirstTurnCandle;
        if (startCandle is null || !ReferenceEquals(startCandle, impulse.LastProcessedCandle))
        {
            throw new ArgumentException("The correction start must be the last processed impulse candle.", nameof(transition));
        }

        var candidateSide = impulse.TerminalSide switch
        {
            StructuralTurnBodyCoordinateSide.Lower => StructuralCandidateExtremeSide.Upper,
            StructuralTurnBodyCoordinateSide.Upper => StructuralCandidateExtremeSide.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(transition), "The impulse terminal side is not supported."),
        };
        var geometrySide = candidateSide == StructuralCandidateExtremeSide.Upper
            ? StructuralTurnBodyCoordinateSide.Upper
            : StructuralTurnBodyCoordinateSide.Lower;
        var correctionGeometry = geometryCalculator.Evaluate([startCandle], geometrySide);

        return new NasdaqPostInvalidationCorrectionState(
            impulse.Episode,
            impulse.InvalidatingCandle,
            impulse.TerminalSide,
            candidateSide,
            impulse.OriginGeometry,
            impulse.ProvisionalTerminal,
            startCandle,
            correctionGeometry);
    }
}
