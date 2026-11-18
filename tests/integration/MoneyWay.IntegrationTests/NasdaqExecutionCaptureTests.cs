using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

// All entries, identifiers, prices and reviewer identities here are synthetic operator assertions.
public sealed class NasdaqExecutionCaptureTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-capture-test-" + Guid.NewGuid().ToString("N"));
    private LocalNasdaqExecutionEvidenceStore Store() => new(root);
    private WebApplicationFactory<Program> App(string environment = "Development") => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.UseEnvironment(environment).UseSetting("NasdaqExecutionEvidence:Directory", root));
    internal static NasdaqSupervisedExecutionEvidenceDraft Draft() => new("synthetic:session", new(2026, 9, 8), "synthetic:platform", "USTEC", "synthetic:reviewer",
        "note:synthetic human report, original bytes unavailable", "synthetic:initial registration")
    {
        Direction = "Sell",
        Scenario = "WickfillScenario2",
        Entry = new("note:synthetic opening row", "PositionOpeningField")
        {
            Price = "29604.00",
            PriceUnit = "NativeInstrumentPrice",
            OriginalTime = "synthetic:16:32 server",
            TimePrecision = "SourceReportedSecond",
            ClockDomain = "synthetic:unresolved server clock"
        }
    };
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-MoneyWay-Local-Capture", "1"); return await client.SendAsync(request);
    }
    [Fact]
    public void EntryOnlyExitOnlyAndApproximateCaptureRemainIncompleteAndRetrospective()
    {
        var store = Store(); var d = Draft(); var first = store.Save(Guid.NewGuid(), d);
        Assert.Equal("29604.00", first.Record.Evidence.Entry!.Price); Assert.Null(first.Record.Evidence.Entry.DealId);
        Assert.False(first.NativeExecutionVerified); Assert.False(first.CompleteSupervisedTrade); Assert.Null(first.EconomicOutcome); Assert.Null(first.OriginalArtifactSha256);
        Assert.Null(first.DocumentaryLifecycle!.HistoricalEntry!.Observations.Single().CanonicalAtEntry);
        Assert.Equal("NotGrantedByDiagnostic", first.DocumentaryLifecycle.HistoricalEntry.EntryAuthorization);
        Assert.Equal(first.Record.RecordId, first.DocumentaryLifecycle.SupervisedExecutionRecord!.RecordId);
        var exit = new NasdaqSupervisedExecutionEvent("note:synthetic approximate closure", "ReportedClosure")
        { TimePrecision = "Approximate", OriginalTime = "around 08:37", ClockDomain = "synthetic:unresolved" };
        var onlyExit = store.Save(Guid.NewGuid(), d with { Entry = null, Exit = exit });
        Assert.Contains("EntryMissing", onlyExit.MissingEvidence); Assert.Contains("ExitInstrumentUnknown_NoImplicitMapping", onlyExit.MissingEvidence);
        var revised = store.Save(Guid.NewGuid(), d with { Exit = exit, Association = "HumanReported", AssociationSourceReference = "note:synthetic explicitly related trade", RevisionReason = "synthetic:add closure" }, first.Record.RecordId, first.RevisionHash);
        Assert.Equal(2, revised.Record.Revision); Assert.Equal(first.RevisionHash, revised.Record.PreviousRevision);
        Assert.Equal("HumanReported", revised.Record.Evidence.Association); Assert.Contains("HumanReportedAssociationAsserted_NotIndependentlyVerified", revised.MissingEvidence);
        Assert.Null(revised.Record.Evidence.Exit!.Price); Assert.Null(revised.Record.Evidence.Exit.DealId);
        Assert.Equal(first.Record, store.Get(first.Record.RecordId, 1).Record); Assert.Single(store.List(d.SessionId), v => v.Record.RecordId == first.Record.RecordId);
    }
    [Fact]
    public void RepeatedSubmissionSurvivesNewStoreInstanceAndConflictingRevisionsCannotOverwrite()
    {
        var key = Guid.NewGuid(); var d = Draft(); var first = Store().Save(key, d);
        var retry = Store().Save(key, d); Assert.Equal(first.Record, retry.Record); Assert.Equal(first.RevisionHash, retry.RevisionHash);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().Save(key, d with { Reviewer = "synthetic:other" }));
        var update = d with { RevisionReason = "synthetic:source correction", SourceReference = "https://example.invalid/synthetic-source" };
        var revised = Store().Save(Guid.NewGuid(), update, first.Record.RecordId, first.RevisionHash);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().Save(Guid.NewGuid(), d, first.Record.RecordId, first.RevisionHash));
        Assert.Equal(revised.Record, Store().Get(first.Record.RecordId).Record); Assert.Equal(2, Directory.GetFiles(root, "*.json").Length);
    }
    [Fact]
    public async Task ConcurrentStoreInstancesPublishOnlyOneCorrectionForTheSameExpectedRevision()
    {
        var first = Store().Save(Guid.NewGuid(), Draft());
        async Task<bool> Correct(string reason) => await Task.Run(() =>
        {
            try { Store().Save(Guid.NewGuid(), Draft() with { RevisionReason = reason }, first.Record.RecordId, first.RevisionHash); return true; }
            catch (NasdaqEvidenceConflictException) { return false; }
        });
        var results = await Task.WhenAll(Correct("synthetic:operator-one"), Correct("synthetic:operator-two"));
        Assert.Single(results, succeeded => succeeded); Assert.Equal(2, Store().Get(first.Record.RecordId).Record.Revision);
        Assert.Equal(2, Directory.GetFiles(root, "*.json").Length); Assert.Equal(first.RevisionHash, Store().Get(first.Record.RecordId, 1).RevisionHash);
    }
    [Fact]
    public void MissingSourceIdentityAndMissingObservationsCannotBeSavedAsEvidence()
    {
        Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { SourcePlatform = "" }));
        Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { Reviewer = "" }));
        Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { Entry = null, Exit = null }));
        Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { OriginalSymbol = "other-symbol" }));
    }
    [Theory]
    [InlineData("instrument")]
    [InlineData("positions")]
    [InlineData("direction")]
    [InlineData("before")]
    public void ConflictsAreRetainedAsUnresolvedEvidenceWithoutExecutionApproval(string kind)
    {
        var d = Draft();
        var entry = d.Entry! with { Symbol = "USTEC", Direction = "Sell", DealId = "synthetic:opening", PositionId = "synthetic:position", DocumentedUtc = DateTimeOffset.Parse("2026-09-08T13:32:00Z"), TimeMappingReference = "note:synthetic UTC mapping" };
        var exit = new NasdaqSupervisedExecutionEvent("note:synthetic closing assertion", "ReportedClosure")
        {
            Symbol = kind == "instrument" ? "US100" : "USTEC",
            Direction = kind == "direction" ? "Buy" : "Sell",
            DealId = "synthetic:closing",
            PositionId = kind == "positions" ? "synthetic:other-position" : "synthetic:position",
            TimePrecision = "ExactNativeRecord",
            DocumentedUtc = DateTimeOffset.Parse(kind == "before" ? "2026-09-08T13:31:00Z" : "2026-09-08T13:37:00Z"),
            TimeMappingReference = "note:synthetic mapping"
        };
        var view = Store().Save(Guid.NewGuid(), d with { Entry = entry, Exit = exit, Association = "DocumentarySupported", AssociationSourceReference = "note:synthetic documentary assertion" });
        Assert.Contains(kind switch { "instrument" => "EntryExitInstrumentConflict_NoMapping", "positions" => "NativePositionConflict", "direction" => "ConflictingDirections", _ => "ExitPrecedesEntry" }, view.MissingEvidence);
        Assert.False(view.NativeExecutionVerified); Assert.False(view.EntryExitRelationshipVerified); Assert.False(view.CompleteSupervisedTrade);
    }
    [Theory]
    [InlineData("NaN")]
    [InlineData("1e3")]
    [InlineData("1,000")]
    [InlineData("1.12345678901234567890123456789")]
    [InlineData("99999999999999999999999999999")]
    [InlineData("12.1234567890123456789012345678")]
    public void InvalidOrRoundedNumericInputIsRejected(string price) => Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { Entry = Draft().Entry! with { Price = price } }));
    [Fact]
    public void ExactDecimalsAndExplicitBuyObservationDoNotCreateAStrategyBranch()
    {
        var d = Draft(); var view = Store().Save(Guid.NewGuid(), d with { Direction = "Buy", Scenario = null, Entry = d.Entry! with { Price = "1.1234567890123456789012345678" } });
        Assert.Equal("1.1234567890123456789012345678", view.Record.Evidence.Entry!.Price); Assert.Null(view.DocumentaryLifecycle);
        Assert.False(view.CompleteSupervisedTrade);
        var unknown = Store().Save(Guid.NewGuid(), d with { Direction = null, Scenario = null });
        Assert.Contains("DirectionUnknown", unknown.MissingEvidence);
    }
    [Theory]
    [InlineData("file:///C:/secret")]
    [InlineData("C:\\secret")]
    [InlineData("http://example.com")]
    [InlineData("https://user:password@example.com")]
    public void SourceReferencesCannotCauseFilesystemOrNetworkAccess(string source) => Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { SourceReference = source }));
    [Fact]
    public void TamperedRevisionFailsIntegrityAndOriginalRevisionIsPreserved()
    {
        var saved = Store().Save(Guid.NewGuid(), Draft()); var file = Directory.GetFiles(root, "*.json").Single();
        File.WriteAllText(file, File.ReadAllText(file).Replace("29604.00", "29605.00", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => Store().Get(saved.Record.RecordId)); Assert.Single(Directory.GetFiles(root, "*.json"));
    }
    [Fact]
    public void InitialVersionEnvelopesKeepTheirOriginalPayloadHashWhenReadAgain()
    {
        var saved = Store().Save(Guid.NewGuid(), Draft()); var file = Directory.GetFiles(root, "*.json").Single();
        var envelope = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!;
        Assert.Equal(1, envelope["schemaVersion"]!.GetValue<int>());
        // Early local captures used the same version-1 payload without an explicit envelope version.
        var raw = File.ReadAllText(file).Replace(",\"schemaVersion\":1", "", StringComparison.Ordinal);
        File.WriteAllText(file, raw);
        Assert.Equal(saved.RevisionHash, Store().Get(saved.Record.RecordId).RevisionHash);
    }
    [Fact]
    public async Task ApiCreateListReadReviseAndApplicationRestartUseRealDurableFiles()
    {
        Guid id; string hash;
        using (var app = App())
        {
            var client = app.CreateClient(); var key = Guid.NewGuid(); var response = await Post(client, "/api/nasdaq/execution-evidence", new { submissionId = key, evidence = Draft() });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var saved = (await response.Content.ReadFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>())!; id = saved.Record.RecordId; hash = saved.RevisionHash;
            var duplicate = await Post(client, "/api/nasdaq/execution-evidence", new { submissionId = key, evidence = Draft() });
            Assert.Equal(id, (await duplicate.Content.ReadFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>())!.Record.RecordId);
            var list = await client.GetFromJsonAsync<NasdaqSupervisedExecutionEvidenceView[]>("/api/nasdaq/execution-evidence?sessionId=synthetic:session"); Assert.Single(list!);
        }
        using (var restarted = App())
        {
            var client = restarted.CreateClient(); var read = await client.GetFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>($"/api/nasdaq/execution-evidence/{id}");
            Assert.Equal(hash, read!.RevisionHash); Assert.Equal("29604.00", read.Record.Evidence.Entry!.Price);
            var revision = await Post(client, $"/api/nasdaq/execution-evidence/{id}/revisions", new { submissionId = Guid.NewGuid(), expectedRevision = hash, evidence = Draft() with { RevisionReason = "synthetic:correction" } });
            Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
            var conflict = await Post(client, $"/api/nasdaq/execution-evidence/{id}/revisions", new { submissionId = Guid.NewGuid(), expectedRevision = hash, evidence = Draft() });
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            var old = await client.GetFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>($"/api/nasdaq/execution-evidence/{id}?revision=1"); Assert.Equal(hash, old!.RevisionHash);
        }
    }
    [Theory]
    [InlineData("trusted")]
    [InlineData("originalArtifactSha256")]
    [InlineData("historicalAvailableAtUtc")]
    [InlineData("nativeExecutionVerified")]
    public async Task CallerTrustHashAndAvailabilityBypassesAreRejected(string field)
    {
        using var app = App(); var json = JsonSerializer.SerializeToNode(new { submissionId = Guid.NewGuid(), evidence = Draft() }, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        json["evidence"]![field] = "fabricated";
        var response = await Post(app.CreateClient(), "/api/nasdaq/execution-evidence", json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(Directory.Exists(root) ? Directory.GetFiles(root, "*.json") : []);
    }
    [Fact]
    public async Task EndpointIsDevelopmentLocalOnlyAndRequiresExplicitWriteHeader()
    {
        using var app = App(); var client = app.CreateClient();
        var missingHeader = await client.PostAsJsonAsync("/api/nasdaq/execution-evidence", new { submissionId = Guid.NewGuid(), evidence = Draft() }); Assert.Equal(HttpStatusCode.BadRequest, missingHeader.StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "https://unrelated.example"); var blocked = await Post(client, "/api/nasdaq/execution-evidence", new { submissionId = Guid.NewGuid(), evidence = Draft() }); Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        using var production = App("Production"); Assert.Equal(HttpStatusCode.NotFound, (await production.CreateClient().GetAsync("/api/nasdaq/execution-evidence?sessionId=synthetic:session")).StatusCode);
    }
    [LocalMentorSessionFact]
    public async Task AuthenticReplay2ReportIsUnchangedBySeparatelyCapturedRetrospectivePartialRecord()
    {
        const string path = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-ed4a1539bef845fd84f5a68dcfc81cbe.json";
        const string hash = "48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db";
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
        using var reader = new ReadNasdaqHistoricalReport(); var before = await reader.ExecuteAsync(path, hash, frameStep: 7592);
        var retrospective = Draft() with
        {
            SessionId = "synthetic-operator-demonstration-only:replay2",
            SourceReference = "note:test transcribes existing S14 documentary values, no new personal validation",
            Entry = Draft().Entry! with { OriginalTime = "16:32 server; documentary only" },
            Exit = new("note:current request reports around 08:37; original closing evidence unavailable", "ReportedClosure") { OriginalTime = "around 08:37 Bogota", TimePrecision = "Approximate" }
        };
        var saved = Store().Save(Guid.NewGuid(), retrospective); var after = await reader.ExecuteAsync(path, hash, frameStep: 7592);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)); Assert.False(saved.CompleteSupervisedTrade);
        Assert.Equal("USTEC", saved.Record.Evidence.OriginalSymbol); Assert.Null(saved.Record.Evidence.Exit!.Symbol);
        Assert.Equal("29604.00", Store().Get(saved.Record.RecordId).Record.Evidence.Entry!.Price);
        Assert.False(before.SelectedFrame!.TradeLifecycle!.SupervisedSnapshot!.CanonicalEligibilityRecordedAtEntry);
        var final = await reader.ExecuteAsync(path, hash); var trade = final.SelectedFrame!.TradeLifecycle!;
        Assert.Equal("HumanValidationRequired", final.SelectedFrame.Verdict); Assert.Equal("HumanValidationRequired", trade.Stages.Single(s => s.Stage == "StopLoss").RuleResult);
        Assert.Equal("Waiting", trade.Stages.Single(s => s.Stage == "Risk").RuleResult); Assert.Equal("Waiting", trade.Stages.Single(s => s.Stage == "TakeProfit").RuleResult);
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
