using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Preserves one frozen-terminal breakout and its typed causal origin without fabricating definitive vertex geometry.</summary>
public sealed class NasdaqPostInvalidationCandidateRebuildBreakoutState
{
    internal NasdaqPostInvalidationCandidateRebuildBreakoutState(
        NasdaqPostInvalidationCandidateRebuildPendingState pending,
        Candle validatingCandle,
        bool hasStrictMigration,
        decimal effectiveProtectionAnchor,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(validatingCandle);

        Episode = pending.Episode;
        InvalidatingCandle = pending.InvalidatingCandle;
        ImpulseTerminalSide = pending.ImpulseTerminalSide;
        CandidateSide = pending.CandidateSide;
        OriginGeometry = pending.OriginGeometry;
        FrozenImpulseTerminal = pending.FrozenImpulseTerminal;
        Origin = new NasdaqPostInvalidationBreakoutOrigin.Rebuild(pending.MigrationCandle, null);
        PreviousProtectionAnchor = pending.KnownProtectionAnchor;
        ValidatingCandle = validatingCandle;
        HasStrictMigration = hasStrictMigration;
        EffectiveProtectionAnchor = effectiveProtectionAnchor;
        CollisionKind = collisionKind;
        LastProcessedCandle = validatingCandle;
    }

    internal NasdaqPostInvalidationCandidateRebuildBreakoutState(
        NasdaqPostInvalidationRebuiltCandidateTrackingState tracking,
        Candle validatingCandle,
        bool hasStrictMigration,
        decimal effectiveProtectionAnchor,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        ArgumentNullException.ThrowIfNull(validatingCandle);

        Episode = tracking.Episode;
        InvalidatingCandle = tracking.InvalidatingCandle;
        ImpulseTerminalSide = tracking.ImpulseTerminalSide;
        CandidateSide = tracking.CandidateSide;
        OriginGeometry = tracking.OriginGeometry;
        FrozenImpulseTerminal = tracking.FrozenImpulseTerminal;
        Origin = new NasdaqPostInvalidationBreakoutOrigin.Rebuild(tracking.MigrationCandle, tracking.FirstTurnCandle);
        PreviousProtectionAnchor = tracking.KnownProtectionAnchor;
        ValidatingCandle = validatingCandle;
        HasStrictMigration = hasStrictMigration;
        EffectiveProtectionAnchor = effectiveProtectionAnchor;
        CollisionKind = collisionKind;
        LastProcessedCandle = validatingCandle;
    }

    internal NasdaqPostInvalidationCandidateRebuildBreakoutState(
        NasdaqPostInvalidationCandidateState candidate,
        Candle validatingCandle,
        decimal effectiveProtectionAnchor,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(validatingCandle);

        Episode = candidate.Episode;
        InvalidatingCandle = candidate.InvalidatingCandle;
        ImpulseTerminalSide = candidate.ImpulseTerminalSide;
        CandidateSide = candidate.CandidateSide;
        OriginGeometry = candidate.OriginGeometry;
        FrozenImpulseTerminal = candidate.FrozenImpulseTerminal;
        Origin = new NasdaqPostInvalidationBreakoutOrigin.Candidate(candidate);
        PreviousProtectionAnchor = candidate.CandidateGeometry.ProtectionAnchor;
        ValidatingCandle = validatingCandle;
        HasStrictMigration = true;
        EffectiveProtectionAnchor = effectiveProtectionAnchor;
        CollisionKind = collisionKind;
        LastProcessedCandle = validatingCandle;
    }

    public NasdaqHumanOriginVertexEpisode Episode { get; }

    public Candle InvalidatingCandle { get; }
    public StructuralTurnBodyCoordinateSide ImpulseTerminalSide { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
    public StructuralTurnGeometryResult OriginGeometry { get; }
    public StructuralTurnGeometryResult FrozenImpulseTerminal { get; }
    public NasdaqPostInvalidationBreakoutOrigin Origin { get; }
    /// <summary>The effective candidate protection anchor before the validating candle was processed.</summary>
    public decimal PreviousProtectionAnchor { get; }
    public Candle? FirstTurnCandle => (Origin as NasdaqPostInvalidationBreakoutOrigin.Rebuild)?.FirstTurnCandle;
    public Candle ValidatingCandle { get; }
    public bool HasStrictMigration { get; }
    public decimal EffectiveProtectionAnchor { get; }
    public NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind { get; }
    public Candle LastProcessedCandle { get; }
}
