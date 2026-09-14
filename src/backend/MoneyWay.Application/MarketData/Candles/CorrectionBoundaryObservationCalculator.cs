using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Composes body-direction and correction-origin-extreme primitives into observable candle facts.
/// It does not decide correction start, candle membership, gaps, or structural validation.
/// </summary>
public sealed class CorrectionBoundaryObservationCalculator
{
    private readonly CandleBodyDirectionCalculator candleBodyDirectionCalculator = new();
    private readonly CorrectionOriginExtremeCalculator correctionOriginExtremeCalculator = new();

    public CorrectionBoundaryObservationResult Evaluate(
        decimal currentExtreme,
        Candle candle,
        CorrectionOriginExtremeSide side)
    {
        var bodyDirection = candleBodyDirectionCalculator.Evaluate(candle);
        var extremeObservation = correctionOriginExtremeCalculator.Evaluate(currentExtreme, candle, side);

        return new CorrectionBoundaryObservationResult(extremeObservation, bodyDirection);
    }
}
