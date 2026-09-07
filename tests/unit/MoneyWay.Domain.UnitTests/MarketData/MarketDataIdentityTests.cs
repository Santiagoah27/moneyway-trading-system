using MoneyWay.Domain.MarketData;

namespace MoneyWay.Domain.UnitTests.MarketData;

public sealed class MarketDataIdentityTests
{
    [Fact]
    public void ProviderIdentifierPreservesValueCasingAndEquality()
    {
        var identifier = new MarketDataProviderId("Provider-A");

        Assert.Equal("Provider-A", identifier.Value);
        Assert.Equal("Provider-A", identifier.ToString());
        Assert.Equal(identifier, new MarketDataProviderId("Provider-A"));
        Assert.NotEqual(identifier, new MarketDataProviderId("provider-a"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" provider-a")]
    [InlineData("provider-a ")]
    public void ProviderIdentifierRejectsInvalidText(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MarketDataProviderId(value!));
    }

    [Fact]
    public void MarketSymbolPreservesProviderFacingValueCasingAndEquality()
    {
        var symbol = new MarketSymbol("EUR/USD:Spot-A");

        Assert.Equal("EUR/USD:Spot-A", symbol.Value);
        Assert.Equal("EUR/USD:Spot-A", symbol.ToString());
        Assert.Equal(symbol, new MarketSymbol("EUR/USD:Spot-A"));
        Assert.NotEqual(symbol, new MarketSymbol("eur/usd:spot-a"));
        Assert.NotEqual(symbol, new MarketSymbol("EURUSD:Spot-A"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" EURUSD")]
    [InlineData("EURUSD ")]
    public void MarketSymbolRejectsInvalidText(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MarketSymbol(value!));
    }
}
