using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Calculates only the geometry of a resolved human origin vertex at its typed invalidation boundary.</summary>
public sealed class NasdaqHumanOriginVertexGeometryCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public StructuralTurnGeometryResult Evaluate(
        NasdaqHumanOriginVertexMemberResolution members,
        StructuralCandidateTurnBoundaryResult invalidation)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(invalidation);

        if (invalidation.Kind != StructuralCandidateTurnBoundaryKind.StructureInvalidated)
            throw new ArgumentException("The structural boundary must invalidate the previous side.", nameof(invalidation));

        var source = members.InvalidatingCandle;
        var eventCandle = invalidation.CurrentCandle;
        if (source.ProviderId != eventCandle.ProviderId || source.Symbol != eventCandle.Symbol
            || source.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || eventCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || source.OpenTimeUtc != eventCandle.OpenTimeUtc
            || source.CloseTimeUtc != eventCandle.CloseTimeUtc)
            throw new ArgumentException("Resolved members must belong to the same H4 invalidation event.", nameof(members));

        var originSide = invalidation.CandidateValidation.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Upper,
            StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(invalidation)),
        };

        return geometryCalculator.Evaluate(members.SelectedMembers, originSide);
    }
}
