using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
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
