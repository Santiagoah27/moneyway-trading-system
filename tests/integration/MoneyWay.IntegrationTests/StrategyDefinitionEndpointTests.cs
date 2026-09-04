using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Api.Contracts.StrategyDefinitions;

namespace MoneyWay.IntegrationTests;

public sealed class StrategyDefinitionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CollectionEndpoint = "/api/strategy-definitions";
    private const string ExactEndpoint = "/api/strategy-definitions/moneyway-forex/forex-0.1.0-draft";
    private readonly HttpClient client;

    public StrategyDefinitionEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllReturnsTheSingleAuditedForexDefinition()
    {
        using var response = await client.GetAsync(CollectionEndpoint);
        var definitions = await response.Content.ReadFromJsonAsync<StrategyDefinitionResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var definition = Assert.Single(Assert.IsType<StrategyDefinitionResponse[]>(definitions));
        Assert.Equal("moneyway-forex", definition.StrategyId);
        Assert.Equal("forex-0.1.0-draft", definition.Version);
        Assert.Equal("MoneyWay Forex", definition.DisplayName);
        Assert.True(definition.RuleCount > 0);
        Assert.True(definition.RequiredRuleCount > 0);
        Assert.Equal(definition.Rules.Count, definition.RuleCount);
        Assert.Equal(definition.Rules.Count(rule => rule.IsRequired), definition.RequiredRuleCount);
        Assert.Equal(definition.Rules.OrderBy(rule => rule.Sequence), definition.Rules);
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.RuleId).Distinct().Count());
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.Sequence).Distinct().Count());
        Assert.DoesNotContain(definition.Rules, rule => rule.DefinitionStatus == "RejectedAiInference");
        Assert.DoesNotContain(definitions, item => item.DisplayName.Contains("Nasdaq", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExactLookupReturnsTheSameVersionAndRulesAsGetAll()
    {
        var all = await client.GetFromJsonAsync<StrategyDefinitionResponse[]>(CollectionEndpoint);
        using var response = await client.GetAsync(ExactEndpoint);
        var exact = await response.Content.ReadFromJsonAsync<StrategyDefinitionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(exact);
        Assert.Equal("forex-0.1.0-draft", exact.Version);
        Assert.Equal(all![0].Rules, exact.Rules);
    }

    [Theory]
    [InlineData("/api/strategy-definitions/MoneyWay-Forex/forex-0.1.0-draft")]
    [InlineData("/api/strategy-definitions/moneyway-forex/unknown-version")]
    [InlineData("/api/strategy-definitions/unknown-strategy/forex-0.1.0-draft")]
    public async Task ValidButUnregisteredExactLookupReturnsNotFound(string endpoint)
    {
        using var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StructurallyInvalidIdentifierReturnsSafeValidationProblem()
    {
        using var response = await client.GetAsync("/api/strategy-definitions/%20/forex-0.1.0-draft");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Argument", content, StringComparison.Ordinal);
    }
}
