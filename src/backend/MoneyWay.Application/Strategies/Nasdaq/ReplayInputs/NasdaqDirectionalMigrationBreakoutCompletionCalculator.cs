using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Completes the directional same-candle migration and frozen-terminal break of NQ-Q-H4-007.</summary>
public sealed class NasdaqDirectionalMigrationBreakoutCompletionCalculator
{
    private readonly NasdaqDirectionalMigrationBreakoutCandidateCalculator candidateCalculator = new();

    public NasdaqDirectionalMigrationBreakoutCompletionResult Evaluate(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        if (!Enum.IsDefined(breakout.CandidateSide))
            throw new ArgumentOutOfRangeException(nameof(breakout), "The candidate side is not supported.");
        if (!breakout.HasStrictMigration
            || breakout.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)
        {
            throw new ArgumentException("Only a directional same-candle migration breakout can be completed.", nameof(breakout));
        }

        var candle = breakout.ValidatingCandle;
        var direction = breakout.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? StructuralBreakDirection.Upper
            : StructuralBreakDirection.Lower;
        var directionalBody = direction == StructuralBreakDirection.Upper
            ? candle.Close > candle.Open
            : candle.Close < candle.Open;
        var strictMigration = direction == StructuralBreakDirection.Upper
            ? candle.Low < breakout.PreviousProtectionAnchor
            : candle.High > breakout.PreviousProtectionAnchor;
        var reference = breakout.FrozenImpulseTerminal.StructuralPrice;
        var strictBreak = direction == StructuralBreakDirection.Upper
            ? candle.Close > reference
            : candle.Close < reference;
        if (!strictMigration || !directionalBody || !strictBreak)
        {
            throw new ArgumentException("The validating candle must have strict migration, directional body, and strict frozen-terminal break.", nameof(breakout));
        }

        if ((direction == StructuralBreakDirection.Upper ? candle.Low : candle.High) != breakout.EffectiveProtectionAnchor)
        {
            throw new ArgumentException("The same-candle protection anchor must equal the effective migrated anchor.", nameof(breakout));
        }

        if (breakout.CandidateDecision is { } decision)
        {
            var definitive = decision.CandidateResolution as NasdaqCollisionCandidateResolution.Directional
                ?? throw new ArgumentException("The canonical collision must retain definitive directional facts.", nameof(breakout));
            return new(breakout, definitive.Validation.CandidateGeometry, definitive.Validation);
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
        var candidate = candidateCalculator.Evaluate(new StructuralCandidateExtremeResult(
            breakout.PreviousProtectionAnchor, breakout.EffectiveProtectionAnchor, breakout.CandidateSide),
            breakObservation, direction == StructuralBreakDirection.Upper ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish);
        return new NasdaqDirectionalMigrationBreakoutCompletionResult(breakout, candidate.Validation.CandidateGeometry, candidate.Validation);
    }
}
