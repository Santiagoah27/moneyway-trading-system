using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Observes strict new-high reset and prior-HL body-close invalidation facts for an active bullish correction.
/// It does not decide precedence, correction transitions, structural validation, or candle membership.
/// </summary>
public sealed class BullishCorrectionTerminalObservationCalculator
{
    private readonly CorrectionBoundaryObservationCalculator boundaryCalculator = new();
    private readonly StructuralBodyCloseBreakCalculator structuralBreakCalculator = new();

    public BullishCorrectionTerminalObservationResult EvaluateCurrentBoundary(
        StrategyReplayContext context,
        Timeframe timeframe,
        decimal currentCorrectionOriginCeiling,
        decimal priorValidatedHl)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeframe);

        var invalidation = structuralBreakCalculator.EvaluateCurrentBoundary(
            context,
            timeframe,
            priorValidatedHl,
            StructuralBreakDirection.Lower);

        if (invalidation.Candle is null)
        {
            return new(
                context.ProviderId,
                context.Symbol,
                timeframe,
                context.AsOfUtc,
                currentCorrectionOriginCeiling,
                currentCorrectionOriginCeiling,
                null,
                null,
                false,
                false);
        }

        var boundary = boundaryCalculator.Evaluate(
            currentCorrectionOriginCeiling,
            invalidation.Candle,
            CorrectionOriginExtremeSide.Ceiling);

        return new(
            context.ProviderId,
            context.Symbol,
            timeframe,
            context.AsOfUtc,
            boundary.PreviousExtreme,
            boundary.ResultingExtreme,
            invalidation.Candle,
            boundary.BodyDirection,
            boundary.WasExtremeUpdated,
            invalidation.IsConfirmed);
    }
}
