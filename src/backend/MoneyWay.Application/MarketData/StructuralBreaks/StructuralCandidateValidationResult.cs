using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.MarketData.StructuralBreaks;

/// <summary>
/// Reports causal validation evidence for a caller-selected structural candidate and keeps its geometry distinct.
/// </summary>
public sealed record StructuralCandidateValidationResult
{
    internal StructuralCandidateValidationResult(
        StructuralCandidateExtremeSide candidateSide,
        StructuralTurnGeometryResult candidateGeometry,
        StructuralBodyCloseBreakResult breakObservation)
    {
        if (!Enum.IsDefined(candidateSide))
        {
            throw new ArgumentOutOfRangeException(nameof(candidateSide), candidateSide, "The structural candidate extreme side is not supported.");
        }

        ArgumentNullException.ThrowIfNull(candidateGeometry);
        ArgumentNullException.ThrowIfNull(breakObservation);

        CandidateSide = candidateSide;
        CandidateGeometry = candidateGeometry;
        BreakObservation = breakObservation;
    }

    public StructuralCandidateExtremeSide CandidateSide { get; }

    public bool IsValidated => BreakObservation.IsConfirmed;

    public decimal StructuralPrice => CandidateGeometry.StructuralPrice;

    public decimal ProtectionAnchor => CandidateGeometry.ProtectionAnchor;

    public StructuralTurnGeometryResult CandidateGeometry { get; }

    public StructuralBodyCloseBreakResult BreakObservation { get; }
}
