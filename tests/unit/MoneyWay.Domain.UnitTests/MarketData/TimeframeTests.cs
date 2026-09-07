using MoneyWay.Domain.MarketData;

namespace MoneyWay.Domain.UnitTests.MarketData;

public sealed class TimeframeTests
{
    [Theory]
    [InlineData(1, TimeframeUnit.Minute, "1m")]
    [InlineData(5, TimeframeUnit.Minute, "5m")]
    [InlineData(30, TimeframeUnit.Minute, "30m")]
    [InlineData(1, TimeframeUnit.Hour, "1h")]
    [InlineData(2, TimeframeUnit.Hour, "2h")]
    [InlineData(4, TimeframeUnit.Hour, "4h")]
    [InlineData(1, TimeframeUnit.Day, "1d")]
    [InlineData(1, TimeframeUnit.Week, "1w")]
    [InlineData(15, TimeframeUnit.Minute, "15m")]
    [InlineData(3, TimeframeUnit.Hour, "3h")]
    [InlineData(2, TimeframeUnit.Day, "2d")]
    public void ValidTimeframeIsPreservedAndUsesCanonicalString(int amount, TimeframeUnit unit, string expected)
    {
        var timeframe = new Timeframe(amount, unit);

        Assert.Equal(amount, timeframe.Amount);
        Assert.Equal(unit, timeframe.Unit);
        Assert.Equal(expected, timeframe.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveAmountIsRejected(int amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Timeframe(amount, TimeframeUnit.Minute));
    }

    [Fact]
    public void UndefinedUnitIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Timeframe(1, (TimeframeUnit)99));
    }

    [Fact]
    public void EqualityUsesAmountAndUnitWithoutNormalization()
    {
        Assert.Equal(new Timeframe(1, TimeframeUnit.Hour), new Timeframe(1, TimeframeUnit.Hour));
        Assert.NotEqual(new Timeframe(1, TimeframeUnit.Hour), new Timeframe(60, TimeframeUnit.Minute));
    }

    [Fact]
    public void UnitContainsExactVocabulary()
    {
        Assert.Equal(["Minute", "Hour", "Day", "Week"], Enum.GetNames<TimeframeUnit>());
    }
}
