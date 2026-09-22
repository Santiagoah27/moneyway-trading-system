using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Tracks a causally observed rebuilt turn before definitive vertex membership and geometry exist.</summary>
public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingState
{
    internal NasdaqPostInvalidationRebuiltCandidateTrackingState(
        NasdaqPostInvalidationCandidateRebuildPendingState pending,
        Candle firstTurnCandle)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(firstTurnCandle);

        InvalidatingCandle = pending.InvalidatingCandle;
        ImpulseTerminalSide = pending.ImpulseTerminalSide;
        CandidateSide = pending.CandidateSide;
        OriginGeometry = pending.OriginGeometry;
        FrozenImpulseTerminal = pending.FrozenImpulseTerminal;
        MigrationCandle = pending.MigrationCandle;
        KnownProtectionAnchor = pending.KnownProtectionAnchor;
        FirstTurnCandle = firstTurnCandle;
        LastProcessedCandle = firstTurnCandle;
    }

    public Candle InvalidatingCandle { get; }
    public StructuralTurnBodyCoordinateSide ImpulseTerminalSide { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
    public StructuralTurnGeometryResult OriginGeometry { get; }
    public StructuralTurnGeometryResult FrozenImpulseTerminal { get; }
    public Candle MigrationCandle { get; }
    public decimal KnownProtectionAnchor { get; }
    public Candle FirstTurnCandle { get; }
    public Candle LastProcessedCandle { get; }
}
