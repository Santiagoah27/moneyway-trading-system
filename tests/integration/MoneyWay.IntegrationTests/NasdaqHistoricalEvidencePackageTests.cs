using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

// Explicit synthetic source/claim declarations. No authentic availability or human approval is fabricated.
public sealed class NasdaqHistoricalEvidencePackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-package-" + Guid.NewGuid().ToString("N"));
    private readonly ReadNasdaqHistoricalReport reader = new();
    private NasdaqHistoricalEvidencePackage package;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    public NasdaqHistoricalEvidencePackageTests()
    {
        Directory.CreateDirectory(root);
        var artifacts = new List<NasdaqHistoricalPackageArtifact>();
        foreach (var tf in new[] { "1m", "5m", "1h", "4h" })
        {
            var path = Path.Combine(root, tf + ".csv"); var hours = tf.EndsWith('h'); var amount = int.Parse(tf[..1]);
            var start = DateTimeOffset.Parse("2026-06-29T05:00:00Z"); var span = hours ? TimeSpan.FromHours(amount) : TimeSpan.FromMinutes(amount);
            var rows = Enumerable.Range(0, 2).Select(i => FormattableString.Invariant($"synthetic-package,US100,{amount},{(hours ? "Hour" : "Minute")},{start.AddTicks(span.Ticks * i):O},{start.AddTicks(span.Ticks * (i + 1)):O},100,101,99,100,1"));
            File.WriteAllText(path, "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume\n" + string.Join('\n', rows));
            artifacts.Add(new(tf, "NormalizedCandles", "source", tf + ".csv", Hash(path), "synthetic fixture only", tf, 2));
        }
        File.WriteAllText(Path.Combine(root, "review.txt"), "synthetic retrospective claim; no genuine human approval");
        artifacts.Add(new("review", "HumanReviewText", "source", "review.txt", Hash(Path.Combine(root, "review.txt")), "synthetic text only"));
        package = new("synthetic-standard-package", 1, "moneyway-nasdaq", "nasdaq-0.1.0-draft", "Standard", "synthetic-package", "US100", "synthetic analysis, no feed equivalence", new(2026, 6, 29),
            "synthetic:documentary", "SyntheticFixture", "synthetic OHLC, not authentic market data", "synthetic native clock", "synthetic conversion claim only",
            "DocumentedExternalNormalization_NotAnAvailabilityAttestation", "not independently established", artifacts,
            [new("review-1", "preparation", "not established", "HumanObservationOnly", "review", "synthetic:reviewer", new(2026, 7, 1), "approximate historical source time", null, "retrospective source; no contemporaneous availability")],
            ["synthetic only, no broker or human authentication"]);
        Register();
    }
    private void Register()
    {
        File.WriteAllText(Path.Combine(root, "package.json"), JsonSerializer.Serialize(package, Json));
        File.WriteAllText(Path.Combine(root, "registry.json"), JsonSerializer.Serialize(new NasdaqHistoricalPackageRegistry(1,
            [new(package.PackageId, package.PackageVersion, "package.json", Hash(Path.Combine(root, "package.json")))]), Json));
    }
    private ReadNasdaqHistoricalEvidencePackages Service() => new(root, new Dictionary<string, string> { ["source"] = root }, reader);
    private WebApplicationFactory<Program> App() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development")
        .UseSetting("NasdaqHistoricalPackages:RegistryDirectory", root).UseSetting("NasdaqHistoricalPackages:Roots:source", root)
        .UseSetting("NasdaqExecutionEvidence:Directory", Path.Combine(root, "capture")));
    [Fact]
    public async Task CompleteSyntheticDocumentaryIntegrityNeverGrantsAdmissionOrApproval()
    {
        using var service = Service(); var result = await service.InspectAsync(package.PackageId);
        Assert.True(result.DocumentaryStructureValid); Assert.True(result.SourceIntegrityVerified); Assert.Empty(result.InvalidReasons);
        Assert.Equal(4, result.Coverage.Count); Assert.Equal("SyntheticFixture", result.Package.Origin);
        Assert.False(result.CanonicalReplayPermitted); Assert.Null(result.NewCanonicalVerdict); Assert.Null(result.AdmissibleDiagnosticReference);
        Assert.Equal("DataUnavailable", result.AdmissionResult); Assert.Contains("HistoricalAvailabilityNotEstablished", result.AdmissionBlockers);
        Assert.Contains("1100", result.TradingWindowConflict); Assert.Contains("1130", result.TradingWindowConflict);
        Assert.False(result.BrokerExecutionVerified); Assert.Null(result.CalculatedPnl);
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("altered")]
    [InlineData("order")]
    [InlineData("duplicate")]
    [InlineData("conflicting")]
    [InlineData("provider")]
    [InlineData("instrument")]
    [InlineData("duration")]
    public async Task InvalidSourceOrCandleSequenceIsNotConfusedWithAValidDocumentaryPackage(string variant)
    {
        var file = Path.Combine(root, "1m.csv"); var lines = File.ReadAllLines(file);
        if (variant == "missing") File.Move(file, file + ".preserved");
        else if (variant == "altered") File.AppendAllText(file, "unexpected source mutation");
        else
        {
            if (variant == "order") (lines[1], lines[2]) = (lines[2], lines[1]);
            if (variant == "duplicate") lines[2] = lines[1];
            if (variant == "conflicting") lines[2] = lines[1].Replace(",100,101,99,100,", ",100,102,99,100,", StringComparison.Ordinal);
            if (variant == "provider") lines = lines.Select(l => l.Replace("synthetic-package,US100", "other-provider,US100", StringComparison.Ordinal)).ToArray();
            if (variant == "instrument") lines = lines.Select(l => l.Replace(",US100,", ",USTEC,", StringComparison.Ordinal)).ToArray();
            if (variant == "duration") lines[2] = lines[2].Replace("05:02:00", "05:02:30", StringComparison.Ordinal);
            File.WriteAllLines(file, lines);
            package = package with { Artifacts = package.Artifacts.Select(a => a.Id == "1m" ? a with { Sha256 = Hash(file) } : a).ToArray() }; Register();
        }
        using var service = Service(); var result = await service.InspectAsync(package.PackageId);
        Assert.False(result.DocumentaryStructureValid); Assert.NotEmpty(result.InvalidReasons); Assert.False(result.CanonicalReplayPermitted);
    }
    [Theory]
    [InlineData("identity")]
    [InlineData("version")]
    [InlineData("wf2")]
    [InlineData("missing-timeframe")]
    [InlineData("transformation")]
    public async Task InvalidManifestRequirementsRemainExplicit(string variant)
    {
        package = variant switch
        {
            "identity" => package with { StrategyId = "moneyway-forex" },
            "version" => package with { StrategyVersion = "unapproved-version" },
            "wf2" => package with { Scenario = "WickfillScenario2" },
            "missing-timeframe" => package with { Artifacts = package.Artifacts.Where(a => a.Timeframe != "4h").ToArray() },
            _ => package with { Transformation = "invented-resampling-or-backfill" }
        }; Register();
        using var service = Service(); var result = await service.InspectAsync(package.PackageId);
        Assert.False(result.DocumentaryStructureValid); Assert.NotEmpty(result.InvalidReasons);
    }
    [Fact]
    public async Task DiscontinuitiesAndLaterCandlesAreVisibleWithoutInventingScheduleOrAvailability()
    {
        var file = Path.Combine(root, "1m.csv"); var lines = File.ReadAllLines(file);
        lines[2] = lines[2].Replace("2026-06-29", "2026-06-30", StringComparison.Ordinal); File.WriteAllLines(file, lines);
        package = package with { Artifacts = package.Artifacts.Select(a => a.Id == "1m" ? a with { Sha256 = Hash(file) } : a).ToArray() }; Register();
        using var service = Service(); var result = await service.InspectAsync(package.PackageId);
        Assert.True(result.DocumentaryStructureValid); var sample = result.Coverage.Single(c => c.Timeframe == "1m");
        Assert.Equal(1, sample.UnclassifiedDiscontinuities); Assert.Equal(1, sample.CandlesClosingAfterSessionDay);
        Assert.Contains(result.AdmissionBlockers, c => c.StartsWith("ContainsLaterCandles", StringComparison.Ordinal));
        Assert.False(result.CanonicalReplayPermitted);
    }
    [Fact]
    public async Task ClaimedNativeUtcAndHumanApprovalCannotReplaceTrustedAvailability()
    {
        package = package with
        {
            AvailabilityEvidence = "unverified MT5 native timestamp and candle-close claim",
            HumanReviews = [package.HumanReviews[0] with {
            Decision = "ClaimedPackageApproval", ReviewCreatedOn = new(2026, 6, 28), ClaimedAvailableAtUtc = DateTimeOffset.Parse("2026-06-30T15:00:00Z") }]
        }; Register();
        using var service = Service(); var result = await service.InspectAsync(package.PackageId);
        Assert.True(result.DocumentaryStructureValid); Assert.False(result.CanonicalReplayPermitted);
        Assert.Contains(result.AdmissionBlockers, c => c.StartsWith("ReviewClaimAvailableAfterSession", StringComparison.Ordinal));
        Assert.Contains("NoTrustedExportAvailabilityAdmissionContract", result.AdmissionBlockers);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateIdentityAndConflictingApprovalAreDistinguished(bool duplicate)
    {
        var original = package.HumanReviews[0];
        package = package with { HumanReviews = [original, original with { Id = duplicate ? original.Id : "review-2", Claim = "conflicting synthetic claim", Decision = "Rejected" }] }; Register();
        using var service = Service(); var result = await service.InspectAsync(package.PackageId);
        Assert.Contains(result.AdmissionBlockers, c => c.StartsWith("ConflictingHumanClaimsOrApprovals", StringComparison.Ordinal));
        Assert.Equal(!duplicate, result.DocumentaryStructureValid); Assert.False(result.CanonicalReplayPermitted);
    }
    [Fact]
    public async Task ManifestTamperingAndTraversalFailClosedWithoutAlternateSources()
    {
        using var service = Service(); File.AppendAllText(Path.Combine(root, "package.json"), " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.InspectAsync(package.PackageId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.InspectAsync("../../Windows/win.ini"));
        package = package with { Artifacts = [package.Artifacts[0] with { RelativePath = "../outside.csv" }] }; Register();
        var result = await service.InspectAsync(package.PackageId); Assert.False(result.DocumentaryStructureValid);
    }
    [Fact]
    public async Task AChangedRegisteredVersionCannotSilentlyReplaceTheSelectedManifest()
    {
        var previous = Hash(Path.Combine(root, "package.json")); package = package with { PackageVersion = 2 }; Register();
        using var service = Service();
        await Assert.ThrowsAsync<NasdaqEvidenceConflictException>(() => service.InspectAsync(package.PackageId, expectedManifestSha256: previous));
        Assert.False((await service.InspectAsync(package.PackageId)).CanonicalReplayPermitted);
    }
    [Fact]
    public async Task RealApiSelectionRestartAndAdmissionRejectionCannotChangeSessionTerminality()
    {
        using (var first = App())
        {
            var http = first.CreateClient();
            Assert.Single((await http.GetFromJsonAsync<NasdaqHistoricalPackageRegistration[]>("/api/nasdaq/historical-packages"))!);
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/nasdaq/historical-packages/" + package.PackageId)).StatusCode);
        }
        using var restarted = App(); var client = restarted.CreateClient();
        var read = (await client.GetFromJsonAsync<NasdaqHistoricalPackageInspection>("/api/nasdaq/historical-packages/" + package.PackageId))!;
        Assert.Equal(Hash(Path.Combine(root, "package.json")), read.ManifestSha256);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/nasdaq/historical-packages/{package.PackageId}/diagnostic")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/nasdaq/historical-packages/{package.PackageId}?canonicalReady=true")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/nasdaq/historical-packages?path=C:/Windows/win.ini")).StatusCode);
        using var nonlocal = new HttpRequestMessage(HttpMethod.Get, "/api/nasdaq/historical-packages"); nonlocal.Headers.Host = "untrusted.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(nonlocal)).StatusCode);
        using var production = App().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        Assert.Equal(HttpStatusCode.NotFound, (await production.CreateClient().GetAsync("/api/nasdaq/historical-packages")).StatusCode);
    }
    [LocalMentorSessionFact]
    public async Task AuthenticJunePackagePreservesNoTradeButCannotBeReadmittedFromRetrospectiveExports()
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
        using var service = new ReadNasdaqHistoricalEvidencePackages(Path.Combine(repository, "docs/data/nasdaq/packages"),
            new Dictionary<string, string> { ["session"] = @"C:\MoneyWay\session", ["repository"] = repository, ["attachments"] = @"C:\Users\Santi\.codex\attachments" }, reader);
        var result = await service.InspectAsync("nasdaq-standard-2026-06-29-documentary", 7830);
        Assert.True(result.DocumentaryStructureValid, string.Join(',', result.InvalidReasons)); Assert.True(result.SourceIntegrityVerified);
        Assert.Equal(9660, result.ExistingHistoricalReport!.FrameCount); Assert.Equal("NoTrade", result.ExistingHistoricalReport.FinalVerdict);
        Assert.Equal("NoTrade", result.ExistingHistoricalReport.SelectedFrame!.Verdict); Assert.False(result.CanonicalReplayPermitted);
        Assert.Equal("Failed", result.ExistingHistoricalReport.SelectedFrame.Rules.Single(r => r.RuleId == "NQ-TIME-003").Result);
        Assert.Equal(9660, result.Coverage.Single(c => c.Timeframe == "1m").CandleCount);
        Assert.Equal(1932, result.Coverage.Single(c => c.Timeframe == "5m").CandleCount);
        Assert.Equal(161, result.Coverage.Single(c => c.Timeframe == "1h").CandleCount);
        Assert.Equal(42, result.Coverage.Single(c => c.Timeframe == "4h").CandleCount);
        Assert.Equal("6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8", result.ExistingHistoricalReport.ReportSha256.ToLowerInvariant());
        var before = JsonSerializer.Serialize(result.ExistingHistoricalReport.SelectedFrame);
        var later = await service.InspectAsync(result.Package.PackageId, 7980); Assert.Equal("NoTrade", later.ExistingHistoricalReport!.SelectedFrame!.Verdict);
        var repeated = await service.InspectAsync(result.Package.PackageId, 7830); Assert.Equal(before, JsonSerializer.Serialize(repeated.ExistingHistoricalReport!.SelectedFrame));
        Assert.Equal(result.ManifestSha256, repeated.ManifestSha256);
        var local = new LocalNasdaqExecutionEvidenceStore(Path.Combine(root, "authentic-diagnostic-gate"));
        var session = local.OpenSession(Guid.NewGuid(), new(result.Package.SessionId, result.Package.TradingDay, result.Package.ProviderId,
            result.Package.OriginalSymbol, "Standard", "synthetic:operator inspecting real June research", "note:synthetic own session, no mentor trade"));
        var diagnostic = new ReadNasdaqSupervisedDiagnostic(local, reader, @"C:\MoneyWay\session\report.json", result.ExistingHistoricalReport.ReportSha256, service);
        var context = await diagnostic.ReadAsync(session.SessionKey, 7830);
        Assert.StartsWith("BlockedRegisteredPackage", context.PackageAdmission);
        await Assert.ThrowsAsync<NasdaqEvidenceConflictException>(() => diagnostic.ValidateAsync(context.Reference));
        Assert.False(local.Session(session.SessionKey).Terminal); Assert.Null(local.Session(session.SessionKey).ExecutionConfirmation);
    }
    public void Dispose() { reader.Dispose(); Directory.Delete(root, true); }
}
