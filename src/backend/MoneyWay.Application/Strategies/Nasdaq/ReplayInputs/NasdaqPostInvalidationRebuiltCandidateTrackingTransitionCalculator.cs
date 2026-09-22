using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Advances a rebuilt provisional turn by exactly one later closed H4 candle.</summary>
public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();
    private readonly NasdaqPostInvalidationRebuiltCandidateTrackingInitializer trackingInitializer = new();

    public NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult Evaluate(
        NasdaqPostInvalidationRebuiltCandidateTrackingState current,
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

        if (brokeOut)
        {
            var collisionKind = migrated
                ? startsTurn
                    ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                    : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired
                : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None;
            var effectiveAnchor = migrated
                ? current.CandidateSide == StructuralCandidateExtremeSide.Lower ? candle.Low : candle.High
                : current.KnownProtectionAnchor;
            return NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult.DetectBreakout(
                new NasdaqPostInvalidationCandidateRebuildBreakoutState(current, candle, migrated, effectiveAnchor, collisionKind));
        }

        if (migrated)
        {
            var pending = new NasdaqPostInvalidationCandidateRebuildPendingState(current, candle);
            return startsTurn
                ? NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult.Restart(
                    trackingInitializer.Initialize(pending, candle))
                : NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult.Reset(pending);
        }

        return NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult.Continue(
            new NasdaqPostInvalidationRebuiltCandidateTrackingState(current, candle));
    }
}
