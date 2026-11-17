using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MoneyWay.Application.MarketData.Receipt;

public sealed record Mt5TemporalObservationKey(string RecordFile, string Timeframe, long NativeOpenRaw);
public sealed record Mt5TemporalSourceIdentity(string Company, string Server, string NativeSymbol, bool Demo);
public sealed record Mt5TemporalObservationIdentity(string DirectoryScope, Mt5ReceiptPrefixBoundary Boundary,
    string PrefixManifestSha256, Mt5TemporalSourceIdentity Source, Mt5ReceivedRecordIdentity Receipt,
    string ProbeFile, string ProbeSha256, string Timeframe, int PeriodSeconds, Mt5NativeBar Candle);

/// <summary>Untrusted submitted evidence. Hash equality proves byte integrity only, never issuer trust or claim coverage.</summary>
public sealed record Mt5TemporalEvidenceProvenance(string? Issuer, string? Reference, string? PayloadUtf8,
    string? Sha256, string? VerificationMethod, string? ClockDomain, string? UtcReferenceEvidence, string? ClockUncertaintyEvidence,
    string? ClockContinuityEvidence, string? SourcePublicationLinkEvidence, string? ContemporaneousCoverageEvidence,
    string? Limitations);
public sealed record Mt5TemporalEvidenceClaim(Mt5TemporalObservationIdentity Observation,
    Mt5TemporalEvidenceProvenance? Provenance, DateTimeOffset? EarliestPossibleUtc,
    DateTimeOffset? LatestPossibleUtc, DateTimeOffset? AttestationIssuedAtUtc);

