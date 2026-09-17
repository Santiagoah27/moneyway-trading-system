using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Preserves the final boundary decision, turn membership, and any causal structural rollover.</summary>
public sealed record StructuralCandidateTurnBoundaryResult
{
    internal StructuralCandidateTurnBoundaryResult(
        StructuralCandidateTurnBoundaryKind kind,
        Candle currentCandle,
        StructuralCandidateValidationResult candidateValidation,
        CorrectionTurnLifecycleResult lifecycle,
        decimal resultingOriginExtreme,
        decimal? newStructuralExtreme)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(currentCandle);
        ArgumentNullException.ThrowIfNull(candidateValidation);
        ArgumentNullException.ThrowIfNull(lifecycle);

        var confirms = kind is StructuralCandidateTurnBoundaryKind.ConfirmCandidate
            or StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection
            or StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndAwaitNextCorrection;
        var compound = kind is StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection
            or StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndAwaitNextCorrection;
        var expectedMembership = kind switch
        {
            StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn => CorrectionTurnCurrentCandleMembership.ExistingTurn,
            StructuralCandidateTurnBoundaryKind.ConfirmCandidateAndStartNextCorrection
                or StructuralCandidateTurnBoundaryKind.ResetAndStartNewCorrection => CorrectionTurnCurrentCandleMembership.NewTurn,
            _ => CorrectionTurnCurrentCandleMembership.NoCorrectionTurn,
        };

        if (candidateValidation.IsValidated != confirms
            || (newStructuralExtreme is not null) != compound
            || lifecycle.CurrentCandleMembership != expectedMembership
            || (!lifecycle.WasPreviousTurnTerminated && kind != StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn)
            || (lifecycle.Disposition == CorrectionTurnLifecycleDisposition.StructureInvalidated)
                != (kind == StructuralCandidateTurnBoundaryKind.StructureInvalidated))
        {
            throw new ArgumentException("The boundary decision must match validation, rollover, and turn membership.", nameof(kind));
        }

        Kind = kind;
        CurrentCandle = currentCandle;
        CandidateValidation = candidateValidation;
        Lifecycle = lifecycle;
        ResultingOriginExtreme = resultingOriginExtreme;
        NewStructuralExtreme = newStructuralExtreme;
    }

    public StructuralCandidateTurnBoundaryKind Kind { get; }
    public Candle CurrentCandle { get; }
    public StructuralCandidateValidationResult CandidateValidation { get; }
    public CorrectionTurnLifecycleResult Lifecycle { get; }
    public decimal ResultingOriginExtreme { get; }

    /// <summary>The confirming candle's HH/LL-side price in a compound event; null otherwise.</summary>
    public decimal? NewStructuralExtreme { get; }

    /// <summary>The newly validated opposite point of the active pair in a compound event.</summary>
    public decimal? NewActivePairCandidatePrice => NewStructuralExtreme is null
        ? null
        : CandidateValidation.StructuralPrice;
}
