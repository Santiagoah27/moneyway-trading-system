using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Classifies one candidate H4 candle and delegates to exactly one ADR 0013 transition primitive.</summary>
public sealed class NasdaqPostInvalidationCandidateTransitionCalculator
{
    private readonly NasdaqPostInvalidationCandidateContinuationCalculator continuationCalculator = new();
    private readonly NasdaqDirectCandidateBreakoutCompletionCalculator directBreakoutCalculator = new();
    private readonly NasdaqPostInvalidationCandidateRebuildTransitionCalculator rebuildCalculator = new();
    private readonly NasdaqPostInvalidationCandidateTrackingStartCalculator trackingStartCalculator = new();
    private readonly NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator collisionCalculator = new();
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();

    public NasdaqPostInvalidationCandidateTransitionResult Evaluate(
        NasdaqPostInvalidationCandidateState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);
        ValidateNextCandle(current, candle);

        var migrated = current.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => candle.Low < current.CandidateGeometry.ProtectionAnchor,
            StructuralCandidateExtremeSide.Upper => candle.High > current.CandidateGeometry.ProtectionAnchor,
            _ => throw new ArgumentOutOfRangeException(nameof(current), "The candidate side is not supported."),
        };
        var brokeOut = current.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => candle.Close > current.FrozenImpulseTerminal.StructuralPrice,
            StructuralCandidateExtremeSide.Upper => candle.Close < current.FrozenImpulseTerminal.StructuralPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(current), "The candidate side is not supported."),
        };

        if (!migrated)
        {
            return brokeOut
                ? new NasdaqPostInvalidationCandidateTransitionResult.DirectCompleted(directBreakoutCalculator.Evaluate(current, candle))
                : new NasdaqPostInvalidationCandidateTransitionResult.CandidateContinues(continuationCalculator.Evaluate(current, candle));
        }

        if (brokeOut)
            return new NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout(collisionCalculator.Evaluate(current, candle));

        var expectedBody = current.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? CandleBodyDirection.Bullish
            : CandleBodyDirection.Bearish;
        return bodyDirectionCalculator.Evaluate(candle) == expectedBody
            ? new NasdaqPostInvalidationCandidateTransitionResult.RebuiltTracking(trackingStartCalculator.Evaluate(current, candle))
            : new NasdaqPostInvalidationCandidateTransitionResult.RebuildPending(rebuildCalculator.Evaluate(current, candle));
    }

    private static void ValidateNextCandle(NasdaqPostInvalidationCandidateState current, Candle candle)
    {
        var previous = current.LastProcessedCandle;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || candle.Timeframe != previous.Timeframe
            || candle.OpenTimeUtc <= previous.OpenTimeUtc
            || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The next candle must follow the candidate in the same H4 series.", nameof(candle));
        }
    }
}
