using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Advances one active opposite correction with one later closed H4 candle.</summary>
public sealed class NasdaqPostInvalidationCorrectionTransitionCalculator
{
    private readonly CandleBodyDirectionCalculator bodyDirectionCalculator = new();
    private readonly CorrectionCandidateTurnCalculator turnCalculator = new();

    public NasdaqPostInvalidationCorrectionTransitionResult Evaluate(
        NasdaqPostInvalidationCorrectionState current,
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

        var result = turnCalculator.Evaluate(current.CorrectionTurnCandles, current.CorrectionGeometry,
            current.CandidateSide, candle, bodyDirectionCalculator.Evaluate(candle));
        return result switch
        {
            CorrectionCandidateTurnResult.ContinuingCorrection continuing =>
                NasdaqPostInvalidationCorrectionTransitionResult.Continue(new NasdaqPostInvalidationCorrectionState(
                    current.Episode, current.InvalidatingCandle, current.ImpulseTerminalSide, current.CandidateSide,
                    current.OriginGeometry, current.FrozenImpulseTerminal, current.CorrectionStartCandle,
                    continuing.CorrectionTurnCandles, continuing.Geometry, continuing.MarketCursor)),
            CorrectionCandidateTurnResult.CandidateProvisional candidate =>
                NasdaqPostInvalidationCorrectionTransitionResult.FormCandidate(
                    new NasdaqPostInvalidationCandidateState(current, candidate.Geometry, candidate.TerminalCandle)),
            _ => throw new InvalidOperationException("The correction turn result is not supported."),
        };
    }
}