/// <summary>A diagnostic assessment, deliberately not a credential accepted by canonical replay.</summary>
public sealed class Mt5TemporalAdmissionAssessment
{
    internal Mt5TemporalAdmissionAssessment(Mt5TemporalObservationKey key, Mt5TemporalObservationIdentity? observation,
        DateTimeOffset asOfUtc, Mt5ReceiptStatus status, IEnumerable<string> diagnostics, bool? claimedUpperBoundReached)
    {
        Key = key; Observation = observation; AsOfUtc = asOfUtc; Status = status;
        Diagnostics = Array.AsReadOnly(diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        ClaimedUpperBoundReached = claimedUpperBoundReached;
    }

    public Mt5TemporalObservationKey Key { get; }
    public Mt5TemporalObservationIdentity? Observation { get; }
    public DateTimeOffset AsOfUtc { get; }
    // Uses receipt-evidence vocabulary, independently of the original receipt's integrity status.
    public Mt5ReceiptStatus Status { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    // Arithmetic on UNVERIFIED supplied bounds only. This must never authorize disclosure.
    public bool? ClaimedUpperBoundReached { get; }
    public bool TemporalAdmissionSupported => false;
    public DateTimeOffset? ConservativeAvailableByUtc => null;
    public string HistoricalUtcAvailability => "unresolved";
    public string NativeTimestampMapping => "unresolved";
    public string ProviderClosure => "unresolved";
    public string AtomicMultiTimeframeSnapshot => "unresolved";
    public string SelectionIntervalPropagation => "unresolved";
    public string OverallUtcUncertainty => "unresolved";
    public bool WindowsClockAuthenticated => false;
    public bool Mt5NativeUtcAuthenticated => false;
    public bool CanonicalUtcConversionAuthorized => false;
    public bool UtcConversionAuthenticated => false;
    public bool CanonicalReady => false;
}

/// <summary>
/// Pure requirements assessment of an exact frozen receipt observation. No trusted attestation verifier exists;
/// all public paths withhold admission, even when every submitted claim and its local hash are consistent.
/// </summary>
public sealed class Mt5TemporalEvidenceAdmission
{
    public Mt5TemporalAdmissionAssessment Assess(ValidatedMt5ReceiptPrefix prefix, Mt5TemporalObservationKey key,
        DateTimeOffset asOfUtc, Mt5TemporalEvidenceClaim? claim = null)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(key);
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Assessment cutoff must be UTC.", nameof(asOfUtc));
        var codes = new List<string>();
        var status = Mt5ReceiptStatus.Unresolved;
        bool? reached = null;
        Mt5TemporalObservationIdentity? identity = null;
        var receipt = prefix.Records.SingleOrDefault(r => r.FileName == key.RecordFile);
        if (receipt is null) Missing("observation_receipt_not_in_prefix");
        else
        {
            var a = receipt.Observation!; // Prefix construction already validated source, lineage and probe dependencies.
            var frame = a.Timeframes.SingleOrDefault(f => f.Timeframe == key.Timeframe);
            var bar = frame?.Bars.SingleOrDefault(b => b.OpenTimeNative.Raw == key.NativeOpenRaw);
            if (a.EvidenceKind != Mt5ReceiptEvidenceKind.AcquisitionSnapshot) Missing("diagnostic_receipt_has_no_candle_sample");
            else if (bar is null) Missing("native_candle_not_observed_in_receipt");
            else
            {
                var m = a.Metadata;
                identity = new(prefix.DirectoryScope, prefix.Boundary, prefix.EvidenceManifestSha256,
                    new(a.Company, a.Server, a.NativeSymbol, a.Demo),
                    new(a.RecordFile, a.RecordSha256, m.Sequence, m.RunId, m.PollSequence, m.Event),
                    m.ProbeFile!, m.ProbeSha256!, frame!.Timeframe, frame.PeriodSeconds, bar);
                if (!receipt.CompleteNativeSampleObserved || frame.HeadConsistent != true || frame.CopyErrorCode != 0
                    || frame.SynchronizedAfter?.Value != 1 || !bar.OhlcValid)
                    Missing("observation_acquisition_incomplete_or_unstable");
            }
        }

        if (claim is null) Missing("temporal_attestation_missing");
        else
        {
            if (identity is null || claim.Observation != identity) Conflict("attestation_observation_identity_mismatch");
            var p = claim.Provenance;
            if (p is null) Missing("attestation_provenance_missing");
            else
            {
                Require(p.Issuer, "attestation_issuer_missing"); Require(p.Reference, "attestation_reference_missing");
                Require(p.VerificationMethod, "attestation_verification_method_missing");
                Require(p.ClockDomain, "clock_domain_missing"); Require(p.UtcReferenceEvidence, "utc_reference_evidence_missing");
                Require(p.ClockUncertaintyEvidence, "clock_uncertainty_evidence_missing");
                Require(p.ClockContinuityEvidence, "clock_continuity_evidence_missing");
                Require(p.SourcePublicationLinkEvidence, "source_publication_link_evidence_missing");
                Require(p.ContemporaneousCoverageEvidence, "contemporaneous_coverage_evidence_missing");
                if (p.PayloadUtf8 is null || string.IsNullOrWhiteSpace(p.Sha256)) Missing("attestation_artifact_missing");
                else if (!Regex.IsMatch(p.Sha256, "^[0-9a-fA-F]{64}\\z")) Invalid("attestation_digest_invalid");
                else if (!string.Equals(p.Sha256, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(p.PayloadUtf8))),
                    StringComparison.OrdinalIgnoreCase)) Conflict("attestation_artifact_hash_mismatch");
                // References, issuer names, method labels and even a matching hash are NOT verification results.
                codes.AddRange(["attestation_issuer_and_semantics_unverified", "utc_reference_authenticity_unverified",
                    "clock_uncertainty_unverified", "clock_continuity_unverified", "source_publication_ordering_unverified", "observation_coverage_unverified"]);
            }
            if (claim.EarliestPossibleUtc is null) Missing("utc_lower_bound_missing");
            if (claim.LatestPossibleUtc is null) Missing("utc_upper_bound_missing");
            if (claim.AttestationIssuedAtUtc is null) Missing("attestation_issue_time_missing");
            if (new[] { claim.EarliestPossibleUtc, claim.LatestPossibleUtc, claim.AttestationIssuedAtUtc }
                .Any(t => t is { } time && time.Offset != TimeSpan.Zero))
                Invalid("attestation_bounds_must_be_utc");
            else if (claim.EarliestPossibleUtc is { } lower && claim.LatestPossibleUtc is { } upper)
            {
                if (lower > upper) Invalid("utc_interval_reversed");
                else reached = asOfUtc >= upper;
            }
            if (claim.AttestationIssuedAtUtc > claim.LatestPossibleUtc)
                codes.Add("retrospective_attestation_requires_verified_contemporaneous_source");
        }
        codes.Add("trusted_temporal_attestation_verifier_unavailable");
        return new(key, identity, asOfUtc, status, codes, reached);

        void Require(string? value, string code) { if (string.IsNullOrWhiteSpace(value)) Missing(code); }
        void Missing(string code) { codes.Add(code); if (status == Mt5ReceiptStatus.Unresolved) status = Mt5ReceiptStatus.DataUnavailable; }
        void Invalid(string code) { codes.Add(code); if (status != Mt5ReceiptStatus.Conflict) status = Mt5ReceiptStatus.Invalid; }
        void Conflict(string code) { codes.Add(code); status = Mt5ReceiptStatus.Conflict; }
    }
}
