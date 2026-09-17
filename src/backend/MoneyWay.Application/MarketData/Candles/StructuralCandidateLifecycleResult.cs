using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Exposes the next candidate and correction lifecycle directly from one authoritative boundary decision.</summary>
public sealed class StructuralCandidateLifecycleResult
{
    internal StructuralCandidateLifecycleResult(StructuralCandidateTurnBoundaryResult boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        Boundary = boundary;
        CandidateStatus = boundary.Kind switch
        {
            StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn => StructuralCandidateLifecycleStatus.Active,
            StructuralCandidateTurnBoundaryKind.ConfirmCandidate
                or StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection
                or StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndAwaitNextCorrection => StructuralCandidateLifecycleStatus.Validated,
            StructuralCandidateTurnBoundaryKind.ResetAndStartNewCorrection
                or StructuralCandidateTurnBoundaryKind.ResetAndAwaitCorrectionStart => StructuralCandidateLifecycleStatus.Discarded,
            StructuralCandidateTurnBoundaryKind.StructureInvalidated => StructuralCandidateLifecycleStatus.StructureInvalidated,
            _ => throw new ArgumentOutOfRangeException(nameof(boundary)),
        };

        CompletedCandidateTurnCandles = CandidateStatus == StructuralCandidateLifecycleStatus.Validated
            ? boundary.PreviousCandidateTurnCandles
            : Array.Empty<Candle>();
    }

    public StructuralCandidateTurnBoundaryResult Boundary { get; }

    public StructuralCandidateLifecycleStatus CandidateStatus { get; }

    public CorrectionTurnLifecycleResult Lifecycle => Boundary.Lifecycle;

    /// <summary>The immutable old-turn membership when its candidate was validated; empty otherwise.</summary>
    public IReadOnlyList<Candle> CompletedCandidateTurnCandles { get; }

    public StructuralCandidateValidationResult? ValidatedCandidate =>
        CandidateStatus == StructuralCandidateLifecycleStatus.Validated ? Boundary.CandidateValidation : null;

    public decimal ResultingOriginExtreme => Boundary.ResultingOriginExtreme;

    public decimal? NewStructuralExtreme => Boundary.NewStructuralExtreme;

    public decimal? NewActivePairCandidatePrice => Boundary.NewActivePairCandidatePrice;
}
