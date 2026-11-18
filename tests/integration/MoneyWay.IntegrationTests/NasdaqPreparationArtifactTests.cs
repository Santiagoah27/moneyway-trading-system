using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqPreparationArtifactTests : IDisposable
{
    // Synthetic two-pixel red/blue PNG. Never an authentic market screenshot.
    internal const string SyntheticPng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGP4z8AAQv8BD/kD/YURmXYAAAAASUVORK5CYII=";
    internal const string SyntheticHash = "6fe8b897998e41db82f635c2bc1ee3c552369debf7fe862f043f6c6feb2bb60b";
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-artifact-test-" + Guid.NewGuid().ToString("N"));
    private LocalNasdaqExecutionEvidenceStore Store() => new(root);
    private NasdaqProspectivePreparationView Open(string symbol = "US100")
    {
        var s = Store().OpenSession(Guid.NewGuid(), new("synthetic:artifact-" + symbol, new(2026, 11, 18), "synthetic-provider", symbol, "Standard", "synthetic:operator", "note:synthetic artifact test"));
        return Store().SavePreparation(Guid.NewGuid(), new(s.SessionKey, "synthetic:operator", "note:synthetic partial", "synthetic initial"));
    }
    internal static NasdaqPreparationArtifactAnnotation Source() => new("synthetic-source.png", "image/png", "synthetic-provider", "US100", "synthetic two pixels, not market evidence", "synthetic attachment") { ObservationReference = "H4" };
    private NasdaqProspectivePreparationView Attach(NasdaqProspectivePreparationView p, Guid? submission = null, NasdaqPreparationArtifactAnnotation? source = null, byte[]? bytes = null) =>
        Store().AttachPreparationArtifact(submission ?? Guid.NewGuid(), p.Record.Evidence.SessionKey, p.Record.RecordId, p.Record.Revision, p.RevisionHash, source ?? Source(), bytes ?? Convert.FromBase64String(SyntheticPng));
    private WebApplicationFactory<Program> App(string environment = "Development") => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment(environment).UseSetting("NasdaqExecutionEvidence:Directory", root));
    private static async Task<HttpResponseMessage> Post(HttpClient c, string path, object body, bool header = true)
    {
        using var r = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (header) r.Headers.Add("X-MoneyWay-Local-Capture", "1"); return await c.SendAsync(r);
    }
    [Fact]
    public void ExactOriginalBytesMetadataAndPartialPreparationSurviveRestartAndDocumentaryCorrection()
    {
        var p = Open(); var v = Attach(p); var a = Assert.Single(v.Record.Artifacts);
        Assert.Equal(71, a.ByteLength); Assert.Equal(SyntheticHash, a.Sha256); Assert.Equal(2, a.AttachedInRevision);
        Assert.Null(a.Source.OriginalTime); Assert.False(a.UtcAuthenticated); Assert.False(a.OriginalSourceIndependentlyVerified); Assert.False(a.CanonicalAdmissionPermitted);
        Assert.Contains("Ordinary", a.ReceiptClockSource); Assert.Null(a.VerifiedHistoricalAvailableAtUtc);
        Assert.Equal("PartialPreparation", v.DocumentaryStatus); Assert.Equal(0, v.AdmissibleCompletionObservationCount); Assert.Null(v.PreparationRuleResult); Assert.Null(v.StrategyVerdict);
        Assert.DoesNotContain("OriginalArtifactReferenceMissing", v.MissingDocumentaryInputs);
        var original = Store().PreparationArtifact(p.Record.Evidence.SessionKey, p.Record.RecordId, 2, a.ArtifactId);
        Assert.Equal(Convert.FromBase64String(SyntheticPng), original.Bytes); Assert.Equal(SyntheticHash, Convert.ToHexStringLower(SHA256.HashData(original.Bytes)));
        var correction = Store().SavePreparation(Guid.NewGuid(), v.Record.Evidence with { RevisionReason = "synthetic new annotation" }, p.Record.RecordId, v.RevisionHash);
        Assert.Equal(a, Assert.Single(correction.Record.Artifacts)); Assert.Empty(Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId, 1).Record.Artifacts);
        Assert.Equal(p.RevisionHash, Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId, 1).RevisionHash);
        var next = Attach(correction, source: Source() with { OriginalFilename = "synthetic-second.png", OriginalTime = "synthetic earlier time", OriginalClockDomain = "unverified", TimePrecision = "Approximate" });
        Assert.Equal(2, next.Record.Artifacts.Count); Assert.Single(Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId, 2).Record.Artifacts);
        Assert.False(Store().Session(p.Record.Evidence.SessionKey).Terminal);
    }
    [Theory]
    [InlineData("../source.png")]
    [InlineData("C:\\source.png")]
    [InlineData("source.html")]
    [InlineData("CON.png")]
    [InlineData("source.png.exe")]
    [InlineData("source\n.png")]
    [InlineData("a..png")]
    [InlineData(".hidden.png")]
    public void UnsafeDisplayFilenamesNeverBecomePaths(string name)
    { var p = Open(); Assert.Throws<ArgumentException>(() => Attach(p, source: Source() with { OriginalFilename = name })); Assert.Empty(Directory.GetFiles(root, "artifact-*")); }
    [Theory]
    [InlineData("html")]
    [InlineData("truncated")]
    [InlineData("crc")]
    [InlineData("trailing")]
    [InlineData("oversize")]
    [InlineData("empty")]
    public void InvalidContentCannotBePublished(string issue)
    {
        var p = Open(); var png = Convert.FromBase64String(SyntheticPng);
        var bytes = issue switch { "html" => "<html><script>alert(1)</script>"u8.ToArray(), "truncated" => png[..^1], "crc" => png.Select((b, i) => i == 45 ? (byte)(b ^ 1) : b).ToArray(), "trailing" => png.Concat("<script>"u8.ToArray()).ToArray(), "oversize" => new byte[NasdaqPreparationPng.MaximumBytes + 1], _ => [] };
        Assert.Throws<ArgumentException>(() => Attach(p, bytes: bytes)); Assert.Empty(Directory.GetFiles(root, "artifact-*"));
    }
    [Fact]
    public void SourceTypeInstrumentAndObservationIdentityAreValidatedIndependently()
    {
        var p = Open();
        foreach (var source in new[] { Source() with { ContentType = "image/svg+xml" }, Source() with { OriginalSymbol = "USTEC" }, Source() with { SourceProviderId = "other-provider" }, Source() with { ObservationReference = "native:invented" }, Source() with { TimePrecision = "AuthenticatedUtc" } })
            Assert.Throws<ArgumentException>(() => Attach(p, source: source));
        Assert.Empty(Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId).Record.Artifacts);
    }
    [Fact]
    public async Task IdempotencyConcurrentRevisionConflictsAndChangedSubmissionContentNeverReplaceBytes()
    {
        var p = Open(); var id = Guid.NewGuid(); var first = Attach(p, id); Assert.Equal(first.RevisionHash, Attach(p, id).RevisionHash);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Attach(p, id, Source() with { SourceDescription = "synthetic changed assertion" }));
        async Task<bool> Try() => await Task.Run(() => { try { Attach(first); return true; } catch (NasdaqEvidenceConflictException) { return false; } });
        Assert.Single(await Task.WhenAll(Try(), Try()), value => value); Assert.Equal(2, Directory.GetFiles(root, "artifact-*.png").Length);
        Assert.Equal(SyntheticHash, Store().PreparationArtifact(p.Record.Evidence.SessionKey, p.Record.RecordId, 2, id).Metadata.Sha256);
    }
    [Fact]
    public void InterruptedPublicationLeavesNoPreparationPointerAndRetryRecoversVerifiedBlob()
    {
        var p = Open(); var id = Guid.NewGuid(); var blocking = Path.Combine(root, $"preparation-{p.Record.RecordId:N}.002.prep");
        Directory.CreateDirectory(blocking); Assert.ThrowsAny<IOException>(() => Attach(p, id));
        Assert.Equal(1, Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId).Record.Revision);
        Assert.Single(Directory.GetFiles(root, "artifact-*.png")); Directory.Delete(blocking);
        var recovered = Attach(p, id); Assert.Equal(2, recovered.Record.Revision); Assert.Single(Directory.GetFiles(root, "artifact-*.png"));
    }
    [Fact]
    public void UnreferencedBlobAndInterruptedPendingFileAreNotReadableAndConflictingOrphanFailsClosed()
    {
        var p = Open(); var id = Guid.NewGuid(); File.WriteAllText(Path.Combine(root, ".pending-synthetic"), "synthetic incomplete");
        File.WriteAllBytes(Path.Combine(root, $"artifact-{id:N}.png"), new byte[71]);
        Assert.Throws<KeyNotFoundException>(() => Store().PreparationArtifact(p.Record.Evidence.SessionKey, p.Record.RecordId, 1, id));
        Assert.Throws<InvalidDataException>(() => Attach(p, id)); Assert.Equal(1, Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId).Record.Revision);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrAlteredArtifactIsExplicitlyUnavailable(bool tamper)
    {
        var p = Attach(Open()); var a = p.Record.Artifacts.Single(); var path = Path.Combine(root, $"artifact-{a.ArtifactId:N}.png");
        if (tamper) File.WriteAllBytes(path, new byte[71]); else File.Delete(path);
        if (tamper) Assert.Throws<InvalidDataException>(() => Store().PreparationArtifact(p.Record.Evidence.SessionKey, p.Record.RecordId, 2, a.ArtifactId));
        else Assert.Throws<FileNotFoundException>(() => Store().PreparationArtifact(p.Record.Evidence.SessionKey, p.Record.RecordId, 2, a.ArtifactId));
    }
    [Fact]
    public void LegacyPreparationWithoutArtifactPropertyRetainsOriginalHashAndCanReceiveANewRevision()
    {
        var p = Open(); var path = Directory.GetFiles(root, "*.prep").Single(); var node = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(File.ReadAllText(path))!;
        node["record"]!.AsObject().Remove("artifacts");
        var raw = node["record"]!.ToJsonString(); var oldHash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
        node["sha256"] = oldHash; File.WriteAllText(path, node.ToJsonString());
        var legacy = Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId); Assert.Empty(legacy.Record.Artifacts); Assert.Equal(oldHash, legacy.RevisionHash);
        var next = Attach(legacy); Assert.Single(next.Record.Artifacts); Assert.Equal(oldHash, next.Record.PreviousRevisionHash);
        Assert.Equal(oldHash, Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId, 1).RevisionHash);
    }
    [Fact]
    public void BoundedArtifactCountDoesNotPublishAnUnreferencedNinthUpload()
    {
        var p = Open(); for (var n = 0; n < 8; n++) p = Attach(p);
        Assert.Throws<ArgumentException>(() => Attach(p)); Assert.Equal(8, Directory.GetFiles(root, "artifact-*.png").Length);
        Assert.Equal(9, Store().Preparation(p.Record.Evidence.SessionKey, p.Record.RecordId).Record.Revision);
    }
    [Fact]
    public async Task OversizedWireRequestIsRejectedBeforeAnyArtifactPublication()
    {
        var p = Open(); using var app = App(); var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/nasdaq/supervised-sessions/{p.Record.Evidence.SessionKey}/preparation/{p.Record.RecordId}/artifacts");
        request.Headers.Add("X-MoneyWay-Local-Capture", "1"); request.Content = new StringContent("{\"bytes\":\"" + new string('A', 6 * 1024 * 1024) + "\"}", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode); Assert.Empty(Directory.GetFiles(root, "artifact-*.png"));
    }
    [Fact]
    public async Task ApiUploadRestartDownloadAndSessionScopedSecurityAreFunctional()
    {
        var p = Open(); var route = $"/api/nasdaq/supervised-sessions/{p.Record.Evidence.SessionKey}/preparation/{p.Record.RecordId}";
        var body = new { submissionId = Guid.NewGuid(), preparationRevision = 1, expectedRevision = p.RevisionHash, source = Source(), bytes = Convert.FromBase64String(SyntheticPng) }; NasdaqProspectivePreparationView uploaded;
        using (var app = App())
        {
            var client = app.CreateClient();
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, route + "/artifacts", body, false)).StatusCode);
            var response = await Post(client, route + "/artifacts", body); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            uploaded = (await response.Content.ReadFromJsonAsync<NasdaqProspectivePreparationView>())!;
            var forged = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!; forged["source"]!["utcAuthenticated"] = true;
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, route + "/artifacts", forged)).StatusCode);
        }
        using var restarted = App(); var http = restarted.CreateClient(); var a = uploaded.Record.Artifacts.Single();
        var read = route + $"/revisions/2/artifacts/{a.ArtifactId}";
        Assert.Equal(SyntheticHash, (await http.GetFromJsonAsync<NasdaqPreparationArtifact>(read))!.Sha256);
        var download = await http.GetAsync(read + "/content"); Assert.Equal(Convert.FromBase64String(SyntheticPng), await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single()); Assert.Contains("sandbox", download.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(read.Replace("/revisions/2/", "/revisions/1/"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(read + "/content?path=C:/private.png")).StatusCode);
        var other = Open("USTEC"); Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(read.Replace(p.Record.Evidence.SessionKey, other.Record.Evidence.SessionKey))).StatusCode);
        http.DefaultRequestHeaders.Add("Origin", "https://untrusted.example"); Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(read)).StatusCode);
        using var prod = App("Production"); Assert.Equal(HttpStatusCode.NotFound, (await prod.CreateClient().GetAsync(read)).StatusCode);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
