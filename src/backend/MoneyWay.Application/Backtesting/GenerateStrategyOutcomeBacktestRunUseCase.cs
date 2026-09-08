using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Generates outcomes for the transitional single-timeframe pipeline retained for backward compatibility with
/// earlier MoneyWay backtesting infrastructure. New strategy-rule and backtesting features must use the canonical
/// StrategyReplayContext multi-timeframe pipeline.
/// </summary>
public sealed class GenerateStrategyOutcomeBacktestRunUseCase(
    GenerateStrategyBacktestRunUseCase strategyBacktestUseCase,
    EvaluateStrategyReplayFrameOutcomeUseCase outcomeUseCase)
{
    private readonly GenerateStrategyBacktestRunUseCase strategyBacktestUseCase = strategyBacktestUseCase
        ?? throw new ArgumentNullException(nameof(strategyBacktestUseCase));
    private readonly EvaluateStrategyReplayFrameOutcomeUseCase outcomeUseCase = outcomeUseCase
        ?? throw new ArgumentNullException(nameof(outcomeUseCase));

    public StrategyOutcomeBacktestRun Execute(StrategyDefinition strategyDefinition, CandleSeries series)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(series);

        var strategyRun = strategyBacktestUseCase.Execute(strategyDefinition, series);
        var outcomes = strategyRun.StrategyObservations
            .Select(observation => outcomeUseCase.Execute(strategyDefinition, observation))
            .ToArray();

        return new StrategyOutcomeBacktestRun(strategyRun, outcomes);
    }
}
