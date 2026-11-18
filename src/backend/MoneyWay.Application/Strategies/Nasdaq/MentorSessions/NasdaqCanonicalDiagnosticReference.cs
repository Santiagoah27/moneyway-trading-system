namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Content-addressed report/frame association only; never an eligibility or execution claim.</summary>
public sealed record NasdaqCanonicalDiagnosticReference(string SessionKey, string ReportSha256, long FrameStep, DateTimeOffset AsOfUtc)
{
    public void Validate()
    {
        if (SessionKey is null || SessionKey.Length != 64 || !SessionKey.All(char.IsAsciiHexDigit)
            || ReportSha256 is null || ReportSha256.Length != 64 || !ReportSha256.All(char.IsAsciiHexDigit)
            || FrameStep <= 0 || AsOfUtc == default || AsOfUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Exact session key, report SHA256, positive canonical Step and UTC timestamp required.");
    }
}

public sealed record NasdaqCanonicalRuleDependency(string RuleId, IReadOnlyList<string> Prerequisites,
    string? AlternativePrerequisite, bool RequiresLatestPrerequisitePass);
public sealed record NasdaqSupervisedDiagnosticContext(NasdaqCanonicalDiagnosticReference Reference,
    NasdaqSupervisedSessionState Session, NasdaqHistoricalReportSummary Report,
    IReadOnlyList<NasdaqCanonicalRuleDependency> Dependencies)
{
    public string PackageAdmission { get; init; } = "NoRegisteredPackageClaim_ReferenceIsDocumentaryOnly";
    public string CanonicalPath => Report.SelectedFrame!.TradeLifecycle?.Path ?? "Unresolved";
    public string Authorization => "NotGranted_DocumentaryContextOnly_SpecificationConflict";
    public string SpecificationConflict => "HumanAuditedWindowClaim_0830To1130_Bogota_ConflictsWithRepositoryRereviewAndRuntime_0830To1100_Exclusive";
    public string ConflictProvenance => "Current human instruction: 08:30–11:30 America/Bogota; repository strategy-specification section 6.0 and changelog retain a prior direct re-review of Video 3 approximately 04:15–04:30 changing 11:30 to 11:00. Original media not independently reverified by this integration.";
    public string CorrectionPath => "CandidateVersion_IndependentBoundaryAndReplayValidation_HumanApproval_Activation; no runtime or ACTIVE metadata change in this feature";
    public string SourceLimitations => "PersistedCanonicalReport_NotLive; EvidenceValidation=" + Report.EvidenceValidation + "; source references/availability only as recorded; no new MT5 temporal admission or broker verification";
}
