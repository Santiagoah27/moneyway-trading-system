using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Consumes only an already-observed inside-range candle through the canonical correction calculation.</summary>
public sealed class NasdaqPostCompletionCorrectionTurnCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();
    private readonly CorrectionCandidateTurnCalculator turnCalculator = new();

    public NasdaqPostCompletionCorrectionTurnResult Evaluate(
        NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var source = observation.SourceState;
        var candidateSide = source.ActivePair.ProtectedTurn.CandidateSide;
        var geometry = geometryCalculator.Evaluate(source.CorrectionTurnCandles, source.ActivePair.ProtectedTurnGeometry.Side);
        var result = turnCalculator.Evaluate(source.CorrectionTurnCandles, geometry, candidateSide,
            observation.IncomingCandle, observation.BodyDirection);
        return result switch
        {
            CorrectionCandidateTurnResult.ContinuingCorrection continuing =>
                new NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection(observation, continuing),
            CorrectionCandidateTurnResult.CandidateProvisional candidate =>
                new NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional(observation, candidate),
            _ => throw new InvalidOperationException("The correction turn result is not supported."),
        };
    }
}
