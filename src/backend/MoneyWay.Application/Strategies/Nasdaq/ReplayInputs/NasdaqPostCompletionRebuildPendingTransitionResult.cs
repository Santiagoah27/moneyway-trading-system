using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed causal outcomes of one consumed candle; no tracking, evidence or completion state is materialized.</summary>
public abstract class NasdaqPostCompletionRebuildPendingTransitionResult
{
    private NasdaqPostCompletionRebuildPendingTransitionResult(NasdaqPostCompletionRebuildPendingState sourceState,
        StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout, CandleBodyDirection bodyDirection)
    {
        SourceState = sourceState;
        Migration = migration;
        Breakout = breakout;
        BodyDirection = bodyDirection;
    }

    public NasdaqPostCompletionRebuildPendingState SourceState { get; }
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
    /// <summary>The latest observed strict wick extension; source lineage retains the prior migration for collision completion.</summary>
    public Candle EffectiveMigrationCandle => Migration.WasReplaced ? MarketCursor : SourceState.MigrationCandle;

    public sealed class PendingContinues : NasdaqPostCompletionRebuildPendingTransitionResult
    {
        internal PendingContinues(NasdaqPostCompletionRebuildPendingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body) : base(source, migration, breakout, body) { }
    }

    public sealed class PendingReset : NasdaqPostCompletionRebuildPendingTransitionResult
    {
        internal PendingReset(NasdaqPostCompletionRebuildPendingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body) : base(source, migration, breakout, body) { }
    }

    public sealed class TrackingStarted : NasdaqPostCompletionRebuildPendingTransitionResult
    {
        internal TrackingStarted(NasdaqPostCompletionRebuildPendingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body) : base(source, migration, breakout, body) { }
        public Candle FirstTurnCandle => MarketCursor;
    }

    public sealed class BreakoutDetected : NasdaqPostCompletionRebuildPendingTransitionResult
    {
        internal BreakoutDetected(NasdaqPostCompletionRebuildPendingState source, StructuralCandidateExtremeResult migration,
            StructuralBodyCloseBreakResult breakout, CandleBodyDirection body,
            NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind) : base(source, migration, breakout, body)
            => CollisionKind = collisionKind;
        public Candle BreakoutCandle => MarketCursor;
        public NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind { get; }
    }
}
