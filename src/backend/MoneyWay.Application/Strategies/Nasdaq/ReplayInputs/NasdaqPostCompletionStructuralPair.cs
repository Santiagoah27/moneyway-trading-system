using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>The resolved HH with validated HL, or resolved LL with validated LH.</summary>
public sealed class NasdaqPostCompletionStructuralPair
{
    internal NasdaqPostCompletionStructuralPair(
        NasdaqHumanPostCompletionActiveExtremeGeometryResult activeExtreme,
        StructuralCandidateValidationResult protectedTurn)
    {
        ArgumentNullException.ThrowIfNull(activeExtreme);
        ArgumentNullException.ThrowIfNull(protectedTurn);
        var side = activeExtreme.MemberResolution.MembershipEvent.Episode.ActiveExtremeSide;
        var protectedSide = side == StructuralTurnBodyCoordinateSide.Upper
            ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper;
        var candidateSide = side == StructuralTurnBodyCoordinateSide.Upper
            ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper;
        if (activeExtreme.Geometry.Side != side || protectedTurn.CandidateSide != candidateSide
            || protectedTurn.CandidateGeometry.Side != protectedSide || !protectedTurn.IsValidated)
            throw new ArgumentException("The active extreme and validated protected turn must have matching opposite structural sides.");
        if (side == StructuralTurnBodyCoordinateSide.Upper
            ? activeExtreme.Geometry.StructuralPrice <= protectedTurn.StructuralPrice
            : activeExtreme.Geometry.StructuralPrice >= protectedTurn.StructuralPrice)
            throw new ArgumentException("The active structural references must be strictly ordered.");
        ActiveExtreme = activeExtreme;
        ProtectedTurn = protectedTurn;
    }

    public NasdaqHumanPostCompletionActiveExtremeGeometryResult ActiveExtreme { get; }
    public StructuralCandidateValidationResult ProtectedTurn { get; }
    public StructuralTurnBodyCoordinateSide ActiveExtremeSide => ActiveExtreme.Geometry.Side;
    public StructuralTurnGeometryResult ActiveExtremeGeometry => ActiveExtreme.Geometry;
    public StructuralTurnGeometryResult ProtectedTurnGeometry => ProtectedTurn.CandidateGeometry;
}
