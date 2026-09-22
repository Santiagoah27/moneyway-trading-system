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

    internal NasdaqPostInvalidationRebuiltCandidateTrackingState(
        NasdaqPostInvalidationRebuiltCandidateTrackingState current,
        Candle lastProcessedCandle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(lastProcessedCandle);

        if (lastProcessedCandle.ProviderId != current.LastProcessedCandle.ProviderId
            || lastProcessedCandle.Symbol != current.LastProcessedCandle.Symbol
            || lastProcessedCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || lastProcessedCandle.OpenTimeUtc <= current.LastProcessedCandle.OpenTimeUtc
            || lastProcessedCandle.OpenTimeUtc < current.LastProcessedCandle.CloseTimeUtc
            || lastProcessedCandle.CloseTimeUtc <= current.LastProcessedCandle.CloseTimeUtc)
        {
            throw new ArgumentException("The tracking cursor must advance to a later candle in the same H4 series.", nameof(lastProcessedCandle));
        }

        InvalidatingCandle = current.InvalidatingCandle;
        ImpulseTerminalSide = current.ImpulseTerminalSide;
        CandidateSide = current.CandidateSide;
        OriginGeometry = current.OriginGeometry;
        FrozenImpulseTerminal = current.FrozenImpulseTerminal;
        MigrationCandle = current.MigrationCandle;
        KnownProtectionAnchor = current.KnownProtectionAnchor;
        FirstTurnCandle = current.FirstTurnCandle;
        LastProcessedCandle = lastProcessedCandle;
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
