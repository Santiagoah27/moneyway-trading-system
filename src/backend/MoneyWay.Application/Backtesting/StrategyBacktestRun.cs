using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Represents a completed historical replay aligned with per-frame strategy rule observations.
/// It contains no strategy verdict and no trade simulation.
/// </summary>
public sealed class StrategyBacktestRun
{
    public StrategyBacktestRun(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        BacktestRun marketReplay,
        IEnumerable<StrategyReplayFrameObservation> strategyObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(marketReplay);
        ArgumentNullException.ThrowIfNull(strategyObservations);

        var snapshot = strategyObservations.ToArray();
        if (snapshot.Any(observation => observation is null))
        {
            throw new ArgumentException("Strategy observations cannot contain null elements.", nameof(strategyObservations));
        }

        if (snapshot.Length != marketReplay.ObservationCount)
        {
            throw new ArgumentException("Strategy and market observation counts must match.", nameof(strategyObservations));
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            var strategyObservation = snapshot[index];
            var marketObservation = marketReplay.Observations[index];
            if (strategyObservation.StrategyId != strategyId || strategyObservation.StrategyVersion != strategyVersion)
            {
                throw new ArgumentException("Every strategy observation must match the run identity.", nameof(strategyObservations));
            }

            if (strategyObservation.Step != marketObservation.Step
                || strategyObservation.AsOfUtc != marketObservation.AsOfUtc
                || !ReferenceEquals(strategyObservation.CurrentCandle, marketObservation.CurrentCandle))
            {
                throw new ArgumentException("Strategy observations must align exactly with market observations.", nameof(strategyObservations));
            }
        }

        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        MarketReplay = marketReplay;
        StrategyObservations = new ReadOnlyCollection<StrategyReplayFrameObservation>(snapshot);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public BacktestRun MarketReplay { get; }
    public IReadOnlyList<StrategyReplayFrameObservation> StrategyObservations { get; }
    public int ObservationCount => MarketReplay.ObservationCount;
    public MarketDataProviderId ProviderId => MarketReplay.ProviderId;
    public MarketSymbol Symbol => MarketReplay.Symbol;
    public Timeframe Timeframe => MarketReplay.Timeframe;
    public DateTimeOffset? FirstAsOfUtc => MarketReplay.FirstAsOfUtc;
    public DateTimeOffset? LastAsOfUtc => MarketReplay.LastAsOfUtc;
}
