using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Composes the established 008 human body coordinate and canonical migrated wick anchor; assigns no market body owner.</summary>
public sealed class NasdaqHumanStructuralPriceCandidateGeometryCalculator
{
    public StructuralTurnGeometryResult Evaluate(StructuralCandidateExtremeSide candidateSide,
        decimal effectiveProtectionAnchor, decimal structuralPrice)
    {
        if (!Enum.IsDefined(candidateSide))
            throw new ArgumentOutOfRangeException(nameof(candidateSide), "The candidate side is not supported.");
        var lower = candidateSide == StructuralCandidateExtremeSide.Lower;
        var side = lower ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper;
        var protectionSide = lower ? StructuralTurnProtectionSide.Lower : StructuralTurnProtectionSide.Upper;
        return new StructuralTurnGeometryResult(
            new StructuralTurnBodyCoordinateResult(structuralPrice, side),
            new StructuralTurnProtectionAnchorResult(effectiveProtectionAnchor, protectionSide));
    }
}
