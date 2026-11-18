using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

public sealed record NasdaqPreparationReferenceReport(string LocalReferenceId, string Category)
{
    public string? NativeReferenceId { get; init; }
    public string? Role { get; init; }
    public string? Timeframe { get; init; }
    public string? OriginalSymbol { get; init; }
    public string? SourceProviderId { get; init; }
    public string? Price { get; init; }
    public string? LowerPrice { get; init; }
    public string? UpperPrice { get; init; }
    public string? PriceUnit { get; init; }
    public string? SourceReference { get; init; }
    public string? OriginalTime { get; init; }
    public string? OriginalClockDomain { get; init; }
    public string TimePrecision { get; init; } = "Unknown";
    public string HumanValidationStatus { get; init; } = "human_validation_required";
}
public sealed record NasdaqProspectivePreparationDraft(string SessionKey, string Reviewer, string SourceReference, string RevisionReason)
{
    public string? OriginalObservationId { get; init; }
    public string? SourceProviderId { get; init; }
    public string? OriginalSymbol { get; init; }
    public string? Direction { get; init; }
    public string? H4Context { get; init; }
    public string? ArtifactReference { get; init; }
    public string? OriginalTime { get; init; }
    public string? OriginalClockDomain { get; init; }
    public string TimePrecision { get; init; } = "Unknown";
    public string? OperatorReportedObservationTime { get; init; }
    public bool H4ReviewReportedComplete { get; init; }
    public bool LiquidityReviewReportedComplete { get; init; }
    public string HumanValidationStatus { get; init; } = "human_validation_required";
    public IReadOnlyList<NasdaqPreparationReferenceReport> References { get; init; } = [];

