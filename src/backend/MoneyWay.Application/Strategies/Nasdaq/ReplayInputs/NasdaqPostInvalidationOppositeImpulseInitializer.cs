using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Initializes the one-candle opposite impulse from a verified invalidation and already resolved human origin geometry.
/// It does not process later candles, start a correction, or validate an opposite pair.
/// </summary>
public sealed class NasdaqPostInvalidationOppositeImpulseInitializer
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public NasdaqPostInvalidationOppositeImpulseState Initialize(
        NasdaqHumanOriginVertexMemberResolution members,
        StructuralCandidateTurnBoundaryResult invalidation,
        StructuralTurnGeometryResult originGeometry)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(invalidation);
        ArgumentNullException.ThrowIfNull(originGeometry);

        if (invalidation.Kind != StructuralCandidateTurnBoundaryKind.StructureInvalidated)
        {
            throw new ArgumentException("The structural boundary must invalidate the previous side.", nameof(invalidation));
        }

        var invalidatingCandle = members.InvalidatingCandle;
        if (invalidatingCandle.ProviderId != invalidation.CurrentCandle.ProviderId
            || invalidatingCandle.Symbol != invalidation.CurrentCandle.Symbol
            || invalidatingCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || invalidation.CurrentCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || invalidatingCandle.OpenTimeUtc != invalidation.CurrentCandle.OpenTimeUtc
            || invalidatingCandle.CloseTimeUtc != invalidation.CurrentCandle.CloseTimeUtc)
        {
            throw new ArgumentException("Resolved members must belong to the same H4 invalidation event.", nameof(members));
        }

        var terminalSide = ToTerminalSide(invalidation.CandidateValidation.CandidateSide);
        var originSide = OppositeOf(terminalSide);
        if (originGeometry.Side != originSide)
        {
            throw new ArgumentException("Origin geometry must match the invalidated structure's opposite impulse origin side.", nameof(originGeometry));
        }

        var terminal = geometryCalculator.Evaluate([invalidatingCandle], terminalSide);
        return new NasdaqPostInvalidationOppositeImpulseState(invalidatingCandle, terminalSide, originGeometry, terminal);
    }

    private static StructuralTurnBodyCoordinateSide ToTerminalSide(StructuralCandidateExtremeSide candidateSide) =>
        candidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Lower,
            StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(candidateSide), candidateSide, "The structural candidate side is not supported."),
        };

    private static StructuralTurnBodyCoordinateSide OppositeOf(StructuralTurnBodyCoordinateSide side) =>
        side switch
        {
            StructuralTurnBodyCoordinateSide.Lower => StructuralTurnBodyCoordinateSide.Upper,
            StructuralTurnBodyCoordinateSide.Upper => StructuralTurnBodyCoordinateSide.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "The structural turn side is not supported."),
        };
}
