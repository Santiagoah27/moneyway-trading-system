using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Calculates deterministic geometry from one resolved human rebuilt-candidate membership.</summary>
public sealed class NasdaqRebuiltCandidateGeometryCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public StructuralTurnGeometryResult Evaluate(IReadOnlyList<Candle> members, StructuralCandidateExtremeSide candidateSide, decimal knownProtectionAnchor)
    {
        ArgumentNullException.ThrowIfNull(members);

        var side = candidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Lower,
            StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(candidateSide)),
        };

        var geometry = geometryCalculator.Evaluate(members, side);
        if (geometry.ProtectionAnchor != knownProtectionAnchor)
        {
            throw new InvalidOperationException(
                "Resolved rebuilt candidate members do not reproduce the verified protection anchor.");
        }

        return geometry;
    }
}
