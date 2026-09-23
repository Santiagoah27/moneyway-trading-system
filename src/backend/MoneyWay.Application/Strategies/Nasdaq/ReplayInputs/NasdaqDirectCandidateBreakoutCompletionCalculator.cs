using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Completes one non-migrating strict frozen-terminal breakout of an existing provisional candidate.</summary>
public sealed class NasdaqDirectCandidateBreakoutCompletionCalculator
{
    public NasdaqDirectCandidateBreakoutCompletionResult Evaluate(
        NasdaqPostInvalidationCandidateState candidate,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candle);

        var previous = candidate.LastProcessedCandle;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4
            || candle.Timeframe != previous.Timeframe)
        {
            throw new ArgumentException("The validating candle must belong to the same H4 series.", nameof(candle));
        }

        if (candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
        {
            throw new ArgumentException("The validating candle must follow the last processed candle without overlap.", nameof(candle));
        }

        var direction = candidate.CandidateSide switch
        {
            StructuralCandidateExtremeSide.Lower => StructuralBreakDirection.Upper,
            StructuralCandidateExtremeSide.Upper => StructuralBreakDirection.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate), "The candidate side is not supported."),
        };
        var migrated = candidate.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? candle.Low < candidate.CandidateGeometry.ProtectionAnchor
            : candle.High > candidate.CandidateGeometry.ProtectionAnchor;
        var reference = candidate.FrozenImpulseTerminal.StructuralPrice;
        var breaksFrozenTerminal = direction == StructuralBreakDirection.Upper
            ? candle.Close > reference
            : candle.Close < reference;
        if (migrated || !breaksFrozenTerminal)
        {
            throw new ArgumentException("The candle must strictly break the frozen terminal without migrating the candidate.", nameof(candle));
        }

        var breakObservation = new StructuralBodyCloseBreakResult(
            candidate.Episode.ProviderId,
            candidate.Episode.Symbol,
            candidate.Episode.Timeframe,
            reference,
            direction,
            candle.CloseTimeUtc,
            candle,
            true);
        var validation = new StructuralCandidateValidationResult(
            candidate.CandidateSide,
            candidate.CandidateGeometry,
            breakObservation);
        return new NasdaqDirectCandidateBreakoutCompletionResult(candidate, candle, validation);
    }
}
