using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// All tokens/certificates here are SYNTHETIC. None is evidence of authentic DigiCert trust.
public sealed class Mt5TimestampWitnessTests
{
    private static readonly byte[] Manifest = Encoding.UTF8.GetBytes("synthetic-manifest");
    private static readonly DateTimeOffset GenTime = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class Tsa : IDisposable
    {
        private readonly RSA key = RSA.Create(2048);
        private readonly RSA rootKey = RSA.Create(2048);
        public X509Certificate2 Root { get; }
        public X509Certificate2 Signer { get; }
        public Tsa(string fault = "")
        {
            var root = new CertificateRequest("CN=Synthetic RFC3161 Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            root.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            root.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            root.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(root.PublicKey, false));
            Root = root.CreateSelfSigned(GenTime.AddYears(-2), GenTime.AddYears(2));
            var leaf = new CertificateRequest("CN=Synthetic TSA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leaf.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leaf.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            if (fault != "missing_eku") leaf.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new(fault == "wrong_eku" ? "1.3.6.1.5.5.7.3.3" : Rfc3161WitnessVerifier.TimestampEku) }, fault != "noncritical_eku"));
            using var cert = leaf.Create(Root, GenTime.AddDays(-2), fault == "expired" ? GenTime.AddDays(-1) : GenTime.AddDays(2), [1, 2, 3]);
            Signer = cert.CopyWithPrivateKey(key);
        }
        public byte[] Response(byte[] requestDer, string fault = "", long? accuracy = 1_234_567)
        {
            Assert.True(Rfc3161TimestampRequest.TryDecode(requestDer, out var request, out _));
            var info = new Rfc3161TimestampTokenInfo(new(fault == "policy" ? "1.2.3.4" : Rfc3161WitnessVerifier.DigiCertPolicy),
                request!.HashAlgorithmId, fault == "imprint" ? new byte[32] : request.GetMessageHash(), new byte[] { 1, 2, 3 }, GenTime,
                accuracy, false, fault == "nonce" ? new byte[] { 1, 2, 3, 4 } : request.GetNonce());
            var cms = new SignedCms(new ContentInfo(new("1.2.840.113549.1.9.16.1.4"), info.Encode()));
            var signer = new CmsSigner(Signer)
            {
                IncludeOption = fault == "missing_certificate" ? X509IncludeOption.None : X509IncludeOption.EndCertOnly,
                DigestAlgorithm = new("2.16.840.1.101.3.4.2.1")
            };
            var ess = new AsnWriter(AsnEncodingRules.DER); ess.PushSequence(); ess.PushSequence(); ess.PushSequence();
            ess.WriteOctetString(fault == "ess" ? new byte[32] : SHA256.HashData(Signer.RawData));
            ess.PopSequence(); ess.PopSequence(); ess.PopSequence();
            signer.SignedAttributes.Add(new CryptographicAttributeObject(new("1.2.840.113549.1.9.16.2.47"),
                new AsnEncodedDataCollection(new AsnEncodedData(new Oid("1.2.840.113549.1.9.16.2.47"), ess.Encode()))));
            cms.ComputeSignature(signer);
            var bytes = cms.Encode();
            if (fault == "signature") { var sig = cms.SignerInfos[0].GetSignature(); var at = bytes.AsSpan().IndexOf(sig); bytes[at] ^= 1; }
            var response = new AsnWriter(AsnEncodingRules.DER); response.PushSequence(); response.PushSequence();
            response.WriteInteger(fault == "rejected" ? 2 : 0); response.PopSequence();
            if (fault != "rejected") response.WriteEncodedValue(bytes);
            response.PopSequence(); return response.Encode();
        }
        public void Dispose() { Signer.Dispose(); Root.Dispose(); key.Dispose(); rootKey.Dispose(); }
    }

    private static ValidatedMt5ReceiptPrefix Prefix(Mt5ReceiptSyntheticFixture f)
    {
        var b = new Mt5ReceiptPrefixBoundary(1, Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)));
        return ValidatedMt5ReceiptPrefix.Select(new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, b), b).Prefix!;
    }
    private static byte[] Subject(Mt5ReceiptSyntheticFixture f) => Mt5TimestampManifest.Create(Prefix(f), "record-000000000001.json",
        Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)));

    [Fact]
    public void ManifestIsDeterministicAndBindsTheValidatedPairWithoutPrivatePayload()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var a = Subject(f);
        Assert.Equal(a, Subject(f)); var text = Encoding.UTF8.GetString(a);
        Assert.Contains(Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), text);
        Assert.Contains(Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)), text);
        Assert.DoesNotContain(f.DirectoryPath, text); Assert.DoesNotContain("open_time", text); Assert.DoesNotContain("100.25", text);
        Assert.NotEqual(Mt5TimestampManifest.Hash(a), Mt5TimestampManifest.Hash(a.Append((byte)32).ToArray()));
    }

    [Theory]
    [InlineData("record")]
    [InlineData("probe")]
    [InlineData("outside_prefix")]
    public void SubjectRejectsUnestablishedIdentity(string fault)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Prefix(f);
        Assert.Throws<ArgumentException>(() => Mt5TimestampManifest.Create(p,
            fault == "outside_prefix" ? "record-000000000002.json" : "record-000000000001.json",
            fault == "record" ? new string('0', 64) : Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)),
            fault == "probe" ? new string('0', 64) : Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1))));
    }

    [Theory]
    [InlineData("probe_missing")]
    [InlineData("probe_tampered")]
    [InlineData("source")]
    [InlineData("boundary")]
    public void ExistingReaderBlocksInvalidSubjectBeforeTimestamping(string fault)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var hash = Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1));
        if (fault == "probe_missing") File.Delete(f.ProbePath(1));
        if (fault == "probe_tampered") File.AppendAllText(f.ProbePath(1), " ");
        if (fault == "source") { f.ChangeRecord(1, r => r["source"]!["server"] = "wrong-source"); hash = Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)); }
        var b = new Mt5ReceiptPrefixBoundary(1, fault == "boundary" ? new string('0', 64) : hash);
        Assert.Null(ValidatedMt5ReceiptPrefix.Select(new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, b), b).Prefix);
    }

    [Fact]
    public void RequestHasSha256FreshStrongNonceAndOnlyProtocolFields()
    {
        var a = Rfc3161WitnessVerifier.CreateRequest(Manifest); var b = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        Assert.True(Rfc3161TimestampRequest.TryDecode(a, out var request, out var used));
        Assert.Equal(a.Length, used); Assert.Equal(Rfc3161WitnessVerifier.Sha256Oid, request!.HashAlgorithmId.Value);
        Assert.Equal(SHA256.HashData(Manifest), request.GetMessageHash().ToArray()); Assert.True(request.RequestSignerCertificate);
        Assert.True(request.GetNonce()!.Value.Length >= 16); Assert.NotEqual(a, b);
        Assert.DoesNotContain("synthetic-manifest", Encoding.UTF8.GetString(a));
    }

    [Fact]
    public void SyntheticSignatureIsVerifiedButCannotEstablishPublicTrustOrAdmission()
    {
        using var tsa = new Tsa(); var request = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        var response = tsa.Response(request); var result = Rfc3161WitnessVerifier.Verify(Manifest, request, response);
        Assert.True(result.RequestLinked); Assert.True(result.SignatureVerified); Assert.True(result.PolicyRecognized);
        Assert.Equal(GenTime, result.SignedTime!.GenTime); Assert.Equal("20261117120000Z", result.SignedTime.OriginalGenTime);
        Assert.Equal(GenTime.AddTicks(-12_345_670), result.SignedTime.ConditionalEarliestUtc);
        Assert.Equal(GenTime.AddTicks(12_345_670), result.SignedTime.ConditionalLatestUtc);
        Assert.Equal(1, result.SignedTime.AccuracySeconds); Assert.Equal(234, result.SignedTime.AccuracyMillis);
        Assert.Equal(567, result.SignedTime.AccuracyMicros);
        var trust = Rfc3161WitnessVerifier.VerifyChain(result, tsa.Root, false, 1);
        Assert.False(trust.PathToPinnedAnchor); Assert.False(trust.CompleteTrustValidation);
        Assert.False(result.TemporalAdmissionSupported); Assert.False(result.CanonicalReady);
        Assert.Empty(typeof(Rfc3161ProtocolResult).GetConstructors());
        Assert.Null(typeof(Rfc3161ProtocolResult).GetProperty(nameof(result.SignatureVerified))!.SetMethod);
        Assert.Equal(result.SignedTime, Rfc3161WitnessVerifier.Verify(Manifest, request, response).SignedTime);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("imprint")]
    [InlineData("signature")]
    [InlineData("missing_certificate")]
    [InlineData("ess")]
    [InlineData("rejected")]
    public void InvalidProtocolOrCryptographyCannotVerify(string fault)
    {
        using var tsa = new Tsa(); var q = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        var result = Rfc3161WitnessVerifier.Verify(Manifest, q, tsa.Response(q, fault));
        Assert.False(result.SignatureVerified); Assert.Null(result.SignedTime);
    }

    [Theory]
    [InlineData("missing_eku")]
    [InlineData("wrong_eku")]
    [InlineData("noncritical_eku")]
    [InlineData("expired")]
    public void SignerMustMeetTsaCertificateContractAtSignedTime(string fault)
    {
        using var tsa = new Tsa(fault); var q = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        Assert.False(Rfc3161WitnessVerifier.Verify(Manifest, q, tsa.Response(q)).SignatureVerified);
    }

    [Fact]
    public void MissingAccuracyCannotBecomeZeroAndUnsupportedPolicyCannotAuthorizeInterval()
    {
        using var tsa = new Tsa(); var q = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        var absent = Rfc3161WitnessVerifier.Verify(Manifest, q, tsa.Response(q, accuracy: null));
        Assert.True(absent.SignatureVerified); Assert.Null(absent.SignedTime!.AccuracyMicroseconds);
        Assert.Null(absent.SignedTime.ConditionalLatestUtc);
        var policy = Rfc3161WitnessVerifier.Verify(Manifest, q, tsa.Response(q, "policy"));
        Assert.True(policy.SignatureVerified); Assert.False(policy.PolicyRecognized); Assert.Null(policy.SignedTime!.ConditionalLatestUtc);
    }

    [Fact]
    public void ManifestMutationDifferentAlgorithmAndStaleResponseDoNotLink()
    {
        using var tsa = new Tsa(); var q = Rfc3161WitnessVerifier.CreateRequest(Manifest); var r = tsa.Response(q);
        Assert.False(Rfc3161WitnessVerifier.Verify([1, 2], q, r).RequestLinked);
        Assert.False(Rfc3161WitnessVerifier.Verify(Manifest, Rfc3161WitnessVerifier.CreateRequest(Manifest), r).RequestLinked);
        var wrong = Rfc3161TimestampRequest.CreateFromHash(SHA384.HashData(Manifest), HashAlgorithmName.SHA384,
            nonce: RandomNumberGenerator.GetBytes(24), requestSignerCertificates: true).Encode();
        Assert.False(Rfc3161WitnessVerifier.Verify(Manifest, wrong, tsa.Response(wrong)).RequestLinked);
        Assert.False(Rfc3161WitnessVerifier.Verify(Manifest, q, [1, 2, 3]).SignatureVerified);
        Assert.False(Rfc3161WitnessVerifier.Verify(Manifest, q, r.Concat(new byte[] { 0 }).ToArray()).RequestLinked);
    }

    [Fact]
    public void SyntheticCustomAnchorDoesNotSilenceMissingRevocationProof()
    {
        using var tsa = new Tsa(); var q = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        var p = Rfc3161WitnessVerifier.Verify(Manifest, q, tsa.Response(q));
        var c = Rfc3161WitnessVerifier.VerifyChainCore(p, tsa.Root, Mt5TimestampManifest.Hash(tsa.Root.RawData), false, 1);
        Assert.True(c.PathToPinnedAnchor); Assert.False(c.PlatformChainAndRevocationPassed);
        Assert.Equal("unresolved", c.RevocationStatus); Assert.False(c.CompleteTrustValidation);
        Assert.False(c.IndependentRevocationEvidenceRetained);
    }

    [Theory]
    [InlineData(X509ChainStatusFlags.Revoked, "revoked")]
    [InlineData(X509ChainStatusFlags.RevocationStatusUnknown, "unresolved")]
    [InlineData(X509ChainStatusFlags.OfflineRevocation, "unresolved")]
    [InlineData(X509ChainStatusFlags.UntrustedRoot, "unresolved")]
    public void PlatformFailureClassificationNeverTurnsRevokedOrUnknownIntoTrusted(X509ChainStatusFlags flags, string expected)
        => Assert.Equal(expected, Rfc3161WitnessVerifier.ClassifyRevocation(flags));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        { Calls++; return send(request, cancellation); }
    }

    [Theory]
    [InlineData("timeout", "request_timeout_or_cancelled")]
    [InlineData("network", "timestamp_network_error")]
    [InlineData("size", "response_size_limit")]
    [InlineData("type", "unexpected_timestamp_content_type")]
    [InlineData("status", "http_response_not_successful")]
    public async Task BoundedTransportMakesOnlyOneAttemptAndRetainsFailure(string scenario, string expected)
    {
        using var handler = new Handler(async (request, cancellation) =>
        {
            Assert.Equal("application/timestamp-query", request.Content!.Headers.ContentType!.MediaType);
            if (scenario == "timeout") await Task.Delay(3000, cancellation);
            if (scenario == "network") throw new HttpRequestException();
            var response = new HttpResponseMessage(scenario == "status" ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            { Content = new ByteArrayContent(new byte[scenario == "size" ? Rfc3161WitnessVerifier.MaxResponseBytes + 1 : 1]) };
            response.Content.Headers.ContentType = new(scenario == "type" ? "text/html" : "application/timestamp-reply"); return response;
        });
        using var client = new HttpClient(handler);
        var r = await Rfc3161WitnessTransport.SendCoreAsync(client, new("http://timestamp.digicert.com/"), [1], 1);
        Assert.Equal(expected, r.Error); Assert.Equal(1, handler.Calls); Assert.True(r.FinishedCounter >= r.StartedCounter);
    }

    [Fact]
    public async Task UnreviewedEndpointsAndExistingOutputsCannotTriggerARequest()
    {
        Assert.False(Rfc3161WitnessTransport.SupportedEndpoint(new("https://timestamp.digicert.com/")));
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); using var e = new StringWriter();
        Assert.Equal(4, await new Mt5TimestampWitnessCommand().ExecuteAsync(["mt5-timestamp-witness", "--source", f.DirectoryPath,
            "--output", f.DirectoryPath, "--tsa", "http://timestamp.digicert.com/", "--timeout-seconds", "1"], TextWriter.Null, e));
        Assert.DoesNotContain("trusted=true", typeof(Mt5TimestampWitnessCommand).Name);
    }
    [Fact]
    public async Task OfflineBundleVerificationIsCreateOnlyAndPreservesSourceAndCheckpoint()
    {
        // Public root certificate, pinned against DigiCert's official trust-anchor identifier list; no private key.
        var rootDer = Convert.FromBase64String("MIIFkDCCA3igAwIBAgIQBZsbV56OITLiOQe9p3d1XDANBgkqhkiG9w0BAQwFADBiMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3d3cuZGlnaWNlcnQuY29tMSEwHwYDVQQDExhEaWdpQ2VydCBUcnVzdGVkIFJvb3QgRzQwHhcNMTMwODAxMTIwMDAwWhcNMzgwMTE1MTIwMDAwWjBiMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3d3cuZGlnaWNlcnQuY29tMSEwHwYDVQQDExhEaWdpQ2VydCBUcnVzdGVkIFJvb3QgRzQwggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQC/5pBzaN675F1KPDAiMGkz7MKnJS7JIT3yithZwuEppz1Yq3aaza57G4QNxDAf8xukOBbrVsaXbR2rsnnyyhHS5F/WBTxSD1Ifxp4VpX6+n6lXFllVcq9ok3DCsrp1mWpzMpTREEQQLt+C8weE5nQ7bXHiLQwb7iDVySAdYyktzuxeTsiT+CFhmzTrBcZe7FsavOvJz82sNEBfsXpm7nfISKhmV1efVFiODCu3T6cw2Vbuyntd463JT17lNecxy9qTXtyOj4DatpGYQJB5w3jHtrHEtWoYOAMQjdjUN6QuBX2I9YI+EJFwq1WCQTLX2wRzKm6RAXwhTNS8rhsDdV14Ztk6MUSaM0C/CNdaSaTC5qmgZ92kJ7yhTzm1EVgX9yRcRo9k98FpiHaYdj1ZXUJ2h4mXaXpI8OCiEhtmmnTK3kse5w5jrubU75KSOp493ADkRSWJtppEGSt+wJS00mFt6zPZxd9LBADMfRyVw4/3IbKyEbe7f/LVjHAsQWCqsWMYRJUadmJ+9oCw++hkpjPRiQfhvbfmQ6QYuKZ3AeEPlAwhHbJUKSWJbOUOUlFHdL4mrLZBdd56rF+NP8m800ERElvlEFDrMcXKchYiCd98THU/Y+whX8QgUWtvsauGi0/C1kVfnSD8oR7FwI+isX4KJpn15GkvmB0t9dmpsh3lGwIDAQABo0IwQDAPBgNVHRMBAf8EBTADAQH/MA4GA1UdDwEB/wQEAwIBhjAdBgNVHQ4EFgQU7NfjgtJxXWRM3y5nP+e6mK4cD08wDQYJKoZIhvcNAQEMBQADggIBALth2X2pbL4XxJEbw6GiAI3jZGgPVs93rnD5/ZpKmbnJeFwMDF/k5hQpVgs2SV1EY+CtnJYYZhsjDT156W1r1lT40jzBQ0CuHVD1UvyQO7uYmWlrx8GnqGikJ9yd+SeuMIW59mdNOj6PWTkiU0TryF0Dyu1Qen1iIQqAyHNm0aAFYF/opbSnr6j3bTWcfFqK1qI4mfN4i/RN0iAL3gTujJtHgXINwBQy7zBZLq7gcfJW5GqXb5JQbZaNaHqasjYUegbyJLkJEVDXCLG4iXqEI2FCKeWjzaIgQdfRnGTZ6iahixTXTBmyUEFxPT9NcCOGDErcgdLMMpSEDQgJlxxPwO5rIHQw0uA5NBCFIRUBCOhVMt5xSdkoF1BN5r5N0XWs0Mr7QbhDparTwwVETyw2m+L64kW4I1NsBm9nVX9GtUw/bihaeSbSpKhil9Ie4u1Ki7wb/UdKDd9nZn6yW0HQO+T0O/QEY+nvwlQAUaCKKsnOeMzV6ocEGLPOr0mIr/OSmbaz5mEP0oUA51Aa5BuVnRmhuZyxm7EAHu/QD09CbMkKvO5D+jpxpchNJqU1/YldvIViHTLSoCtU7ZpXwdv6EM8Zt4tKG48BtieVU+i2iW1bvGjUI+iLUaJW+fCmgKDWHrO8Dw9TdSmq6hN35N6MgSGtBxBHEa2HPQfRdbzP82Z+");
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); using var dest = new Mt5ReceiptSyntheticFixture();
        using var tsa = new Tsa();
        var rootFile = Path.Combine(dest.DirectoryPath, "root.cer"); File.WriteAllBytes(rootFile, rootDer);
        var checkpoint = Path.Combine(dest.DirectoryPath, "checkpoint.json"); File.WriteAllText(checkpoint, "checkpoint-sentinel");
        var paths = Directory.GetFiles(f.DirectoryPath).Append(checkpoint).ToArray();
        var before = paths.Select(Mt5ReceiptSyntheticFixture.Hash).ToArray();
        var original = Path.Combine(dest.DirectoryPath, "original"); Directory.CreateDirectory(original);
        var manifest = Subject(f); var request = Rfc3161WitnessVerifier.CreateRequest(manifest); var response = tsa.Response(request);
        File.WriteAllBytes(Path.Combine(original, "manifest.json"), manifest);
        File.WriteAllBytes(Path.Combine(original, "request.tsq"), request);
        File.WriteAllBytes(Path.Combine(original, "response.tsr"), response);
        var outputPath = Path.Combine(dest.DirectoryPath, "verified");
        string[] Args(string output) => ["mt5-timestamp-witness", "--source", f.DirectoryPath, "--through-sequence", "1",
            "--through-sha256", Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), "--record", "record-000000000001.json",
            "--record-sha256", Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), "--probe-sha256", Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)),
            "--tsa", "http://timestamp.digicert.com/", "--timeout-seconds", "1", "--trust-root", rootFile, "--output", output,
            "--verify-bundle", original];
        try
        {
            using var errors = new StringWriter();
            Assert.Equal(0, await new Mt5TimestampWitnessCommand().ExecuteAsync(Args(outputPath), TextWriter.Null, errors));
            Assert.Equal("", errors.ToString());
            Assert.Equal(4, await new Mt5TimestampWitnessCommand().ExecuteAsync(Args(outputPath), TextWriter.Null, TextWriter.Null));
            var report = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(outputPath, "verification.json")))!;
            Assert.True(report["signature_verified"]!.GetValue<bool>()); Assert.True(report["offline"]!.GetValue<bool>());
            Assert.False(report["complete_trust_validation"]!.GetValue<bool>()); Assert.False(report["canonical_ready"]!.GetValue<bool>());
            Assert.Null(report["authorized_utc_interval"]);
            var inventory = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(outputPath, "artifacts.json")))!.AsArray();
            foreach (var entry in inventory) Assert.Equal(entry!["sha256"]!.GetValue<string>(),
                Mt5ReceiptSyntheticFixture.Hash(Path.Combine(outputPath, entry["name"]!.GetValue<string>())));
            Assert.Equal(before, paths.Select(Mt5ReceiptSyntheticFixture.Hash));
            response[^1] ^= 1; File.WriteAllBytes(Path.Combine(original, "response.tsr"), response);
            Assert.Equal(3, await new Mt5TimestampWitnessCommand().ExecuteAsync(Args(Path.Combine(dest.DirectoryPath, "tampered")), TextWriter.Null, TextWriter.Null));
        }
        finally
        {
            // Only artifacts created in this fixture's uniquely owned temporary directory.
            foreach (var file in Directory.EnumerateFiles(dest.DirectoryPath, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        }
    }
    [Theory]
    [InlineData("valid", true)]
    [InlineData("revoked", false)]
    [InlineData("stale", false)]
    [InlineData("future", false)]
    [InlineData("tampered", false)]
    [InlineData("missing", false)]
    public async Task ArchivedSyntheticCrlsAreCryptographicallyCheckedWithoutSystemStoreChanges(string scenario, bool expected)
    {
        using var tsa = new Tsa(); using var crls = new Mt5ReceiptSyntheticFixture(); using var dest = new Mt5ReceiptSyntheticFixture();
        var q = Rfc3161WitnessVerifier.CreateRequest(Manifest);
        var protocol = Rfc3161WitnessVerifier.Verify(Manifest, q, tsa.Response(q));
        var pin = Mt5TimestampManifest.Hash(tsa.Root.RawData);
        var chain = Rfc3161WitnessVerifier.VerifyChainCore(protocol, tsa.Root, pin, false, 1);
        if (scenario != "missing")
        {
            var builder = new CertificateRevocationListBuilder();
            if (scenario == "revoked") builder.AddEntry(tsa.Signer, GenTime.AddHours(-1));
            var der = builder.Build(tsa.Root, 1, scenario == "stale" ? GenTime.AddHours(-1) : GenTime.AddDays(1),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1,
                scenario == "future" ? GenTime.AddHours(1) : GenTime.AddDays(-1));
            if (scenario == "tampered") der[^1] ^= 1;
            File.WriteAllBytes(Path.Combine(crls.DirectoryPath, "signer.crl"), der);
            File.WriteAllBytes(Path.Combine(crls.DirectoryPath, "issuer.crl"), der);
        }
        var result = await Rfc3161ArchivedRevocation.VerifyCoreAsync(protocol, chain, crls.DirectoryPath, dest.DirectoryPath, 10, pin);
        if (!OperatingSystem.IsWindows()) { Assert.False(result.Verified); return; }
        if (expected && !result.Verified)
            throw new Xunit.Sdk.XunitException(result.Status + "\n" + File.ReadAllText(Path.Combine(dest.DirectoryPath, "archived-revocation-verification.json")));
        Assert.Equal(expected, result.Verified);
        Assert.False(protocol.TemporalAdmissionSupported); Assert.False(protocol.CanonicalReady);
    }
}
