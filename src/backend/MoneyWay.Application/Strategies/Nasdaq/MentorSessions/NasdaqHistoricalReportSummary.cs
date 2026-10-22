namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Read-only projection of persisted v2 evidence; never evaluates a strategy.</summary>
public sealed record NasdaqHistoricalReportSummary(
    string SessionId, string StrategyId, string StrategyVersion, string ProviderId, string Symbol,
    DateOnly TradingDay, int SchemaVersion, long FrameCount, string? FinalVerdict,
    DateTimeOffset? ReplayStartUtc, DateTimeOffset? ReplayEndUtc,
    string ReportSha256, long ReportBytes, string Validation, string EvidenceValidation,
    IReadOnlyList<string> InputDiagnosticCodes, IReadOnlyList<string> BindingDiagnosticCodes,
    NasdaqHistoricalFrameSummary? SelectedSessionLastFrame,
    NasdaqHistoricalFrameSummary? SelectedFrame = null,
    NasdaqHistoricalFrameNavigation? Navigation = null);

/// <summary>Actual report-global, one-based Steps encountered on the declared session day.</summary>
public sealed record NasdaqHistoricalFrameNavigation(long? FirstStep, long? LastStep, long? SelectedStep,
    long? PreviousStep, long? NextStep, long SessionFrameCount);

public sealed record NasdaqHistoricalFrameSummary(
    long Step, DateTimeOffset AsOfUtc, string? Verdict, string? Reason,
    string? BlockingRuleId, string? BlockingResult, bool? HasCompleteRequiredCoverage,
    IReadOnlyList<string> MissingRequiredRuleIds,
    string? SnapshotState, string? SnapshotDiagnostic, string? FactualEvaluationState,
    bool? FactualRequiresHumanValidation, IReadOnlyList<NasdaqHistoricalRuleSummary> Rules);

public sealed record NasdaqHistoricalRuleSummary(
    string RuleId, string DefinitionStatus, string Result, int Sequence, bool IsRequired,
    string Reason, DateTimeOffset EvaluatedAtUtc, string? EvidenceId);
