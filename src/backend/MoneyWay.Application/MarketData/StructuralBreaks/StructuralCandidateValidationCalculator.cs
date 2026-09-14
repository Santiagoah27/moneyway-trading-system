using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.StructuralBreaks;

/// <summary>
/// Composes candidate-turn geometry with a causal closed-body break observed at the current replay boundary.
/// It does not detect candidates, select turn membership, or bootstrap structural references.
/// </summary>
public sealed class StructuralCandidateValidationCalculator
{
    private readonly StructuralBodyCloseBreakCalculator bodyCloseBreakCalculator = new();
    private readonly StructuralTurnGeometryCalculator turnGeometryCalculator = new();

    public StructuralCandidateValidationResult EvaluateCurrentBoundary(
        StrategyReplayContext context,
        Timeframe timeframe,
        decimal priorStructuralReference,
        IReadOnlyList<Candle> candidateTurnCandles,
        StructuralCandidateExtremeSide candidateSide)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(candidateTurnCandles);

        if (!Enum.IsDefined(candidateSide))
        {
            throw new ArgumentOutOfRangeException(nameof(candidateSide), candidateSide, "The structural candidate extreme side is not supported.");
        }

        var candidateGeometry = turnGeometryCalculator.Evaluate(
            candidateTurnCandles,
            ToGeometrySide(candidateSide));
        var breakObservation = bodyCloseBreakCalculator.EvaluateCurrentBoundary(
            context,
            timeframe,
            priorStructuralReference,
            ToBreakDirection(candidateSide));

        return new StructuralCandidateValidationResult(candidateSide, candidateGeometry, breakObservation);
    }

    private static StructuralTurnBodyCoordinateSide ToGeometrySide(StructuralCandidateExtremeSide side) =>
        side switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Lower,
            StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "The structural candidate extreme side is not supported."),
        };

    private static StructuralBreakDirection ToBreakDirection(StructuralCandidateExtremeSide side) =>
        side switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralBreakDirection.Upper,
            StructuralCandidateExtremeSide.Upper => StructuralBreakDirection.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "The structural candidate extreme side is not supported."),
        };
}
