using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>The canonical candidate decision and exact observed facts, without lifecycle-specific state materialization.</summary>
public abstract class NasdaqCandidateLifecycleDecision
{
    private NasdaqCandidateLifecycleDecision(StructuralTurnGeometryResult candidateGeometry,
        StructuralTurnGeometryResult frozenTerminal, Candle previousCursor, StructuralCandidateExtremeResult migration,
        StructuralBodyCloseBreakResult breakout, CandleBodyDirection bodyDirection)
    {
        CandidateGeometry = candidateGeometry;
        FrozenTerminal = frozenTerminal;
        PreviousCursor = previousCursor;
        Migration = migration;
        Breakout = breakout;
        BodyDirection = bodyDirection;
    }

    public abstract NasdaqPostInvalidationCandidateTransitionKind Kind { get; }
    /// <summary>The pre-candle provisional geometry; migration branches do not claim it as definitive rebuilt geometry.</summary>
    public StructuralTurnGeometryResult CandidateGeometry { get; }
    public StructuralTurnGeometryResult FrozenTerminal { get; }
    public Candle PreviousCursor { get; }
    public StructuralCandidateExtremeResult Migration { get; }
    public StructuralBodyCloseBreakResult Breakout { get; }
    public CandleBodyDirection BodyDirection { get; }
    public StructuralCandidateExtremeSide CandidateSide => Migration.Side;
    /// <summary>The single consumed market step; the source state remains unchanged.</summary>
    public Candle MarketCursor => Breakout.Candle!;

    public sealed class CandidateContinues : NasdaqCandidateLifecycleDecision
    {
        internal CandidateContinues(StructuralTurnGeometryResult geometry, StructuralTurnGeometryResult terminal,
            Candle previousCursor, StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout,
            CandleBodyDirection body)
            : base(geometry, terminal, previousCursor, migration, breakout, body) { }
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues;
    }

    public sealed class DirectCompleted : NasdaqCandidateLifecycleDecision
    {
        internal DirectCompleted(StructuralTurnGeometryResult geometry, StructuralTurnGeometryResult terminal,
            Candle previousCursor, StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout,
            CandleBodyDirection body)
            : base(geometry, terminal, previousCursor, migration, breakout, body) => Validation = new(CandidateSide, geometry, breakout);
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.DirectCompleted;
        public StructuralCandidateValidationResult Validation { get; }
    }

    public sealed class RebuildPending : NasdaqCandidateLifecycleDecision
    {
        internal RebuildPending(StructuralTurnGeometryResult geometry, StructuralTurnGeometryResult terminal,
            Candle previousCursor, StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout,
            CandleBodyDirection body)
            : base(geometry, terminal, previousCursor, migration, breakout, body) { }
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.RebuildPending;
        public decimal EffectiveProtectionAnchor => Migration.ResultingExtreme;
    }

    public sealed class RebuiltTracking : NasdaqCandidateLifecycleDecision
    {
        internal RebuiltTracking(StructuralTurnGeometryResult geometry, StructuralTurnGeometryResult terminal,
            Candle previousCursor, StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout,
            CandleBodyDirection body)
            : base(geometry, terminal, previousCursor, migration, breakout, body) { }
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.RebuiltTracking;
        public decimal EffectiveProtectionAnchor => Migration.ResultingExtreme;
        public Candle FirstTurnCandle => MarketCursor;
    }

    public sealed class CollisionBreakout : NasdaqCandidateLifecycleDecision
    {
        internal CollisionBreakout(StructuralTurnGeometryResult geometry, StructuralTurnGeometryResult terminal,
            Candle previousCursor, StructuralCandidateExtremeResult migration, StructuralBodyCloseBreakResult breakout,
            CandleBodyDirection body, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind)
            : base(geometry, terminal, previousCursor, migration, breakout, body) => CollisionKind = collisionKind;
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.CollisionBreakout;
        public decimal EffectiveProtectionAnchor => Migration.ResultingExtreme;
        public NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind { get; }
    }
}
