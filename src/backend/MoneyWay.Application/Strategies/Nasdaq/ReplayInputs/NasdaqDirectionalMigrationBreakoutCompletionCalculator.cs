using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Completes the directional same-candle migration and frozen-terminal break of NQ-Q-H4-007.</summary>
public sealed class NasdaqDirectionalMigrationBreakoutCompletionCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public NasdaqDirectionalMigrationBreakoutCompletionResult Evaluate(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        if (!breakout.HasStrictMigration
            || breakout.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)
        {
            throw new ArgumentException("Only a directional same-candle migration breakout can be completed.", nameof(breakout));
        }

        var candle = breakout.ValidatingCandle;
        var side = breakout.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralTurnBodyCoordinateSide.Lower,
            StructuralCandidateExtremeSide.Upper => StructuralTurnBodyCoordinateSide.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(breakout), "The candidate side is not supported."),
        };
        var direction = breakout.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? StructuralBreakDirection.Upper
            : StructuralBreakDirection.Lower;
        var directionalBody = direction == StructuralBreakDirection.Upper
            ? candle.Close > candle.Open
            : candle.Close < candle.Open;
        var strictMigration = direction == StructuralBreakDirection.Upper
            ? candle.Low < breakout.PriorMigrationCandle.Low
            : candle.High > breakout.PriorMigrationCandle.High;
        var reference = breakout.FrozenImpulseTerminal.StructuralPrice;
        var strictBreak = direction == StructuralBreakDirection.Upper
            ? candle.Close > reference
            : candle.Close < reference;
        if (!strictMigration || !directionalBody || !strictBreak)
        {
            throw new ArgumentException("The validating candle must have strict migration, directional body, and strict frozen-terminal break.", nameof(breakout));
        }

        var geometry = geometryCalculator.Evaluate([candle], side);
        if (geometry.ProtectionAnchor != breakout.EffectiveProtectionAnchor)
        {
            throw new ArgumentException("The same-candle protection anchor must equal the effective migrated anchor.", nameof(breakout));
        }

        var breakObservation = new StructuralBodyCloseBreakResult(
            breakout.Episode.ProviderId,
            breakout.Episode.Symbol,
            breakout.Episode.Timeframe,
            reference,
            direction,
            candle.CloseTimeUtc,
            candle,
            true);
        var validation = new StructuralCandidateValidationResult(breakout.CandidateSide, geometry, breakObservation);
        return new NasdaqDirectionalMigrationBreakoutCompletionResult(breakout, geometry, validation);
    }
}
