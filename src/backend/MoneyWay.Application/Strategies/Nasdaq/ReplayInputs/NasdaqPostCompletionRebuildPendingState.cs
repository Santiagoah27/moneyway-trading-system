using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A consumed migration-only reset; the superseded candidate is provenance, and definitive rebuilt membership remains human-assisted.</summary>
public sealed class NasdaqPostCompletionRebuildPendingState
{
    internal NasdaqPostCompletionRebuildPendingState(NasdaqPostCompletionCandidateLifecycleResult lifecycleResult,
        NasdaqCandidateLifecycleDecision.RebuildPending decision)
    {
        LifecycleResult = lifecycleResult;
        Decision = decision;
        EvidenceContext = new(Episode, MigrationCandle.OpenTimeUtc, CandidateSide);
    }

    public NasdaqPostCompletionCandidateLifecycleResult LifecycleResult { get; }
    public NasdaqCandidateLifecycleDecision.RebuildPending Decision { get; }
    public NasdaqPostCompletionRebuildContext EvidenceContext { get; }
    /// <summary>The superseded provisional candidate, not a current definitive rebuilt candidate.</summary>
    public NasdaqPostCompletionCandidateState SourceCandidate => LifecycleResult.SourceState;
    public NasdaqPostCompletionEpisode Episode => SourceCandidate.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => SourceCandidate.ActivePair;
    public StructuralCandidateValidationResult PreviousProtectedTurn => ActivePair.ProtectedTurn;
    public StructuralTurnGeometryResult FrozenBreakoutTerminal => Decision.FrozenTerminal;
    public StructuralCandidateExtremeSide CandidateSide => Decision.CandidateSide;
    public StructuralCandidateExtremeResult Migration => Decision.Migration;
    /// <summary>The known migrated wick does not establish a rebuilt body price or complete geometry.</summary>
    public decimal KnownProtectionAnchor => Decision.EffectiveProtectionAnchor;
    public StructuralBodyCloseBreakResult BoundaryObservation => Decision.Breakout;
    public Candle MigrationCandle => Decision.MarketCursor;
    public CandleBodyDirection BodyDirection => Decision.BodyDirection;
    /// <summary>Frozen at the already-consumed migration candle; evidence does not advance market time.</summary>
    public Candle MarketCursor => LifecycleResult.MarketCursor;
}
