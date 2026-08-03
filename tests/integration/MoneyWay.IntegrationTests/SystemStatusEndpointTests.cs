using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MoneyWay.Api;

namespace MoneyWay.IntegrationTests;

public sealed class SystemStatusEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public SystemStatusEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthReturnsSuccess()
    {
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemStatusReturnsSafeBootstrapContract()
    {
        var status = await client.GetFromJsonAsync<SystemStatusResponse>("/api/system/status");

        Assert.NotNull(status);
        Assert.Equal("MoneyWay Trading System", status.Application);
        Assert.Equal("bootstrap", status.Status);
        Assert.Equal("disabled", status.ExecutionMode);
        Assert.False(status.RealMoneyTradingEnabled);
    }
}
