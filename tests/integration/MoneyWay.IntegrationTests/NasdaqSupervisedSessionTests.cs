using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

// Explicitly synthetic operational attestations. No actual broker execution or strategy authorization is established.
public sealed class NasdaqSupervisedSessionTests : IDisposable
{
    private sealed class TestClock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T14:00:00Z"); public override DateTimeOffset GetUtcNow() => Now; }
    private readonly TestClock clock = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-session-test-" + Guid.NewGuid().ToString("N"));
    private LocalNasdaqExecutionEvidenceStore Store() => new(root, clock);
    private static NasdaqSupervisedSessionOpen Input(string path = "Standard", DateOnly? date = null) => new("synthetic:own-session", date ?? new(2026, 11, 18), "synthetic-provider", "US100", path, "synthetic:operator", "note:synthetic local MoneyWay session");
    private NasdaqSupervisedSessionState Open(string path = "Standard", DateOnly? date = null) => Store().OpenSession(Guid.NewGuid(), Input(path, date));
    private static NasdaqSupervisedSessionCommand Command(string action, NasdaqSupervisedExecutionEvidenceView? evidence = null, string kind = "OtherEvidencedExit") =>
        new(Guid.NewGuid(), action, "synthetic:operator", "note:synthetic source confirms own execution/closure, not a native verification")
        {
            SetupReference = action is "ActivateSetup" or "EntryIntent" or "InvalidateSetup" ? "synthetic:setup" : null,
            Execution = evidence is null ? null : new(evidence.Record.RecordId, evidence.Record.Revision, evidence.RevisionHash),
            MoneyWayOwnedExecutionConfirmed = action == "ConfirmExecution",
            MoneyWayTradeClosedConfirmed = action == "ConfirmClosure",
            ClosureKind = action == "ConfirmClosure" ? kind : null,
            Reason = action == "ConfirmClosure" ? "synthetic:evidenced close, no return calculation" : null
        };
    private NasdaqSupervisedExecutionEvidenceView Evidence(string path = "Standard", bool closed = true, DateOnly? date = null)
    {
        var input = Input(path, date);
        var draft = NasdaqExecutionCaptureTests.Draft() with
        {
            SessionId = input.SessionId,
            TradingDay = input.TradingDay,
            SourceProviderId = input.ProviderId,
            OriginalSymbol = input.Symbol,
            Scenario = path,
            SourcePlatform = "synthetic:own-supervised-demo",
            Entry = new("note:synthetic own entry source", "ReportedExecution")
            { Symbol = "US100", OriginalTime = "synthetic:around 09:00 Bogota", TimePrecision = "Approximate", PositionId = "synthetic:position" },
            Exit = closed ? new("note:synthetic actual closing event source", "ReportedClosure")
            { Symbol = "US100", OriginalTime = "synthetic:around 09:05 Bogota", TimePrecision = "Approximate", PositionId = "synthetic:position" } : null,
            Association = closed ? "HumanReported" : "None",
            AssociationSourceReference = closed ? "note:synthetic explicitly same operation" : null
        };
        return Store().Save(Guid.NewGuid(), draft);
    }
    [Fact]
    public void StandardSetupAndInvalidationWithoutTradeDoNotTerminateOrRequireWf2Facts()
    {
        var s = Open(); Assert.False(s.Terminal); Assert.Equal("Standard", s.Path); Assert.True(s.LocalEntryIntentPermitted);
        var active = Store().SessionAction(s.SessionKey, Command("ActivateSetup")); Assert.Equal("SetupRegistered_NoTrade", active.Status);
        var invalid = Store().SessionAction(s.SessionKey, Command("InvalidateSetup")); Assert.False(invalid.Terminal); Assert.Null(invalid.ActiveSetup);
        Assert.Equal("NotEstablishedByOperationalWorkflow", invalid.StrategyAuthorization); Assert.False(invalid.BrokerExecutionVerified);
    }
    [Theory]
    [InlineData("TakeProfit")]
    [InlineData("StopLoss")]
    [InlineData("BreakEven")]
    [InlineData("ProtectedOneToOne")]
    [InlineData("OtherEvidencedExit")]
    public void EveryEvidencedOwnClosureTerminatesAndBlocksEveryFurtherOperationalEntryOrSetup(string kind)
    {
        var s = Open(); var evidence = Evidence(); Store().SessionAction(s.SessionKey, Command("ConfirmExecution", evidence));
        var close = Command("ConfirmClosure", evidence, kind); var terminal = Store().SessionAction(s.SessionKey, close);
        Assert.True(terminal.Terminal); Assert.False(terminal.LocalEntryIntentPermitted); Assert.Equal("Terminal", terminal.Status);
        Assert.False(terminal.BrokerExecutionVerified); Assert.False(terminal.CompleteSupervisedTrade); Assert.Null(terminal.CalculatedPnl);
        Assert.Equal("Approximate", Store().Get(evidence.Record.RecordId).Record.Evidence.Exit!.TimePrecision);
        Assert.Equal(clock.Now, terminal.TerminalRegisteredAtUtc); Assert.Contains("NotAnExactHistorical", terminal.TerminalTiming);
        foreach (var action in new[] { "EntryIntent", "ActivateSetup", "ConfirmExecution" })
            Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SessionAction(s.SessionKey, Command(action, action == "ConfirmExecution" ? evidence : null)));
        Assert.Equal(terminal.RevisionHash, Store().SessionAction(s.SessionKey, close).RevisionHash);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmClosure", evidence)));
        var original = Store().Get(evidence.Record.RecordId);
        var corrected = Store().Save(Guid.NewGuid(), original.Record.Evidence with { RevisionReason = "synthetic:documentary correction while terminal" }, original.Record.RecordId, original.RevisionHash);
        Assert.Equal(2, corrected.Record.Revision); Assert.True(Store().Session(s.SessionKey).Terminal);
        Assert.Equal(original.RevisionHash, Store().Get(original.Record.RecordId, 1).RevisionHash);
    }
    [Theory]
    [InlineData("mentor-entry")]
    [InlineData("mentor-exit")]
    [InlineData("reported-money")]
    [InlineData("entry-only")]
    public void DocumentaryRecordsNeverAutomaticallyConfirmMoneyWayOwnershipOrTerminate(string type)
    {
        var s = Open(); var evidence = Evidence(closed: type != "entry-only");
        if (type == "reported-money")
        {
            var o = new NasdaqReportedMonetaryOutcome(evidence.Record.RecordId, evidence.Record.Revision, evidence.RevisionHash, "note:synthetic monetary comment", "synthetic:reporter", "HumanReported") { Amount = "+10.00", Currency = "USD" };
            _ = Store().Save(Guid.NewGuid(), evidence.Record.Evidence with { ReportedOutcome = o, RevisionReason = "synthetic:reported money only" }, evidence.Record.RecordId, evidence.RevisionHash);
        }
        Assert.False(Store().Session(s.SessionKey).Terminal); Assert.Null(Store().Session(s.SessionKey).ExecutionConfirmation);
        Assert.Throws<ArgumentException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmClosure", evidence)));
        Assert.Throws<ArgumentException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmExecution", evidence) with { MoneyWayOwnedExecutionConfirmed = false }));
    }
    [Theory]
    [InlineData("missing-exit")]
    [InlineData("protection-only")]
    [InlineData("instrument")]
    [InlineData("position")]
    [InlineData("association")]
    public void UnsupportedClosureEvidenceCannotBecomeTerminal(string problem)
    {
        var s = Open(); var entry = Evidence(closed: false); Store().SessionAction(s.SessionKey, Command("ConfirmExecution", entry));
        var d = entry.Record.Evidence;
        var exit = new NasdaqSupervisedExecutionEvent("note:synthetic closing assertion", problem == "protection-only" ? "ReportedStopProtection" : "ReportedClosure")
        { Symbol = problem == "instrument" ? "USTEC" : "US100", PositionId = problem == "position" ? "synthetic:other" : "synthetic:position", TimePrecision = "Approximate", OriginalTime = "around 09:05" };
        var close = Store().Save(Guid.NewGuid(), d with
        {
            Exit = problem == "missing-exit" ? null : exit,
            Association = problem is "association" or "missing-exit" ? "None" : "HumanReported",
            AssociationSourceReference = problem is "association" or "missing-exit" ? null : "note:synthetic link",
            RevisionReason = "synthetic:incomplete close evidence"
        }, entry.Record.RecordId, entry.RevisionHash);
        Assert.Throws<ArgumentException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmClosure", close)));
        Assert.False(Store().Session(s.SessionKey).Terminal); Assert.NotNull(Store().Session(s.SessionKey).ExecutionConfirmation);
    }
    [Fact]
    public void OwnEntryOnlyIsRegisteredButNoSecondTradeCanBeRecordedBeforeClosure()
    {
        var s = Open(); var evidence = Evidence(closed: false); var own = Store().SessionAction(s.SessionKey, Command("ConfirmExecution", evidence));
        Assert.Equal("TradeRegistered_AwaitingClosure", own.Status); Assert.False(own.Terminal);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SessionAction(s.SessionKey, Command("EntryIntent")));
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmExecution", Evidence())));
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("WickfillScenario2")]
    public void WindowBoundaryDoesNotCloseRecordedTradeOrReopenTerminalSession(string path)
    {
        var session = Open(path);
        var evidence = Evidence(path);
        Store().SessionAction(session.SessionKey, Command("ConfirmExecution", evidence));
        clock.Now = new DateTimeOffset(2026, 11, 18, 16, 30, 0, TimeSpan.Zero);
        var afterWindow = Store().Session(session.SessionKey);
        Assert.Equal("TradeRegistered_AwaitingClosure", afterWindow.Status);
        Assert.False(afterWindow.Terminal);
        Assert.False(afterWindow.LocalEntryIntentPermitted);
        Assert.Null(afterWindow.ClosureConfirmation);
        Assert.True(Store().SessionAction(session.SessionKey, Command("ConfirmClosure", evidence)).Terminal);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SessionAction(session.SessionKey, Command("EntryIntent")));
    }
    [Fact]
    public void SameScopeAliasesOrOtherBranchCannotBypassLockAndNextDateDoesNotInheritIt()
    {
        var s = Open(); var evidence = Evidence(); Store().SessionAction(s.SessionKey, Command("ConfirmExecution", evidence)); Store().SessionAction(s.SessionKey, Command("ConfirmClosure", evidence));
        Assert.True(Store().OpenSession(Guid.NewGuid(), Input()).Terminal);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().OpenSession(Guid.NewGuid(), Input() with { SessionId = "synthetic:alias" }));
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().OpenSession(Guid.NewGuid(), Input("WickfillScenario2")));
        clock.Now = clock.Now.AddDays(1); var next = Open(date: new(2026, 11, 19)); Assert.False(next.Terminal); Assert.True(next.LocalEntryIntentPermitted);
        Assert.False(Store().SessionAction(next.SessionKey, Command("ActivateSetup")).Terminal); Assert.True(Store().Session(s.SessionKey).Terminal);
        Assert.Throws<ArgumentException>(() => Store().SessionAction(next.SessionKey, Command("ConfirmExecution", evidence)));
    }
    [Theory]
    [InlineData("2026-03-09T13:29:59Z", false)]
    [InlineData("2026-03-09T13:30:00Z", true)]
    [InlineData("2026-03-09T15:59:59Z", true)]
    [InlineData("2026-03-09T16:00:00Z", false)]
    [InlineData("2026-11-18T13:30:00Z", true)]
    public void GuardReusesBogotaWindowWithNoUsDstOrNewCutoff(string time, bool expected)
    {
        clock.Now = DateTimeOffset.Parse(time); var s = Open(date: NasdaqSupervisedSessionPolicy.TradingDay(clock.Now));
        Assert.Equal(expected, s.LocalEntryIntentPermitted);
        if (!expected) Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SessionAction(s.SessionKey, Command("EntryIntent")));
        Assert.Equal(new DateOnly(2026, 11, 17), NasdaqSupervisedSessionPolicy.TradingDay(DateTimeOffset.Parse("2026-11-18T04:59:59Z")));
        Assert.Equal(new DateOnly(2026, 11, 18), NasdaqSupervisedSessionPolicy.TradingDay(DateTimeOffset.Parse("2026-11-18T05:00:00Z")));
    }
    [Fact]
    public void Wf2UsesOnlyCommonOperationalOwnershipPolicyWithoutStandardFacts()
    {
        var s = Open("WickfillScenario2"); var e = Evidence("WickfillScenario2"); Store().SessionAction(s.SessionKey, Command("ConfirmExecution", e));
        Assert.True(Store().SessionAction(s.SessionKey, Command("ConfirmClosure", e)).Terminal);
    }
    [Fact]
    public async Task ConcurrentClosuresPreserveOneTerminalDecisionAndRestartReadsIt()
    {
        var s = Open(); var e = Evidence(); Store().SessionAction(s.SessionKey, Command("ConfirmExecution", e));
        Task<bool> Close() => Task.Run(() => { try { Store().SessionAction(s.SessionKey, Command("ConfirmClosure", e)); return true; } catch (NasdaqEvidenceConflictException) { return false; } });
        var results = await Task.WhenAll(Close(), Close()); Assert.Single(results, value => value);
        var read = new LocalNasdaqExecutionEvidenceStore(root, clock).Session(s.SessionKey); Assert.True(read.Terminal); Assert.Equal(3, read.Revision);
        Assert.Equal(3, Directory.GetFiles(root, "*.session").Length);
    }
    [Fact]
    public void TamperedTerminalPayloadFailsClosedAndNeverReopensSession()
    {
        var s = Open(); var e = Evidence(); Store().SessionAction(s.SessionKey, Command("ConfirmExecution", e));
        Store().SessionAction(s.SessionKey, Command("ConfirmClosure", e));
        var file = Directory.GetFiles(root, "*.003.session").Single();
        File.WriteAllText(file, File.ReadAllText(file).Replace("OtherEvidencedExit", "TakeProfit", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => Store().Session(s.SessionKey));
        Assert.Throws<InvalidDataException>(() => Store().OpenSession(Guid.NewGuid(), Input()));
        Assert.Equal(3, Directory.GetFiles(root, "*.session").Length);
    }
    [Fact]
    public void ExitOnlyMentorDocumentCannotEstablishOwnExecutionOrClosure()
    {
        var s = Open(); var complete = Evidence();
        var exitOnly = Store().Save(Guid.NewGuid(), complete.Record.Evidence with { Entry = null, Association = "None", AssociationSourceReference = null });
        Assert.Throws<ArgumentException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmExecution", exitOnly)));
        Assert.Throws<ArgumentException>(() => Store().SessionAction(s.SessionKey, Command("ConfirmClosure", exitOnly)));
        Assert.False(Store().Session(s.SessionKey).Terminal); Assert.Single(Store().Session(s.SessionKey).Events);
    }
    [LocalMentorSessionFact]
    public async Task LaterOwnOperationalClosureCannotRewriteAuthenticEarlierCanonicalObservation()
    {
        const string report = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-ed4a1539bef845fd84f5a68dcfc81cbe.json";
        const string hash = "48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db";
        using var reader = new ReadNasdaqHistoricalReport();
        var earlier = await reader.ExecuteAsync(report, hash, frameStep: 7592);
        var final = await reader.ExecuteAsync(report, hash);
        var s = Open(); var evidence = Evidence();
        Store().SessionAction(s.SessionKey, Command("ConfirmExecution", evidence));
        Assert.True(Store().SessionAction(s.SessionKey, Command("ConfirmClosure", evidence)).Terminal);
        Assert.Equal(JsonSerializer.Serialize(earlier), JsonSerializer.Serialize(await reader.ExecuteAsync(report, hash, frameStep: 7592)));
        Assert.Equal(JsonSerializer.Serialize(final), JsonSerializer.Serialize(await reader.ExecuteAsync(report, hash)));
        Assert.Equal(hash, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(report))));
        Assert.False(final.SelectedFrame!.TradeLifecycle!.SupervisedSnapshot!.CompleteSupervisedTrade);
        Assert.Equal("HumanValidationRequired", final.SelectedFrame.Verdict);
    }
    [Fact]
    public async Task ApiEnforcesTerminalityAcrossApplicationRestartAndKeepsDocumentaryApiUsable()
    {
        WebApplicationFactory<Program> App() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development")
            .ConfigureServices(s => s.AddSingleton(new LocalNasdaqExecutionEvidenceStore(root, clock))));
        async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-MoneyWay-Local-Capture", "1"); return await client.SendAsync(request);
        }
        var evidence = Evidence(); string key;
        using (var app = App())
        {
            var client = app.CreateClient(); var opened = await Post(client, "/api/nasdaq/supervised-sessions", new { submissionId = Guid.NewGuid(), session = Input() });
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode); key = (await opened.Content.ReadFromJsonAsync<NasdaqSupervisedSessionState>())!.SessionKey;
            Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/nasdaq/supervised-sessions/{key}/actions", Command("ConfirmExecution", evidence))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/nasdaq/supervised-sessions/{key}/actions", Command("ConfirmClosure", evidence))).StatusCode);
        }
        using var restarted = App(); var http = restarted.CreateClient();
        Assert.True((await http.GetFromJsonAsync<NasdaqSupervisedSessionState>($"/api/nasdaq/supervised-sessions/{key}"))!.Terminal);
        foreach (var action in new[] { "ActivateSetup", "EntryIntent", "ConfirmExecution" })
        {
            var response = await Post(http, $"/api/nasdaq/supervised-sessions/{key}/actions", Command(action, action == "ConfirmExecution" ? evidence : null));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Contains("NoReentry", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync($"/api/nasdaq/execution-evidence/{evidence.Record.RecordId}")).StatusCode);
        var next = await Post(http, "/api/nasdaq/supervised-sessions", new { submissionId = Guid.NewGuid(), session = Input(date: new(2026, 11, 19)) });
        Assert.False((await next.Content.ReadFromJsonAsync<NasdaqSupervisedSessionState>())!.Terminal);
        var fabricated = JsonSerializer.SerializeToNode(Command("EntryIntent"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!; fabricated["status"] = "Open";
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(http, $"/api/nasdaq/supervised-sessions/{key}/actions", fabricated)).StatusCode);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
