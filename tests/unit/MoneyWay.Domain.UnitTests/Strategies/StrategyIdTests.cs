using MoneyWay.Domain.Strategies;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class StrategyIdTests
{
    [Fact]
    public void ValidValueIsPreserved()
    {
        var identifier = new StrategyId("strategy Alpha");

        Assert.Equal("strategy Alpha", identifier.Value);
        Assert.Equal("strategy Alpha", identifier.ToString());
    }

    [Fact]
    public void EqualityUsesValue()
    {
        Assert.Equal(new StrategyId("alpha"), new StrategyId("alpha"));
        Assert.NotEqual(new StrategyId("alpha"), new StrategyId("Alpha"));
    }

    [Fact]
    public void NullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new StrategyId(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" alpha")]
    [InlineData("alpha ")]
    public void InvalidTextIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new StrategyId(value));
    }
}
