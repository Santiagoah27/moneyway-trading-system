using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Preserves one migration-only post-invalidation rebuild while final candidate body membership remains unavailable.
/// </summary>
public sealed class NasdaqPostInvalidationCandidateRebuildPendingState
{
    internal NasdaqPostInvalidationCandidateRebuildPendingState(
        NasdaqPostInvalidationCandidateState supersededCandidate,
        Candle migrationCandle,
        decimal knownProtectionAnchor)
    {
        ArgumentNullException.ThrowIfNull(supersededCandidate);
        ArgumentNullException.ThrowIfNull(migrationCandle);

        if (migrationCandle.ProviderId != supersededCandidate.LastProcessedCandle.ProviderId
            || migrationCandle.Symbol != supersededCandidate.LastProcessedCandle.Symbol
            || migrationCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || migrationCandle.Timeframe != supersededCandidate.LastProcessedCandle.Timeframe)
        {
            throw new ArgumentException("The migration candle must belong to the same H4 series.", nameof(migrationCandle));
        }

        InvalidatingCandle = supersededCandidate.InvalidatingCandle;
        ImpulseTerminalSide = supersededCandidate.ImpulseTerminalSide;
        CandidateSide = supersededCandidate.CandidateSide;
        OriginGeometry = supersededCandidate.OriginGeometry;
        FrozenImpulseTerminal = supersededCandidate.FrozenImpulseTerminal;
        MigrationCandle = migrationCandle;
        KnownProtectionAnchor = knownProtectionAnchor;
    }

    public Candle InvalidatingCandle { get; }

    public StructuralTurnBodyCoordinateSide ImpulseTerminalSide { get; }

    public StructuralCandidateExtremeSide CandidateSide { get; }

    public StructuralTurnGeometryResult OriginGeometry { get; }

    public StructuralTurnGeometryResult FrozenImpulseTerminal { get; }

    /// <summary>The strict new wick-extreme candle that superseded the former candidate.</summary>
    public Candle MigrationCandle { get; }

    /// <summary>The known strict migration wick; this is not complete candidate geometry.</summary>
    public decimal KnownProtectionAnchor { get; }

    public Candle LastProcessedCandle => MigrationCandle;
}
