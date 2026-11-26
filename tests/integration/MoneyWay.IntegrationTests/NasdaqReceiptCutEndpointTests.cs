using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqReceiptCutEndpointTests : IDisposable
{
    private const string Route = "/api/nasdaq/historical-demo/receipt-cuts";
    private readonly Mt5ReceiptSyntheticFixture source = new();
    private readonly Mt5ReceiptSyntheticFixture development = new();

    private string Report(long sequence, string name)
    {
        var path = Path.Combine(development.DirectoryPath, name);
        Assert.Equal(0, new Mt5CausalAvailabilityCommand().Execute(
            ["mt5-causal-availability", "--view", "receipt-cut", "--source", source.DirectoryPath,
                "--through-sequence", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--through-sha256", Mt5ReceiptSyntheticFixture.Hash(source.RecordPath(sequence)), "--report", path],
            TextWriter.Null, TextWriter.Null));
        return path;
    }

    private WebApplicationFactory<Program> App(string path, long sequence = 1, string environment = "Development",
        string? hash = null, string? file = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseEnvironment(environment).ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["NasdaqReceiptCuts:DevelopmentDirectory"] = development.DirectoryPath,
                    ["NasdaqReceiptCuts:Reports:case-a:File"] = file ?? Path.GetFileName(path),
                    ["NasdaqReceiptCuts:Reports:case-a:Sha256"] = hash ?? Mt5ReceiptSyntheticFixture.Hash(path),
                    ["NasdaqReceiptCuts:Reports:case-a:Sequence"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })));

    [Fact]
    public async Task RegisteredWorkerArtifactIsReadOnlyAndEarlierResponseSurvivesFuturePublication()
    {
        source.Add();
        var original = Report(1, "cut-1.json");
        using var app = App(original);
        using var client = app.CreateClient();
        using var catalog = JsonDocument.Parse(await client.GetStringAsync(Route));
        var registered = Assert.Single(catalog.RootElement.EnumerateArray());
        Assert.Equal("case-a", registered.GetProperty("id").GetString());
        Assert.False(registered.TryGetProperty("file", out _));

        using var first = await client.GetAsync(Route + "/case-a");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.CacheControl!.NoStore);
        var bytes = await first.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(bytes);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(original), payload.RootElement.GetProperty("reportSha256").GetString());
        var view = payload.RootElement.GetProperty("diagnostic");
        Assert.Equal("Validated", view.GetProperty("integrity").GetString());
        Assert.Equal("US100", view.GetProperty("expectedSource").GetProperty("nativeSymbol").GetString());
        Assert.Equal("NotEvaluated", view.GetProperty("standardEvaluation").GetString());
        Assert.False(view.GetProperty("canonicalReady").GetBoolean());
        Assert.False(view.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(view.GetProperty("operationalSignal").GetBoolean());
        Assert.Equal(4, view.GetProperty("samples").GetArrayLength());
        Assert.False(bytes.Contains(source.DirectoryPath.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.False(view.TryGetProperty("strategyVerdict", out _));

        source.Add(2, 1);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(original), Mt5ReceiptSyntheticFixture.Hash(Report(1, "cut-1-repeat.json")));
        Assert.Equal(bytes, await client.GetStringAsync(Route + "/case-a"));
        using var post = await client.PostAsync(Route + "/case-a", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task QueryTraversalUnknownIdAndNonLocalAndProductionRequestsAreRejected()
    {
        source.Add();
        var path = Report(1, "cut-1.json");
        using var app = App(path);
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "/case-a?path=../cut-1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "/case-a?canonicalReady=true")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route + "/unregistered")).StatusCode);
        using var external = new HttpRequestMessage(HttpMethod.Get, Route);
        external.Headers.Host = "outside.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(external)).StatusCode);
        using var origin = new HttpRequestMessage(HttpMethod.Get, Route);
        origin.Headers.Add("Origin", "https://outside.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(origin)).StatusCode);
        using var developmentOrigin = new HttpRequestMessage(HttpMethod.Get, Route);
        developmentOrigin.Headers.Add("Origin", "http://localhost:5173");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(developmentOrigin)).StatusCode);
        using var production = App(path, environment: "Production");
        Assert.Equal(HttpStatusCode.NotFound, (await production.CreateClient().GetAsync(Route)).StatusCode);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("source")]
    [InlineData("signal")]
    [InlineData("sequence")]
    [InlineData("sample-after-cut")]
    [InlineData("sample-active")]
    public async Task InvalidPinnedIdentityOrUnsafeArtifactFailsClosed(string fault)
    {
        source.Add();
        var path = Report(1, "cut-1.json");
        var report = JsonNode.Parse(File.ReadAllText(path))!;
        var diagnostic = report["audit"]!;
        switch (fault)
        {
            case "source": diagnostic["expected_source"]!["native_symbol"] = "USTEC"; break;
            case "signal": diagnostic["operational_signal"] = true; break;
            case "sample-after-cut": diagnostic["samples"]![0]!["first_usable_historical"]!["receipt"]!["sequence"] = 2; break;
            case "sample-active": diagnostic["samples"]![0]!["first_usable_historical"]!["source_position"] = 0; break;
        }
        File.WriteAllText(path, report.ToJsonString());
        using var app = App(path, fault == "sequence" ? 2 : 1, hash: fault == "hash" ? new string('0', 64) : null);
        using var response = await app.CreateClient().GetAsync(Route + "/case-a");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ReceiptCutIntegrityFailure", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ConfiguredFileMustBeAnExplicitNameUnderDevelopmentRoot()
    {
        source.Add();
        var path = Report(1, "cut-1.json");
        using var app = App(path, file: "../cut-1.json");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await app.CreateClient().GetAsync(Route + "/case-a")).StatusCode);
    }

    public void Dispose()
    {
        source.Dispose();
        development.Dispose();
    }
}
