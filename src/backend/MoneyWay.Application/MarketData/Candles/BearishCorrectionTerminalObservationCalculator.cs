using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>
/// Observes strict new-low reset and prior-LH body-close invalidation facts for an active bearish correction.
/// It does not decide precedence, correction transitions, structural validation, or candle membership.
/// </summary>
public sealed class BearishCorrectionTerminalObservationCalculator
{
    private readonly CorrectionBoundaryObservationCalculator boundaryCalculator = new();
    private readonly StructuralBodyCloseBreakCalculator structuralBreakCalculator = new();

    public BearishCorrectionTerminalObservationResult EvaluateCurrentBoundary(
        StrategyReplayContext context,
        Timeframe timeframe,
        decimal currentCorrectionOriginFloor,
        decimal priorValidatedLh)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeframe);

        var invalidation = structuralBreakCalculator.EvaluateCurrentBoundary(
            context,
            timeframe,
            priorValidatedLh,
            StructuralBreakDirection.Upper);

        if (invalidation.Candle is null)
        {
            return new(
                context.ProviderId,
                context.Symbol,
                timeframe,
                context.AsOfUtc,
                currentCorrectionOriginFloor,
                currentCorrectionOriginFloor,
                null,
                null,
                false,
                false);
        }

        var boundary = boundaryCalculator.Evaluate(
            currentCorrectionOriginFloor,
            invalidation.Candle,
            CorrectionOriginExtremeSide.Floor);

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
