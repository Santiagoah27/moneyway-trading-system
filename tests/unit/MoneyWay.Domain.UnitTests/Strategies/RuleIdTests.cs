using MoneyWay.Domain.Strategies;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class RuleIdTests
{
    [Fact]
    public void ValidValueIsPreserved()
    {
        var identifier = new RuleId("NQ-FVG-001");

        Assert.Equal("NQ-FVG-001", identifier.Value);
        Assert.Equal("NQ-FVG-001", identifier.ToString());
    }

    [Fact]
    public void EqualityUsesValue()
    {
        Assert.Equal(new RuleId("FX-W-001"), new RuleId("FX-W-001"));
        Assert.NotEqual(new RuleId("FX-W-001"), new RuleId("fx-w-001"));
    }

    [Fact]
    public void NullIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleId(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" FX-W-001")]
    [InlineData("FX-W-001 ")]
    public void InvalidTextIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new RuleId(value));
    }
}
