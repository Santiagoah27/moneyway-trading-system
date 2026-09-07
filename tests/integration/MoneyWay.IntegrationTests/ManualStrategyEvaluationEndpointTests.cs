using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Api;
using MoneyWay.Api.Contracts.StrategyEvaluation;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.IntegrationTests;

public sealed class ManualStrategyEvaluationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Endpoint = "/api/strategy-evaluations/manual";
    private readonly HttpClient client;

    public ManualStrategyEvaluationEndpointTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Theory]
    [InlineData("forex")]
    [InlineData("nasdaq")]
    public async Task CompleteRegisteredRequiredRulesReturnReady(string strategy)
    {
        var definition = strategy == "forex" ? MoneyWayForexStrategyDefinition.Instance : MoneyWayNasdaqStrategyDefinition.Instance;
        var response = await PostAsync(CreateValidPayload(definition));
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(definition.StrategyId.Value, result!.StrategyId);
        Assert.Equal(definition.Version.Value, result.StrategyVersion);
        Assert.Equal("Ready", result.Verdict);
        Assert.Equal(definition.Rules.Count(static rule => rule.IsRequired), result.RequiredEvaluationCount);
        Assert.Equal(result.Evaluations.OrderBy(static evaluation => evaluation.Sequence), result.Evaluations);
    }

    [Fact]
    public async Task UnorderedInputReturnsEvaluationsOrderedBySequence()
    {
        var payload = CreateValidPayload(MoneyWayForexStrategyDefinition.Instance);
        payload["evaluations"] = new JsonArray(payload["evaluations"]!.AsArray().Reverse().Select(node => node!.DeepClone()).ToArray());

        var response = await PostAsync(payload);
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(result!.Evaluations.OrderBy(static evaluation => evaluation.Sequence), result.Evaluations);
    }

    [Fact]
    public async Task FirstRequiredWaitingRuleReturnsWaitAndCompleteAuditTrail()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        var blocker = definition.Rules.Where(static rule => rule.IsRequired).Skip(1).First();
        FindEvaluation(payload, blocker.RuleId.Value)["result"] = "Waiting";
        FindEvaluation(payload, blocker.RuleId.Value)["reason"] = "Manual evidence is still pending.";

        var response = await PostAsync(payload);
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Wait", result!.Verdict);
        Assert.Equal(blocker.RuleId.Value, result.BlockingRuleId);
        Assert.Equal(blocker.Sequence, result.BlockingSequence);
        Assert.Equal("Waiting", result.BlockingResult);
    }

    [Theory]
    [InlineData("Failed", "NoTrade")]
    [InlineData("HumanValidationRequired", "HumanValidationRequired")]
    [InlineData("DataUnavailable", "DataUnavailable")]
    public async Task ValidBlockingResultReturnsDomainVerdict(string ruleResult, string expectedVerdict)
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        FindEvaluation(payload, definition.Rules.First(static rule => rule.IsRequired).RuleId.Value)["result"] = ruleResult;

        var result = await (await PostAsync(payload)).Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();
        Assert.Equal(expectedVerdict, result!.Verdict);
    }

    [Fact]
    public async Task OptionalRuleMayBeOmittedAndWhenIncludedDoesNotBlock()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        payload["evaluations"]!.AsArray().Add(CreateEvaluation(definition.Rules.First(static rule => !rule.IsRequired), "Failed"));

        var response = await PostAsync(payload);
        var result = await response.Content.ReadFromJsonAsync<ManualStrategyEvaluationResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ready", result!.Verdict);
    }

    [Theory]
    [InlineData("moneyway-unknown", "unknown-0.1.0-draft")]
    [InlineData("moneyway-forex", "forex-99.0.0-draft")]
    public async Task UnknownDefinitionReturnsNotFound(string strategyId, string version)
    {
        var response = await PostAsync(new JsonObject
        {
            ["strategyId"] = strategyId,
            ["strategyVersion"] = version,
            ["evaluations"] = new JsonArray(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("invented")]
    public async Task ForexRejectsUnknownRule(string kind)
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        var extra = kind == "foreign"
            ? MoneyWayNasdaqStrategyDefinition.Instance.Rules.First()
            : new StrategyRuleDefinition(new RuleId("FX-INVENTED-999"), "Invented", "Test", 999, false,
                RuleDefinitionStatus.Confirmed, "Test-only incompatible input.", "test");
        payload["evaluations"]!.AsArray().Add(CreateEvaluation(extra));

        await AssertValidationIssue(payload, "unknown_rule", extra.RuleId.Value);
    }

    [Fact]
    public async Task NasdaqRejectsForexRule()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        var foreign = MoneyWayForexStrategyDefinition.Instance.Rules.First();
        payload["evaluations"]!.AsArray().Add(CreateEvaluation(foreign));
        await AssertValidationIssue(payload, "unknown_rule", foreign.RuleId.Value);
    }

    [Fact]
    public async Task MissingRequiredRuleDoesNotReturnReady()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        var missing = definition.Rules.First(static rule => rule.IsRequired);
        payload["evaluations"] = new JsonArray(payload["evaluations"]!.AsArray()
            .Where(node => node!["ruleId"]!.GetValue<string>() != missing.RuleId.Value)
            .Select(node => node!.DeepClone()).ToArray());
        await AssertValidationIssue(payload, "missing_required_rule", missing.RuleId.Value);
    }

    [Theory]
    [InlineData("sequence", "sequence_mismatch")]
    [InlineData("required", "required_flag_mismatch")]
    [InlineData("status", "definition_status_mismatch")]
    public async Task MetadataTamperingReturnsMachineReadableIssue(string field, string expectedCode)
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var payload = CreateValidPayload(definition);
        var rule = definition.Rules.First(static item => item.IsRequired);
        var evaluation = FindEvaluation(payload, rule.RuleId.Value);
        if (field == "sequence") evaluation["sequence"] = rule.Sequence + 1;
        if (field == "required") evaluation["isRequired"] = false;
        if (field == "status") evaluation["definitionStatus"] = "Confirmed";
        await AssertValidationIssue(payload, expectedCode, rule.RuleId.Value);
    }

    [Fact]
    public async Task DuplicateRuleReturnsMachineReadableIssue()
    {
        var payload = CreateValidPayload(MoneyWayForexStrategyDefinition.Instance);
        var first = payload["evaluations"]!.AsArray()[0]!;
        payload["evaluations"]!.AsArray().Add(first.DeepClone());
        await AssertValidationIssue(payload, "duplicate_rule", first["ruleId"]!.GetValue<string>());
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

    private async Task AssertValidationIssue(JsonObject payload, string code, string ruleId)
    {
        var response = await PostAsync(payload);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(problem["errors"]!.AsArray(), issue =>
            issue!["code"]!.GetValue<string>() == code && issue["ruleId"]!.GetValue<string>() == ruleId);
    }

    private async Task<HttpResponseMessage> PostAsync(JsonObject payload) => await client.PostAsJsonAsync(Endpoint, payload);

    private static JsonObject CreateValidPayload(StrategyDefinition definition) => new()
    {
        ["strategyId"] = definition.StrategyId.Value,
        ["strategyVersion"] = definition.Version.Value,
        ["evaluations"] = new JsonArray(definition.Rules.Where(static rule => rule.IsRequired)
            .Select(static rule => (JsonNode?)CreateEvaluation(rule)).ToArray()),
    };

    private static JsonObject CreateEvaluation(StrategyRuleDefinition rule, string result = "Passed") => new()
    {
        ["ruleId"] = rule.RuleId.Value,
        ["definitionStatus"] = rule.DefinitionStatus.ToString(),
        ["result"] = result,
        ["sequence"] = rule.Sequence,
        ["isRequired"] = rule.IsRequired,
        ["reason"] = $"Manual result for {rule.RuleId.Value}.",
        ["evaluatedAtUtc"] = "2026-09-03T20:00:00Z",
        ["evidenceReference"] = "audited-test-evidence",
    };

    private static JsonObject FindEvaluation(JsonObject payload, string ruleId) => payload["evaluations"]!.AsArray()
        .Select(static node => node!.AsObject()).Single(node => node["ruleId"]!.GetValue<string>() == ruleId);

    private static JsonObject CreateInvalidPayload(string invalidCase)
    {
        var payload = CreateValidPayload(MoneyWayForexStrategyDefinition.Instance);
        var first = payload["evaluations"]!.AsArray()[0]!.AsObject();
        switch (invalidCase)
        {
            case "unknown-definition-status": first["definitionStatus"] = "Unknown"; break;
            case "unknown-result": first["result"] = "Unknown"; break;
            case "numeric-result": first["result"] = "1"; break;
            case "lowercase-result": first["result"] = "passed"; break;
            case "zero-sequence": first["sequence"] = 0; break;
            case "negative-sequence": first["sequence"] = -1; break;
            case "duplicate-sequence": payload["evaluations"]!.AsArray()[1]!["sequence"] = first["sequence"]!.GetValue<int>(); break;
            case "empty-reason": first["reason"] = string.Empty; break;
            case "spaced-rule-id": first["ruleId"] = $" {first["ruleId"]!.GetValue<string>()}"; break;
            case "empty-strategy-id": payload["strategyId"] = string.Empty; break;
            case "null-strategy-id": payload["strategyId"] = null; break;
            case "spaced-strategy-id": payload["strategyId"] = " moneyway-forex"; break;
            case "empty-strategy-version": payload["strategyVersion"] = string.Empty; break;
            case "spaced-strategy-version": payload["strategyVersion"] = "forex-0.1.0-draft "; break;
            case "null-evaluations": payload["evaluations"] = null; break;
            case "null-evaluation": payload["evaluations"] = new JsonArray((JsonNode?)null); break;
            case "non-utc-timestamp": first["evaluatedAtUtc"] = "2026-09-03T15:00:00-05:00"; break;
            case "empty-evidence": first["evidenceReference"] = string.Empty; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, null);
        }

        return payload;
    }
}
