using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Detects one same-candle strict migration and frozen-terminal breakout from a candidate.</summary>
public sealed class NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();

    public NasdaqPostInvalidationCandidateRebuildBreakoutState Evaluate(
        NasdaqPostInvalidationCandidateState current,
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

        var previousAnchor = current.CandidateGeometry.ProtectionAnchor;
        var (migrated, effectiveAnchor, brokeOut, expectedBody) = current.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower =>
                (candle.Low < previousAnchor, candle.Low,
                    candle.Close > current.FrozenImpulseTerminal.StructuralPrice, CandleBodyDirection.Bullish),
            StructuralCandidateExtremeSide.Upper =>
                (candle.High > previousAnchor, candle.High,
                    candle.Close < current.FrozenImpulseTerminal.StructuralPrice, CandleBodyDirection.Bearish),
            _ => throw new ArgumentOutOfRangeException(nameof(current), "The candidate side is not supported."),
        };
        if (!migrated || !brokeOut)
        {
            throw new ArgumentException("The candle must strictly migrate the candidate and break the frozen terminal.", nameof(candle));
        }

        var collisionKind = bodyDirectionCalculator.Evaluate(candle) == expectedBody
            ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
            : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired;
        return new NasdaqPostInvalidationCandidateRebuildBreakoutState(current, candle, effectiveAnchor, collisionKind);
    }
}
