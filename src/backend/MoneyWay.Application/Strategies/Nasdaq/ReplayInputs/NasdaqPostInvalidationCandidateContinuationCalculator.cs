using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Consumes one no-event H4 candle while retaining the existing provisional candidate unchanged.</summary>
public sealed class NasdaqPostInvalidationCandidateContinuationCalculator
{
    public NasdaqPostInvalidationCandidateState Evaluate(
        NasdaqPostInvalidationCandidateState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);

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
        if (migrated || brokeOut)
        {
            throw new ArgumentException("The candle must neither strictly migrate the candidate nor break the frozen terminal.", nameof(candle));
        }

        return current.WithLastProcessedCandle(candle);
    }
}
