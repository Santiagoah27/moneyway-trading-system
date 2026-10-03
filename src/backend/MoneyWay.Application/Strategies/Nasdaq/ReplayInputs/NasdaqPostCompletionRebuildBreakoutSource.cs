using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed causal origins of a rebuild breakout, preserving the exact consumed transition and route-specific lineage.</summary>
public abstract class NasdaqPostCompletionRebuildBreakoutSource
{
    private NasdaqPostCompletionRebuildBreakoutSource() { }

    public abstract NasdaqPostCompletionEpisode Episode { get; }
    public abstract NasdaqPostCompletionStructuralPair ActivePair { get; }
    public abstract StructuralCandidateExtremeSide CandidateSide { get; }
    public abstract StructuralTurnGeometryResult FrozenBreakoutTerminal { get; }
    public abstract StructuralCandidateExtremeResult Migration { get; }
    public abstract StructuralBodyCloseBreakResult Breakout { get; }
    public abstract CandleBodyDirection BodyDirection { get; }
    public abstract Candle BreakoutCandle { get; }
    public abstract Candle MarketCursor { get; }
    public abstract Candle PriorMigrationCandle { get; }
    public abstract Candle EffectiveMigrationCandle { get; }
    public abstract decimal PreviousProtectionAnchor { get; }
    public abstract decimal EffectiveProtectionAnchor { get; }
    public abstract NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind { get; }

    public sealed class Pending : NasdaqPostCompletionRebuildBreakoutSource
    {
        internal Pending(NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected breakoutResult)
            => BreakoutResult = breakoutResult ?? throw new ArgumentNullException(nameof(breakoutResult));

        public NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected BreakoutResult { get; }
        public override NasdaqPostCompletionEpisode Episode => BreakoutResult.Episode;
        public override NasdaqPostCompletionStructuralPair ActivePair => BreakoutResult.ActivePair;
        public override StructuralCandidateExtremeSide CandidateSide => BreakoutResult.CandidateSide;
        public override StructuralTurnGeometryResult FrozenBreakoutTerminal => BreakoutResult.FrozenBreakoutTerminal;
        public override StructuralCandidateExtremeResult Migration => BreakoutResult.Migration;
        public override StructuralBodyCloseBreakResult Breakout => BreakoutResult.Breakout;
        public override CandleBodyDirection BodyDirection => BreakoutResult.BodyDirection;
        public override Candle BreakoutCandle => BreakoutResult.BreakoutCandle;
        public override Candle MarketCursor => BreakoutResult.MarketCursor;
        public override Candle PriorMigrationCandle => BreakoutResult.SourceState.MigrationCandle;
        public override Candle EffectiveMigrationCandle => BreakoutResult.EffectiveMigrationCandle;
        public override decimal PreviousProtectionAnchor => BreakoutResult.PreviousProtectionAnchor;
        public override decimal EffectiveProtectionAnchor => BreakoutResult.EffectiveProtectionAnchor;
        public override NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind => BreakoutResult.CollisionKind;
    }

    public sealed class RebuiltTracking : NasdaqPostCompletionRebuildBreakoutSource
    {
        internal RebuiltTracking(NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected breakoutResult)
            => BreakoutResult = breakoutResult ?? throw new ArgumentNullException(nameof(breakoutResult));

        public NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected BreakoutResult { get; }
        public override NasdaqPostCompletionEpisode Episode => BreakoutResult.Episode;
        public override NasdaqPostCompletionStructuralPair ActivePair => BreakoutResult.ActivePair;
        public override StructuralCandidateExtremeSide CandidateSide => BreakoutResult.CandidateSide;
        public override StructuralTurnGeometryResult FrozenBreakoutTerminal => BreakoutResult.FrozenBreakoutTerminal;
        public override StructuralCandidateExtremeResult Migration => BreakoutResult.Migration;
        public override StructuralBodyCloseBreakResult Breakout => BreakoutResult.Breakout;
        public override CandleBodyDirection BodyDirection => BreakoutResult.BodyDirection;
        public override Candle BreakoutCandle => BreakoutResult.BreakoutCandle;
        public override Candle MarketCursor => BreakoutResult.MarketCursor;
        public override Candle PriorMigrationCandle => BreakoutResult.SourceState.MigrationCandle;
        public override Candle EffectiveMigrationCandle => BreakoutResult.EffectiveMigrationCandle;
        public override decimal PreviousProtectionAnchor => BreakoutResult.PreviousProtectionAnchor;
        public override decimal EffectiveProtectionAnchor => BreakoutResult.EffectiveProtectionAnchor;
        public override NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind => BreakoutResult.CollisionKind;
        public Candle FirstTurnCandle => BreakoutResult.FirstTurnCandle;
    }
}
