using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Creates the initial pre-breakout rebuilt-candidate tracker from one causally processed opposite-body candle.</summary>
public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingInitializer
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();

    public NasdaqPostInvalidationRebuiltCandidateTrackingState Initialize(
        NasdaqPostInvalidationCandidateRebuildPendingState pending,
        Candle firstTurnCandle)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(firstTurnCandle);

        var migration = pending.MigrationCandle;
        if (firstTurnCandle.ProviderId != migration.ProviderId || firstTurnCandle.Symbol != migration.Symbol
            || firstTurnCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || firstTurnCandle.Timeframe != migration.Timeframe)
        {
            throw new ArgumentException("The first turn candle must belong to the pending H4 series.", nameof(firstTurnCandle));
        }

        if (firstTurnCandle.OpenTimeUtc < migration.OpenTimeUtc || firstTurnCandle.CloseTimeUtc < migration.CloseTimeUtc)
            throw new ArgumentException("The first turn candle cannot precede the effective migration candle.", nameof(firstTurnCandle));

        var expectedDirection = pending.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? CandleBodyDirection.Bullish
            : CandleBodyDirection.Bearish;
        if (bodyDirectionCalculator.Evaluate(firstTurnCandle) != expectedDirection)
            throw new ArgumentException("The first turn candle must have the strict opposite body direction.", nameof(firstTurnCandle));

        if (firstTurnCandle.OpenTimeUtc > migration.OpenTimeUtc)
        {
            var supersedesMigration = pending.CandidateSide == StructuralCandidateExtremeSide.Lower
                ? firstTurnCandle.Low < pending.KnownProtectionAnchor
                : firstTurnCandle.High > pending.KnownProtectionAnchor;
            if (supersedesMigration)
                throw new ArgumentException("The first turn candle requires a new migration reset.", nameof(firstTurnCandle));
        }

        var alreadyBreaksFrozenTerminal = pending.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? firstTurnCandle.Close > pending.FrozenImpulseTerminal.StructuralPrice
            : firstTurnCandle.Close < pending.FrozenImpulseTerminal.StructuralPrice;
        if (alreadyBreaksFrozenTerminal)
            throw new ArgumentException("A frozen-terminal breakout cannot be represented as pre-breakout tracking.", nameof(firstTurnCandle));

        return new NasdaqPostInvalidationRebuiltCandidateTrackingState(pending, firstTurnCandle);
    }
}