    public static void Text(string? value, bool required = false)
    {
        if (value is null && !required) return;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value != value.Trim() || value.Any(char.IsControl)) throw new ArgumentException("Explicit trimmed preparation text required; unknown values remain null.");
    }
    private static void Precision(string value)
    { if (value is not ("Unknown" or "Approximate" or "SourceReportedSecond" or "ExactNativeRecord")) throw new ArgumentException("Unsupported original timestamp precision."); }
    private static void Review(string value)
    { if (value is not ("confirmed" or "candidate" or "context_specific" or "visual_only" or "human_validation_required" or "unresolved" or "rejected_ai_inference")) throw new ArgumentException("Unsupported human evidence status."); }
    public void Validate(NasdaqDemoSessionIdentity identity)
    {
        Text(SessionKey, true); Text(Reviewer, true); Text(RevisionReason, true); NasdaqSupervisedExecutionEvidenceDraft.Reference(SourceReference);
        foreach (var v in new[] { OriginalObservationId, SourceProviderId, OriginalSymbol, OriginalTime, OriginalClockDomain, OperatorReportedObservationTime }) Text(v);
        Precision(TimePrecision); Review(HumanValidationStatus);
        if (ArtifactReference is not null) NasdaqSupervisedExecutionEvidenceDraft.Reference(ArtifactReference);
        if (SourceProviderId is not null && SourceProviderId != identity.ProviderId.Value || OriginalSymbol is not null && OriginalSymbol != identity.Symbol.Value)
            throw new ArgumentException("Preparation source must match this exact original session instrument/provider; no alias mapping.");
        if (Direction is not null && (!Enum.TryParse<NasdaqHumanH4PermittedDirection>(Direction, out var direction) || !Enum.IsDefined(direction) || direction.ToString() != Direction)
            || H4Context is not null && (!Enum.TryParse<NasdaqHumanH4ContextKind>(H4Context, out var kind) || !Enum.IsDefined(kind) || kind.ToString() != H4Context))
            throw new ArgumentException("Use existing human H4 direction/context labels only.");
        if (References is null || References.Count > 32 || References.Any(r => r is null)) throw new ArgumentException("Bounded preparation references required.");
        if (References.Select(r => r.LocalReferenceId).Distinct(StringComparer.Ordinal).Count() != References.Count) throw new ArgumentException("Duplicate local documentary reference identity.");
        foreach (var r in References)
        {
            Text(r.LocalReferenceId, true);
            foreach (var v in new[] { r.NativeReferenceId, r.OriginalTime, r.OriginalClockDomain, r.SourceProviderId, r.OriginalSymbol }) Text(v);
            Precision(r.TimePrecision); Review(r.HumanValidationStatus);
            if (r.Category is not ("Structure" or "Liquidity") || r.Timeframe is not (null or "4h" or "1h")) throw new ArgumentException("Unsupported preparation reference category/timeframe.");
            if (r.Role is not (null or "HH" or "HL" or "LH" or "LL" or "AsiaHigh" or "AsiaLow" or "LondonHigh" or "LondonLow" or "PreviousDayHigh" or "PreviousDayLow")) throw new ArgumentException("Unsupported existing structural/liquidity role.");
            if (r.OriginalSymbol is not null && r.OriginalSymbol != identity.Symbol.Value || r.SourceProviderId is not null && r.SourceProviderId != identity.ProviderId.Value) throw new ArgumentException("Reference source identity mismatch.");
            if (r.SourceReference is not null) NasdaqSupervisedExecutionEvidenceDraft.Reference(r.SourceReference);
            var supplied = new[] { r.Price, r.LowerPrice, r.UpperPrice }.Where(v => v is not null).ToArray();
            if (supplied.Length > 0 && r.PriceUnit != "NativeInstrumentPrice" || r.PriceUnit is not (null or "NativeInstrumentPrice")) throw new ArgumentException("Supplied prices need the original native-instrument unit.");
            foreach (var price in supplied) _ = NasdaqSupervisedExecutionEvidenceDraft.Decimal(price!);
            if (r.Price is not null && (r.LowerPrice is not null || r.UpperPrice is not null)) throw new ArgumentException("Preserve scalar or zone representation; do not mix coordinates.");
            if (r.LowerPrice is not null && r.UpperPrice is not null && NasdaqSupervisedExecutionEvidenceDraft.Decimal(r.LowerPrice) > NasdaqSupervisedExecutionEvidenceDraft.Decimal(r.UpperPrice)) throw new ArgumentException("Original zone lower bound exceeds upper bound.");
        }
    }
}
public sealed record NasdaqProspectivePreparationRecord(Guid RecordId, int Revision, long SubmissionSequence, Guid SubmissionId,
    NasdaqDemoSessionIdentity Identity, NasdaqProspectivePreparationDraft Evidence, DateTimeOffset ReceivedAtUtc,
    DateTimeOffset RegisteredAtUtc, string ReceiptClockSource, string SubmissionContext, string? PreviousRevisionHash)
{
    public IReadOnlyList<NasdaqPreparationArtifact> Artifacts { get; init; } = [];
}
public sealed record NasdaqProspectivePreparationView(NasdaqProspectivePreparationRecord Record, string RevisionHash,
    IReadOnlyList<string> MissingDocumentaryInputs, int AdmissibleCompletionObservationCount)
{
    public string DocumentaryStatus => MissingDocumentaryInputs.Count == 0 ? "ReportedPreparationDocumented_NotCanonicalPass" : "PartialPreparation";
    public string Scope => "SupervisedHumanReport_OutsideCanonicalReplay";
    public string ReceiptMeaning => "Application service entry after bounded request parsing; successful request receipt and durable registration are separate; not source publication or authenticated UTC";
    public string SequenceMeaning => "PreparationStorePublicationOrder_NotNativeMarketSequence";
    public string RegistrationMeaning => "OrdinaryClockSampleBeforeAtomicPublication_NotAnExactDiskCommitTimestamp";
    public bool OriginalSourceIndependentlyVerified => false;
    public bool UtcAuthenticated => false;
    public DateTimeOffset? VerifiedHistoricalAvailableAtUtc => null;
    public string? OriginalArtifactSha256 => null;
    public string SelectorContract => nameof(NasdaqPreparationCompletionObservationSeries);
    public string H4SelectorContract => nameof(NasdaqHumanH4ContextObservationSelector);
    public string SelectorResult => "NotEvaluated_AdmissibleSourceAvailabilityAndCanonicalMarketContextMissing";
    public string AdmissionResult => "DataUnavailable";
    public string? PreparationRuleResult => null;
    public string? StrategyVerdict => null;
    public bool CanonicalAdmissionPermitted => false;
    public string[] AdmissionBlockers => ["NoIndependentlySupportedAuthoritativePreparationObservedAtUtc", "NoTrustedTemporalAdmissionFromApplicationReceipt", "NoAdmittedClosedH4SourceMembersOrStructuralEpisodes", "HumanSelectionsAndCompletionClaimsAreNotRuleResults"];
    public string PreparationWindow => "Existing [08:00,08:30) America/Bogota; receipt alone never establishes timely canonical completion";
    public string TradingWindowConflict => "Human0830To1130_vs_Runtime0830To1100; unchanged; separate candidate -> independent backtest -> human approval -> activation";
}
public static class NasdaqProspectivePreparationProjection
{
    public static NasdaqPreparationCompletionObservationSeries AdmissibleInputs(NasdaqDemoSessionIdentity identity) =>
        new(identity.StrategyId, identity.StrategyVersion, identity.ProviderId, identity.Symbol, []);
    public static NasdaqProspectivePreparationView View(NasdaqProspectivePreparationRecord record, string hash)
    {
        var d = record.Evidence; d.Validate(record.Identity); var missing = new List<string>();
        if (d.SourceProviderId is null || d.OriginalSymbol is null) missing.Add("ObservationSourceIdentityUnreported");
        if (d.Direction is null or "Unresolved" || d.H4Context is null or "Unresolved") missing.Add("HumanH4ContextSelectionMissingOrUnresolved");
        if (!d.H4ReviewReportedComplete) missing.Add("H4ReviewCompletionNotReported");
        if (!d.LiquidityReviewReportedComplete) missing.Add("LiquidityReviewCompletionNotReported");
        if (d.ArtifactReference is null && record.Artifacts.Count == 0) missing.Add("OriginalArtifactReferenceMissing");
        if (d.OriginalTime is null || d.OriginalClockDomain is null || d.TimePrecision == "Unknown") missing.Add("OriginalObservationTimingUnreported");
        if (d.HumanValidationStatus != "confirmed") missing.Add("HumanValidationRequired");
        if (!d.References.Any(r => r.Category == "Structure")) missing.Add("HumanStructuralReferenceMissing");
        if (!d.References.Any(r => r.Category == "Liquidity")) missing.Add("HumanLiquidityReferenceMissing");
        foreach (var r in d.References)
        {
            if (r.Role is null || r.Timeframe is null || r.SourceReference is null) missing.Add("PartialReferenceIdentityOrSource:" + r.LocalReferenceId);
            if (r.Price is null && (r.LowerPrice is null || r.UpperPrice is null)) missing.Add("OriginalReferencePriceMissing:" + r.LocalReferenceId);
            if (r.HumanValidationStatus != "confirmed") missing.Add("ReferenceHumanValidationRequired:" + r.LocalReferenceId);
        }
        return new(record, hash, missing, AdmissibleInputs(record.Identity).Observations.Count);
    }
}
