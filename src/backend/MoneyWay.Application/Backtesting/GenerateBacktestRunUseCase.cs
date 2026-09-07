using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Builds a neutral <see cref="BacktestRun"/> by consuming replay frames from <see cref="RunCandleReplayUseCase"/>.
/// It performs no strategy or trading evaluation.
/// </summary>
public sealed class GenerateBacktestRunUseCase(RunCandleReplayUseCase replayUseCase)
{
    private readonly RunCandleReplayUseCase replayUseCase = replayUseCase
        ?? throw new ArgumentNullException(nameof(replayUseCase));

    public BacktestRun Execute(CandleSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var observations = new List<BacktestObservation>(series.Count);
        var result = replayUseCase.Execute(series, frame =>
            observations.Add(new BacktestObservation(frame.Step, frame.AsOfUtc, frame.CurrentCandle)));

        if (result.FramesProcessed != observations.Count || observations.Count != series.Count)
        {
            throw new InvalidOperationException("Replay output is inconsistent with the source series.");
        }

        return new BacktestRun(result.ProviderId, result.Symbol, result.Timeframe, observations);
    }
}
