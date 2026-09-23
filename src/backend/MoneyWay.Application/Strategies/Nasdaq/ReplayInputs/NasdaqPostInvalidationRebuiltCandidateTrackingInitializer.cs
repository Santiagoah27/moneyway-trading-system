using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Creates the initial pre-breakout rebuilt-candidate tracker from one causally processed opposite-body candle.</summary>
public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingInitializer
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();

    /// <summary>Creates tracking directly when one candidate candle is both strict migration and its first valid turn.</summary>
    public NasdaqPostInvalidationRebuiltCandidateTrackingState Initialize(
        NasdaqPostInvalidationCandidateState candidate,
        Candle migrationAndFirstTurnCandle)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(migrationAndFirstTurnCandle);

        var previous = candidate.LastProcessedCandle;
        if (migrationAndFirstTurnCandle.ProviderId != previous.ProviderId
            || migrationAndFirstTurnCandle.Symbol != previous.Symbol
            || migrationAndFirstTurnCandle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || migrationAndFirstTurnCandle.Timeframe != previous.Timeframe
            || migrationAndFirstTurnCandle.OpenTimeUtc <= previous.OpenTimeUtc
            || migrationAndFirstTurnCandle.OpenTimeUtc < previous.CloseTimeUtc
            || migrationAndFirstTurnCandle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The migration and first turn candle must follow the candidate in the same H4 series.", nameof(migrationAndFirstTurnCandle));
        }

        var migrated = candidate.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? migrationAndFirstTurnCandle.Low < candidate.CandidateGeometry.ProtectionAnchor
            : migrationAndFirstTurnCandle.High > candidate.CandidateGeometry.ProtectionAnchor;
        var brokeOut = candidate.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? migrationAndFirstTurnCandle.Close > candidate.FrozenImpulseTerminal.StructuralPrice
            : migrationAndFirstTurnCandle.Close < candidate.FrozenImpulseTerminal.StructuralPrice;
        var expectedDirection = candidate.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? CandleBodyDirection.Bullish
            : CandleBodyDirection.Bearish;
        if (!migrated || brokeOut || bodyDirectionCalculator.Evaluate(migrationAndFirstTurnCandle) != expectedDirection)
        {
            throw new ArgumentException("The candle must strictly migrate without breaking the frozen terminal and start the rebuilt turn.", nameof(migrationAndFirstTurnCandle));
        }

        return new NasdaqPostInvalidationRebuiltCandidateTrackingState(candidate, migrationAndFirstTurnCandle);
    }

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
