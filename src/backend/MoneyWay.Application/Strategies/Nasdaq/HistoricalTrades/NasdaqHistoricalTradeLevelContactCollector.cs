using System.Collections.ObjectModel;
using System.Globalization;
using MoneyWay.Application.MarketData.PriceLevels;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Collects every visible candle source and current canonical price observation for the two exact snapshot levels.</summary>
public sealed class NasdaqHistoricalTradeLevelContactCollector
{
    public IReadOnlyList<NasdaqHistoricalTradeLevelContact> Collect(StrategyReplayContext context, NasdaqHistoricalTradeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.Session.Matches(context) || snapshot.AsOfUtc > context.AsOfUtc)
            throw new ArgumentException("Snapshot identity and availability must match the current replay context.", nameof(snapshot));
        var contacts = new List<NasdaqHistoricalTradeLevelContact>();
        var buy = snapshot.Direction == NasdaqHumanH4PermittedDirection.Buy;
        foreach (var role in new[] { NasdaqHistoricalTradeLevelRole.StopLoss, NasdaqHistoricalTradeLevelRole.TakeProfit })
        {
            var stop = role == NasdaqHistoricalTradeLevelRole.StopLoss;
            var level = stop ? snapshot.StopPrice : snapshot.TakeProfitPrice;
            var direction = stop == buy ? PriceLevelDirection.Lower : PriceLevelDirection.Upper;
            foreach (var timeframe in context.AvailableTimeframes.OrderBy(t => t.Unit).ThenBy(t => t.Amount))
            {
                context.TryGetFrame(timeframe, out var frame);
                foreach (var candle in frame!.AvailableCandles.Where(c => c.CloseTimeUtc <= context.AsOfUtc))
                {
                    if (!PriceLevelTouchCalculator.ReachesCandle(candle, level, direction)) continue;
                    var id = $"candle:{candle.ProviderId.Value}:{candle.Symbol.Value}:{timeframe}:{candle.OpenTimeUtc:O}:{candle.CloseTimeUtc:O}";
                    contacts.Add(new(snapshot, role, level, new(id, candle.OpenTimeUtc, candle.CloseTimeUtc), context.AsOfUtc, candle: candle));
                }
            }
            var group = context.CurrentMarketPriceObservations;
            if (group is not null && group.ObservedAtUtc <= context.AsOfUtc)
            {
                // Collection presentation order is diagnostic only; unsequenced equal-time prices remain unordered.
                foreach (var price in group.Observations.OrderBy(p => p.SourceSequence).ThenBy(p => p.ObservationKind, StringComparer.Ordinal)
                    .ThenBy(p => p.SourceResolution, StringComparer.Ordinal).ThenBy(p => p.ObservedPrice))
                {
                    if (!PriceLevelTouchCalculator.Reaches(price.ObservedPrice, level, direction)) continue;
                    var id = $"price:{price.ProviderId.Value}:{price.Symbol.Value}:{price.ObservedAtUtc:O}:{price.ObservationKind}:{price.SourceResolution}:{price.SourceSequence}:{price.ObservedPrice.ToString(CultureInfo.InvariantCulture)}";
                    contacts.Add(new(snapshot, role, level, new(id, price.ObservedAtUtc, price.ObservedAtUtc), context.AsOfUtc, price: price, group: group));
                }
            }
        }
        return new ReadOnlyCollection<NasdaqHistoricalTradeLevelContact>(contacts.ToArray());
    }
}
