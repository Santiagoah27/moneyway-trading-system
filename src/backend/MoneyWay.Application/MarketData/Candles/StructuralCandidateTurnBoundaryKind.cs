namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Identifies the single causal outcome of a closed candle in an active structural candidate turn.</summary>
public enum StructuralCandidateTurnBoundaryKind
{
    ContinueCandidateTurn = 0,
    ConfirmCandidate = 1,
    ConfirmCandidateAndStartNextCorrection = 2,
    ConfirmCandidateAndAwaitNextCorrection = 3,
    ResetAndStartNewCorrection = 4,
    ResetAndAwaitCorrectionStart = 5,
    StructureInvalidated = 6,
}
