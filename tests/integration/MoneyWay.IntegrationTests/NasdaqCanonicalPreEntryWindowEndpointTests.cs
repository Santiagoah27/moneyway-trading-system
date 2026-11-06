using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.IntegrationTests;

// Synthetic transport fixture matching native envelope shape, not an authentic historical source.
public sealed class NasdaqCanonicalPreEntryWindowEndpointTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"moneyway-window-{Guid.NewGuid():N}.json");
    private const string Endpoint = "/api/nasdaq/historical-demo";
    private static JsonObject Id(string value) => new() { ["Value"] = value };
    private static string At(int minute) => $"2026-09-08T13:{minute:00}:00Z";
    private static readonly string[] RuleIds = ["NQ-WF2-001", "NQ-WF2-002", "NQ-SL-001", "NQ-RISK-001"];

    private static JsonObject Source()
    {
        var frames = new JsonArray();
        for (var minute = 31; minute <= 36; minute++)
        {
            var step = 7591 + minute - 31;
            var evaluations = new JsonArray();
            for (var i = 0; i < RuleIds.Length; i++)
            {
                var result = i == 0 && minute >= 34 || i == 1 && minute >= 35 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting;
                var ruleId = minute == 31 ? new JsonObject { ["$id"] = "rule" + i, ["Value"] = RuleIds[i] }
                    : new JsonObject { ["$ref"] = "rule" + i };
                evaluations.Add(new JsonObject { ["RuleId"] = ruleId, ["DefinitionStatus"] = (int)RuleDefinitionStatus.Confirmed,
                    ["Result"] = (int)result, ["Sequence"] = 90 + i * 10, ["IsRequired"] = true,
                    ["Reason"] = $"synthetic rule {step}", ["EvaluatedAtUtc"] = At(minute),
                    ["EvidenceReference"] = @"C:\private\source-reference-not-an-EvidenceId" });
            }
            var blocking = minute < 34 ? 0 : minute == 34 ? 1 : 2;
            frames.Add(new JsonObject
            {
                ["CanonicalDiagnostic"] = new JsonObject { ["Step"] = step, ["AsOfUtc"] = At(minute),
                    ["Verdict"] = (int)StrategyVerdict.Wait, ["Reason"] = $"synthetic canonical {step}",
                    ["BlockingRuleId"] = Id(RuleIds[blocking]), ["BlockingResult"] = (int)RuleEvaluationResult.Waiting,
                    ["HasCompleteRequiredCoverage"] = true, ["MissingRequiredRuleIds"] = new JsonArray() },
                ["StrategyObservation"] = new JsonObject { ["StrategyId"] = new JsonObject { ["$ref"] = "strategy" },
                    ["StrategyVersion"] = Id("nasdaq-0.1.0-draft"), ["ProviderId"] = Id("synthetic-provider"), ["Symbol"] = Id("US100"),
                    ["Step"] = step, ["AsOfUtc"] = At(minute), ["EvaluationCount"] = 4, ["Evaluations"] = evaluations },
                ["SnapshotDiagnostic"] = "CanonicalEligibilityUnavailable", ["Snapshot"] = null, ["FactualEvaluation"] = null
            });
        }
        return new JsonObject
        {
            ["Schema"] = "moneyway.canonical-preentry-window", ["SchemaVersion"] = 1,
            ["Session"] = new JsonObject { ["StrategyId"] = new JsonObject { ["$id"] = "strategy", ["Value"] = "moneyway-nasdaq" },
                ["StrategyVersion"] = Id("nasdaq-0.1.0-draft"), ["ProviderId"] = Id("synthetic-provider"), ["Symbol"] = Id("US100"),
                ["TradingDay"] = "2026-09-08" },
            ["EvaluatedPrefixStartUtc"] = "2026-08-31T22:01:00Z", ["EvaluatedPrefixEndUtc"] = At(36), ["EvaluatedPrefixFrameCount"] = 7596,
            ["DiagnosticWindowStartUtc"] = At(31), ["DiagnosticWindowEndUtc"] = At(36), ["CanonicalRuntimeCommit"] = "synthetic:runtime",
            ["BindingDiagnostics"] = new JsonArray(
                new JsonObject { ["RecordId"] = "early-source", ["Code"] = "Bound", ["AsOfUtc"] = At(31) },
                new JsonObject { ["RecordId"] = "later-reversal", ["Code"] = "Bound", ["AsOfUtc"] = At(34) }),
            ["Frames"] = frames
        };
    }
    public NasdaqCanonicalPreEntryWindowEndpointTests() => File.WriteAllText(path, Source().ToJsonString());
    private WebApplicationFactory<Program> Factory(string? expectedHash = null) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.UseEnvironment("Development").UseSetting("NasdaqHistoricalDemo:ReportPath", path)
            .UseSetting("NasdaqHistoricalDemo:ExpectedSha256", expectedHash ?? ""));

    [Theory]
    [InlineData(7591, "Waiting", "Waiting", "NQ-WF2-001")]
    [InlineData(7594, "Passed", "Waiting", "NQ-WF2-002")]
    [InlineData(7595, "Passed", "Passed", "NQ-SL-001")]
    [InlineData(7596, "Passed", "Passed", "NQ-SL-001")]
    public async Task NativeWindowPreservesStepTransitionsScopeAndSourceIdentity(long step, string reversal, string eligibility, string blocker)
    {
        var before = SHA256.HashData(File.ReadAllBytes(path));
        using var factory = Factory(Convert.ToHexString(before));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Endpoint + $"?frameStep={step}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<NasdaqHistoricalReportSummary>())!;
        Assert.Equal("BoundedCanonicalDiagnostic", result.ReportScope);
        Assert.Equal("moneyway.canonical-preentry-window", result.Schema);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal(6, result.FrameCount);
        Assert.Equal(7596, result.EvaluatedPrefixFrameCount);
        Assert.Null(result.SessionId);
        Assert.Null(result.FinalVerdict);
        Assert.Null(result.SelectedSessionLastFrame);
        var frame = result.SelectedFrame!;
        Assert.Equal(step, frame.Step);
        Assert.Equal(DateTimeOffset.Parse(At((int)(step - 7591 + 31))), frame.AsOfUtc);
        Assert.Equal("Wait", frame.Verdict);
        Assert.Equal(blocker, frame.BlockingRuleId);
        Assert.Equal(reversal, frame.Rules[0].Result);
        Assert.Equal(eligibility, frame.Rules[1].Result);
        Assert.All(frame.Rules.Skip(2), r => Assert.Equal("Waiting", r.Result));
        Assert.All(frame.Rules, r => Assert.Null(r.EvidenceId));
        Assert.Equal("Unavailable", frame.SnapshotState);
        Assert.Equal("Unavailable", frame.FactualEvaluationState);
        Assert.Equal(7591, result.Navigation!.FirstStep);
        Assert.Equal(7596, result.Navigation.LastStep);
        Assert.Equal(step > 7591 ? step - 1 : (long?)null, result.Navigation.PreviousStep);
        Assert.Equal(step < 7596 ? step + 1 : (long?)null, result.Navigation.NextStep);
        if (step == 7591) Assert.Equal(["early-source"], result.SourceRecordIds!);
        Assert.DoesNotContain(@"C:\private", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(path, await response.Content.ReadAsStringAsync());
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Fact]
    public async Task DefaultIsLastExportedFrameAndUnavailableSelectionNeverFallsBack()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        var last = (await client.GetFromJsonAsync<NasdaqHistoricalReportSummary>(Endpoint))!;
        Assert.Equal(7596, last.SelectedFrame!.Step); Assert.Null(last.SelectedSessionLastFrame);
        using var missing = await client.GetAsync(Endpoint + "?frameStep=7590");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var changed = await client.GetAsync(Endpoint + $"?frameStep=7590&reportSha256={new string('0', 64)}");
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("version")]
    [InlineData("missing-schema")]
    [InlineData("hash")]
    [InlineData("future-ref")]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-property")]
    [InlineData("step")]
    [InlineData("time")]
    [InlineData("identity")]
    [InlineData("status")]
    [InlineData("evaluation-time")]
    [InlineData("count")]
    [InlineData("boundary")]
    [InlineData("blocker")]
    [InlineData("oversized")]
    public async Task UnsupportedOrCorruptNativeEnvelopeFailsSafely(string corruption)
    {
        var node = Source();
        switch (corruption)
        {
            case "schema": node["Schema"] = "unknown"; break;
            case "version": node["SchemaVersion"] = 2; break;
            case "missing-schema": node.Remove("Schema"); break;
            case "future-ref": node["Frames"]![0]!["StrategyObservation"]!["Evaluations"]![0]!["RuleId"] = new JsonObject { ["$ref"] = "rule3" }; break;
            case "duplicate-id": node["Duplicate"] = new JsonObject { ["$id"] = "strategy" }; break;
            case "step": node["Frames"]![1]!["StrategyObservation"]!["Step"] = 7591; break;
            case "time": node["Frames"]![1]!["StrategyObservation"]!["AsOfUtc"] = At(31); break;
            case "identity": node["Frames"]![0]!["StrategyObservation"]!["Symbol"] = Id("foreign"); break;
            case "status": node["Frames"]![0]!["StrategyObservation"]!["Evaluations"]![0]!["Result"] = 999; break;
            case "evaluation-time": node["Frames"]![0]!["StrategyObservation"]!["Evaluations"]![0]!["EvaluatedAtUtc"] = At(36); break;
            case "count": node["EvaluatedPrefixFrameCount"] = 5; break;
            case "boundary": node["DiagnosticWindowEndUtc"] = At(35); break;
            case "blocker": node["Frames"]![0]!["CanonicalDiagnostic"]!["BlockingResult"] = (int)RuleEvaluationResult.Failed; break;
            case "oversized": node["Padding"] = new string('x', 8 * 1024 * 1024); break;
        }
        var json = node.ToJsonString();
        if (corruption == "duplicate-property") json = json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1,\"SchemaVersion\":1");
        File.WriteAllText(path, json);
        using var factory = Factory(corruption == "hash" ? new string('0', 64) : null);
        using var response = await factory.CreateClient().GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.DoesNotContain(path, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NonconsecutiveStepsUseActualNeighborsNotArrayIndices()
    {
        var source = Source(); source["Frames"]!.AsArray().RemoveAt(1); File.WriteAllText(path, source.ToJsonString());
        using var factory = Factory();
        var result = (await factory.CreateClient().GetFromJsonAsync<NasdaqHistoricalReportSummary>(Endpoint + "?frameStep=7591"))!;
        Assert.Equal(5, result.FrameCount); Assert.Equal(7593, result.Navigation!.NextStep);
    }
    public void Dispose() => File.Delete(path);
}
