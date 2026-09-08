using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Runs one synchronized multi-timeframe replay, creates and evaluates one strategy context per global step, and records
/// aligned observations. It performs no strategy-verdict aggregation or trading simulation.
/// </summary>
public sealed class GenerateMultiTimeframeStrategyBacktestRunUseCase(
    RunMultiTimeframeReplayUseCase replayUseCase,
    CreateStrategyReplayContextUseCase contextUseCase,
    EvaluateStrategyReplayContextUseCase evaluationUseCase)
{
    private readonly RunMultiTimeframeReplayUseCase replayUseCase = replayUseCase ?? throw new ArgumentNullException(nameof(replayUseCase));
    private readonly CreateStrategyReplayContextUseCase contextUseCase = contextUseCase ?? throw new ArgumentNullException(nameof(contextUseCase));
    private readonly EvaluateStrategyReplayContextUseCase evaluationUseCase = evaluationUseCase ?? throw new ArgumentNullException(nameof(evaluationUseCase));

    public MultiTimeframeStrategyBacktestRun Execute(StrategyDefinition strategyDefinition, IEnumerable<CandleSeries> series)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        var marketObservations = new List<MultiTimeframeBacktestObservation>();
        var strategyObservations = new List<StrategyReplayContextObservation>();
        var replayResult = replayUseCase.Execute(series, frame =>
        {
            var context = contextUseCase.Execute(strategyDefinition, frame);
            var marketObservation = new MultiTimeframeBacktestObservation(context.Step, context.AsOfUtc, context.UpdatedTimeframes, context.AvailableTimeframes);
            var strategyObservation = evaluationUseCase.Execute(strategyDefinition, context);
            if (strategyObservation.StrategyId != strategyDefinition.StrategyId || strategyObservation.StrategyVersion != strategyDefinition.Version
                || strategyObservation.ProviderId != context.ProviderId || strategyObservation.Symbol != context.Symbol
                || strategyObservation.Step != context.Step || strategyObservation.AsOfUtc != context.AsOfUtc)
                throw new InvalidOperationException("Strategy observation is inconsistent with the replay context.");
            marketObservations.Add(marketObservation); strategyObservations.Add(strategyObservation);
        });
        if (replayResult.GlobalFramesProcessed != marketObservations.Count || replayResult.GlobalFramesProcessed != strategyObservations.Count)
            throw new InvalidOperationException("Replay output is inconsistent with recorded observations.");
        return new(strategyDefinition.StrategyId, strategyDefinition.Version, replayResult.ProviderId, replayResult.Symbol,
            replayResult.ConfiguredTimeframes, marketObservations, strategyObservations);
    }
}
