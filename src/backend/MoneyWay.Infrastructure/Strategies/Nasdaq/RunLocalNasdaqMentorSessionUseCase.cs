using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Local file boundary for one reviewed session. Reuses the canonical Application owner and existing importer.</summary>
public sealed class RunLocalNasdaqMentorSessionUseCase
{
    public NasdaqMentorSessionReplayReport Execute(NasdaqMentorSessionReplayInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var imports = new List<NasdaqMentorSessionImport>();
        var diagnostics = new List<NasdaqMentorSessionInputDiagnostic>();
        var series = new List<CandleSeries>();
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        if (input.Session.StrategyId != definition.StrategyId || input.Session.StrategyVersion != definition.Version)
            diagnostics.Add(new("UnsupportedStrategyVersion", "Use the registered canonical Nasdaq definition."));
        foreach (var supplied in input.CsvFiles.Keys.Where(t => !NasdaqMentorSessionReplayInput.RequiredTimeframes.Contains(t)))
            diagnostics.Add(new("UnexpectedTimeframe", "Only the four declared session timeframes are accepted.", supplied));
        foreach (var timeframe in NasdaqMentorSessionReplayInput.RequiredTimeframes)
        {
            if (!input.CsvFiles.TryGetValue(timeframe, out var path) || string.IsNullOrWhiteSpace(path))
            { diagnostics.Add(new("MissingTimeframe", "Required local CSV was not supplied.", timeframe)); continue; }
            try
            {
                var imported = new CsvMarketDataImporter().ImportFile(path);
                if (imported.Timeframe != timeframe || imported.ProviderId != input.Session.ProviderId || imported.Symbol != input.Session.Symbol)
                { diagnostics.Add(new("SeriesIdentityMismatch", "Imported timeframe/provider/symbol differs from the declared session.", timeframe, path)); continue; }
                series.Add(imported);
                imports.Add(new(timeframe, path, imported.Count, imported.Candles.FirstOrDefault()?.OpenTimeUtc,
                    imported.Candles.LastOrDefault()?.CloseTimeUtc));
            }
            catch (MarketDataCsvImportException e)
            { diagnostics.Add(new(e.Code, e.Message, timeframe, path, e.LineNumber, e.ColumnName)); }
            catch (IOException e) { diagnostics.Add(new("FileUnavailable", e.Message, timeframe, path)); }
            catch (UnauthorizedAccessException e) { diagnostics.Add(new("FileUnavailable", e.Message, timeframe, path)); }
            catch (ArgumentException e) { diagnostics.Add(new("InvalidFileReference", e.Message, timeframe, path)); }
        }
        if (diagnostics.Count == 0)
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
            var minute = series.Single(s => s.Timeframe == new Timeframe(1, TimeframeUnit.Minute));
            if (!minute.Candles.Any(c => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.CloseTimeUtc, timezone).DateTime)
                == input.Session.TradingDay))
                diagnostics.Add(new("SelectedSessionDataUnavailable", "No 1M close belongs to the selected Bogota trading day."));
        }
        if (diagnostics.Count > 0) return new(input, imports, diagnostics);
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var progression = new AdvanceStrategyReplayProgressionUseCase();
        var strategyRun = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), progression,
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(progression));
        var canonical = new GenerateCanonicalMultiTimeframeBacktestUseCase(new(strategyRun, new()), new());
        var session = new NasdaqMentorSessionReplay(input);
        var report = canonical.ExecuteMentorSession(definition, series, session);
        return new(input, imports, diagnostics, report, session.Frames, session.Diagnostics);
    }
}
