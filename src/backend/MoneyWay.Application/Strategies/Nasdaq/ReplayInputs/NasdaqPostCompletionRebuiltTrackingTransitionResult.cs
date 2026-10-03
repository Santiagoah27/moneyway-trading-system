using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed causal outcomes of one consumed candle; no tracking, evidence or completion state is materialized.</summary>
public abstract class NasdaqPostCompletionRebuiltTrackingTransitionResult
{
    private NasdaqPostCompletionRebuiltTrackingTransitionResult(NasdaqPostCompletionRebuiltTrackingState sourceState,
        StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout, CandleBodyDirection bodyDirection)
    {
        SourceState = sourceState;
        Migration = migration;
        Breakout = breakout;
        BodyDirection = bodyDirection;
    }

    public NasdaqPostCompletionRebuiltTrackingState SourceState { get; }
    /// <summary>The incoming observation against the pre-candle known anchor, distinct from the source migration event.</summary>
    public StructuralCandidateExtremeResult Migration { get; }
    public StructuralBodyCloseBreakResult Breakout { get; }
    public CandleBodyDirection BodyDirection { get; }
    public Candle MarketCursor => Breakout.Candle!;
    public NasdaqPostCompletionEpisode Episode => SourceState.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => SourceState.ActivePair;
    public StructuralCandidateExtremeSide CandidateSide => SourceState.CandidateSide;
    public StructuralTurnGeometryResult FrozenBreakoutTerminal => SourceState.FrozenBreakoutTerminal;
    public decimal PreviousProtectionAnchor => SourceState.KnownProtectionAnchor;
    public decimal EffectiveProtectionAnchor => Migration.ResultingExtreme;
    /// <summary>The latest observed strict wick extension; source lineage retains the prior migration and first turn for collision completion.</summary>
    public Candle EffectiveMigrationCandle => Migration.WasReplaced ? MarketCursor : SourceState.MigrationCandle;

    public sealed class TrackingContinues : NasdaqPostCompletionRebuiltTrackingTransitionResult
    {
        internal TrackingContinues(NasdaqPostCompletionRebuiltTrackingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body) : base(source, migration, breakout, body) { }
        public Candle FirstTurnCandle => SourceState.FirstTurnCandle;
    }

    public sealed class PendingReset : NasdaqPostCompletionRebuiltTrackingTransitionResult
    {
        internal PendingReset(NasdaqPostCompletionRebuiltTrackingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body) : base(source, migration, breakout, body) { }
    }

    public sealed class TrackingRestarted : NasdaqPostCompletionRebuiltTrackingTransitionResult
    {
        internal TrackingRestarted(NasdaqPostCompletionRebuiltTrackingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body) : base(source, migration, breakout, body) { }
        public Candle FirstTurnCandle => MarketCursor;
    }

    public sealed class BreakoutDetected : NasdaqPostCompletionRebuiltTrackingTransitionResult
    {
        internal BreakoutDetected(NasdaqPostCompletionRebuiltTrackingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body,
            NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind) : base(source, migration, breakout, body)
            => CollisionKind = collisionKind;
        public Candle FirstTurnCandle => SourceState.FirstTurnCandle;
        public Candle BreakoutCandle => MarketCursor;
        public NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind { get; }
    }
}
