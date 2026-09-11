using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.StructuralBreaks;

/// <summary>
/// Evaluates whether a candle newly observable at one canonical replay boundary closes strictly beyond a supplied structural level.
/// It does not select structural levels, candidates, pivots, or body coordinates.
/// </summary>
public sealed class StructuralBodyCloseBreakCalculator
{
    public StructuralBodyCloseBreakResult EvaluateCurrentBoundary(
        StrategyReplayContext context,
        Timeframe timeframe,
        decimal referenceLevel,
        StructuralBreakDirection direction)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeframe);
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));

        if (!context.WasUpdated(timeframe) || !context.TryGetFrame(timeframe, out var frame))
            return new(context.ProviderId, context.Symbol, timeframe, referenceLevel, direction, context.AsOfUtc, null, false);

        var candle = frame!.CurrentCandle;
        var isConfirmed = direction == StructuralBreakDirection.Upper
            ? candle.Close > referenceLevel
            : candle.Close < referenceLevel;

        return new(context.ProviderId, context.Symbol, timeframe, referenceLevel, direction, context.AsOfUtc, candle, isConfirmed);
    }
}
