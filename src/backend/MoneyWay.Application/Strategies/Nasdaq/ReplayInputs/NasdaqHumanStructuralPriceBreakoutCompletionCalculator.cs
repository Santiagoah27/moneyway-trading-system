using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Completes one NQ-Q-H4-008 breakout from a unique human StructuralPrice answer.</summary>
public sealed class NasdaqHumanStructuralPriceBreakoutCompletionCalculator
{
    public NasdaqHumanStructuralPriceBreakoutCompletionResult Evaluate(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        NasdaqHumanCollisionStructuralPriceObservationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        ArgumentNullException.ThrowIfNull(selection);
        if (!breakout.HasStrictMigration
            || breakout.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)
        {
            throw new ArgumentException("Only an NQ-Q-H4-008 collision can be completed.", nameof(breakout));
        }

        var episode = new NasdaqHumanCollisionStructuralPriceEpisode(
            breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide);
        if (selection.Kind != NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique
            || selection.StructuralPrice is not decimal structuralPrice
            || selection.SupportingObservations.Count == 0
            || selection.DistinctStructuralPrices.Count != 1
            || selection.DistinctStructuralPrices[0] != structuralPrice
            || selection.SupportingObservations.Any(item => !episode.Matches(item) || item.StructuralPrice != structuralPrice))
        {
            throw new ArgumentException("A unique human price for the exact collision episode is required.", nameof(selection));
        }

        var candle = breakout.ValidatingCandle;
        var lower = breakout.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => true,
            StructuralCandidateExtremeSide.Upper => false,
            _ => throw new ArgumentOutOfRangeException(nameof(breakout), "The candidate side is not supported."),
        };
        var strictMigration = lower
            ? candle.Low < breakout.PreviousProtectionAnchor
            : candle.High > breakout.PreviousProtectionAnchor;
        var anchor = lower ? candle.Low : candle.High;
        var directionalBody = lower ? candle.Close > candle.Open : candle.Close < candle.Open;
        var reference = breakout.FrozenImpulseTerminal.StructuralPrice;
        var strictBreak = lower ? candle.Close > reference : candle.Close < reference;
        if (!strictMigration || anchor != breakout.EffectiveProtectionAnchor || directionalBody || !strictBreak)
        {
            throw new ArgumentException("The stored collision must have strict migration, opposite or doji body, and a strict frozen-terminal break.", nameof(breakout));
        }

        var side = lower ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper;
        var protectionSide = lower ? StructuralTurnProtectionSide.Lower : StructuralTurnProtectionSide.Upper;
        var geometry = new StructuralTurnGeometryResult(
            new StructuralTurnBodyCoordinateResult(structuralPrice, side),
            new StructuralTurnProtectionAnchorResult(breakout.EffectiveProtectionAnchor, protectionSide));
        var breakObservation = new StructuralBodyCloseBreakResult(
            breakout.Episode.ProviderId,
            breakout.Episode.Symbol,
            breakout.Episode.Timeframe,
            reference,
            lower ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower,
            candle.CloseTimeUtc,
            candle,
            true);
        var validation = new StructuralCandidateValidationResult(breakout.CandidateSide, geometry, breakObservation);
        return new NasdaqHumanStructuralPriceBreakoutCompletionResult(breakout, selection, geometry, validation);
    }
}
