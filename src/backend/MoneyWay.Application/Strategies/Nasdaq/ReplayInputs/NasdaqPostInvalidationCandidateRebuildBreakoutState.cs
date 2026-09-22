using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Preserves one rebuilt-pending frozen-terminal breakout without fabricating definitive vertex geometry.</summary>
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
        PriorMigrationCandle = pending.MigrationCandle;
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
        PriorMigrationCandle = tracking.MigrationCandle;
        FirstTurnCandle = tracking.FirstTurnCandle;
        ValidatingCandle = validatingCandle;
        HasStrictMigration = hasStrictMigration;
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
    public Candle PriorMigrationCandle { get; }
    public Candle? FirstTurnCandle { get; }
    public Candle ValidatingCandle { get; }
    public bool HasStrictMigration { get; }
    public decimal EffectiveProtectionAnchor { get; }
    public NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind { get; }
    public Candle LastProcessedCandle { get; }
}
