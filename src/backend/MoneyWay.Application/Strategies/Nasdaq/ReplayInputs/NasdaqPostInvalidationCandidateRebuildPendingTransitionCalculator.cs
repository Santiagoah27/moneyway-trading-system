using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Advances one rebuild-pending episode with one later closed H4 candle without completing definitive geometry.</summary>
public sealed class NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();
    private readonly NasdaqPostInvalidationRebuiltCandidateTrackingInitializer trackingInitializer = new();

    public NasdaqPostInvalidationCandidateRebuildPendingTransitionResult Evaluate(
        NasdaqPostInvalidationCandidateRebuildPendingState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);

        var previous = current.LastProcessedCandle;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || candle.Timeframe != previous.Timeframe)
        {
            throw new ArgumentException("The next candle must belong to the same H4 series.", nameof(candle));
        }

        if (candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The next closed candle must follow the last processed candle without overlap.", nameof(candle));
        }

        var migrated = current.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? candle.Low < current.KnownProtectionAnchor
            : candle.High > current.KnownProtectionAnchor;
        var brokeOut = current.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? candle.Close > current.FrozenImpulseTerminal.StructuralPrice
            : candle.Close < current.FrozenImpulseTerminal.StructuralPrice;
        var expectedTurnDirection = current.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? CandleBodyDirection.Bullish
            : CandleBodyDirection.Bearish;
        var startsTurn = bodyDirectionCalculator.Evaluate(candle) == expectedTurnDirection;
        var effectiveAnchor = migrated
            ? current.CandidateSide == StructuralCandidateExtremeSide.Lower ? candle.Low : candle.High
            : current.KnownProtectionAnchor;

        if (brokeOut)
        {
            var collisionKind = migrated
                ? startsTurn
                    ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                    : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired
                : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None;
            return NasdaqPostInvalidationCandidateRebuildPendingTransitionResult.DetectBreakout(
                new NasdaqPostInvalidationCandidateRebuildBreakoutState(current, candle, migrated, effectiveAnchor, collisionKind));
        }

        var pending = new NasdaqPostInvalidationCandidateRebuildPendingState(
            current, migrated ? candle : current.MigrationCandle, effectiveAnchor, candle);
        if (startsTurn)
            return NasdaqPostInvalidationCandidateRebuildPendingTransitionResult.StartTracking(
                trackingInitializer.Initialize(pending, candle));

        return migrated
            ? NasdaqPostInvalidationCandidateRebuildPendingTransitionResult.Reset(pending)
            : NasdaqPostInvalidationCandidateRebuildPendingTransitionResult.Continue(pending);
    }
}
