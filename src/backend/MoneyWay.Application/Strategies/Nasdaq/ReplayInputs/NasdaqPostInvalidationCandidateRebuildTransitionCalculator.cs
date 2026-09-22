using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Creates a rebuild-pending state from exactly one strict migration without a frozen-terminal breakout.</summary>
public sealed class NasdaqPostInvalidationCandidateRebuildTransitionCalculator
{
    public NasdaqPostInvalidationCandidateRebuildPendingState Evaluate(
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

        var migration = current.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => candle.Low < current.CandidateGeometry.ProtectionAnchor,
            StructuralCandidateExtremeSide.Upper => candle.High > current.CandidateGeometry.ProtectionAnchor,
            _ => throw new ArgumentOutOfRangeException(nameof(current), "The candidate side is not supported."),
        };
        var validates = current.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => candle.Close > current.FrozenImpulseTerminal.StructuralPrice,
            StructuralCandidateExtremeSide.Upper => candle.Close < current.FrozenImpulseTerminal.StructuralPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(current), "The candidate side is not supported."),
        };

        if (!migration || validates)
        {
            throw new ArgumentException("The candle must strictly migrate the candidate without validating the frozen terminal.", nameof(candle));
        }

        var knownProtectionAnchor = current.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? candle.Low
            : candle.High;
        return new NasdaqPostInvalidationCandidateRebuildPendingState(current, candle, knownProtectionAnchor);
    }
}
