using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Runs the transitional single-timeframe strategy backtest retained for backward compatibility with earlier
/// MoneyWay backtesting infrastructure. New strategy-rule and backtesting features must use the canonical
/// StrategyReplayContext multi-timeframe pipeline.
/// </summary>
public sealed class GenerateStrategyBacktestRunUseCase(
    RunCandleReplayUseCase replayUseCase,
    EvaluateStrategyReplayFrameUseCase frameEvaluationUseCase)
{
    private readonly RunCandleReplayUseCase replayUseCase = replayUseCase
        ?? throw new ArgumentNullException(nameof(replayUseCase));
    private readonly EvaluateStrategyReplayFrameUseCase frameEvaluationUseCase = frameEvaluationUseCase
        ?? throw new ArgumentNullException(nameof(frameEvaluationUseCase));

    public StrategyBacktestRun Execute(StrategyDefinition strategyDefinition, CandleSeries series)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(series);

        var marketObservations = new List<BacktestObservation>(series.Count);
        var strategyObservations = new List<StrategyReplayFrameObservation>(series.Count);
        var result = replayUseCase.Execute(series, frame =>
        {
            marketObservations.Add(new BacktestObservation(frame.Step, frame.AsOfUtc, frame.CurrentCandle));
            var strategyObservation = frameEvaluationUseCase.Execute(strategyDefinition, frame);
            if (strategyObservation.Step != frame.Step
                || strategyObservation.AsOfUtc != frame.AsOfUtc
                || !ReferenceEquals(strategyObservation.CurrentCandle, frame.CurrentCandle)
                || strategyObservation.StrategyId != strategyDefinition.StrategyId
                || strategyObservation.StrategyVersion != strategyDefinition.Version)
            {
                throw new InvalidOperationException("Strategy observation is inconsistent with the replay frame.");
            }
            strategyObservations.Add(strategyObservation);
        });

        if (result.FramesProcessed != marketObservations.Count
            || result.FramesProcessed != strategyObservations.Count
            || result.FramesProcessed != series.Count)
        {
            throw new InvalidOperationException("Replay output is inconsistent with the source series.");
        }

        var marketReplay = new BacktestRun(result.ProviderId, result.Symbol, result.Timeframe, marketObservations);
        return new StrategyBacktestRun(strategyDefinition.StrategyId, strategyDefinition.Version, marketReplay, strategyObservations);
    }
}
