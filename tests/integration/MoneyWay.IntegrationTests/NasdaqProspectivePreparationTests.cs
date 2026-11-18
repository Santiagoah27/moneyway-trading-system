using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

// Synthetic operator claims only. No genuine contemporaneous observation or admission is asserted.
public sealed class NasdaqProspectivePreparationTests : IDisposable
{
    private sealed class TestClock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T13:05:00Z"); public override DateTimeOffset GetUtcNow() => Now; }
    private readonly TestClock clock = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-preparation-test-" + Guid.NewGuid().ToString("N"));
    private LocalNasdaqExecutionEvidenceStore Store() => new(root, clock);
    private NasdaqSupervisedSessionState Open(string path = "Standard", string symbol = "US100", DateOnly? day = null) => Store().OpenSession(Guid.NewGuid(),
        new("synthetic:prospective", day ?? new(2026, 11, 18), "synthetic-provider", symbol, path, "synthetic:operator", "note:synthetic supervised session"));
    private static NasdaqProspectivePreparationDraft Partial(string key) => new(key, "synthetic:operator", "note:synthetic partial source observation", "synthetic initial record")
    { OriginalTime = "synthetic source clock around 08:03", OriginalClockDomain = "synthetic native clock, unverified", TimePrecision = "Approximate", OperatorReportedObservationTime = "synthetic operator says earlier today" };
    private static NasdaqProspectivePreparationDraft Complete(string key) => Partial(key) with
    {
        SourceProviderId = "synthetic-provider",
        OriginalSymbol = "US100",
        Direction = "Sell",
        H4Context = "Fakeout",
        HumanValidationStatus = "confirmed",
        H4ReviewReportedComplete = true,
        LiquidityReviewReportedComplete = true,
        ArtifactReference = "note:synthetic source screenshot, bytes not supplied",
        References = [new("documentary-structural-1", "Structure") { Role = "LH", Timeframe = "4h", Price = "100.00000000000000000001", PriceUnit = "NativeInstrumentPrice", SourceReference = "note:synthetic selected structural point", HumanValidationStatus = "confirmed" },
            new("documentary-liquidity-1", "Liquidity") { Role = "LondonHigh", Timeframe = "1h", Price = "101.00", PriceUnit = "NativeInstrumentPrice", SourceReference = "note:synthetic selected liquidity", HumanValidationStatus = "confirmed" }]
    };
    private WebApplicationFactory<Program> App() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development")
        .ConfigureServices(s => s.AddSingleton(new LocalNasdaqExecutionEvidenceStore(root, clock))));
    private static async Task<HttpResponseMessage> Post(HttpClient client, string route, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-MoneyWay-Local-Capture", "1"); return await client.SendAsync(request);
    }
    [Fact]
    public void NewSessionHasNoPreparationAndPartialCaptureRequiresNoInventedNativeIdentityOrPrices()
    {
        var s = Open(); Assert.Empty(Store().Preparations(s.SessionKey));
        var view = Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey));
        Assert.Null(view.Record.Evidence.OriginalObservationId); Assert.Empty(view.Record.Evidence.References);
        Assert.Equal("PartialPreparation", view.DocumentaryStatus); Assert.NotEmpty(view.MissingDocumentaryInputs);
        Assert.False(view.CanonicalAdmissionPermitted); Assert.Equal("DataUnavailable", view.AdmissionResult);
        Assert.Null(view.PreparationRuleResult); Assert.Null(view.StrategyVerdict); Assert.False(Store().Session(s.SessionKey).Terminal);
    }
    [Fact]
    public void FullyDocumentedHumanClaimsReuseExistingInputSeriesButNeverInventAdmissibleCompletion()
    {
        var s = Open(); var view = Store().SavePreparation(Guid.NewGuid(), Complete(s.SessionKey));
        Assert.Empty(view.MissingDocumentaryInputs); Assert.Equal("ReportedPreparationDocumented_NotCanonicalPass", view.DocumentaryStatus);
        var series = NasdaqProspectivePreparationProjection.AdmissibleInputs(view.Record.Identity);
        Assert.IsType<NasdaqPreparationCompletionObservationSeries>(series); Assert.Empty(series.Observations);
        Assert.Equal(0, view.AdmissibleCompletionObservationCount); Assert.Contains("NotEvaluated", view.SelectorResult);
        Assert.False(view.OriginalSourceIndependentlyVerified); Assert.Null(view.VerifiedHistoricalAvailableAtUtc); Assert.Null(view.OriginalArtifactSha256);
        Assert.Null(view.PreparationRuleResult); Assert.False(view.CanonicalAdmissionPermitted); Assert.Null(Store().Session(s.SessionKey).ExecutionConfirmation);
        Assert.Null(view.Record.Evidence.References[0].NativeReferenceId); Assert.Equal("100.00000000000000000001", view.Record.Evidence.References[0].Price);
    }
    [Fact]
    public void ReportedBackdatingNeverChangesActualReceiptAndRevisionsKeepOriginalTimestamp()
    {
        var s = Open(); var first = Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey) with { OperatorReportedObservationTime = "2026-06-29 08:01 UTC-5 (synthetic retrospective assertion)" });
        Assert.Equal(clock.Now, first.Record.ReceivedAtUtc); Assert.Equal(clock.Now, first.Record.RegisteredAtUtc);
        Assert.Equal("InjectedTimeProvider_NotAuthenticated", first.Record.ReceiptClockSource); Assert.False(first.UtcAuthenticated);
        clock.Now = clock.Now.AddMinutes(2);
        var next = Store().SavePreparation(Guid.NewGuid(), first.Record.Evidence with { RevisionReason = "synthetic source-time correction", OriginalTime = "synthetic corrected original display" }, first.Record.RecordId, first.RevisionHash);
        Assert.Equal(clock.Now, next.Record.ReceivedAtUtc); Assert.Equal(first.RevisionHash, Store().Preparation(s.SessionKey, first.Record.RecordId, 1).RevisionHash);
        Assert.Equal(first.Record.ReceivedAtUtc, Store().Preparation(s.SessionKey, first.Record.RecordId, 1).Record.ReceivedAtUtc);
        Assert.Equal(1, first.Record.SubmissionSequence); Assert.Equal(2, next.Record.SubmissionSequence); Assert.Equal(2, next.Record.Revision);
    }
    [Theory]
    [InlineData("USTEC", "synthetic-provider")]
    [InlineData("US100", "different-provider")]
    public void SourceAndReferenceIdentityCannotBeSubstituted(string symbol, string provider)
    {
        var s = Open();
        Assert.Throws<ArgumentException>(() => Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey) with { OriginalSymbol = symbol, SourceProviderId = provider }));
        Assert.Throws<ArgumentException>(() => Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey) with { References = [new("documentary:ref", "Structure") { OriginalSymbol = symbol, SourceProviderId = provider }] }));
        Assert.Empty(Store().Preparations(s.SessionKey));
    }
    [Fact]
    public void MissingNativeReferencePriceOrArtifactRemainInspectableAndZonesKeepOriginalRepresentation()
    {
        var s = Open(); var view = Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey) with { References = [new("documentary:zone", "Structure") { Role = "LH", LowerPrice = "100.0", UpperPrice = "101.0", PriceUnit = "NativeInstrumentPrice" }, new("documentary:unknown", "Liquidity")] });
        Assert.Null(view.Record.Evidence.References[0].Price); Assert.Null(view.Record.Evidence.References[0].NativeReferenceId);
        Assert.Contains("OriginalArtifactReferenceMissing", view.MissingDocumentaryInputs);
        Assert.Contains("OriginalReferencePriceMissing:documentary:unknown", view.MissingDocumentaryInputs);
        Assert.False(view.CanonicalAdmissionPermitted);
    }
    [Theory]
    [InlineData("price-unit")]
    [InlineData("duplicate")]
    [InlineData("unknown-role")]
    [InlineData("timestamp-precision")]
    [InlineData("artifact-path")]
    [InlineData("native-identity")]
    public void InvalidPreparationPayloadsFailWithoutPublishing(string issue)
    {
        var s = Open(); var d = Complete(s.SessionKey);
        d = issue switch
        {
            "price-unit" => d with { References = [d.References[0] with { PriceUnit = null }] },
            "duplicate" => d with { References = [d.References[0], d.References[0]] },
            "unknown-role" => d with { References = [d.References[0] with { Role = "AutomaticallyDetectedSwing" }] },
            "timestamp-precision" => d with { TimePrecision = "UtcAuthenticated" },
            "artifact-path" => d with { ArtifactReference = @"C:\private\original.png" },
            _ => d with { References = [d.References[0] with { LocalReferenceId = "" }] }
        };
        Assert.Throws<ArgumentException>(() => Store().SavePreparation(Guid.NewGuid(), d)); Assert.Empty(Store().Preparations(s.SessionKey));
    }
    [Fact]
    public async Task IdempotencyConcurrentCorrectionsAndRestartPreserveOneRevisionChain()
    {
        var s = Open(); var submission = Guid.NewGuid(); var d = Partial(s.SessionKey); var first = Store().SavePreparation(submission, d);
        Assert.Equal(first.RevisionHash, Store().SavePreparation(submission, d).RevisionHash);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Store().SavePreparation(submission, d with { RevisionReason = "synthetic conflicting reuse" }));
        Task<bool> Correct(string reason) => Task.Run(() => { try { Store().SavePreparation(Guid.NewGuid(), d with { RevisionReason = reason }, first.Record.RecordId, first.RevisionHash); return true; } catch (NasdaqEvidenceConflictException) { return false; } });
        Assert.Single(await Task.WhenAll(Correct("synthetic correction A"), Correct("synthetic correction B")), value => value);
        var restarted = new LocalNasdaqExecutionEvidenceStore(root, clock); Assert.Equal(2, restarted.Preparation(s.SessionKey, first.Record.RecordId).Record.Revision);
        Assert.Equal(first.RevisionHash, restarted.Preparation(s.SessionKey, first.Record.RecordId, 1).RevisionHash);
    }
    [Fact]
    public void RetrospectiveDayAndWf2SessionCannotBeMisrepresentedAsProspectiveStandardAdmission()
    {
        var s = Open(day: new(2026, 6, 29)); var doc = Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey));
        Assert.Contains("DifferentBogotaDate", doc.Record.SubmissionContext); Assert.False(doc.CanonicalAdmissionPermitted);
        var wf2 = Open("WickfillScenario2"); Assert.Throws<ArgumentException>(() => Store().SavePreparation(Guid.NewGuid(), Partial(wf2.SessionKey)));
    }
    [Fact]
    public void SourceClockRegressionAndTamperedPayloadFailClosedWithoutRewritingOriginals()
    {
        var s = Open(); var first = Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey));
        clock.Now = clock.Now.AddMinutes(-1);
        Assert.Throws<IOException>(() => Store().SavePreparation(Guid.NewGuid(), first.Record.Evidence with { RevisionReason = "synthetic regressed clock" }, first.Record.RecordId, first.RevisionHash));
        var file = Directory.GetFiles(root, "*.prep").Single(); File.WriteAllText(file, File.ReadAllText(file).Replace("synthetic initial record", "tampered payload", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => Store().Preparations(s.SessionKey)); Assert.Single(Directory.GetFiles(root, "*.prep"));
    }
    [Fact]
    public void ExistingCanonicalPreparationEvaluatorAcceptsOnlyExistingTypedSyntheticCausalInputs()
    {
        var identity = LiquidityFixture.Session(M5Fixture.At(13, 15));
        Assert.Empty(NasdaqProspectivePreparationProjection.AdmissibleInputs(identity).Observations);
        // Existing fixture supplies the original typed, source-observable completion; never derived from a captured form/receipt.
        var observation = new NasdaqPreparationCompletionObservation(identity.StrategyId, identity.StrategyVersion, identity.ProviderId, identity.Symbol, identity.TradingDay, M5Fixture.At(13, 15), "synthetic:existing canonical fixture causal assertion");
        var series = new NasdaqPreparationCompletionObservationSeries(identity.StrategyId, identity.StrategyVersion, identity.ProviderId, identity.Symbol, [observation]);
        Assert.Single(series.Observations);
        var context = M5Fixture.Context(M5Fixture.At(13, 15), [observation]);
        Assert.Equal(MoneyWay.Domain.Strategies.RuleEvaluationResult.Passed, new MoneyWayNasdaqSessionPreparationCompletionEvaluator().Evaluate(context).Result);
        var empty = M5Fixture.Context(M5Fixture.At(13, 15), []);
        Assert.Equal(MoneyWay.Domain.Strategies.RuleEvaluationResult.Waiting, new MoneyWayNasdaqSessionPreparationCompletionEvaluator().Evaluate(empty).Result);
    }
    [Theory]
    [InlineData("receivedAtUtc")]
    [InlineData("observedAtUtc")]
    [InlineData("trusted")]
    [InlineData("utcAuthenticated")]
    [InlineData("canonicalAdmissionApproved")]
    [InlineData("strategyVersion")]
    public async Task PublicBackdatingTrustAndVersionOverridesAreRejected(string field)
    {
        var s = Open(); using var app = App();
        var body = JsonSerializer.SerializeToNode(new { submissionId = Guid.NewGuid(), evidence = Partial(s.SessionKey) }, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        body["evidence"]![field] = "forged";
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(app.CreateClient(), $"/api/nasdaq/supervised-sessions/{s.SessionKey}/preparation", body)).StatusCode);
        Assert.Empty(Store().Preparations(s.SessionKey));
    }
    [LocalMentorSessionFact]
    public async Task AuthenticJuneAndReplay2FramesCannotBeRewrittenByLaterPreparationCorrections()
    {
        using var reader = new ReadNasdaqHistoricalReport();
        foreach (var source in new[] {
            (Path: @"C:\MoneyWay\session\report.json", Hash: "6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8", Step: 7830L),
            (Path: @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-ed4a1539bef845fd84f5a68dcfc81cbe.json", Hash: "48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db", Step: 7592L) })
        {
            var before = await reader.ExecuteAsync(source.Path, source.Hash, frameStep: source.Step);
            var s = Store().OpenSession(Guid.NewGuid(), new("synthetic:late-preparation-" + before.TradingDay, before.TradingDay, before.ProviderId, before.Symbol, "Standard", "synthetic:regression operator", "note:synthetic later document; no authentic preparation asserted"));
            var first = Store().SavePreparation(Guid.NewGuid(), Partial(s.SessionKey)); clock.Now = clock.Now.AddMinutes(1);
            var next = Store().SavePreparation(Guid.NewGuid(), first.Record.Evidence with { RevisionReason = "synthetic later correction", H4ReviewReportedComplete = true }, first.Record.RecordId, first.RevisionHash);
            Assert.False(next.CanonicalAdmissionPermitted); Assert.Equal(0, next.AdmissibleCompletionObservationCount);
            var attachment = Store().AttachPreparationArtifact(Guid.NewGuid(), s.SessionKey, next.Record.RecordId, next.Record.Revision, next.RevisionHash,
                new("synthetic-later.png", "image/png", s.Identity.ProviderId.Value, s.Identity.Symbol.Value, "synthetic later artifact, not original market evidence", "synthetic later attachment"), Convert.FromBase64String(NasdaqPreparationArtifactTests.SyntheticPng));
            Assert.False(attachment.CanonicalAdmissionPermitted); Assert.Null(attachment.PreparationRuleResult);
            Assert.Equal(JsonSerializer.Serialize(before.SelectedFrame), JsonSerializer.Serialize((await reader.ExecuteAsync(source.Path, source.Hash, frameStep: source.Step)).SelectedFrame));
            Assert.False(Store().Session(s.SessionKey).Terminal);
        }
    }
    [Fact]
    public async Task ApiSaveReadReviseRestartAndTerminalSessionProtectionRemainSeparate()
    {
        var s = Open(); string route = $"/api/nasdaq/supervised-sessions/{s.SessionKey}/preparation"; NasdaqProspectivePreparationView first;
        using (var app = App())
        {
            var http = app.CreateClient(); var response = await Post(http, route, new { submissionId = Guid.NewGuid(), evidence = Partial(s.SessionKey) });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); first = (await response.Content.ReadFromJsonAsync<NasdaqProspectivePreparationView>())!;
            var next = await Post(http, route + $"/{first.Record.RecordId}/revisions", new { submissionId = Guid.NewGuid(), expectedRevision = first.RevisionHash, evidence = first.Record.Evidence with { RevisionReason = "synthetic revised report" } });
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        }
        using var restarted = App(); var client = restarted.CreateClient();
        Assert.Equal(2, (await client.GetFromJsonAsync<NasdaqProspectivePreparationView>(route + $"/{first.Record.RecordId}"))!.Record.Revision);
        Assert.Equal(first.RevisionHash, (await client.GetFromJsonAsync<NasdaqProspectivePreparationView>(route + $"/{first.Record.RecordId}?revision=1"))!.RevisionHash);
        var trade = Store().Save(Guid.NewGuid(), NasdaqExecutionCaptureTests.Draft() with
        {
            SessionId = s.SessionId,
            TradingDay = s.Identity.TradingDay,
            SourceProviderId = "synthetic-provider",
            OriginalSymbol = "US100",
            Scenario = "Standard",
            Entry = new("note:synthetic own entry", "ReportedExecution") { Symbol = "US100" },
            Exit = new("note:synthetic own close", "ReportedClosure") { Symbol = "US100" },
            Association = "HumanReported",
            AssociationSourceReference = "note:synthetic same trade"
        });
        var reference = new NasdaqSupervisedExecutionRecordReference(trade.Record.RecordId, 1, trade.RevisionHash);
        Store().SessionAction(s.SessionKey, new(Guid.NewGuid(), "ConfirmExecution", "synthetic:operator", "note:synthetic owned execution") { Execution = reference, MoneyWayOwnedExecutionConfirmed = true });
        Store().SessionAction(s.SessionKey, new(Guid.NewGuid(), "ConfirmClosure", "synthetic:operator", "note:synthetic close") { Execution = reference, MoneyWayTradeClosedConfirmed = true, ClosureKind = "OtherEvidencedExit", Reason = "synthetic terminal test" });
        var correction = await Post(client, route + $"/{first.Record.RecordId}/revisions", new { submissionId = Guid.NewGuid(), expectedRevision = Store().Preparation(s.SessionKey, first.Record.RecordId).RevisionHash, evidence = Partial(s.SessionKey) with { RevisionReason = "synthetic post-terminal annotation" } });
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode); Assert.True(Store().Session(s.SessionKey).Terminal);
        var terminalDocument = Store().Preparation(s.SessionKey, first.Record.RecordId);
        var attached = Store().AttachPreparationArtifact(Guid.NewGuid(), s.SessionKey, first.Record.RecordId, terminalDocument.Record.Revision, terminalDocument.RevisionHash,
            NasdaqPreparationArtifactTests.Source(), Convert.FromBase64String(NasdaqPreparationArtifactTests.SyntheticPng));
        Assert.Single(attached.Record.Artifacts); Assert.True(Store().Session(s.SessionKey).Terminal); Assert.False(attached.CanonicalAdmissionPermitted);
        var attempt = new NasdaqSupervisedSessionCommand(Guid.NewGuid(), "EntryIntent", "synthetic:operator", "note:synthetic forbidden reentry") { SetupReference = "synthetic:new setup" };
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, $"/api/nasdaq/supervised-sessions/{s.SessionKey}/actions", attempt)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
