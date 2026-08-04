using MoneyWay.Domain.Strategies;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class StrategyVersionTests
{
    [Fact]
    public void ValidValueIsPreserved()
    {
        var version = new StrategyVersion("forex-0.1.0-draft");

        Assert.Equal("forex-0.1.0-draft", version.Value);
        Assert.Equal("forex-0.1.0-draft", version.ToString());
    }

    [Fact]
    public void EqualityUsesValue()
    {
        Assert.Equal(new StrategyVersion("alpha-draft"), new StrategyVersion("alpha-draft"));
        Assert.NotEqual(new StrategyVersion("alpha-draft"), new StrategyVersion("Alpha-draft"));
    }

    [Fact]
    public void NullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new StrategyVersion(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" alpha-draft")]
    [InlineData("alpha-draft ")]
    public void InvalidTextIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new StrategyVersion(value));
    }
}
