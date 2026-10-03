using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A causally started provisional rebuilt turn; definitive membership and rebuilt geometry remain unknown.</summary>
public sealed class NasdaqPostCompletionRebuiltTrackingState
{
    internal NasdaqPostCompletionRebuiltTrackingState(NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted sourceTransition)
    {
        ArgumentNullException.ThrowIfNull(sourceTransition);
        SourceTransition = sourceTransition;
    }

    public NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted SourceTransition { get; }
    public NasdaqPostCompletionRebuildPendingState SourcePending => SourceTransition.SourceState;
    /// <summary>The superseded candidate is retained only as provenance.</summary>
    public NasdaqPostCompletionCandidateState SourceCandidate => SourcePending.SourceCandidate;
    public NasdaqPostCompletionEpisode Episode => SourceTransition.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => SourceTransition.ActivePair;
    public StructuralCandidateExtremeSide CandidateSide => SourceTransition.CandidateSide;
    public StructuralTurnGeometryResult FrozenBreakoutTerminal => SourceTransition.FrozenBreakoutTerminal;
    /// <summary>The effective strict migration event, retaining the original event when the turn did not migrate.</summary>
    public StructuralCandidateExtremeResult Migration => SourceTransition.Migration.WasReplaced ? SourceTransition.Migration : SourcePending.Migration;
    public Candle MigrationCandle => SourceTransition.EffectiveMigrationCandle;
    /// <summary>A known migrated wick, not a definitive rebuilt vertex's protection coordinate.</summary>
    public decimal KnownProtectionAnchor => SourceTransition.EffectiveProtectionAnchor;
    public Candle FirstTurnCandle => SourceTransition.FirstTurnCandle;
    /// <summary>Materialization retains the already-consumed first-turn cursor without processing another candle.</summary>
    public Candle MarketCursor => SourceTransition.MarketCursor;
}
