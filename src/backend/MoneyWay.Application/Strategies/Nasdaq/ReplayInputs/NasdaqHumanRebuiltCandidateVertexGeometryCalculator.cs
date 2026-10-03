using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Calculates deterministic geometry from one resolved human rebuilt-candidate membership.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexGeometryCalculator
{
    private readonly NasdaqRebuiltCandidateGeometryCalculator geometryCalculator = new();

    public StructuralTurnGeometryResult Evaluate(NasdaqHumanRebuiltCandidateVertexMemberResolution members)
    {
        ArgumentNullException.ThrowIfNull(members);
        return geometryCalculator.Evaluate(members.SelectedMembers, members.Episode.CandidateSide, members.KnownProtectionAnchor);
    }
}
