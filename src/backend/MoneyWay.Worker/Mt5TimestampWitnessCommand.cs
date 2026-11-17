using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Worker;

/// <summary>One explicitly requested timestamp, or offline verification. No Worker host/checkpoint/admission hooks.</summary>
public sealed class Mt5TimestampWitnessCommand
{
    private const string Usage = "mt5-timestamp-witness --source <absolute-directory> --through-sequence <N> --through-sha256 <hash> "
        + "--record <record-N.json> --record-sha256 <hash> --probe-sha256 <hash> --tsa <reviewed-uri> --timeout-seconds <1..30> "
        + "--trust-root <absolute-DER-file> --output <new-absolute-directory-outside-source-and-git> "
        + "[--revocation-mode <offline|online>] [--verify-bundle <existing-absolute-directory>] [--crl-directory <absolute-directory>]";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };

    public async Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 2 && args[1] == "--help") { output.WriteLine(Usage); return 0; }
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 1; i < args.Count; i += 2)
                if (i + 1 == args.Count || args[i] is not ("--source" or "--through-sequence" or "--through-sha256"
                    or "--record" or "--record-sha256" or "--probe-sha256" or "--tsa" or "--timeout-seconds"
                    or "--trust-root" or "--output" or "--revocation-mode" or "--verify-bundle" or "--crl-directory") || !options.TryAdd(args[i], args[i + 1]))
                    throw new ArgumentException();
            string Required(string name) => options.TryGetValue(name, out var value) ? value : throw new ArgumentException();
            string Absolute(string name) => Path.IsPathFullyQualified(Required(name)) ? Path.GetFullPath(Required(name)) : throw new ArgumentException();
            var source = Absolute("--source"); var destination = Absolute("--output");
            var endpoint = new Uri(Required("--tsa"));
            var timeout = int.Parse(Required("--timeout-seconds"), CultureInfo.InvariantCulture);
            var online = options.GetValueOrDefault("--revocation-mode", "offline") switch
            { "online" => true, "offline" => false, _ => throw new ArgumentException() };
            if (!Rfc3161WitnessTransport.SupportedEndpoint(endpoint) || timeout is < 1 or > 30) throw new ArgumentException();
            GuardDestination(source, destination);
            using var root = X509CertificateLoader.LoadCertificate(ReadBounded(Absolute("--trust-root"), 16384));
            if (Mt5TimestampManifest.Hash(root.RawData) != Rfc3161WitnessVerifier.DigiCertRootSha256)
                throw new ArgumentException("Trust anchor not in reviewed profile.");
            var boundary = new Mt5ReceiptPrefixBoundary(long.Parse(Required("--through-sequence"), CultureInfo.InvariantCulture), Required("--through-sha256"));
            var batch = new LocalMt5CollectorReceiptReader().ReadPrefix(source, boundary);
            var prefix = ValidatedMt5ReceiptPrefix.Select(batch, boundary).Prefix;
            if (prefix is null) { error.WriteLine("validated_prefix_unavailable_no_request_sent"); return 3; }
            var manifest = Mt5TimestampManifest.Create(prefix, Required("--record"), Required("--record-sha256"), Required("--probe-sha256"));
            byte[] request; byte[] response; Rfc3161TransportResult? transport = null;
            var offline = options.ContainsKey("--verify-bundle");
            if (offline)
            {
                if (online) throw new ArgumentException("Offline bundle verification cannot enable network revocation.");
                var original = Absolute("--verify-bundle");
                if (!ReadBounded(Path.Combine(original, "manifest.json"), 8192).AsSpan().SequenceEqual(manifest))
                    throw new ArgumentException("Retained manifest differs from validated source subject.");
                request = ReadBounded(Path.Combine(original, "request.tsq"), 8192);
                response = ReadBounded(Path.Combine(original, "response.tsr"), Rfc3161WitnessVerifier.MaxResponseBytes);
            }
            else { request = Rfc3161WitnessVerifier.CreateRequest(manifest); response = []; }

            Directory.CreateDirectory(destination);
            Write("manifest.json", manifest); Write("request.tsq", request); Write("trust-root.cer", root.RawData);
            WriteJson("intent.json", new
            {
                Endpoint = endpoint.AbsoluteUri,
                Offline = offline,
                TimeoutSeconds = timeout,
                Attempts = offline ? 0 : 1,
                DigestOnlyTransmission = true,
                Record = Required("--record"),
                prefix.Boundary,
                SourceValidationBeforeRequestCounter = Stopwatch.GetTimestamp(),
                CounterFrequency = Stopwatch.Frequency,
                OrderingEvidence = "local_instrumentation_not_independently_attested",
                RevocationMode = online ? "online" : "offline"
            });
            if (!offline)
            {
                transport = await Rfc3161WitnessTransport.SendAsync(endpoint, request, timeout);
                response = transport.Response;
                WriteJson("transport.json", new
                {
                    transport.HttpStatus,
                    transport.ContentType,
                    transport.StartedCounter,
                    transport.FinishedCounter,
                    transport.CounterFrequency,
                    transport.Error,
                    ResponseBytes = response.Length
                });
            }
            Write("response.tsr", response);
            var protocol = Rfc3161WitnessVerifier.Verify(manifest, request, response);
            if (protocol.TokenDer is { } token) Write("token.p7s", token);
            for (var i = 0; i < protocol.Certificates.Count; i++) Write($"token-certificate-{i}.cer", protocol.Certificates[i]);
            var chain = Rfc3161WitnessVerifier.VerifyChain(protocol, root, online, Math.Min(timeout, 5));
            for (var i = 0; i < chain.ChainCertificates.Count; i++) Write($"chain-certificate-{i}.cer", chain.ChainCertificates[i]);
            var archived = options.ContainsKey("--crl-directory")
                ? await Rfc3161ArchivedRevocation.VerifyAsync(protocol, chain, Absolute("--crl-directory"), destination, timeout) : null;
            WriteJson("verification.json", new
            {
                Schema = "moneyway.mt5-rfc3161-witness",
                Version = 1,
                ManifestSha256 = Mt5TimestampManifest.Hash(manifest),
                RequestSha256 = Mt5TimestampManifest.Hash(request),
                ResponseSha256 = Mt5TimestampManifest.Hash(response),
                protocol.RequestLinked,
                protocol.SignatureVerified,
                protocol.PolicyRecognized,
                protocol.ResponseStatusSuccessful,
                protocol.NonceMatched,
                protocol.MessageImprintMatched,
                protocol.HashAlgorithmMatched,
                protocol.EssCertificateBindingVerified,
                protocol.TsaEkuAndSignerValidityVerified,
                protocol.SignerSubject,
                protocol.SignerIssuer,
                protocol.SignerSha256,
                protocol.SignedTime,
                protocol.Diagnostics,
                chain.PathToPinnedAnchor,
                chain.PlatformChainAndRevocationPassed,
                PlatformRevocationStatus = chain.RevocationStatus,
                RevocationStatus = archived?.Status ?? chain.RevocationStatus,
                chain.ChainStatuses,
                IndependentRevocationEvidenceRetained = archived?.Verified == true,
                CompleteTrustValidation = archived?.Verified == true,
                ArchivedRevocation = archived,
                TsaUtcUncertainty = "unresolved",
                AuthorizedUtcInterval = (object?)null,
                LocalPublicationLink = "local_instrumentation_not_independently_attested",
                HistoricalUtcAvailability = "unresolved",
                ProviderClosure = "unresolved",
                AtomicMultiTimeframeSnapshot = "unresolved",
                SelectionIntervalPropagation = "unresolved",
                OverallUtcUncertainty = "unresolved",
                WindowsClockAuthenticated = false,
                Mt5NativeUtcAuthenticated = false,
                CanonicalUtcConversionAuthorized = false,
                UtcConversionAuthenticated = false,
                CanonicalReady = false,
                TemporalAdmissionSupported = false,
                Runtime = Environment.Version.ToString(),
                CryptoLibraryVersion = FileVersionInfo.GetVersionInfo(typeof(System.Security.Cryptography.Pkcs.Rfc3161TimestampToken).Assembly.Location).FileVersion,
                Offline = offline
            });
            WriteJson("artifacts.json", Directory.GetFiles(destination).Order(StringComparer.Ordinal).Select(p => new
            { Name = Path.GetFileName(p), Bytes = new FileInfo(p).Length, Sha256 = Mt5TimestampManifest.Hash(File.ReadAllBytes(p)) }).ToArray());
            foreach (var artifact in Directory.GetFiles(destination)) File.SetAttributes(artifact, FileAttributes.ReadOnly);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Bundle = destination,
                InventorySha256 = Mt5TimestampManifest.Hash(File.ReadAllBytes(Path.Combine(destination, "artifacts.json"))),
                protocol.RequestLinked,
                protocol.SignatureVerified,
                chain.PathToPinnedAnchor,
                RevocationStatus = archived?.Status ?? chain.RevocationStatus,
                CompleteTrustValidation = archived?.Verified == true,
                TsaUtcUncertainty = "unresolved",
                CanonicalReady = false
            }, Json));
            // Exit 0 denotes request-linked cryptographic verification; trust and uncertainty have separate report fields.
            return protocol.SignatureVerified && protocol.RequestLinked ? 0 : 3;

            void Write(string name, byte[] bytes)
            {
                var path = Path.Combine(destination, name);
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
                File.SetAttributes(path, FileAttributes.ReadOnly);
            }
            void WriteJson(string name, object value) => Write(name, JsonSerializer.SerializeToUtf8Bytes(value, Json));
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or System.Security.Cryptography.CryptographicException)
        { error.WriteLine("invalid_subject_arguments_or_trust_profile\n" + Usage); return 2; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { error.WriteLine("evidence_io_unavailable_preserve_partial_bundle_do_not_retry_automatically"); return 4; }
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > maximum) throw new ArgumentException("Evidence file exceeds bound.");
        var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); return bytes;
    }

    private static void GuardDestination(string source, string destination)
    {
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Output already exists.");
        var relative = Path.GetRelativePath(source, destination);
        if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Output must be outside source.");
        for (DirectoryInfo? p = new(destination); p is not null; p = p.Parent)
            if (p.Exists && (p.Attributes.HasFlag(FileAttributes.ReparsePoint) || Directory.Exists(Path.Combine(p.FullName, ".git"))
                || File.Exists(Path.Combine(p.FullName, ".git")))) throw new ArgumentException("Unsafe evidence output path.");
    }
}
