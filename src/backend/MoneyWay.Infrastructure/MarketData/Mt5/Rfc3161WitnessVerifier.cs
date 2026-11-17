using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

public sealed record Rfc3161SignedTime(string OriginalGenTime, DateTimeOffset GenTime, long? AccuracyMicroseconds,
    long? AccuracySeconds, long? AccuracyMillis, long? AccuracyMicros, bool Ordering, string PolicyOid, string SerialHex,
    DateTimeOffset? ConditionalEarliestUtc, DateTimeOffset? ConditionalLatestUtc)
{
    public int FractionalSecondDigits => OriginalGenTime.Contains('.') ? OriginalGenTime.Split('.')[1].TrimEnd('Z').Length : 0;
    public bool AccuracyPresent => AccuracyMicroseconds.HasValue;
}
public sealed class Rfc3161ProtocolResult
{
    private readonly byte[][] certificates;
    private readonly byte[]? tokenDer;
    internal Rfc3161ProtocolResult(bool requestLinked, bool signatureVerified, bool policyRecognized,
        string? signerSubject, string? signerIssuer, string? signerSha256, Rfc3161SignedTime? signedTime,
        IEnumerable<byte[]> certificates, byte[]? tokenDer, IEnumerable<string> diagnostics)
    {
        RequestLinked = requestLinked; SignatureVerified = signatureVerified; PolicyRecognized = policyRecognized;
        SignerSubject = signerSubject; SignerIssuer = signerIssuer; SignerSha256 = signerSha256; SignedTime = signedTime;
        this.certificates = certificates.Select(c => c.ToArray()).ToArray(); this.tokenDer = tokenDer?.ToArray();
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
    public bool RequestLinked { get; }
    public bool SignatureVerified { get; }
    public bool PolicyRecognized { get; }
    public bool? ResponseStatusSuccessful => RequestLinked ? true : null;
    public bool? NonceMatched => RequestLinked ? true : null;
    public bool? MessageImprintMatched => RequestLinked ? true : null;
    public bool? HashAlgorithmMatched => RequestLinked ? true : null;
    public bool? EssCertificateBindingVerified => SignatureVerified ? true : null;
    public bool? TsaEkuAndSignerValidityVerified => SignatureVerified ? true : null;
    public string? SignerSubject { get; }
    public string? SignerIssuer { get; }
    public string? SignerSha256 { get; }
    public Rfc3161SignedTime? SignedTime { get; }
    public IReadOnlyList<byte[]> Certificates => Array.AsReadOnly(certificates.Select(c => c.ToArray()).ToArray());
    public byte[]? TokenDer => tokenDer?.ToArray();
    public IReadOnlyList<string> Diagnostics { get; }
    public bool CanonicalReady => false;
    public bool TemporalAdmissionSupported => false;
}
public sealed record Rfc3161ChainResult(bool PathToPinnedAnchor, bool PlatformChainAndRevocationPassed,
    string RevocationStatus, IReadOnlyList<string> ChainStatuses, IReadOnlyList<byte[]> ChainCertificates)
{
    // The platform does not return the signed CRL/OCSP bytes. A status summary cannot replace retained proof.
    public bool IndependentRevocationEvidenceRetained => false;
    public bool CompleteTrustValidation => false;
}

/// <summary>Framework RFC3161/CMS validation; no filesystem, network, wall clock or canonical admission.</summary>
public static class Rfc3161WitnessVerifier
{
    public const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    public const string TimestampEku = "1.3.6.1.5.5.7.3.8";
    public const string DigiCertPolicy = "2.16.840.1.114412.7.1";
    public const string DigiCertRootSha256 = "552f7bdcf1a7af9e6ce672017f4f12abf77240c78e761ac203d1d9d20ac89988";
    public const int MaxResponseBytes = 128 * 1024;

    public static byte[] CreateRequest(ReadOnlyMemory<byte> manifest)
    {
        var nonce = RandomNumberGenerator.GetBytes(24); nonce[0] &= 0x7f; nonce[0] |= 1;
        return Rfc3161TimestampRequest.CreateFromHash(SHA256.HashData(manifest.Span), HashAlgorithmName.SHA256,
            nonce: nonce, requestSignerCertificates: true).Encode();
    }

