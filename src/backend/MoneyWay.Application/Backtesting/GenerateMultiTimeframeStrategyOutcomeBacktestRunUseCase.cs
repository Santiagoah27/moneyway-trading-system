using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Generates one canonical multi-timeframe strategy run and derives one safe context outcome for each synchronized
/// strategy observation. It performs no additional market replay and no trade simulation.
/// </summary>
public sealed class GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(
    GenerateMultiTimeframeStrategyBacktestRunUseCase strategyBacktestUseCase,
    EvaluateStrategyReplayContextOutcomeUseCase outcomeUseCase)
{
    private readonly GenerateMultiTimeframeStrategyBacktestRunUseCase strategyBacktestUseCase = strategyBacktestUseCase ?? throw new ArgumentNullException(nameof(strategyBacktestUseCase));
    private readonly EvaluateStrategyReplayContextOutcomeUseCase outcomeUseCase = outcomeUseCase ?? throw new ArgumentNullException(nameof(outcomeUseCase));

    public MultiTimeframeStrategyOutcomeBacktestRun Execute(StrategyDefinition strategyDefinition, IEnumerable<CandleSeries> series)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        var strategyRun = strategyBacktestUseCase.Execute(strategyDefinition, series);
        var outcomes = new List<StrategyReplayContextOutcome>(strategyRun.ObservationCount);
        foreach (var observation in strategyRun.StrategyObservations)
        {
            var outcome = outcomeUseCase.Execute(strategyDefinition, observation);
            if (outcome.StrategyId != observation.StrategyId || outcome.StrategyVersion != observation.StrategyVersion
                || outcome.ProviderId != observation.ProviderId || outcome.Symbol != observation.Symbol
                || outcome.Step != observation.Step || outcome.AsOfUtc != observation.AsOfUtc)
                throw new InvalidOperationException("Outcome is inconsistent with the strategy observation.");
            outcomes.Add(outcome);
        }
        if (outcomes.Count != strategyRun.ObservationCount) throw new InvalidOperationException("Outcome count is inconsistent with the strategy run.");
        return new(strategyRun, outcomes);
    }
}
