using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Generates a neutral replay report from an already validated <see cref="CandleSeries"/>.
/// It does not load files and does not evaluate trading strategies.
/// </summary>
public sealed class GenerateHistoricalReplayReportUseCase
{
    private readonly RunCandleReplayUseCase replayUseCase = new();

    public HistoricalReplayReport Execute(CandleSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var replayResult = replayUseCase.Execute(series, _ => { });

        return new HistoricalReplayReport(
            replayResult.ProviderId,
            replayResult.Symbol,
            replayResult.Timeframe,
            series.Count,
            series.StartTimeUtc,
            series.EndTimeUtc,
            replayResult.FramesProcessed,
            replayResult.FirstAsOfUtc,
            replayResult.LastAsOfUtc);
    }
}
