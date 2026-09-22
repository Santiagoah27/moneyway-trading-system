using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Advances one active opposite correction with one later closed H4 candle.</summary>
public sealed class NasdaqPostInvalidationCorrectionTransitionCalculator
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();
    private readonly CorrectionBodyDirectionCalculator correctionDirectionCalculator = new();
    private readonly StructuralTurnBodyCoordinateCalculator bodyCalculator = new();
    private readonly StructuralTurnProtectionAnchorCalculator protectionCalculator = new();
    private readonly StructuralCandidateExtremeCalculator extremeCalculator = new();

    public NasdaqPostInvalidationCorrectionTransitionResult Evaluate(
        NasdaqPostInvalidationCorrectionState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);

        var previous = current.LastProcessedCandle;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || candle.Timeframe != previous.Timeframe)
        {
            throw new ArgumentException("The next candle must belong to the same H4 series.", nameof(candle));
        }

        if (candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The next closed candle must follow the last processed candle without overlap.", nameof(candle));
        }

        var candidateSide = current.CandidateSide;
        var correctionExtremeSide = candidateSide == StructuralCandidateExtremeSide.Upper
            ? CorrectionOriginExtremeSide.Floor
            : CorrectionOriginExtremeSide.Ceiling;
        var protectionSide = candidateSide == StructuralCandidateExtremeSide.Upper
            ? StructuralTurnProtectionSide.Upper
            : StructuralTurnProtectionSide.Lower;
        var bodyDirection = bodyDirectionCalculator.Evaluate(candle);
        if (bodyDirection == CandleBodyDirection.Neutral
            || correctionDirectionCalculator.Evaluate(correctionExtremeSide, bodyDirection))
        {
            var members = current.CorrectionTurnCandles.Append(candle).ToArray();
            var observedBody = bodyCalculator.Evaluate([candle], current.CorrectionGeometry.Side);
            var observedProtection = protectionCalculator.Evaluate([candle], protectionSide);
            var geometry = new StructuralTurnGeometryResult(
                new StructuralTurnBodyCoordinateResult(
                    extremeCalculator.Evaluate(current.CorrectionGeometry.StructuralPrice,
                        observedBody.StructuralPrice, candidateSide).ResultingExtreme,
                    current.CorrectionGeometry.Side),
                new StructuralTurnProtectionAnchorResult(
                    extremeCalculator.Evaluate(current.CorrectionGeometry.ProtectionAnchor,
                        observedProtection.ProtectionAnchor, candidateSide).ResultingExtreme,
                    protectionSide));
            var state = new NasdaqPostInvalidationCorrectionState(
                current.Episode, current.InvalidatingCandle, current.ImpulseTerminalSide, candidateSide,
                current.OriginGeometry, current.FrozenImpulseTerminal, current.CorrectionStartCandle,
                members, geometry, candle);
            return NasdaqPostInvalidationCorrectionTransitionResult.Continue(state);
        }

        var terminalProtection = protectionCalculator.Evaluate([candle], protectionSide).ProtectionAnchor;
        var resultingProtection = extremeCalculator.Evaluate(
            current.CorrectionGeometry.ProtectionAnchor, terminalProtection, candidateSide).ResultingExtreme;
        var candidateGeometry = new StructuralTurnGeometryResult(
            new StructuralTurnBodyCoordinateResult(current.CorrectionGeometry.StructuralPrice,
                current.CorrectionGeometry.Side),
            new StructuralTurnProtectionAnchorResult(resultingProtection, protectionSide));
        var candidate = new NasdaqPostInvalidationCandidateState(current, candidateGeometry, candle);
        return NasdaqPostInvalidationCorrectionTransitionResult.FormCandidate(candidate);
    }
}
