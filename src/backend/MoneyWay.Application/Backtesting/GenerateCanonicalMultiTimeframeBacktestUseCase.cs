using MoneyWay.Application.Backtesting.Diagnostics;
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
}
