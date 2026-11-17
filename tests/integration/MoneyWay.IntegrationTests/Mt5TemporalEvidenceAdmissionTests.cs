using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.IntegrationTests;

// Synthetic byte-integrity/interval claims are deliberately NOT trusted UTC attestations.
public sealed class Mt5TemporalEvidenceAdmissionTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly Mt5TemporalObservationKey Key = new("record-000000000001.json", "M1", Mt5ReceiptSyntheticFixture.Head);
    private static ValidatedMt5ReceiptPrefix Prefix(Mt5ReceiptSyntheticFixture f, long sequence = 1)
    {
        var boundary = new Mt5ReceiptPrefixBoundary(sequence, Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(sequence)));
        return ValidatedMt5ReceiptPrefix.Select(new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary), boundary).Prefix!;
    }
    private static Mt5TemporalAdmissionAssessment Assess(ValidatedMt5ReceiptPrefix p, Mt5TemporalEvidenceClaim? claim = null,
        DateTimeOffset? cutoff = null, Mt5TemporalObservationKey? key = null) =>
        new Mt5TemporalEvidenceAdmission().Assess(p, key ?? Key, cutoff ?? Cutoff, claim);
    private static Mt5TemporalEvidenceClaim Claim(ValidatedMt5ReceiptPrefix p)
    {
        const string payload = "synthetic-evidence-only-not-an-authenticated-attestation";
        return new(Assess(p).Observation!, new("synthetic-issuer", "fixture:evidence", payload,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), "synthetic-method",
            "synthetic-clock-domain", "fixture:reference", "fixture:uncertainty", "fixture:continuity", "fixture:publication-link",
            "fixture:contemporaneous-coverage", "All references are unverified synthetic test data."),
            Cutoff.AddSeconds(-1), Cutoff, Cutoff);
    }
    private static void Denied(Mt5TemporalAdmissionAssessment a)
    {
        Assert.False(a.TemporalAdmissionSupported); Assert.Null(a.ConservativeAvailableByUtc);
        Assert.False(a.CanonicalReady); Assert.False(a.CanonicalUtcConversionAuthorized);
        Assert.False(a.UtcConversionAuthenticated); Assert.False(a.WindowsClockAuthenticated); Assert.False(a.Mt5NativeUtcAuthenticated);
        Assert.Contains("trusted_temporal_attestation_verifier_unavailable", a.Diagnostics);
    }

    [Fact]
    public void ExactReceiptObservationIsBoundWithoutInventingAvailability()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var a = Assess(p);
        Assert.Equal(p.Boundary, a.Observation!.Boundary); Assert.Equal(p.DirectoryScope, a.Observation.DirectoryScope);
        Assert.Equal(p.EvidenceManifestSha256, a.Observation.PrefixManifestSha256);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)), a.Observation.ProbeSha256);
        Assert.Equal("run-000000000001", a.Observation.Receipt.RunId); Assert.Equal(1, a.Observation.Receipt.PollSequence);
        Assert.Equal("100.25", a.Observation.Candle.OpenLiteral);
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, a.Status); Assert.Contains("temporal_attestation_missing", a.Diagnostics);
        Assert.Null(a.ClaimedUpperBoundReached); Denied(a);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("probe")]
    [InlineData("receipt")]
    [InlineData("run")]
    [InlineData("scope")]
    [InlineData("boundary")]
    [InlineData("manifest")]
    [InlineData("candle")]
    [InlineData("position")]
    [InlineData("timeframe")]
    public void AttestationMustMatchEverySourceObservationCoordinate(string fault)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var c = Claim(p); var o = c.Observation;
        o = fault switch
        {
            "source" => o with { Source = o.Source with { Server = "another-server" } },
            "probe" => o with { ProbeSha256 = new string('0', 64) },
            "receipt" => o with { Receipt = o.Receipt with { Sha256 = new string('0', 64) } },
            "run" => o with { Receipt = o.Receipt with { RunId = "another-run" } },
            "scope" => o with { DirectoryScope = "another-directory" },
            "boundary" => o with { Boundary = new(2, new string('0', 64)) },
            "manifest" => o with { PrefixManifestSha256 = new string('0', 64) },
            "candle" => o with { Candle = o.Candle with { CloseLiteral = "101.250000000000000000000000000001" } },
            "position" => o with { Candle = o.Candle with { SeriesPosition = 1 } },
            _ => o with { Timeframe = "M5" }
        };
        var a = Assess(p, c with { Observation = o }); Denied(a);
        Assert.Equal(Mt5ReceiptStatus.Conflict, a.Status); Assert.Contains("attestation_observation_identity_mismatch", a.Diagnostics);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void SyntheticIntervalArithmeticUsesInclusiveUpperBoundWithoutAdmitting(int ticks, bool reached)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f);
        var a = Assess(p, Claim(p), Cutoff.AddTicks(ticks));
        Assert.Equal(reached, a.ClaimedUpperBoundReached); Assert.Equal(Mt5ReceiptStatus.Unresolved, a.Status); Denied(a);
    }

    [Theory]
    [InlineData("lower", "utc_lower_bound_missing", Mt5ReceiptStatus.DataUnavailable)]
    [InlineData("upper", "utc_upper_bound_missing", Mt5ReceiptStatus.DataUnavailable)]
    [InlineData("reversed", "utc_interval_reversed", Mt5ReceiptStatus.Invalid)]
    [InlineData("offset", "attestation_bounds_must_be_utc", Mt5ReceiptStatus.Invalid)]
    public void MissingOrInvalidIntervalsNeverFallBackToNativeTime(string fault, string code, Mt5ReceiptStatus status)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var c = Claim(p);
        c = fault switch
        {
            "lower" => c with { EarliestPossibleUtc = null },
            "upper" => c with { LatestPossibleUtc = null },
            "reversed" => c with { EarliestPossibleUtc = Cutoff.AddTicks(1) },
            _ => c with { LatestPossibleUtc = Cutoff.ToOffset(TimeSpan.FromHours(-5)) }
        };
        var a = Assess(p, c); Assert.Equal(status, a.Status); Assert.Contains(code, a.Diagnostics);
        Assert.Null(a.ClaimedUpperBoundReached); Denied(a);
    }

    [Theory]
    [InlineData("issuer", "attestation_issuer_missing")]
    [InlineData("domain", "clock_domain_missing")]
    [InlineData("reference", "utc_reference_evidence_missing")]
    [InlineData("continuity", "clock_continuity_evidence_missing")]
    [InlineData("uncertainty", "clock_uncertainty_evidence_missing")]
    [InlineData("publication", "source_publication_link_evidence_missing")]
    [InlineData("coverage", "contemporaneous_coverage_evidence_missing")]
    public void ReferenceClaimsCannotReplaceMissingIndependentProofs(string fault, string code)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var c = Claim(p); var v = c.Provenance!;
        v = fault switch
        {
            "issuer" => v with { Issuer = null },
            "domain" => v with { ClockDomain = null },
            "reference" => v with { UtcReferenceEvidence = null },
            "continuity" => v with { ClockContinuityEvidence = null },
            "uncertainty" => v with { ClockUncertaintyEvidence = null },
            "publication" => v with { SourcePublicationLinkEvidence = null },
            _ => v with { ContemporaneousCoverageEvidence = null }
        };
        var a = Assess(p, c with { Provenance = v }); Assert.Contains(code, a.Diagnostics); Denied(a);
    }

    [Fact]
    public void AuthenticLookingLabelsAndARecomputedHashDoNotForgeTrust()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var c = Claim(p);
        const string payload = "{\"authenticated\":true,\"canonical_ready\":true}";
        c = c with
        {
            Provenance = c.Provenance! with
            {
                Issuer = "trusted",
                VerificationMethod = "verified",
                PayloadUtf8 = payload,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            }
        };
        var a = Assess(p, c); Assert.Equal(Mt5ReceiptStatus.Unresolved, a.Status); Denied(a);
        Assert.Contains("attestation_issuer_and_semantics_unverified", a.Diagnostics);
        Assert.Empty(typeof(Mt5TemporalAdmissionAssessment).GetConstructors());
        Assert.Null(typeof(Mt5TemporalAdmissionAssessment).GetProperty(nameof(a.TemporalAdmissionSupported))!.SetMethod);
    }

    [Fact]
    public void AttestationPayloadTamperingIsAConflictWithoutClaimingIssuerVerification()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var c = Claim(p);
        var a = Assess(p, c with { Provenance = c.Provenance! with { PayloadUtf8 = "tampered" } });
        Assert.Equal(Mt5ReceiptStatus.Conflict, a.Status); Assert.Contains("attestation_artifact_hash_mismatch", a.Diagnostics); Denied(a);
    }

    [Fact]
    public void RetrospectiveAttestationCannotBackdateObservationAvailability()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f);
        var a = Assess(p, Claim(p) with { AttestationIssuedAtUtc = Cutoff.AddDays(1) });
        Assert.Contains("retrospective_attestation_requires_verified_contemporaneous_source", a.Diagnostics); Denied(a);
    }

    [Fact]
    public void LaterRevisionAndRunCannotChangeFrozenObservationOrSupplyFutureReceipt()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f); var c = Claim(p);
        var before = JsonSerializer.Serialize(Assess(p, c));
        f.Add(2, 1, head: Mt5ReceiptSyntheticFixture.Head, ev: "initial_snapshot", run: 2, poll: 1);
        f.ChangeProbe(2, probe => probe["timeframes"]![0]!["bars"]![0]!["close"] = 101.75m);
        Assert.Equal(before, JsonSerializer.Serialize(Assess(p, c)));
        Assert.Equal(before, JsonSerializer.Serialize(Assess(Prefix(f), c)));
        var unavailable = Assess(p, key: Key with { RecordFile = "record-000000000002.json" });
        Assert.Contains("observation_receipt_not_in_prefix", unavailable.Diagnostics); Denied(unavailable);
        var later = Prefix(f, 2); var a = Assess(later, c, key: Key with { RecordFile = "record-000000000002.json" });
        Assert.Equal("101.75", a.Observation!.Candle.CloseLiteral);
        Assert.Equal(Mt5ReceiptStatus.Conflict, a.Status); Denied(a);
    }

    [Fact]
    public void IncompleteAcquisitionRemainsUnavailableDespiteCompleteTemporalClaims()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeRecord(1, r => { r["sample_complete"] = false; r["source_stable"] = false; });
        var p = Prefix(f); var a = Assess(p, Claim(p));
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, a.Status);
        Assert.Contains("observation_acquisition_incomplete_or_unstable", a.Diagnostics); Denied(a);
    }

    [Fact]
    public void DiagnosticReceiptDoesNotBecomeACandle()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        var a = Assess(Prefix(f, 2), key: Key with { RecordFile = "record-000000000002.json" });
        Assert.Null(a.Observation); Assert.Contains("diagnostic_receipt_has_no_candle_sample", a.Diagnostics); Denied(a);
    }

    [Theory]
    [InlineData("M1")]
    [InlineData("M5")]
    [InlineData("H1")]
    [InlineData("H4")]
    public void NativeFramesNeverAcquireDstClosureOrAtomicityGuarantees(string timeframe)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f);
        var frame = p.Records[0].Observation!.Timeframes.Single(x => x.Timeframe == timeframe);
        var key = new Mt5TemporalObservationKey(Key.RecordFile, timeframe, frame.Bars[1].OpenTimeNative.Raw);
        var a = Assess(p, key: key); Assert.Equal(1, a.Observation!.Candle.SeriesPosition);
        Assert.Null(a.Observation.Candle.ProviderConfirmedClosed); Assert.Null(a.Observation.Candle.OpenTimeUtc);
        Assert.Equal("unresolved", a.NativeTimestampMapping); Assert.Equal("unresolved", a.ProviderClosure);
        Assert.Equal("unresolved", a.AtomicMultiTimeframeSnapshot); Denied(a);
    }

    [Fact]
    public void AssessmentIsDeterministicReadOnlyAndRequiresExplicitUtcCutoff()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var checkpoint = Path.Combine(f.DirectoryPath, "checkpoint-sentinel.json"); File.WriteAllText(checkpoint, "unchanged");
        var paths = Directory.GetFiles(f.DirectoryPath); var pins = paths.Select(Mt5ReceiptSyntheticFixture.Hash).ToArray();
        var p = Prefix(f); Assert.Equal(JsonSerializer.Serialize(Assess(p, Claim(p))), JsonSerializer.Serialize(Assess(p, Claim(p))));
        Assert.Equal(pins, paths.Select(Mt5ReceiptSyntheticFixture.Hash));
        Assert.Throws<ArgumentException>(() => Assess(p, cutoff: Cutoff.ToOffset(TimeSpan.FromHours(1))));
    }
}
