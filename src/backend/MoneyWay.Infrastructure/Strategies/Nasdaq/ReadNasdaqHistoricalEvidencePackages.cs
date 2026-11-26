using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Controlled registry inspection, not a market-data admission authority or a replay engine.</summary>
public sealed class ReadNasdaqHistoricalEvidencePackages(string? registryDirectory, IReadOnlyDictionary<string, string> roots,
    ReadNasdaqHistoricalReport reader) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 16 };
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    private static string Hash(Stream stream) => Convert.ToHexStringLower(SHA256.HashData(stream));
    internal static string ResolveArtifactPath(string root, string relative)
    {
        Require(Path.IsPathFullyQualified(root) && !root.StartsWith(@"\\", StringComparison.Ordinal) && !root.StartsWith("//", StringComparison.Ordinal), "Local server root required.");
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains(':') && !relative.Split('/', '\\').Contains(".."), "Invalid registered relative artifact path.");
        var absolute = Path.GetFullPath(Path.Combine(root, relative));
        Require(absolute.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Artifact escaped its registered root.");
        for (var p = new DirectoryInfo(Path.GetDirectoryName(absolute)!); p is not null; p = p.Parent)
            Require(!p.Exists || !p.Attributes.HasFlag(FileAttributes.ReparsePoint), "Linked evidence root rejected.");
        Require(!File.Exists(absolute) || !File.GetAttributes(absolute).HasFlag(FileAttributes.ReparsePoint), "Linked artifact rejected.");
        return absolute;
    }
    private static T Document<T>(string path, string? expected = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length is > 0 and <= 65536, "Bounded registry/manifest size exceeded.");
        if (expected is not null) Require(Digest(expected) && Hash(stream).Equals(expected, StringComparison.OrdinalIgnoreCase), "ManifestHashMismatch");
        stream.Position = 0; using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
        void Unique(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                var props = node.EnumerateObject().ToArray(); Require(props.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == props.Length, "Duplicate JSON property.");
                foreach (var p in props) Unique(p.Value);
            }
            if (node.ValueKind == JsonValueKind.Array) foreach (var v in node.EnumerateArray()) Unique(v);
        }
        Unique(doc.RootElement);
        return JsonSerializer.Deserialize<T>(doc.RootElement, Json) ?? throw new InvalidDataException("Missing registered document.");
    }
    public IReadOnlyList<NasdaqHistoricalPackageRegistration> List()
    {
        if (string.IsNullOrWhiteSpace(registryDirectory)) throw new KeyNotFoundException("HistoricalPackageRegistryNotConfigured");
        var registry = Document<NasdaqHistoricalPackageRegistry>(ResolveArtifactPath(registryDirectory, "registry.json"));
        Require(registry.SchemaVersion == 1 && registry.Packages is { Count: <= 32 } && registry.Packages.All(p => p is not null
            && p.PackageId is { Length: > 0 and <= 100 } && p.PackageId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && p.PackageVersion > 0 && Digest(p.ManifestSha256)), "Invalid registry identity.");
        Require(registry.Packages.Select(p => p.PackageId).Distinct(StringComparer.Ordinal).Count() == registry.Packages.Count, "Duplicate registered package ID.");
        return registry.Packages;
    }
    public IReadOnlyList<string> RegisteredReportPackages(string sha256)
    {
        if (string.IsNullOrWhiteSpace(registryDirectory)) return [];
        var found = new List<string>();
        foreach (var p in List())
        {
            var manifest = Document<NasdaqHistoricalEvidencePackage>(ResolveArtifactPath(registryDirectory!, p.ManifestFile), p.ManifestSha256);
            var declaredArtifacts = manifest.Artifacts ?? throw new InvalidDataException("Missing artifact declarations.");
            Require(declaredArtifacts.All(a => a is not null && Digest(a.Sha256)), "Invalid artifact declarations.");
            if (declaredArtifacts.Any(a => a.Kind == "HistoricalReport" && a.Sha256.Equals(sha256, StringComparison.OrdinalIgnoreCase))) found.Add(p.PackageId);
        }
        return found;
    }
    public async Task<NasdaqHistoricalPackageInspection> InspectAsync(string id, long? frameStep = null, CancellationToken cancellationToken = default, string? expectedManifestSha256 = null)
    {
        if (frameStep is <= 0) throw new ArgumentException("Positive historical frame required.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (expectedManifestSha256 is not null)
            {
                if (!Digest(expectedManifestSha256)) throw new ArgumentException("Invalid package manifest digest.");
                var declared = List().SingleOrDefault(p => p.PackageId == id) ?? throw new KeyNotFoundException();
                if (!declared.ManifestSha256.Equals(expectedManifestSha256, StringComparison.OrdinalIgnoreCase)) throw new NasdaqEvidenceConflictException("RegisteredPackageChanged_NoVersionSubstitution");
            }
            var inspected = await InspectCore(id, frameStep, cancellationToken);
            if (expectedManifestSha256 is not null && !inspected.ManifestSha256.Equals(expectedManifestSha256, StringComparison.OrdinalIgnoreCase)) throw new NasdaqEvidenceConflictException("RegisteredPackageChangedDuringInspection");
            return inspected;
        }
        finally { gate.Release(); }
    }
    private async Task<NasdaqHistoricalPackageInspection> InspectCore(string id, long? frameStep, CancellationToken cancellationToken)
    {
        var registration = List().SingleOrDefault(p => p.PackageId == id) ?? throw new KeyNotFoundException("RegisteredPackageMissing");
        var package = Document<NasdaqHistoricalEvidencePackage>(ResolveArtifactPath(registryDirectory!, registration.ManifestFile), registration.ManifestSha256);
        var invalid = new List<string>(); var blockers = new List<string> { "HistoricalAvailabilityNotEstablished", "NoTrustedExportAvailabilityAdmissionContract",
            "CandleTimestampAndCloseAreNotPublicationOrCaptureTime", "RetrospectiveHumanReviewCannotBackdateAvailability", "TradingWindow1130Vs1100SpecificationConflict", "HistoricalReportRuntimeAndSourcesNotRevalidatedAsNewAdmission" };
        blockers.Add("PackageAdmissionApprovalNotIndependentlyEstablished");
        blockers.Add("OriginalMentorMediaNotAuthenticatedByRetainedReviewText");
        var artifacts = new List<NasdaqHistoricalArtifactInspection>(); var coverage = new List<NasdaqHistoricalCoverageInspection>();
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        if (package.PackageId != id || package.PackageVersion != registration.PackageVersion || package.Scenario != "Standard" || package.TradingDay == default
            || package.StrategyId != definition.StrategyId.Value || package.StrategyVersion != definition.Version.Value || string.IsNullOrWhiteSpace(package.SessionId)
            || string.IsNullOrWhiteSpace(package.ProviderId) || string.IsNullOrWhiteSpace(package.OriginalSymbol)) invalid.Add("InvalidPackageOrStrategyIdentity");
        if (package.Origin is not ("AuthenticHistoricalExportCandidate" or "SyntheticFixture")) invalid.Add("InvalidSourceOrigin");
        if (package.Transformation != "DocumentedExternalNormalization_NotAnAvailabilityAttestation") invalid.Add("UnsupportedSourceTransformation");
        if (string.IsNullOrWhiteSpace(package.CandleProvenance) || string.IsNullOrWhiteSpace(package.OriginalClockDomain) || string.IsNullOrWhiteSpace(package.TimeMapping)
            || string.IsNullOrWhiteSpace(package.AvailabilityEvidence)) invalid.Add("MissingSourceOrAvailabilityProvenance");
        Require(package.Artifacts is { Count: > 0 and <= 32 } && package.HumanReviews is { Count: <= 32 } && package.Limitations is { Count: <= 32 }, "Invalid bounded manifest collections.");
        Require(package.Artifacts.All(a => a is not null && !string.IsNullOrWhiteSpace(a.Id)) && package.HumanReviews.All(r => r is not null && !string.IsNullOrWhiteSpace(r.Id)), "Null artifact/review identity.");
        if (package.Artifacts.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != package.Artifacts.Count) invalid.Add("DuplicateArtifactIdentity");
        foreach (var a in package.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested(); string? actual = null; var status = "VerifiedHashOnly_NotSourceAuthentication"; var verified = false;
            try
            {
                if (a.Kind is not ("RawCandles" or "NormalizedCandles" or "NormalizationProvenance" or "Adapter" or "HumanReviewText" or "EvidenceLedger" or "HistoricalReport")) invalid.Add("UnsupportedArtifactKind:" + a.Id);
                if (!roots.TryGetValue(a.Root, out var root)) throw new InvalidDataException("UnknownRegisteredRoot");
                var path = ResolveArtifactPath(root, a.RelativePath);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Require(stream.Length > 0 && stream.Length <= (a.Kind == "HistoricalReport" ? 512L * 1024 * 1024 : 16L * 1024 * 1024), "Artifact size limit.");
                actual = Hash(stream); verified = Digest(a.Sha256) && actual.Equals(a.Sha256, StringComparison.OrdinalIgnoreCase);
                if (!verified) { invalid.Add("ArtifactHashMismatch:" + a.Id); status = "HashMismatch"; }
                if (string.IsNullOrWhiteSpace(a.Provenance)) invalid.Add("ArtifactProvenanceMissing:" + a.Id);
                if (verified && a.Kind == "NormalizedCandles")
                {
                    stream.Position = 0; using var text = new StreamReader(stream, System.Text.Encoding.UTF8, true, 4096, leaveOpen: true);
                    var series = new CsvMarketDataImporter().Import(text);
                    if (series.ProviderId.Value != package.ProviderId || series.Symbol.Value != package.OriginalSymbol || series.Timeframe.ToString() != a.Timeframe)
                        invalid.Add("CandleSourceIdentityMismatch:" + a.Id);
                    if (a.ExpectedCandleCount != series.Count) invalid.Add("CandleCountMismatch:" + a.Id);
                    var duration = series.Timeframe.Unit == TimeframeUnit.Hour ? TimeSpan.FromHours(series.Timeframe.Amount) : TimeSpan.FromMinutes(series.Timeframe.Amount);
                    if (series.Candles.Any(c => c.CloseTimeUtc - c.OpenTimeUtc != duration)) invalid.Add("NormalizedCandleDurationMismatch:" + a.Id);
                    var discontinuities = series.Candles.Zip(series.Candles.Skip(1)).Where(p => p.First.CloseTimeUtc != p.Second.OpenTimeUtc).ToArray();
                    var future = series.Candles.Count(c => NasdaqSupervisedSessionPolicy.TradingDay(c.CloseTimeUtc) > package.TradingDay);
                    coverage.Add(new(a.Id, series.Timeframe.ToString(), series.Count, series.Candles[0].OpenTimeUtc, series.Candles[^1].CloseTimeUtc,
                        discontinuities.Length, discontinuities.FirstOrDefault().First?.CloseTimeUtc, discontinuities.FirstOrDefault().Second?.OpenTimeUtc, future,
                        "DeclaredNormalizedUtc_NotVerifiedHistoricalAvailability"));
                    if (discontinuities.Length > 0) blockers.Add("SourceIntervalDiscontinuities_NoScheduleOrMissingDataInference:" + a.Id);
                    if (future > 0) blockers.Add("ContainsLaterCandles_AnyFutureReplayMustUseExistingAsOfPrefix:" + a.Id);
                }
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or MarketDataCsvImportException)
            { invalid.Add("ArtifactUnavailableOrInvalid:" + a.Id); status = "UnavailableOrInvalid_SourcePreserved"; }
            artifacts.Add(new(a.Id, a.Kind, a.Root, a.RelativePath, a.Sha256, actual, verified, status));
        }
        var required = NasdaqMentorSessionReplayInput.RequiredTimeframes.Select(t => t.ToString()).ToArray();
        if (!required.Order().SequenceEqual(coverage.Select(c => c.Timeframe).Order())) invalid.Add("MissingDuplicateOrUnsupportedTimeframe");
        if (package.HumanReviews.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != package.HumanReviews.Count) invalid.Add("DuplicateHumanReviewIdentity");
        foreach (var review in package.HumanReviews)
        {
            if (!artifacts.Any(a => a.Id == review.ArtifactId && a.IntegrityVerified && a.Kind is "HumanReviewText" or "EvidenceLedger") || string.IsNullOrWhiteSpace(review.Reviewer)
                || string.IsNullOrWhiteSpace(review.Claim) || string.IsNullOrWhiteSpace(review.SemanticSlot) || string.IsNullOrWhiteSpace(review.AvailabilityEvidence)) invalid.Add("HumanReviewProvenanceMissing:" + review.Id);
            if (review.ClaimedAvailableAtUtc is { } at && (at.Offset != TimeSpan.Zero || NasdaqSupervisedSessionPolicy.TradingDay(at) > package.TradingDay)) blockers.Add("ReviewClaimAvailableAfterSession:" + review.Id);
            if (review.ReviewCreatedOn is null || review.ReviewCreatedOn > package.TradingDay) blockers.Add("RetrospectiveOrUndatedReview:" + review.Id);
            if (review.Decision is not ("HumanObservationOnly" or "ClaimedPackageApproval" or "Rejected")) invalid.Add("UnsupportedHumanDecision:" + review.Id);
        }
        foreach (var group in package.HumanReviews.GroupBy(r => r.SemanticSlot))
            if (group.Select(r => (r.Claim, r.Decision)).Distinct().Count() > 1) blockers.Add("ConflictingHumanClaimsOrApprovals:" + group.Key);
        if (package.HumanReviews.Count == 0) blockers.Add("HumanSpecificObservationsMissing");
        NasdaqHistoricalReportSummary? report = null;
        var stored = package.Artifacts.Where(a => a.Kind == "HistoricalReport").ToArray();
        if (stored.Length > 1) invalid.Add("ConflictingHistoricalReports");
        if (invalid.Count == 0 && stored.Length == 1)
        {
            var a = stored[0];
            try
            {
                report = await reader.ExecuteAsync(ResolveArtifactPath(roots[a.Root], a.RelativePath), a.Sha256, cancellationToken, frameStep);
                if (report.StrategyId != package.StrategyId || report.StrategyVersion != package.StrategyVersion || report.ProviderId != package.ProviderId
                    || report.Symbol != package.OriginalSymbol || report.TradingDay != package.TradingDay || report.SessionId != package.SessionId) invalid.Add("HistoricalReportIdentityMismatch");
                if (report.SelectedFrame is null) invalid.Add("HistoricalFrameUnavailable");
                if (report.SelectedFrame?.TradeLifecycle?.Path == "WickfillScenario2" || report.SelectedFrame?.Rules.Any(r => r.RuleId == "NQ-WF2-002" && r.Result == "Passed") == true) invalid.Add("Wf2ReportCannotReplaceStandard");
            }
            catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
            { invalid.Add("HistoricalReportUnavailableOrInvalid"); }
        }
        else if (stored.Length == 0) blockers.Add("HistoricalReportMissing");
        if (invalid.Count > 0) report = null;
        return new(package, registration.ManifestSha256, invalid.Count == 0, artifacts.All(a => a.IntegrityVerified), invalid.Distinct().ToArray(), blockers.Distinct().ToArray(), artifacts, coverage, report);
    }
    public void Dispose() => gate.Dispose();
}
