namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

// Server-registered declarations, not admission credentials. File hashes are inspected independently.
public sealed record NasdaqHistoricalPackageArtifact(string Id, string Kind, string Root, string RelativePath, string Sha256,
    string Provenance, string? Timeframe = null, int? ExpectedCandleCount = null);
public sealed record NasdaqHistoricalPackageReview(string Id, string SemanticSlot, string Claim, string Decision,
    string ArtifactId, string Reviewer, DateOnly? ReviewCreatedOn, string OriginalEventTime,
    DateTimeOffset? ClaimedAvailableAtUtc, string AvailabilityEvidence);
public sealed record NasdaqHistoricalEvidencePackage(string PackageId, int PackageVersion, string StrategyId, string StrategyVersion,
    string Scenario, string ProviderId, string OriginalSymbol, string AnalysisReference, DateOnly TradingDay,
    string SessionId, string Origin, string CandleProvenance, string OriginalClockDomain, string TimeMapping,
    string Transformation, string AvailabilityEvidence, IReadOnlyList<NasdaqHistoricalPackageArtifact> Artifacts,
    IReadOnlyList<NasdaqHistoricalPackageReview> HumanReviews, IReadOnlyList<string> Limitations);
public sealed record NasdaqHistoricalPackageRegistration(string PackageId, int PackageVersion, string ManifestFile, string ManifestSha256);
public sealed record NasdaqHistoricalPackageRegistry(int SchemaVersion, IReadOnlyList<NasdaqHistoricalPackageRegistration> Packages);
public sealed record NasdaqHistoricalArtifactInspection(string Id, string Kind, string Root, string RelativePath,
    string ExpectedSha256, string? ActualSha256, bool IntegrityVerified, string Status);
public sealed record NasdaqHistoricalCoverageInspection(string ArtifactId, string Timeframe, int CandleCount,
    DateTimeOffset FirstOpenUtc, DateTimeOffset LastCloseUtc, int UnclassifiedDiscontinuities,
    DateTimeOffset? FirstDiscontinuityStartUtc, DateTimeOffset? FirstDiscontinuityEndUtc,
    int CandlesClosingAfterSessionDay, string TimestampMeaning);
public sealed record NasdaqHistoricalPackageInspection(NasdaqHistoricalEvidencePackage Package, string ManifestSha256,
    bool DocumentaryStructureValid, bool SourceIntegrityVerified, IReadOnlyList<string> InvalidReasons,
    IReadOnlyList<string> AdmissionBlockers, IReadOnlyList<NasdaqHistoricalArtifactInspection> Artifacts,
    IReadOnlyList<NasdaqHistoricalCoverageInspection> Coverage, NasdaqHistoricalReportSummary? ExistingHistoricalReport)
{
    public string State => DocumentaryStructureValid && SourceIntegrityVerified ? "ValidatedDocumentaryPackage_NotCanonicallyAdmissible" : "InvalidPackage";
    public bool CanonicalReplayPermitted => false;
    public string AdmissionResult => "DataUnavailable";
    public string? NewCanonicalVerdict => null;
    public NasdaqCanonicalDiagnosticReference? AdmissibleDiagnosticReference => null;
    public string HistoricalReportScope => "PreviouslyGeneratedResearchReport_NotNewCausalAdmission_NotEntryAuthorization";
    public string HumanApprovalStatus => "RetainedDeclarationsOnly_NoIndependentPackageAdmissionApproval";
    public string AvailabilityStatus => "NotEstablished_NoTrustedHistoricalAvailabilityVerifierForTheseExports";
    public DateTimeOffset? ProviderPublicationAtUtc => null;
    public DateTimeOffset? OriginalSystemCaptureAtUtc => null;
    public DateTimeOffset? VerifiedHistoricalAvailableAtUtc => null;
    public string TemporalContract => "Mt5TemporalEvidenceAdmission withholds admission even for consistent claims; no new credential or timestamp-to-availability conversion is introduced";
    public string TradingWindowConflict => "CurrentHuman0830To1130_vs_Runtime0830To1100_AmericaBogota; candidate version -> independent backtest -> human approval -> activation";
    public bool BrokerExecutionVerified => false;
    public string? CalculatedPnl => null;
}
