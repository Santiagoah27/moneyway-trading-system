using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Classifies one closed candle while awaiting a correction start, using already-observed
/// body direction and origin-extreme facts. It does not select historical turn boundaries.
/// </summary>
public sealed class CorrectionStartTransitionCalculator
{
    private readonly CorrectionBodyDirectionCalculator correctionBodyDirectionCalculator = new();

    public CorrectionStartTransitionResult Evaluate(
        CorrectionBoundaryObservationResult observation,
        Candle currentCandle)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(currentCandle);

        var startsCorrection = correctionBodyDirectionCalculator.Evaluate(observation.Side, observation.BodyDirection);

        return new CorrectionStartTransitionResult(
            startsCorrection ? CorrectionStartTransitionKind.StartCorrection : CorrectionStartTransitionKind.AwaitCorrectionStart,
            observation,
            startsCorrection ? currentCandle : null);
    }
}
