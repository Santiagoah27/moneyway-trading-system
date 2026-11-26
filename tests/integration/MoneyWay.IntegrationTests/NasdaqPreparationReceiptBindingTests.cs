using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqPreparationReceiptBindingTests : IDisposable
{
    private const string Provider = "oanda-global-markets-mt5-demo";
    private readonly Mt5ReceiptSyntheticFixture source = new();
    private readonly Mt5ReceiptSyntheticFixture reports = new();
    private readonly Mt5ReceiptSyntheticFixture storage = new();
    private static readonly DateTimeOffset CutLabel = new(2026, 11, 25, 14, 59, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 11, 25);
    private readonly string path;
    private readonly string hash;

    public NasdaqPreparationReceiptBindingTests()
    {
        source.Add();
        var journal = Path.Combine(reports.DirectoryPath, "observer.ndjson");
        File.WriteAllText(journal, JsonSerializer.Serialize(new { sequence = 1,
            sha256 = Mt5ReceiptSyntheticFixture.Hash(source.RecordPath(1)), observedAtHostUtcCandidate = CutLabel }) + "\n");
        path = Report(journal, "cut.json");
        hash = Mt5ReceiptSyntheticFixture.Hash(path);
    }

    private string Report(string journal, string file)
    {
        var output = Path.Combine(reports.DirectoryPath, file);
        Assert.Equal(0, new Mt5CausalAvailabilityCommand().Execute([
            "mt5-causal-availability", "--source", source.DirectoryPath, "--view", "receipt-cut",
            "--through-sequence", "1", "--through-sha256", Mt5ReceiptSyntheticFixture.Hash(source.RecordPath(1)),
            "--observer-journal", journal, "--observer-journal-sha256", Mt5ReceiptSyntheticFixture.Hash(journal), "--report", output
        ], TextWriter.Null, TextWriter.Null));
        return output;
    }

    private WebApplicationFactory<Program> App(string sessionKey, string env = "Development") => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.UseEnvironment(env).UseSetting("NasdaqExecutionEvidence:Directory", storage.DirectoryPath)
            .UseSetting("NasdaqReceiptCuts:DevelopmentDirectory", reports.DirectoryPath)
            .UseSetting("NasdaqReceiptCuts:Reports:local-cut:File", "cut.json")
            .UseSetting("NasdaqReceiptCuts:Reports:local-cut:Sequence", "1")
            .UseSetting("NasdaqReceiptCuts:Reports:local-cut:Sha256", hash)
            .UseSetting("NasdaqReceiptCuts:Reports:local-cut:StandardSessionKey", sessionKey));

    private (NasdaqSupervisedSessionState Session, NasdaqProspectivePreparationView Preparation) Setup(
        string provider = Provider, string symbol = "US100", DateOnly? day = null, bool withEvidence = true)
    {
        var store = new LocalNasdaqExecutionEvidenceStore(storage.DirectoryPath, new FixedClock(CutLabel.AddHours(1)));
        var session = store.OpenSession(Guid.NewGuid(),
            new("synthetic:binding-only", day ?? Day, provider, symbol, "Standard", "synthetic:operator", "note:synthetic documentary case"));
        string native;
        using (var probe = JsonDocument.Parse(File.ReadAllBytes(source.ProbePath(1))))
            native = probe.RootElement.GetProperty("timeframes").EnumerateArray().Single(f => f.GetProperty("timeframe").GetString() == "H4")
                .GetProperty("bars")[1].GetProperty("open_time_native").GetProperty("display").GetString()!;
        var draft = new NasdaqProspectivePreparationDraft(session.SessionKey, "fixture", "note:synthetic preparation", "partial")
        {
            OperatorReportedObservationTime = "08:05 operator assertion", SourceProviderId = provider, OriginalSymbol = symbol,
            References = withEvidence ? [new("h4", "Structure") { SourceProviderId = provider, OriginalSymbol = symbol,
                Timeframe = "4h", SourceReference = "note:fixture native chart", OriginalTime = native, OriginalClockDomain = "MT5Native",
                TimePrecision = "ExactNativeRecord" }, new("unknown", "Liquidity")] : []
        };
        var first = store.SavePreparation(Guid.NewGuid(), draft);
        var view = withEvidence ? store.AttachPreparationArtifact(Guid.NewGuid(), session.SessionKey, first.Record.RecordId, first.Record.Revision,
            first.RevisionHash, new("fixture.png", "image/png", provider, symbol, "synthetic image", "attach documentary bytes"),
            Convert.FromBase64String(NasdaqPreparationArtifactTests.SyntheticPng)) : first;
        return (session, view);
    }

    private static string Route(NasdaqProspectivePreparationView p) =>
        $"/api/nasdaq/supervised-sessions/{p.Record.Evidence.SessionKey}/preparation/{p.Record.RecordId}/revisions/{p.Record.Revision}/receipt-cuts";
    private async Task<HttpResponseMessage> Bind(HttpClient client, NasdaqProspectivePreparationView p, string? revisionHash = null, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route(p))
        {
            Content = JsonContent.Create(body ?? new { preparationRevisionSha256 = revisionHash ?? p.RevisionHash,
                receiptCutId = "local-cut", receiptCutReportSha256 = hash })
        };
        request.Headers.Add("X-MoneyWay-Local-Capture", "1");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task ExactRevisionAndArtifactRemainBoundAfterLaterRevisionReceiptsAndRestart()
    {
        var (session, p) = Setup();
        using var app = App(session.SessionKey); using var http = app.CreateClient();
        using var response = await Bind(http, p);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var original = await response.Content.ReadFromJsonAsync<NasdaqPreparationReceiptBindingView>();
        var binding = original!.Binding;
        Assert.Equal(p.RevisionHash, binding.PreparationRevisionSha256);
        Assert.Equal(hash, binding.ReceiptCutReportSha256);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(source.RecordPath(1)), binding.CutReceiptSha256);
        Assert.Equal(NasdaqPreparationArtifactTests.SyntheticHash, Assert.Single(original.Artifacts).Sha256);
        Assert.Equal("ExactNativeCoordinateInCut", binding.References[0].Status);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(source.ProbePath(1)), binding.References[0].FirstUsableHistoricalReceipt!.ProbeSha256);
        Assert.Equal("Partial_UnresolvedNativeReferences", binding.MatchStatus);
        Assert.Equal("PreparationRegistrationAfterCutLabel", binding.WallClockOrdering);
        Assert.Equal("Unresolved", binding.PreparationAvailabilityAtCut);
        Assert.Equal("NotEvaluated", binding.StandardEvaluation);
        Assert.False(binding.CanonicalReady || binding.CanonicalAdmissionPermitted || binding.OperationalSignal);
        Assert.DoesNotContain("strategyVerdict", await response.Content.ReadAsStringAsync());
        using var retry = await Bind(http, p);
        Assert.Equal(original.AssociationSha256, (await retry.Content.ReadFromJsonAsync<NasdaqPreparationReceiptBindingView>())!.AssociationSha256);

        source.Add(2, 1);
        Assert.Equal(hash, Mt5ReceiptSyntheticFixture.Hash(Report(Path.Combine(reports.DirectoryPath, "observer.ndjson"), "cut-repeat.json")));
        var store = new LocalNasdaqExecutionEvidenceStore(storage.DirectoryPath);
        var correction = store.SavePreparation(Guid.NewGuid(), p.Record.Evidence with { RevisionReason = "new correction" }, p.Record.RecordId, p.RevisionHash);
        using var restarted = App(session.SessionKey); using var restartHttp = restarted.CreateClient();
        using var previous = JsonDocument.Parse(await restartHttp.GetStringAsync(Route(p)));
        using var future = JsonDocument.Parse(await restartHttp.GetStringAsync(Route(correction)));
        Assert.Equal(original.AssociationSha256, Assert.Single(previous.RootElement.GetProperty("bindings").EnumerateArray()).GetProperty("associationSha256").GetString());
        Assert.Empty(future.RootElement.GetProperty("bindings").EnumerateArray());
        Assert.False(store.Preparation(session.SessionKey, p.Record.RecordId, p.Record.Revision).Record.Evidence.H4ReviewReportedComplete);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("day")]
    [InlineData("registry-session")]
    [InlineData("revision")]
    [InlineData("report")]
    [InlineData("artifact")]
    [InlineData("override")]
    public async Task ConflictsAndCallerOverridesPublishNoBinding(string fault)
    {
        var (session, preparation) = Setup(fault == "provider" ? "synthetic-other" : Provider,
            fault == "symbol" ? "USTEC" : "US100", fault == "day" ? Day.AddDays(1) : Day);
        using var app = App(fault == "registry-session" ? new string('a', 64) : session.SessionKey);
        using var http = app.CreateClient();
        if (fault == "report") File.AppendAllText(path, " ");
        if (fault == "artifact")
            File.AppendAllText(Directory.GetFiles(storage.DirectoryPath, "*.png").Single(), " ");
        using var response = await Bind(http, preparation, fault == "revision" ? new string('0', 64) : null,
            fault == "override" ? new { preparationRevisionSha256 = preparation.RevisionHash, receiptCutId = "local-cut",
                receiptCutReportSha256 = hash, canonicalAdmissionPermitted = true } : null);
        Assert.Equal(fault is "report" or "artifact" ? HttpStatusCode.UnprocessableEntity
            : fault == "override" ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(Directory.GetFiles(storage.DirectoryPath, "*.binding"));
    }

    [Fact]
    public async Task IncompleteRevisionWithoutOriginalTimesStaysDocumentaryAndLocalOnly()
    {
        var (session, p) = Setup(withEvidence: false);
        using var app = App(session.SessionKey); using var http = app.CreateClient();
        using var response = await Bind(http, p);
        var view = (await response.Content.ReadFromJsonAsync<NasdaqPreparationReceiptBindingView>())!;
        Assert.Empty(view.Artifacts);
        Assert.Null(view.Binding.PreparationOriginalTime);
        Assert.Contains("OriginalObservationTimingUnreported", view.Binding.Diagnostics);
        Assert.Contains("HumanH4ContextSelectionMissingOrUnresolved", view.Binding.Diagnostics);
        Assert.Equal("NotEvaluated", view.Binding.StandardEvaluation);
        using var external = new HttpRequestMessage(HttpMethod.Get, Route(p));
        external.Headers.Host = "outside.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(external)).StatusCode);
        using var production = App(session.SessionKey, "Production");
        Assert.Equal(HttpStatusCode.NotFound, (await production.CreateClient().GetAsync(Route(p))).StatusCode);
    }

    private sealed class FixedClock(DateTimeOffset time) : TimeProvider { public override DateTimeOffset GetUtcNow() => time; }

    public void Dispose()
    {
        source.Dispose(); reports.Dispose(); storage.Dispose();
    }
}
