using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Calculates deterministic geometry from one resolved human rebuilt-candidate membership.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexGeometryCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public StructuralTurnGeometryResult Evaluate(NasdaqHumanRebuiltCandidateVertexMemberResolution members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var side = members.Episode.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Lower,
            StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(members)),
        };

        var geometry = geometryCalculator.Evaluate(members.SelectedMembers, side);
        if (geometry.ProtectionAnchor != members.KnownProtectionAnchor)
        {
            throw new InvalidOperationException(
                "Resolved rebuilt candidate members do not reproduce the verified protection anchor.");
        }

        return geometry;
    }
}
