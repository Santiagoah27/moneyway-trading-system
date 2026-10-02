using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>The correction/candidate calculation extracted from the post-invalidation transition; consumes one candle.</summary>
public sealed class CorrectionCandidateTurnCalculator
{
    private readonly CorrectionBodyDirectionCalculator correctionDirectionCalculator = new();
    private readonly StructuralTurnBodyCoordinateCalculator bodyCalculator = new();
    private readonly StructuralTurnProtectionAnchorCalculator protectionCalculator = new();
    private readonly StructuralCandidateExtremeCalculator extremeCalculator = new();

    public CorrectionCandidateTurnResult Evaluate(IReadOnlyList<Candle> currentTurnCandles,
        StructuralTurnGeometryResult currentGeometry, StructuralCandidateExtremeSide candidateSide,
        Candle candle, CandleBodyDirection bodyDirection)
    {
        ArgumentNullException.ThrowIfNull(currentTurnCandles);
        ArgumentNullException.ThrowIfNull(currentGeometry);
        ArgumentNullException.ThrowIfNull(candle);
        if (currentTurnCandles.Count == 0 || currentTurnCandles.Any(member => member is null))
            throw new ArgumentException("An active correction requires non-null members.", nameof(currentTurnCandles));
        if (!Enum.IsDefined(candidateSide)) throw new ArgumentOutOfRangeException(nameof(candidateSide));
        if (!Enum.IsDefined(bodyDirection)) throw new ArgumentOutOfRangeException(nameof(bodyDirection));
        var geometrySide = candidateSide == StructuralCandidateExtremeSide.Upper
            ? StructuralTurnBodyCoordinateSide.Upper : StructuralTurnBodyCoordinateSide.Lower;
        if (currentGeometry.Side != geometrySide)
            throw new ArgumentException("The correction geometry must match the candidate side.", nameof(currentGeometry));
        var previous = currentTurnCandles[^1];
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol || candle.Timeframe != previous.Timeframe
            || candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
            throw new ArgumentException("The consumed candle must follow the correction in the same series without overlap.", nameof(candle));

        var correctionExtremeSide = candidateSide == StructuralCandidateExtremeSide.Upper
            ? CorrectionOriginExtremeSide.Floor : CorrectionOriginExtremeSide.Ceiling;
        var protectionSide = candidateSide == StructuralCandidateExtremeSide.Upper
            ? StructuralTurnProtectionSide.Upper : StructuralTurnProtectionSide.Lower;
        if (bodyDirection == CandleBodyDirection.Neutral
            || correctionDirectionCalculator.Evaluate(correctionExtremeSide, bodyDirection))
        {
            var members = currentTurnCandles.Append(candle).ToArray();
            var observedBody = bodyCalculator.Evaluate([candle], currentGeometry.Side);
            var observedProtection = protectionCalculator.Evaluate([candle], protectionSide);
            var geometry = new StructuralTurnGeometryResult(
                new StructuralTurnBodyCoordinateResult(
                    extremeCalculator.Evaluate(currentGeometry.StructuralPrice,
                        observedBody.StructuralPrice, candidateSide).ResultingExtreme,
                    currentGeometry.Side),
                new StructuralTurnProtectionAnchorResult(
                    extremeCalculator.Evaluate(currentGeometry.ProtectionAnchor,
                        observedProtection.ProtectionAnchor, candidateSide).ResultingExtreme,
                    protectionSide));
            return new CorrectionCandidateTurnResult.ContinuingCorrection(members, geometry, candidateSide, candle, bodyDirection);
        }

        var terminalProtection = protectionCalculator.Evaluate([candle], protectionSide).ProtectionAnchor;
        var resultingProtection = extremeCalculator.Evaluate(
            currentGeometry.ProtectionAnchor, terminalProtection, candidateSide).ResultingExtreme;
        var candidateGeometry = new StructuralTurnGeometryResult(
            new StructuralTurnBodyCoordinateResult(currentGeometry.StructuralPrice, currentGeometry.Side),
            new StructuralTurnProtectionAnchorResult(resultingProtection, protectionSide));
        return new CorrectionCandidateTurnResult.CandidateProvisional(currentTurnCandles, candidateGeometry, candidateSide, candle, bodyDirection);
    }
}
