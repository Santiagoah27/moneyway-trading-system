using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Canonical Application entry point for historical strategy backtesting. It executes the synchronized
/// multi-timeframe pipeline through safe strategy outcomes and auditable diagnostics. It performs one historical
/// replay and contains no trade simulation.
/// </summary>
public sealed class GenerateCanonicalMultiTimeframeBacktestUseCase(
    GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase outcomeRunUseCase,
    GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase diagnosticsUseCase)
{
    private readonly GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase outcomeRunUseCase = outcomeRunUseCase
        ?? throw new ArgumentNullException(nameof(outcomeRunUseCase));
    private readonly GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase diagnosticsUseCase = diagnosticsUseCase
        ?? throw new ArgumentNullException(nameof(diagnosticsUseCase));

    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(series);

        var outcomeRun = outcomeRunUseCase.Execute(strategyDefinition, series);
        var report = diagnosticsUseCase.Execute(strategyDefinition, outcomeRun);
        if (!ReferenceEquals(report.OutcomeRun, outcomeRun)
            || report.StrategyId != strategyDefinition.StrategyId
            || report.StrategyVersion != strategyDefinition.Version)
        {
            throw new InvalidOperationException("Canonical diagnostics do not match the generated strategy outcome run.");
        }
        return report;
    }

    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations);

        var outcomeRun = outcomeRunUseCase.Execute(strategyDefinition, series, marketPriceObservations);
        var report = diagnosticsUseCase.Execute(strategyDefinition, outcomeRun);
        if (!ReferenceEquals(report.OutcomeRun, outcomeRun)
            || report.StrategyId != strategyDefinition.StrategyId
            || report.StrategyVersion != strategyDefinition.Version)
        {
            throw new InvalidOperationException("Canonical diagnostics do not match the generated strategy outcome run.");
        }
        return report;
    }

    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        NasdaqPreparationCompletionObservationSeries preparationObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(preparationObservations);
        var outcomeRun = outcomeRunUseCase.Execute(strategyDefinition, series, preparationObservations);
        var report = diagnosticsUseCase.Execute(strategyDefinition, outcomeRun);
        if (!ReferenceEquals(report.OutcomeRun, outcomeRun)
            || report.StrategyId != strategyDefinition.StrategyId
            || report.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Canonical diagnostics do not match the generated strategy outcome run.");
        return report;
    }

    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        IEnumerable<IStrategyReplayInputObservation> inputObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(inputObservations);
        var outcomeRun = outcomeRunUseCase.Execute(strategyDefinition, series, inputObservations);
        var report = diagnosticsUseCase.Execute(strategyDefinition, outcomeRun);
        if (!ReferenceEquals(report.OutcomeRun, outcomeRun)
            || report.StrategyId != strategyDefinition.StrategyId
            || report.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Canonical diagnostics do not match the generated strategy outcome run.");
        return report;
    }

    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations,
        NasdaqPreparationCompletionObservationSeries preparationObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations); ArgumentNullException.ThrowIfNull(preparationObservations);
        var outcomeRun = outcomeRunUseCase.Execute(strategyDefinition, series, marketPriceObservations, preparationObservations);
        var report = diagnosticsUseCase.Execute(strategyDefinition, outcomeRun);
        if (!ReferenceEquals(report.OutcomeRun, outcomeRun)
            || report.StrategyId != strategyDefinition.StrategyId
            || report.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Canonical diagnostics do not match the generated strategy outcome run.");
        return report;
    }

    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations,
        IEnumerable<IStrategyReplayInputObservation> inputObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations); ArgumentNullException.ThrowIfNull(inputObservations);
        var outcomeRun = outcomeRunUseCase.Execute(strategyDefinition, series, marketPriceObservations, inputObservations);
        var report = diagnosticsUseCase.Execute(strategyDefinition, outcomeRun);
        if (!ReferenceEquals(report.OutcomeRun, outcomeRun)
            || report.StrategyId != strategyDefinition.StrategyId
            || report.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Canonical diagnostics do not match the generated strategy outcome run.");
        return report;
    }
}
