using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Api;
using MoneyWay.Api.Contracts.StrategyEvaluation;

namespace MoneyWay.IntegrationTests;

public sealed class ManualStrategyEvaluationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Endpoint = "/api/strategy-evaluations/manual";
    private readonly HttpClient client;

    public ManualStrategyEvaluationEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PassedRequiredEvaluationsReturnReadyAndOrderedAuditTrail()
    {
        var payload = CreatePayload(
            CreateEvaluation("RULE-002", 20, "Passed"),
            CreateEvaluation("RULE-001", 10, "Passed"));

        var response = await PostAsync(payload);
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("manual-strategy", result.StrategyId);
        Assert.Equal("manual-0.1.0-draft", result.StrategyVersion);
        Assert.Equal("Ready", result.Verdict);
        Assert.Null(result.BlockingRuleId);
        Assert.Null(result.BlockingSequence);
        Assert.Null(result.BlockingResult);
        Assert.Equal(2, result.EvaluationCount);
        Assert.Equal(2, result.RequiredEvaluationCount);
        Assert.Equal([10, 20], result.Evaluations.Select(evaluation => evaluation.Sequence));
    }

    [Fact]
    public async Task UnorderedInputReturnsEvaluationsOrderedBySequence()
    {
        var response = await PostAsync(CreatePayload(
            CreateEvaluation("THIRTY", 30, "Passed"),
            CreateEvaluation("TEN", 10, "Passed"),
            CreateEvaluation("TWENTY", 20, "Passed")));
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([10, 20, 30], result!.Evaluations.Select(evaluation => evaluation.Sequence));
    }

    [Fact]
    public async Task FirstRequiredWaitingRuleReturnsWaitAndCompleteAuditTrail()
    {
        const string waitingReason = "Manual evidence is still pending.";
        var response = await PostAsync(CreatePayload(
            CreateEvaluation("THIRTY", 30, "Failed"),
            CreateEvaluation("TEN", 10, "Passed"),
            CreateEvaluation("TWENTY", 20, "Waiting", reason: waitingReason)));
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Wait", result.Verdict);
        Assert.Equal("TWENTY", result.BlockingRuleId);
        Assert.Equal(20, result.BlockingSequence);
        Assert.Equal("Waiting", result.BlockingResult);
        Assert.Equal(waitingReason, result.Reason);
        Assert.Equal(3, result.Evaluations.Count);
    }

    [Theory]
    [InlineData("Failed", "NoTrade")]
    [InlineData("HumanValidationRequired", "HumanValidationRequired")]
    [InlineData("DataUnavailable", "DataUnavailable")]
    public async Task ValidBlockingResultReturnsDomainVerdict(string ruleResult, string expectedVerdict)
    {
        var response = await PostAsync(CreatePayload(CreateEvaluation("RULE-001", 1, ruleResult)));
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedVerdict, result!.Verdict);
    }

    [Fact]
    public async Task EmptyEvaluationCollectionReturnsDataUnavailable()
    {
        var response = await PostAsync(CreatePayload());
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("DataUnavailable", result.Verdict);
        Assert.Equal(0, result.EvaluationCount);
        Assert.Equal(0, result.RequiredEvaluationCount);
        Assert.Null(result.BlockingRuleId);
    }

    [Fact]
    public async Task OptionalFailedRuleDoesNotBlockRequiredPassedRule()
    {
        var response = await PostAsync(CreatePayload(
            CreateEvaluation("OPTIONAL", 10, "Failed", false),
            CreateEvaluation("REQUIRED", 20, "Passed")));
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ready", result!.Verdict);
    }

    [Theory]
    [InlineData("unknown-definition-status")]
    [InlineData("unknown-result")]
    [InlineData("numeric-result")]
    [InlineData("lowercase-result")]
    [InlineData("zero-sequence")]
    [InlineData("negative-sequence")]
    [InlineData("duplicate-sequence")]
    [InlineData("empty-reason")]
    [InlineData("spaced-rule-id")]
    [InlineData("empty-strategy-id")]
    [InlineData("null-strategy-id")]
    [InlineData("spaced-strategy-id")]
    [InlineData("empty-strategy-version")]
    [InlineData("spaced-strategy-version")]
    [InlineData("null-evaluations")]
    [InlineData("null-evaluation")]
    [InlineData("non-utc-timestamp")]
    [InlineData("empty-evidence")]
    public async Task InvalidManualRequestReturnsSafeValidationProblem(string invalidCase)
    {
        var response = await PostAsync(CreateInvalidPayload(invalidCase));
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Argument", content, StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> PostAsync(JsonObject payload)
    {
        return await client.PostAsJsonAsync(Endpoint, payload);
    }

    private static JsonObject CreatePayload(params JsonNode?[] evaluations)
    {
        return new JsonObject
        {
            ["strategyId"] = "manual-strategy",
            ["strategyVersion"] = "manual-0.1.0-draft",
            ["evaluations"] = new JsonArray(evaluations),
        };
    }

    private static JsonObject CreateEvaluation(
        string ruleId,
        int sequence,
        string result,
        bool isRequired = true,
        string reason = "Caller determined this result.")
    {
        return new JsonObject
        {
            ["ruleId"] = ruleId,
            ["definitionStatus"] = "Confirmed",
            ["result"] = result,
            ["sequence"] = sequence,
            ["isRequired"] = isRequired,
            ["reason"] = reason,
            ["evaluatedAtUtc"] = "2026-09-03T20:00:00Z",
            ["evidenceReference"] = "external-audit-pending-import",
        };
    }

    private static JsonObject CreateInvalidPayload(string invalidCase)
    {
        var first = CreateEvaluation("RULE-001", 1, "Passed");
        var payload = CreatePayload(first);

        switch (invalidCase)
        {
            case "unknown-definition-status": first["definitionStatus"] = "Unknown"; break;
            case "unknown-result": first["result"] = "Unknown"; break;
            case "numeric-result": first["result"] = "1"; break;
            case "lowercase-result": first["result"] = "passed"; break;
            case "zero-sequence": first["sequence"] = 0; break;
            case "negative-sequence": first["sequence"] = -1; break;
            case "duplicate-sequence":
                payload["evaluations"] = new JsonArray(
                    CreateEvaluation("RULE-001", 1, "Passed"),
                    CreateEvaluation("RULE-002", 1, "Passed"));
                break;
            case "empty-reason": first["reason"] = string.Empty; break;
            case "spaced-rule-id": first["ruleId"] = " RULE-001"; break;
            case "empty-strategy-id": payload["strategyId"] = string.Empty; break;
            case "null-strategy-id": payload["strategyId"] = null; break;
            case "spaced-strategy-id": payload["strategyId"] = " manual-strategy"; break;
            case "empty-strategy-version": payload["strategyVersion"] = string.Empty; break;
            case "spaced-strategy-version": payload["strategyVersion"] = "manual-0.1.0-draft "; break;
            case "null-evaluations": payload["evaluations"] = null; break;
            case "null-evaluation": payload["evaluations"] = new JsonArray((JsonNode?)null); break;
            case "non-utc-timestamp": first["evaluatedAtUtc"] = "2026-09-03T15:00:00-05:00"; break;
            case "empty-evidence": first["evidenceReference"] = string.Empty; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, null);
        }

        return payload;
    }
}
