using System.Globalization;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.PriceLevels;

/// <summary>
/// Detects strategy-neutral price-level touches from evidence newly observable at one canonical replay boundary.
/// Callers own any replay-scoped fold of prior touch results.
/// </summary>
public sealed class PriceLevelTouchCalculator
{
    public PriceLevelTouchResult EvaluateCurrentBoundary(
        StrategyReplayContext context,
        decimal targetPrice,
        PriceLevelDirection direction)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));

        var currentGroup = context.CurrentMarketPriceObservations;
        if (currentGroup is not null)
        {
            var matching = currentGroup.Observations.Where(item => Reaches(item.ObservedPrice, targetPrice, direction)).ToArray();
            if (matching.Length > 0)
            {
                var sourceSequence = currentGroup.HasAuthoritativeOrder ? matching[0].SourceSequence : null;
                var at = currentGroup.ObservedAtUtc;
                return new(
                    context.ProviderId,
                    context.Symbol,
                    targetPrice,
                    direction,
                    true,
                    PriceLevelTouchEvidenceKind.MarketPriceObservation,
                    new(EvidenceId(context, targetPrice, direction, "market-price"), at, at),
                    hasAuthoritativeSourceOrder: currentGroup.HasAuthoritativeOrder,
                    sourceSequence: sourceSequence);
            }
        }

        var candle = context.UpdatedTimeframes
            .Select(timeframe => context.TryGetFrame(timeframe, out var frame) ? frame?.CurrentCandle : null)
            .OfType<Candle>()
            .Where(item => ReachesCandle(item, targetPrice, direction))
            .OrderBy(item => item.CloseTimeUtc - item.OpenTimeUtc)
            .ThenBy(item => item.OpenTimeUtc)
            .ThenBy(item => item.Timeframe.Unit)
            .ThenBy(item => item.Timeframe.Amount)
            .FirstOrDefault();

        return candle is null
            ? new(context.ProviderId, context.Symbol, targetPrice, direction, false)
            : new(
                context.ProviderId,
                context.Symbol,
                targetPrice,
                direction,
                true,
                PriceLevelTouchEvidenceKind.CandleRange,
                new(EvidenceId(context, targetPrice, direction, "candle-range"), candle.OpenTimeUtc, candle.CloseTimeUtc),
                candle.Timeframe);
    }

    private static bool ReachesCandle(Candle candle, decimal targetPrice, PriceLevelDirection direction) =>
        direction == PriceLevelDirection.Lower
            ? candle.Low <= targetPrice
            : candle.High >= targetPrice;

    private static bool Reaches(decimal observedPrice, decimal targetPrice, PriceLevelDirection direction) =>
        direction == PriceLevelDirection.Lower
            ? observedPrice <= targetPrice
            : observedPrice >= targetPrice;

    private static string EvidenceId(
        StrategyReplayContext context,
        decimal targetPrice,
        PriceLevelDirection direction,
        string source) =>
        string.Join(
            ':',
            "price-level-touch",
            context.ProviderId.Value,
            context.Symbol.Value,
            direction,
            targetPrice.ToString(CultureInfo.InvariantCulture),
            context.Step.ToString(CultureInfo.InvariantCulture),
            source);
}