    public static Rfc3161ProtocolResult Verify(byte[] manifest, byte[] requestDer, byte[] responseDer)
    {
        var linked = false; var signature = false; var policy = false;
        var codes = new List<string>(); Rfc3161SignedTime? time = null; X509Certificate2? signer = null;
        byte[]? tokenDer = null; byte[][] certificates = [];
        try
        {
            if (manifest.Length > 8192 || requestDer.Length > 8192 || responseDer.Length > MaxResponseBytes)
                throw new CryptographicException("Input exceeds protocol bound.");
            if (!Rfc3161TimestampRequest.TryDecode(requestDer, out var request, out var consumed) || consumed != requestDer.Length
                || request.HashAlgorithmId.Value != Sha256Oid || request.GetNonce() is not { Length: >= 16 }
                || !request.GetMessageHash().Span.SequenceEqual(SHA256.HashData(manifest)))
                throw new CryptographicException("Request is not linked to this manifest or lacks a strong nonce.");
            // ProcessResponse verifies successful status, algorithm, imprint, nonce and any requested policy.
            var token = request.ProcessResponse(responseDer, out consumed);
            if (consumed != responseDer.Length || token.TokenInfo.Version != 1) throw new CryptographicException("Trailing data or unsupported token version.");
            linked = true;
            var cms = token.AsSignedCms(); tokenDer = cms.Encode();
            certificates = cms.Certificates.Cast<X509Certificate2>().Select(c => c.RawData).ToArray();
            signature = token.VerifySignatureForHash(SHA256.HashData(manifest), HashAlgorithmName.SHA256, out signer);
            if (!signature) codes.Add("cms_signature_or_ess_signer_eku_validity_verification_failed");
            else
            {
                var eku = signer!.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
                if (eku is null || !eku.Critical || eku.EnhancedKeyUsages.Count != 1 || eku.EnhancedKeyUsages[0].Value != TimestampEku)
                { signature = false; codes.Add("tsa_eku_must_be_critical_and_exclusive"); }
            }
            policy = token.TokenInfo.PolicyId.Value == DigiCertPolicy;
            if (!policy) codes.Add("tsa_policy_not_in_reviewed_profile");
            if (signature)
            {
                var nameMatched = true;
                if (token.TokenInfo.GetTimestampAuthorityName() is { } name)
                {
                    var nameReader = new AsnReader(name, AsnEncodingRules.DER);
                    if (!nameReader.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 4, true))) nameMatched = false;
                    else
                    {
                        var directoryName = nameReader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 4, true));
                        nameMatched = directoryName.ReadEncodedValue().Span.SequenceEqual(signer!.SubjectName.RawData);
                        directoryName.ThrowIfNotEmpty(); nameReader.ThrowIfNotEmpty();
                    }
                    if (!nameMatched) codes.Add("tsa_name_hint_not_matched_by_supported_subject_name_profile");
                }
                if (token.TokenInfo.GetExtensions().Cast<X509Extension>().Any(e => e.Critical))
                { nameMatched = false; codes.Add("unsupported_critical_tstinfo_extension"); }
                time = ReadSignedTime(token.TokenInfo, policy && nameMatched);
            }
        }
        catch (Exception e) when (e is CryptographicException or AsnContentException or ArgumentException or OverflowException)
        { codes.Add("rfc3161_request_response_validation_failed"); }
        return new(linked, signature, policy, signer?.Subject, signer?.Issuer,
            signer is null ? null : Mt5TimestampManifest.Hash(signer.RawData), time,
            Array.AsReadOnly(certificates), tokenDer, codes.AsReadOnly());
    }

    private static Rfc3161SignedTime ReadSignedTime(Rfc3161TimestampTokenInfo info, bool policy)
    {
        // Summary only, after framework CMS/ESS validation. Original token bytes are retained unchanged.
        var sequence = new AsnReader(info.Encode(), AsnEncodingRules.DER).ReadSequence();
        sequence.ReadInteger(); sequence.ReadObjectIdentifier(); sequence.ReadEncodedValue(); sequence.ReadIntegerBytes();
        var rawTime = sequence.ReadEncodedValue();
        var original = Encoding.ASCII.GetString(new AsnReader(rawTime, AsnEncodingRules.DER).PeekContentBytes().Span);
        long? seconds = null, millis = null, micros = null;
        if (sequence.HasData && sequence.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
        {
            var accuracy = sequence.ReadSequence();
            if (accuracy.HasData && accuracy.PeekTag().HasSameClassAndValue(Asn1Tag.Integer)) seconds = (long)accuracy.ReadInteger();
            if (accuracy.HasData && accuracy.PeekTag().TagClass == TagClass.ContextSpecific && accuracy.PeekTag().TagValue == 0)
                millis = (long)accuracy.ReadInteger(new Asn1Tag(TagClass.ContextSpecific, 0));
            if (accuracy.HasData) micros = (long)accuracy.ReadInteger(new Asn1Tag(TagClass.ContextSpecific, 1));
            accuracy.ThrowIfNotEmpty();
        }
        DateTimeOffset? lower = null, upper = null;
        var fraction = original.Contains('.') ? original.Split('.')[1].TrimEnd('Z').Length : 0;
        if (policy && info.AccuracyInMicroseconds is { } us && us >= 0 && fraction <= 7)
        {
            try { var ticks = checked(us * 10); lower = info.Timestamp.AddTicks(-ticks); upper = info.Timestamp.AddTicks(ticks); }
            catch (Exception e) when (e is ArgumentOutOfRangeException or OverflowException) { lower = null; upper = null; }
        }
        return new(original, info.Timestamp, info.AccuracyInMicroseconds, seconds, millis, micros, info.IsOrdering,
            info.PolicyId.Value!, Convert.ToHexStringLower(info.GetSerialNumber().Span), lower, upper);
    }

    /// <summary>Explicit platform trust operation. Online mode may retrieve AIA/CRL/OCSP; offline never downloads.</summary>
    public static Rfc3161ChainResult VerifyChain(Rfc3161ProtocolResult result, X509Certificate2 root, bool online, int timeoutSeconds)
        => VerifyChainCore(result, root, DigiCertRootSha256, online, timeoutSeconds);

    internal static Rfc3161ChainResult VerifyChainCore(Rfc3161ProtocolResult result, X509Certificate2 root,
        string rootPin, bool online, int timeoutSeconds)
    {
        if (timeoutSeconds is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (!result.SignatureVerified || result.SignedTime is null || Mt5TimestampManifest.Hash(root.RawData) != rootPin)
            return new(false, false, "unresolved", ["signature_unverified_or_trust_anchor_pin_mismatch"], []);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = online ? X509RevocationMode.Online : X509RevocationMode.Offline;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.VerificationTime = result.SignedTime.GenTime.UtcDateTime;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        chain.ChainPolicy.DisableCertificateDownloads = !online;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(TimestampEku));
        var certificates = result.Certificates.Select(b => X509CertificateLoader.LoadCertificate(b)).ToArray();
        try
        {
            foreach (var certificate in certificates) chain.ChainPolicy.ExtraStore.Add(certificate);
            var signer = certificates.Single(c => Mt5TimestampManifest.Hash(c.RawData) == result.SignerSha256);
            var passed = chain.Build(signer);
            var flags = chain.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (f, s) => f | s.Status);
            var nonRevocation = flags & ~(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation);
            var anchored = chain.ChainElements.Count > 0 && Mt5TimestampManifest.Hash(chain.ChainElements[^1].Certificate.RawData) == rootPin;
            return new(anchored && nonRevocation == X509ChainStatusFlags.NoError, passed,
                ClassifyRevocation(flags), chain.ChainStatus.Select(s => s.Status.ToString()).ToArray(),
                chain.ChainElements.Select(e => e.Certificate.RawData).ToArray());
        }
        finally { foreach (var c in certificates) c.Dispose(); }
    }

    internal static string ClassifyRevocation(X509ChainStatusFlags flags) =>
        flags.HasFlag(X509ChainStatusFlags.Revoked) ? "revoked" :
        (flags & (X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)) != 0 ? "unresolved" :
        flags == X509ChainStatusFlags.NoError ? "platform_check_passed_not_archived" : "unresolved";
}
