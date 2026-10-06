using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

public sealed record NasdaqMentorSessionInputDiagnostic(string Code, string Message,
    Timeframe? Timeframe = null, string? SourceReference = null, int? LineNumber = null, string? ColumnName = null);
public sealed record NasdaqMentorSessionBindingDiagnostic(string RecordId, string Code, DateTimeOffset AsOfUtc);
public sealed record NasdaqMentorSessionImport(Timeframe Timeframe, string SourceReference, int CandleCount,
    DateTimeOffset? StartUtc, DateTimeOffset? EndUtc);

/// <summary>Separate historical artifacts at one causal boundary; strategy remains the canonical observation.</summary>
public sealed class NasdaqMentorSessionFrame
{
    internal NasdaqMentorSessionFrame(StrategyReplayContextObservation strategy,
        IEnumerable<IStrategyReplayInputObservation> inputs, NasdaqHistoricalTradeSnapshotResult? snapshot,
        string? snapshotDiagnostic, NasdaqHistoricalTradeContactResolution? contacts,
        NasdaqHistoricalDocumentedExitSelection? exit, NasdaqHistoricalFactualTradeEvaluation? factual)
    {
        Strategy = strategy; Inputs = Array.AsReadOnly(inputs.ToArray()); Snapshot = snapshot;
        SnapshotDiagnostic = snapshotDiagnostic; ContactResolution = contacts; DocumentedExit = exit; FactualEvaluation = factual;
    }
    public StrategyReplayContextObservation Strategy { get; }
    public IReadOnlyList<IStrategyReplayInputObservation> Inputs { get; }
    public NasdaqHistoricalTradeSnapshotResult? Snapshot { get; }
    public string? SnapshotDiagnostic { get; }
    public NasdaqHistoricalTradeContactResolution? ContactResolution { get; }
    public NasdaqHistoricalDocumentedExitSelection? DocumentedExit { get; }
    public NasdaqHistoricalFactualTradeEvaluation? FactualEvaluation { get; }
}

/// <summary>One immutable report. Input failure does not fabricate canonical rule evaluations or strategy verdicts.</summary>
public sealed class NasdaqMentorSessionReplayReport
{
    public NasdaqMentorSessionReplayReport(NasdaqMentorSessionReplayInput input,
        IEnumerable<NasdaqMentorSessionImport> imports, IEnumerable<NasdaqMentorSessionInputDiagnostic> inputDiagnostics,
        MultiTimeframeStrategyBacktestDiagnosticsReport? canonical = null,
        IEnumerable<NasdaqMentorSessionFrame>? frames = null,
        IEnumerable<NasdaqMentorSessionBindingDiagnostic>? bindingDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(imports); ArgumentNullException.ThrowIfNull(inputDiagnostics);
        SessionId = input.SessionId; Session = input.Session; SuppliedCsvFiles = input.CsvFiles;
        Imports = Array.AsReadOnly(imports.ToArray()); InputDiagnostics = Array.AsReadOnly(inputDiagnostics.ToArray());
        Canonical = canonical; Frames = Array.AsReadOnly((frames ?? []).ToArray());
        BindingDiagnostics = Array.AsReadOnly((bindingDiagnostics ?? []).ToArray());
        if (canonical is not null && (InputDiagnostics.Count != 0 || canonical.Frames.Count != Frames.Count
            || canonical.StrategyId != Session.StrategyId || canonical.StrategyVersion != Session.StrategyVersion
            || canonical.ProviderId != Session.ProviderId || canonical.Symbol != Session.Symbol
            || !canonical.OutcomeRun.StrategyRun.StrategyObservations.SequenceEqual(Frames.Select(f => f.Strategy))))
            throw new ArgumentException("Session frames must retain the exact canonical run observations.");
    }
    public string SessionId { get; }
    public NasdaqDemoSessionIdentity Session { get; }
    public IReadOnlyDictionary<Timeframe, string> SuppliedCsvFiles { get; }
    public IReadOnlyList<NasdaqMentorSessionImport> Imports { get; }
    public IReadOnlyList<NasdaqMentorSessionInputDiagnostic> InputDiagnostics { get; }
    public IReadOnlyList<NasdaqMentorSessionBindingDiagnostic> BindingDiagnostics { get; }
    public MultiTimeframeStrategyBacktestDiagnosticsReport? Canonical { get; }
    public IReadOnlyList<NasdaqMentorSessionFrame> Frames { get; }
    public DateTimeOffset? ReplayStartUtc => Frames.FirstOrDefault()?.Strategy.AsOfUtc;
    public DateTimeOffset? ReplayEndUtc => Frames.LastOrDefault()?.Strategy.AsOfUtc;
}
