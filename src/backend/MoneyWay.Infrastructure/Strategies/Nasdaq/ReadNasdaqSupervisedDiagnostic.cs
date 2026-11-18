using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.StrategyReplay.Workflow;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Application-facing association over the existing bounded reader; never evaluates rules or creates eligibility.</summary>
public sealed class ReadNasdaqSupervisedDiagnostic(LocalNasdaqExecutionEvidenceStore store, ReadNasdaqHistoricalReport reader,
    string? configuredPath, string? configuredHash)
{
    public async Task<NasdaqSupervisedDiagnosticContext> ReadAsync(string sessionKey, long? step = null, string? hash = null,
        DateTimeOffset? at = null, CancellationToken cancellationToken = default)
    {
        if (step is <= 0 || hash is not null && (hash.Length != 64 || !hash.All(char.IsAsciiHexDigit))) throw new ArgumentException("Invalid diagnostic reference.");
        var session = store.Session(sessionKey);
        if (session.Path != "Standard") throw new NasdaqEvidenceConflictException("StandardDiagnosticRequiresStandardSession_Wf2Unchanged");
        if (string.IsNullOrWhiteSpace(configuredPath)) throw new KeyNotFoundException("ReportNotConfigured");
        if (!Path.IsPathFullyQualified(configuredPath) || configuredPath.StartsWith(@"\\", StringComparison.Ordinal) || configuredPath.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid server report configuration.");
        NasdaqHistoricalReportSummary report;
        try { report = await reader.ExecuteAsync(configuredPath, configuredHash, cancellationToken, step); }
        catch (Exception error) when (error is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Configured canonical report is invalid or unsupported; no diagnostic substitution.", error); }
        if (hash is not null && !string.Equals(hash, report.ReportSha256, StringComparison.OrdinalIgnoreCase)) throw new NasdaqEvidenceConflictException("ReportChanged_NoSubstitution");
        var identity = session.Identity;
        if (report.StrategyId != identity.StrategyId.Value || report.StrategyVersion != identity.StrategyVersion.Value
            || report.ProviderId != identity.ProviderId.Value || report.Symbol != identity.Symbol.Value || report.TradingDay != identity.TradingDay)
            throw new NasdaqEvidenceConflictException("DiagnosticSessionStrategyVersionDateProviderOrInstrumentMismatch_NoMapping");
        var frame = report.SelectedFrame ?? throw new KeyNotFoundException("FrameNotInSelectedSession");
        if (frame.Verdict is null || frame.Rules.Count == 0) throw new InvalidDataException("CanonicalObservationUnavailable");
        if (at is not null && frame.AsOfUtc != at || NasdaqSupervisedSessionPolicy.TradingDay(frame.AsOfUtc) != identity.TradingDay)
            throw new NasdaqEvidenceConflictException("DiagnosticFrameTimestampMismatch");
        if (frame.TradeLifecycle?.Path == "WickfillScenario2" || frame.Rules.Any(r => r.RuleId == "NQ-WF2-002" && r.Result == "Passed")
            || frame.Rules.Any(r => r.RuleId == "NQ-M5-001" && r.Result == "NotApplicable"))
            throw new NasdaqEvidenceConflictException("CanonicalWf2FrameCannotBeRelabeledStandard");
        var workflow = new StrategyReplayWorkflowCatalog(new MoneyWay.Application.StrategyDefinitions.StrategyDefinitionCatalog().GetAll(),
            MoneyWayReplayWorkflowDefinitions.GetAll()).Find(identity.StrategyId, identity.StrategyVersion)
            ?? throw new InvalidDataException("ExactVersionWorkflowUnavailable");
        var dependencies = workflow.RulePrerequisites.Select(p => new NasdaqCanonicalRuleDependency(p.DownstreamRuleId.Value,
            p.PrerequisiteRuleIds.Select(r => r.Value).ToArray(), p.AlternativePrerequisiteRuleId?.Value, p.RequiresLatestPrerequisitePass)).ToArray();
        return new(new(session.SessionKey, report.ReportSha256, frame.Step, frame.AsOfUtc), store.Session(sessionKey), report, dependencies);
    }

    public async Task ValidateAsync(NasdaqCanonicalDiagnosticReference? reference, NasdaqSupervisedExecutionEvidenceDraft? evidence = null,
        CancellationToken cancellationToken = default)
    {
        if (reference is null) return;
        reference.Validate();
        var context = await ReadAsync(reference.SessionKey, reference.FrameStep, reference.ReportSha256, reference.AsOfUtc, cancellationToken);
        if (evidence is not null && (evidence.SessionId != context.Session.SessionId || evidence.TradingDay != context.Report.TradingDay
            || evidence.SourceProviderId != context.Report.ProviderId || evidence.OriginalSymbol != context.Report.Symbol || evidence.Scenario != "Standard"))
            throw new ArgumentException("Documentary diagnostic association requires the exact declared Standard session/date/provider/instrument; no inferred mapping.");
    }
}
