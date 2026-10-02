using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.StructuralBreaks;

/// <summary>
/// Evaluates whether a supplied closed candle closes strictly beyond a supplied structural level.
/// The context-based API additionally selects the candle newly observable at a canonical replay boundary.
/// It does not select structural levels, candidates, pivots, or body coordinates.
/// </summary>
public sealed class StructuralBodyCloseBreakCalculator
{
    /// <summary>Evaluates an already-selected closed interval; replay visibility is the caller's responsibility.</summary>
    public StructuralBodyCloseBreakResult Evaluate(
        Candle candle,
        decimal referenceLevel,
        StructuralBreakDirection direction)
    {
        ArgumentNullException.ThrowIfNull(candle);
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));

        var isConfirmed = direction == StructuralBreakDirection.Upper
            ? candle.Close > referenceLevel
            : candle.Close < referenceLevel;

        return new(candle.ProviderId, candle.Symbol, candle.Timeframe, referenceLevel, direction, candle.CloseTimeUtc, candle, isConfirmed);
    }

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

        return Evaluate(frame!.CurrentCandle, referenceLevel, direction);
    }
}
