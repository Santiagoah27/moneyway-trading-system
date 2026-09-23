using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Completes one ordinary ADR 0009 rebuilt breakout from already resolved source evidence.</summary>
public sealed class NasdaqOrdinaryRebuiltBreakoutCompletionCalculator
{
    private readonly NasdaqHumanRebuiltCandidateVertexGeometryCalculator geometryCalculator = new();

    public NasdaqOrdinaryRebuiltBreakoutCompletionResult Evaluate(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        NasdaqHumanRebuiltCandidateVertexMemberResolution memberResolution,
        StructuralTurnGeometryResult candidateGeometry)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        ArgumentNullException.ThrowIfNull(memberResolution);
        ArgumentNullException.ThrowIfNull(candidateGeometry);

        if (breakout.HasStrictMigration
            || breakout.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None)
        {
            throw new ArgumentException("Only an ordinary rebuilt breakout can be completed by this calculator.", nameof(breakout));
        }

        EnsureMatchingEpisode(breakout, memberResolution);

        if (memberResolution.SelectedMembers.Any(member =>
                member.OpenTimeUtc >= breakout.ValidatingCandle.OpenTimeUtc))
        {
            throw new ArgumentException(
                "Ordinary rebuilt vertex members must precede the validating breakout candle.",
                nameof(memberResolution));
        }

        var expectedGeometry = geometryCalculator.Evaluate(memberResolution);
        var expectedSide = breakout.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? StructuralTurnBodyCoordinateSide.Lower
            : StructuralTurnBodyCoordinateSide.Upper;
        if (candidateGeometry.Side != expectedSide
            || candidateGeometry.StructuralPrice != expectedGeometry.StructuralPrice
            || candidateGeometry.ProtectionAnchor != expectedGeometry.ProtectionAnchor
            || candidateGeometry.ProtectionAnchor != memberResolution.KnownProtectionAnchor
            || candidateGeometry.ProtectionAnchor != breakout.EffectiveProtectionAnchor)
        {
            throw new ArgumentException(
                "The supplied geometry must be the exact geometry of the resolved rebuilt candidate.",
                nameof(candidateGeometry));
        }

        var direction = breakout.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? StructuralBreakDirection.Upper
            : StructuralBreakDirection.Lower;
        var reference = breakout.FrozenImpulseTerminal.StructuralPrice;
        var confirms = direction == StructuralBreakDirection.Upper
            ? breakout.ValidatingCandle.Close > reference
            : breakout.ValidatingCandle.Close < reference;
        if (!confirms)
        {
            throw new ArgumentException("The stored validating candle must strictly break the frozen terminal.", nameof(breakout));
        }

        var breakObservation = new StructuralBodyCloseBreakResult(
            breakout.Episode.ProviderId,
            breakout.Episode.Symbol,
            breakout.Episode.Timeframe,
            reference,
            direction,
            breakout.ValidatingCandle.CloseTimeUtc,
            breakout.ValidatingCandle,
            true);
        var validatedCandidate = new StructuralCandidateValidationResult(
            breakout.CandidateSide,
            candidateGeometry,
            breakObservation);

        return new NasdaqOrdinaryRebuiltBreakoutCompletionResult(
            breakout,
            memberResolution,
            candidateGeometry,
            validatedCandidate);
    }

    private static void EnsureMatchingEpisode(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        NasdaqHumanRebuiltCandidateVertexMemberResolution memberResolution)
    {
        var origin = breakout.Episode;
        var rebuilt = memberResolution.Episode;
        if (origin.StrategyId != rebuilt.StrategyId
            || origin.StrategyVersion != rebuilt.StrategyVersion
            || origin.ProviderId != rebuilt.ProviderId
            || origin.Symbol != rebuilt.Symbol
            || origin.Timeframe != rebuilt.Timeframe
            || origin.InvalidatingCandleOpenTimeUtc != rebuilt.InvalidatingCandleOpenTimeUtc
            || breakout.PriorMigrationCandle.OpenTimeUtc != rebuilt.MigrationCandleOpenTimeUtc
            || breakout.CandidateSide != rebuilt.CandidateSide
            || memberResolution.MigrationCandle.ProviderId != breakout.PriorMigrationCandle.ProviderId
            || memberResolution.MigrationCandle.Symbol != breakout.PriorMigrationCandle.Symbol
            || memberResolution.MigrationCandle.Timeframe != breakout.PriorMigrationCandle.Timeframe
            || memberResolution.MigrationCandle.OpenTimeUtc != breakout.PriorMigrationCandle.OpenTimeUtc
            || memberResolution.MigrationCandle.CloseTimeUtc != breakout.PriorMigrationCandle.CloseTimeUtc)
        {
            throw new ArgumentException(
                "The rebuilt candidate membership must match the exact breakout episode.",
                nameof(memberResolution));
        }
    }
}
