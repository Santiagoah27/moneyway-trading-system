using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Represents a completed canonical multi-timeframe strategy replay before outcome aggregation. It aligns one compact
/// market observation with one strategy-context observation per global step and contains no verdict or trade simulation.
/// </summary>
public sealed class MultiTimeframeStrategyBacktestRun
{
    public MultiTimeframeStrategyBacktestRun(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        IEnumerable<Timeframe> configuredTimeframes,
        IEnumerable<MultiTimeframeBacktestObservation> marketObservations,
        IEnumerable<StrategyReplayContextObservation> strategyObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(providerId); ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(configuredTimeframes); ArgumentNullException.ThrowIfNull(marketObservations); ArgumentNullException.ThrowIfNull(strategyObservations);
        var configured = configuredTimeframes.ToArray(); var market = marketObservations.ToArray(); var strategy = strategyObservations.ToArray();
        if (configured.Length == 0 || configured.Any(x => x is null) || configured.Distinct().Count() != configured.Length
            || !configured.SequenceEqual(configured.OrderBy(x => x.Unit).ThenBy(x => x.Amount)))
            throw new ArgumentException("Configured timeframes must be non-empty, non-null, unique, and ordered.", nameof(configuredTimeframes));
        if (market.Any(x => x is null)) throw new ArgumentException("Market observations cannot contain null.", nameof(marketObservations));
        if (strategy.Any(x => x is null)) throw new ArgumentException("Strategy observations cannot contain null.", nameof(strategyObservations));
        if (market.Length != strategy.Length) throw new ArgumentException("Market and strategy observation counts must match.", nameof(strategyObservations));
        for (var index = 0; index < market.Length; index++)
        {
            var marketItem = market[index]; var strategyItem = strategy[index]; var expectedStep = index + 1;
            if (marketItem.Step != expectedStep || strategyItem.Step != expectedStep)
                throw new ArgumentException("Observation steps must be consecutive and aligned.", nameof(marketObservations));
            if (marketItem.AsOfUtc != strategyItem.AsOfUtc || (index > 0 && marketItem.AsOfUtc <= market[index - 1].AsOfUtc))
                throw new ArgumentException("Observation timestamps must be aligned and strictly increasing.", nameof(marketObservations));
            if (strategyItem.StrategyId != strategyId || strategyItem.StrategyVersion != strategyVersion || strategyItem.ProviderId != providerId || strategyItem.Symbol != symbol)
                throw new ArgumentException("Strategy observations must match run identity.", nameof(strategyObservations));
            ValidateTimeframes(marketItem.UpdatedTimeframes, configured, nameof(marketObservations));
            ValidateTimeframes(marketItem.AvailableTimeframes, configured, nameof(marketObservations));
        }
        StrategyId = strategyId; StrategyVersion = strategyVersion; ProviderId = providerId; Symbol = symbol;
        ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(configured);
        MarketObservations = new ReadOnlyCollection<MultiTimeframeBacktestObservation>(market);
        StrategyObservations = new ReadOnlyCollection<StrategyReplayContextObservation>(strategy);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public IReadOnlyList<MultiTimeframeBacktestObservation> MarketObservations { get; }
    public IReadOnlyList<StrategyReplayContextObservation> StrategyObservations { get; }
    public int ObservationCount => MarketObservations.Count;
    public DateTimeOffset? FirstAsOfUtc => ObservationCount == 0 ? null : MarketObservations[0].AsOfUtc;
    public DateTimeOffset? LastAsOfUtc => ObservationCount == 0 ? null : MarketObservations[^1].AsOfUtc;

    private static void ValidateTimeframes(IReadOnlyList<Timeframe> values, IReadOnlyList<Timeframe> configured, string parameterName)
    {
        if (values.Any(x => !configured.Contains(x))) throw new ArgumentException("Observation contains an unknown timeframe.", parameterName);
        var configuredPositions = configured.Select((timeframe, index) => (timeframe, index)).ToDictionary(x => x.timeframe, x => x.index);
        var positions = values.Select(value => configuredPositions[value]).ToArray();
        if (positions.Where((value, index) => index > 0 && value <= positions[index - 1]).Any())
            throw new ArgumentException("Observation timeframes must follow configured order.", parameterName);
    }
}
