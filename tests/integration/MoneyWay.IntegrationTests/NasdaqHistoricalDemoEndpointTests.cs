using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqHistoricalDemoEndpointTests : IDisposable
{
    private const string Endpoint = "/api/nasdaq/historical-demo";
    private readonly string path = Path.Combine(Path.GetTempPath(), $"moneyway-demo-{Guid.NewGuid():N}.json");
    private readonly NasdaqMentorSessionReplayReport report;

    public NasdaqHistoricalDemoEndpointTests()
    {
        using var fixture = new NasdaqMentorSessionImportTests();
        var input = fixture.Input();
        report = new RunLocalNasdaqMentorSessionUseCase().Execute(new(input.SessionId, input.Session, input.CsvFiles, []));
        using var stream = File.Create(path);
        NasdaqMentorSessionReportJson.Write(stream, report);
    }

    private WebApplicationFactory<Program> Factory(string? reportPath = null, string? expectedHash = null, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment)
            .UseSetting("NasdaqHistoricalDemo:ReportPath", reportPath ?? path)
            .UseSetting("NasdaqHistoricalDemo:ExpectedSha256", expectedHash ?? ""));

    [Theory]
    [InlineData("first")]
    [InlineData("middle")]
    [InlineData("last")]
    [InlineData("default")]
    public async Task SelectionAndActualNeighborsMatchPersistedSessionFrames(string position)
    {
        using var source = JsonDocument.Parse(File.ReadAllText(path));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var frames = source.RootElement.GetProperty("Frames").EnumerateArray().Where(f =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(f.GetProperty("AsOfUtc").GetDateTimeOffset(), zone).DateTime) == report.Session.TradingDay).ToArray();
        var index = position == "first" ? 0 : position == "middle" ? frames.Length / 2 : frames.Length - 1;
        var expected = frames[index];
        var step = expected.GetProperty("Step").GetInt64();
        var before = SHA256.HashData(File.ReadAllBytes(path));
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync(Endpoint + (position == "default" ? "" : $"?frameStep={step}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<NasdaqHistoricalReportSummary>())!;
        var selected = Assert.IsType<NasdaqHistoricalFrameSummary>(summary.SelectedFrame);
        var nav = Assert.IsType<NasdaqHistoricalFrameNavigation>(summary.Navigation);
        Assert.Equal(step, selected.Step);
        Assert.Equal(expected.GetProperty("AsOfUtc").GetDateTimeOffset(), selected.AsOfUtc);
        Assert.Equal(frames[0].GetProperty("Step").GetInt64(), nav.FirstStep);
        Assert.Equal(frames[^1].GetProperty("Step").GetInt64(), nav.LastStep);
        Assert.Equal(step, nav.SelectedStep);
        Assert.Equal(frames.Length, nav.SessionFrameCount);
        Assert.Equal(index == 0 ? (long?)null : frames[index - 1].GetProperty("Step").GetInt64(), nav.PreviousStep);
        Assert.Equal(index == frames.Length - 1 ? (long?)null : frames[index + 1].GetProperty("Step").GetInt64(), nav.NextStep);
        var rules = expected.GetProperty("Evaluations").GetProperty("$values").EnumerateArray().ToArray();
        Assert.Equal(rules.Length, selected.Rules.Count);
        for (var i = 0; i < rules.Length; i++)
        {
            Assert.Equal(rules[i].GetProperty("RuleId").GetProperty("Value").GetString(), selected.Rules[i].RuleId);
            Assert.Equal(((MoneyWay.Domain.Strategies.RuleEvaluationResult)rules[i].GetProperty("Result").GetInt32()).ToString(), selected.Rules[i].Result);
            Assert.Equal(rules[i].GetProperty("Reason").GetString(), selected.Rules[i].Reason);
            Assert.Equal(rules[i].GetProperty("EvaluatedAtUtc").GetDateTimeOffset(), selected.Rules[i].EvaluatedAtUtc);
            Assert.Equal(rules[i].GetProperty("EvidenceId").GetString(), selected.Rules[i].EvidenceId);
        }
        Assert.Equal(report.SessionId, summary.SessionId);
        Assert.Equal(report.Session.TradingDay, summary.TradingDay);
        Assert.Equal(report.Canonical!.Frames.Last().Verdict.ToString(), summary.FinalVerdict);
        if (position == "default") Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(summary.SelectedSessionLastFrame));
        else Assert.Null(summary.SelectedSessionLastFrame); // Never transport later rule evidence into an explicit earlier selection.
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length < 64 * 1024);
    }

    [Theory]
    [InlineData("?frameStep=0", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=-1", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=1.5", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=abc", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=1&frameStep=2", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=9223372036854775808", HttpStatusCode.BadRequest)]
    [InlineData("?frameStep=999999", HttpStatusCode.NotFound)]
    [InlineData("?reportSha256=invalid", HttpStatusCode.BadRequest)]
    public async Task InvalidOrUnavailableSelectionsAreSafe(string query, HttpStatusCode expected)
    {
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync(Endpoint + query);
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain(path, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ForeignSessionStepIsNotSelectableAndChangedReportTakesPrecedence()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var foreign = report.Frames.First(f => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(f.Strategy.AsOfUtc, zone).DateTime) != report.Session.TradingDay);
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Endpoint + $"?frameStep={foreign.Strategy.Step}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var changed = await client.GetAsync(Endpoint + $"?frameStep={foreign.Strategy.Step}&reportSha256={new string('0', 64)}");
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Contains("ReportChanged", await changed.Content.ReadAsStringAsync());
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        using var current = await client.GetAsync(Endpoint + "?reportSha256=" + hash);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [Fact]
    public async Task EqualTimestampsAreRejectedWithoutAmbiguousFrameSelection()
    {
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        document["Frames"]![1]!["AsOfUtc"] = document["Frames"]![0]!["AsOfUtc"]!.DeepClone();
        File.WriteAllText(path, document.ToJsonString());
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync(Endpoint + "?frameStep=1");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ExistingCanonicalReportRetainsIdentityOutcomesRuleStatesAndSourceBytes()
    {
        var before = SHA256.HashData(File.ReadAllBytes(path));
        using var factory = Factory(expectedHash: Convert.ToHexString(before));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(Endpoint);
        var summary = await response.Content.ReadFromJsonAsync<NasdaqHistoricalReportSummary>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(report.SessionId, summary.SessionId);
        Assert.Equal(report.Session.StrategyVersion.Value, summary.StrategyVersion);
        Assert.Equal(report.Session.ProviderId.Value, summary.ProviderId);
        Assert.Equal(report.Session.Symbol.Value, summary.Symbol);
        Assert.Equal(report.Session.TradingDay, summary.TradingDay);
        Assert.Equal(report.Frames.Count, summary.FrameCount);
        Assert.Equal(report.Canonical!.Frames.Last().Verdict.ToString(), summary.FinalVerdict);
        Assert.Equal("NoTrade", summary.FinalVerdict);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var frame = report.Frames.Last(f => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(f.Strategy.AsOfUtc, zone).DateTime) == report.Session.TradingDay);
        var projected = Assert.IsType<NasdaqHistoricalFrameSummary>(summary.SelectedSessionLastFrame);
        Assert.Equal(frame.Strategy.Step, projected.Step);
        Assert.Equal(frame.Strategy.Evaluations.Select(e => (e.RuleId.Value, e.Result.ToString(), e.DefinitionStatus.ToString(), e.Reason, e.IsRequired)),
            projected.Rules.Select(e => (e.RuleId, e.Result, e.DefinitionStatus, e.Reason, e.IsRequired)));
        Assert.Equal("Unavailable", projected.FactualEvaluationState);
        Assert.Equal("Unavailable", projected.SnapshotState);
        Assert.Equal(Convert.ToHexString(before), summary.ReportSha256);
        Assert.Equal("SchemaChronologyCountAndExpectedHashChecked", summary.Validation);
        Assert.Equal("NotRevalidated", summary.EvidenceValidation);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length < 64 * 1024);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.DoesNotContain(path, await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("", HttpStatusCode.NotFound, "ReportNotConfigured")]
    [InlineData("missing", HttpStatusCode.NotFound, "ReportMissing")]
    [InlineData("directory", HttpStatusCode.ServiceUnavailable, "ReportInaccessible")]
    [InlineData("relative.json", HttpStatusCode.ServiceUnavailable, "ReportConfigurationInvalid")]
    [InlineData("\\\\server\\private\\report.json", HttpStatusCode.ServiceUnavailable, "ReportConfigurationInvalid")]
    public async Task MissingOrInaccessibleReportHasSafeError(string location, HttpStatusCode status, string code)
    {
        var configured = location switch { "missing" => path + ".missing", "directory" => Path.GetDirectoryName(path)!, _ => location };
        using var factory = Factory(configured);
        using var response = await factory.CreateClient().GetAsync(Endpoint);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, text);
        Assert.DoesNotContain(path, text);
        Assert.DoesNotContain("stackTrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", text);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("schema")]
    [InlineData("missing")]
    [InlineData("count")]
    [InlineData("chronology")]
    [InlineData("status")]
    [InlineData("verdict")]
    [InlineData("hash")]
    [InlineData("trailing")]
    [InlineData("oversized")]
    [InlineData("factual")]
    [InlineData("canonical")]
    public async Task InvalidReportNeverBecomesSuccess(string corruption)
    {
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        switch (corruption)
        {
            case "schema": document["SchemaVersion"] = 999; break;
            case "missing": document.AsObject().Remove("Session"); break;
            case "count": document["FrameCount"] = 1; break;
            case "chronology": document["Frames"]![0]!["Step"] = 900; break;
            case "status": document["Frames"]![0]!["Evaluations"]!["$values"]![0]!["Result"] = 999; break;
            case "verdict": document["FinalVerdict"] = 0; break;
            case "oversized": document["UnrelatedOversizedValue"] = new string('x', 8 * 1024 * 1024); break;
            case "factual": document["Frames"]![0]!["FactualEvaluationState"] = "Available"; break;
            case "canonical": document.AsObject().Remove("Canonical"); break;
        }
        File.WriteAllText(path, corruption == "malformed" ? "{broken" : document.ToJsonString() + (corruption == "trailing" ? "{}" : ""));
        using var factory = Factory(expectedHash: corruption == "hash" ? new string('0', 64) : null);
        using var response = await factory.CreateClient().GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ReportInvalidOrUnsupported", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingOrUnsupportedHistoricalFieldsRemainUnavailable()
    {
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var frame in document["Frames"]!.AsArray())
        {
            frame!.AsObject().Remove("SnapshotState");
            frame["FactualEvaluationState"] = "UnknownFutureVariant";
        }
        File.WriteAllText(path, document.ToJsonString());
        using var factory = Factory();
        var summary = await factory.CreateClient().GetFromJsonAsync<NasdaqHistoricalReportSummary>(Endpoint);
        Assert.Null(summary!.SelectedSessionLastFrame!.SnapshotState);
        Assert.Null(summary.SelectedSessionLastFrame.FactualEvaluationState);
        Assert.Null(summary.SelectedSessionLastFrame.FactualRequiresHumanValidation);
    }

    [Fact]
    public async Task ReportWithInputFailureDoesNotFabricateCanonicalOutcome()
    {
        var input = new NasdaqMentorSessionReplayInput(report.SessionId, report.Session, new Dictionary<Timeframe, string>(), []);
        var failed = new RunLocalNasdaqMentorSessionUseCase().Execute(input);
        using (var stream = File.Create(path)) NasdaqMentorSessionReportJson.Write(stream, failed);
        using var factory = Factory();
        var summary = await factory.CreateClient().GetFromJsonAsync<NasdaqHistoricalReportSummary>(Endpoint);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.FrameCount);
        Assert.Null(summary.FinalVerdict);
        Assert.Null(summary.SelectedSessionLastFrame);
        Assert.Equal(["MissingTimeframe"], summary.InputDiagnosticCodes);
    }

    [Fact]
    public async Task IsoTradingDayDoesNotDependOnTheServersCalendar()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
            using var reader = new ReadNasdaqHistoricalReport();
            var summary = await reader.ExecuteAsync(path, null);
            Assert.Equal(report.Session.TradingDay, summary.TradingDay);
            Assert.NotNull(summary.SelectedSessionLastFrame);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("?path=C:/private/secrets.json")]
    [InlineData("?report=../../secrets")]
    public async Task CallerCannotSelectAFile(string query)
    {
        using var factory = Factory();
        using var response = await factory.CreateClient().GetAsync(Endpoint + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EndpointIsNotExposedInProduction()
    {
        using var factory = Factory(environment: "Production");
        using var response = await factory.CreateClient().GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => File.Delete(path);
}
