using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

// Synthetic amounts, source statements, fees and IDs throughout. These are not authentic Replay #2 results.
public sealed class NasdaqReportedMonetaryOutcomeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "moneyway-reported-outcome-test-" + Guid.NewGuid().ToString("N"));
    private LocalNasdaqExecutionEvidenceStore Store() => new(root);
    private WebApplicationFactory<Program> App() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        b.UseEnvironment("Development").UseSetting("NasdaqExecutionEvidence:Directory", root));
    private static NasdaqSupervisedExecutionEvidenceDraft Draft() => NasdaqExecutionCaptureTests.Draft();
    private static NasdaqReportedMonetaryOutcome Outcome(NasdaqSupervisedExecutionEvidenceView anchor, string? amount = "+125.00") =>
        new(anchor.Record.RecordId, anchor.Record.Revision, anchor.RevisionHash, "note:synthetic source reports this amount", "synthetic:outcome reporter", "HumanReported")
        { SourceSymbol = "USTEC", Amount = amount, Currency = "USD", AmountMeaning = "SignedResult", Basis = "Net" };
    private NasdaqSupervisedExecutionEvidenceView Revise(NasdaqSupervisedExecutionEvidenceView anchor, NasdaqReportedMonetaryOutcome outcome, Guid? key = null) =>
        Store().Save(key ?? Guid.NewGuid(), anchor.Record.Evidence with { ReportedOutcome = outcome, RevisionReason = "synthetic:register reported monetary evidence" }, anchor.Record.RecordId, anchor.RevisionHash);
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-MoneyWay-Local-Capture", "1"); return await client.SendAsync(request);
    }
    [Theory]
    [InlineData("+125.00")]
    [InlineData("-12.3400")]
    [InlineData("0.00")]
    [InlineData("1.1234567890123456789012345678")]
    [InlineData("125")]
    public void ExactSourceAmountsKeepSignScaleAndDoNotBecomeCalculatedVerifiedOrClassifiedResults(string amount)
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); var revised = Revise(first, Outcome(first, amount));
        Assert.Equal(amount, revised.ReportedOutcome!.Amount); Assert.Equal("USD", revised.ReportedOutcome.Currency);
        Assert.Equal(first.Record.RecordId, revised.ReportedOutcome.ExecutionRecordId); Assert.Equal(1, revised.ReportedOutcome.ExecutionEvidenceRevision);
        Assert.Null(revised.MoneyWayCalculatedPnl); Assert.Null(revised.EconomicOutcome); Assert.Null(revised.StrategyClassification);
        Assert.False(revised.BrokerRealizedOutcomeVerified); Assert.False(revised.NativeExecutionVerified); Assert.False(revised.CompleteSupervisedTrade);
        Assert.False(revised.SessionClosureEstablished); Assert.Equal("NotEvaluated_UnresolvedPolicy", revised.ReentryEligibility);
        Assert.Null(revised.ReportedOutcome.Costs); Assert.Contains("ReportedCostsUnknown_NotZero", revised.MissingEvidence);
        Assert.Equal(first.RevisionHash, Store().Get(first.Record.RecordId, 1).RevisionHash); Assert.Null(Store().Get(first.Record.RecordId, 1).ReportedOutcome);
    }
    [Theory]
    [InlineData("NaN")]
    [InlineData("1e2")]
    [InlineData("1,000.00")]
    [InlineData("1.12345678901234567890123456789")]
    [InlineData("12.1234567890123456789012345678")]
    [InlineData("99999999999999999999999999999")]
    [InlineData("++1")]
    public void InvalidOrRoundedMonetaryRepresentationsNeverPersist(string amount)
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); Assert.Throws<ArgumentException>(() => Revise(first, Outcome(first, amount)));
        Assert.Single(Directory.GetFiles(root, "*.json"));
    }
    [Fact]
    public void UnknownMeaningNarrativeMissingCurrencyAndGrossNetStayExplicitlyUnresolved()
    {
        var first = Store().Save(Guid.NewGuid(), Draft());
        var v = Revise(first, Outcome(first, "125.00") with
        {
            AmountMeaning = "Unspecified",
            Currency = null,
            Basis = "Unspecified",
            Narrative = "synthetic:source says profit / protected stop; not a numerical interpretation",
            Costs = [new("synthetic:fee", "note:synthetic fee statement") { Currency = "USD" }]
        });
        Assert.Contains("ReportedAmountSignMeaningUnresolved", v.MissingEvidence); Assert.Contains("ReportedOutcomeCurrencyUnknown", v.MissingEvidence);
        Assert.Contains("ReportedGrossNetMeaningUnresolved", v.MissingEvidence); Assert.Null(v.StrategyClassification);
        Assert.Contains("ReportedCostCurrencyCompatibilityUnresolved_NoCombination", v.MissingEvidence);
        Assert.DoesNotContain("ReportedCostCurrencyMismatch_NoConversionOrCombination", v.MissingEvidence);
        var next = Revise(v, v.ReportedOutcome! with { Amount = null, Narrative = "synthetic:protected approximately 1:1" });
        Assert.Null(next.ReportedOutcome!.Amount); Assert.Contains("ReportedOutcomeAmountMissing_NoNumericInference", next.MissingEvidence);
        Assert.Null(next.MoneyWayCalculatedPnl); Assert.False(next.CompleteSupervisedTrade);
    }
    [Theory]
    [InlineData("Gross")]
    [InlineData("Net")]
    [InlineData("Unspecified")]
    public void OriginalCostsAreIndependentSourceComponentsWithNoNetConversionOrSummation(string basis)
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); var reported = Outcome(first, "200.00") with
        {
            Basis = basis,
            Provenance = "DocumentReported",
            Costs = [new("synthetic:commission", "note:synthetic commission statement") { Amount = "2.00", Currency = "USD", OriginalSignMeaning = "synthetic:charge as shown" },
                new("synthetic:exchange fee", "note:synthetic fee statement") { Amount = "+3.00", Currency = "EUR" },
                new("synthetic:unknown other fee", "note:synthetic incomplete fee statement")]
        };
        var v = Revise(first, reported); Assert.Equal("200.00", v.ReportedOutcome!.Amount); Assert.Equal(basis, v.ReportedOutcome.Basis);
        Assert.Equal("+3.00", v.ReportedOutcome.Costs![1].Amount); Assert.Equal("EUR", v.ReportedOutcome.Costs[1].Currency);
        Assert.Null(v.ReportedOutcome.Costs[2].Amount); Assert.Contains("ReportedCostAmountMissing_NotZero", v.MissingEvidence);
        Assert.Contains("ReportedCostCurrencyMismatch_NoConversionOrCombination", v.MissingEvidence); Assert.Null(v.MoneyWayCalculatedPnl);
    }
    [Theory]
    [InlineData("symbol")]
    [InlineData("deal")]
    [InlineData("position")]
    public void OriginalExecutionAnchorAndCurrentEvidenceKeepIdentityConflictsVisible(string conflict)
    {
        var original = Draft() with { Entry = Draft().Entry! with { DealId = "synthetic:opening", PositionId = "synthetic:position" } };
        var first = Store().Save(Guid.NewGuid(), original);
        var outcome = Outcome(first) with
        {
            SourceSymbol = conflict == "symbol" ? "US100" : "USTEC",
            DealReference = conflict == "deal" ? "synthetic:wrong-deal" : null,
            PositionReference = conflict == "position" ? "synthetic:wrong-position" : null,
            Association = "DocumentarySupported",
            AssociationSourceReference = "note:synthetic purported link"
        };
        var changed = original with { OriginalSymbol = "US100", Entry = original.Entry! with { Symbol = "US100" }, ReportedOutcome = outcome, RevisionReason = "synthetic:conflicting source correction" };
        var v = Store().Save(Guid.NewGuid(), changed, first.Record.RecordId, first.RevisionHash);
        Assert.Contains(conflict switch { "symbol" => "ReportedOutcomeOriginalExecutionInstrumentConflict_NoMapping", "deal" => "ReportedOutcomeOriginalExecutionDealConflict", _ => "ReportedOutcomeOriginalExecutionPositionConflict" }, v.MissingEvidence);
        Assert.False(v.EntryExitRelationshipVerified); Assert.False(v.BrokerRealizedOutcomeVerified); Assert.False(v.CompleteSupervisedTrade);
    }
    [Fact]
    public void ForeignFutureOrWrongHashExecutionReferencesAreRejectedAndCreationCannotGuessAnAnchor()
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); var other = Store().Save(Guid.NewGuid(), Draft());
        Assert.Throws<ArgumentException>(() => Revise(first, Outcome(other)));
        Assert.Throws<ArgumentException>(() => Revise(first, Outcome(first) with { ExecutionEvidenceRevision = 99 }));
        Assert.Throws<ArgumentException>(() => Revise(first, Outcome(first) with { ExecutionEvidenceRevisionHash = new string('f', 64) }));
        Assert.Throws<ArgumentException>(() => Store().Save(Guid.NewGuid(), Draft() with { ReportedOutcome = Outcome(first) }));
        Assert.Equal(1, Store().Get(first.Record.RecordId).Record.Revision);
    }
    [Theory]
    [InlineData("source")]
    [InlineData("currency")]
    [InlineData("provenance")]
    [InlineData("cost-source")]
    [InlineData("link-source")]
    public void InvalidMonetaryProvenanceIsRejected(string field)
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); var o = Outcome(first); o = field switch
        {
            "source" => o with { SourceReference = "file:///C:/unapproved" },
            "currency" => o with { Currency = "unknown" },
            "provenance" => o with { Provenance = "BrokerVerified" },
            "cost-source" => o with { Costs = [new("synthetic:fee", "")] },
            _ => o with { Association = "HumanReported", AssociationSourceReference = null }
        };
        Assert.Throws<ArgumentException>(() => Revise(first, o));
    }
    [Fact]
    public void MonetaryRevisionsRetryIdempotentlyAndConcurrentCorrectionsCannotOverwrite()
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); var key = Guid.NewGuid(); var o = Outcome(first);
        var second = Revise(first, o, key); Assert.Equal(second.RevisionHash, Revise(first, o, key).RevisionHash);
        Assert.Throws<NasdaqEvidenceConflictException>(() => Revise(first, o with { Amount = "500.00" }));
        Assert.Equal("+125.00", Store().Get(first.Record.RecordId).ReportedOutcome!.Amount);
    }
    [Theory]
    [InlineData("SignedResult")]
    [InlineData("ProfitMagnitude")]
    [InlineData("LossMagnitude")]
    [InlineData("Unspecified")]
    public void UnsignedAmountsDoNotAcquireAProfitOrLossInterpretationFromTheirNumericValue(string meaning)
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); var v = Revise(first, Outcome(first, "100.00") with { AmountMeaning = meaning });
        Assert.Equal("100.00", v.ReportedOutcome!.Amount); Assert.Equal(meaning, v.ReportedOutcome.AmountMeaning);
        Assert.Null(v.StrategyClassification); Assert.Null(v.MoneyWayCalculatedPnl);
        if (meaning == "Unspecified") Assert.Contains("ReportedAmountSignMeaningUnresolved", v.MissingEvidence);
    }
    [Fact]
    public async Task ConcurrentReportedAmountCorrectionsPreserveTheOriginalAndPublishOnlyOneRevision()
    {
        var first = Store().Save(Guid.NewGuid(), Draft());
        Task<bool> Save(string amount) => Task.Run(() =>
        {
            try { Revise(first, Outcome(first, amount)); return true; }
            catch (NasdaqEvidenceConflictException) { return false; }
        });
        var results = await Task.WhenAll(Save("+10.00"), Save("-5.00")); Assert.Single(results, value => value);
        Assert.Equal(2, Store().Get(first.Record.RecordId).Record.Revision); Assert.Equal(first.RevisionHash, Store().Get(first.Record.RecordId, 1).RevisionHash);
        Assert.Null(Store().Get(first.Record.RecordId, 1).ReportedOutcome);
    }
    [Fact]
    public async Task RealApiRevisionRoundTripSurvivesNewApplicationInstanceAndKeepsOriginalRecord()
    {
        NasdaqSupervisedExecutionEvidenceView first, second;
        using (var app = App())
        {
            var client = app.CreateClient(); var created = await Post(client, "/api/nasdaq/execution-evidence", new { submissionId = Guid.NewGuid(), evidence = Draft() });
            first = (await created.Content.ReadFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>())!;
            var body = new { submissionId = Guid.NewGuid(), expectedRevision = first.RevisionHash, evidence = first.Record.Evidence with { ReportedOutcome = Outcome(first, "-5.5000"), RevisionReason = "synthetic:reported result" } };
            var response = await Post(client, $"/api/nasdaq/execution-evidence/{first.Record.RecordId}/revisions", body); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            second = (await response.Content.ReadFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>())!;
        }
        using var restarted = App(); var read = await restarted.CreateClient().GetFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>($"/api/nasdaq/execution-evidence/{first.Record.RecordId}");
        Assert.Equal(second.RevisionHash, read!.RevisionHash); Assert.Equal("-5.5000", read.ReportedOutcome!.Amount); Assert.Null(read.ReportedOutcome.Costs);
        Assert.Equal(first.Record.Evidence.Entry, read.Record.Evidence.Entry); Assert.Null(read.MoneyWayCalculatedPnl);
        var original = await restarted.CreateClient().GetFromJsonAsync<NasdaqSupervisedExecutionEvidenceView>($"/api/nasdaq/execution-evidence/{first.Record.RecordId}?revision=1");
        Assert.Equal(first.RevisionHash, original!.RevisionHash); Assert.Null(original.ReportedOutcome);
    }
    [Theory]
    [InlineData("trusted")]
    [InlineData("brokerRealizedOutcomeVerified")]
    [InlineData("moneyWayCalculatedPnl")]
    [InlineData("historicalAvailableAtUtc")]
    public async Task PublicMonetaryTrustAndCalculationClaimsAreRejected(string field)
    {
        var first = Store().Save(Guid.NewGuid(), Draft()); using var app = App();
        var json = JsonSerializer.SerializeToNode(new { submissionId = Guid.NewGuid(), expectedRevision = first.RevisionHash, evidence = first.Record.Evidence with { ReportedOutcome = Outcome(first) } }, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        json["evidence"]!["reportedOutcome"]![field] = "fabricated";
        var response = await Post(app.CreateClient(), $"/api/nasdaq/execution-evidence/{first.Record.RecordId}/revisions", json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(1, Store().Get(first.Record.RecordId).Record.Revision);
    }
    [LocalMentorSessionFact]
    public async Task RetrospectiveSyntheticOutcomeCannotRewriteAuthenticReplayOrExistingCaptureFiles()
    {
        const string report = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-ed4a1539bef845fd84f5a68dcfc81cbe.json";
        const string hash = "48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db";
        static string FileHash(string p) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
        Assert.Equal(hash, FileHash(report)); using var reader = new ReadNasdaqHistoricalReport();
        var before = await reader.ExecuteAsync(report, hash, frameStep: 7592);
        var first = Store().Save(Guid.NewGuid(), Draft()); var revised = Revise(first, Outcome(first) with { Narrative = "synthetic only; not an authentic Replay 2 result" });
        var after = await reader.ExecuteAsync(report, hash, frameStep: 7592);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)); Assert.Equal(hash, FileHash(report));
        Assert.Equal("USTEC", before.SelectedFrame!.TradeLifecycle!.HistoricalEntry!.Observations.Single().Evidence.SourceSymbol);
        Assert.Equal(29604.00m, before.SelectedFrame.TradeLifecycle.HistoricalEntry.Observations.Single().Evidence.SourceEntryPrice);
        Assert.False(before.SelectedFrame.TradeLifecycle.SupervisedSnapshot!.CanonicalEligibilityRecordedAtEntry);
        var final = await reader.ExecuteAsync(report, hash); Assert.Equal("HumanValidationRequired", final.SelectedFrame!.Verdict);
        Assert.Equal("HumanValidationRequired", final.SelectedFrame.TradeLifecycle!.Stages.Single(s => s.Stage == "StopLoss").RuleResult);
        Assert.Equal("Waiting", final.SelectedFrame.TradeLifecycle.Stages.Single(s => s.Stage == "Risk").RuleResult); Assert.Equal("Waiting", final.SelectedFrame.TradeLifecycle.Stages.Single(s => s.Stage == "TakeProfit").RuleResult);
        Assert.False(final.SelectedFrame.TradeLifecycle.SupervisedSnapshot!.CompleteSupervisedTrade); Assert.Null(final.SelectedFrame.TradeLifecycle.SupervisedSnapshot.EconomicOutcome);
        Assert.False(revised.CompleteSupervisedTrade);
        var oldRoot = Path.Combine(Path.GetTempPath(), "moneyway-capture-smoke-20261117-65e11190");
        var oldFiles = Directory.GetFiles(oldRoot, "*.json").ToDictionary(p => p, FileHash);
        var old = new LocalNasdaqExecutionEvidenceStore(oldRoot).Get(Guid.Parse("7029fee6-f91f-4af0-bd88-bad8a507ffc6"));
        Assert.Equal("7182c3f2cc52f350bde80fef2094ebb3b6541a1ba5aa90e6474e0b024d7ae035", old.RevisionHash); Assert.Null(old.ReportedOutcome);
        foreach (var file in oldFiles) Assert.Equal(file.Value, FileHash(file.Key));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
