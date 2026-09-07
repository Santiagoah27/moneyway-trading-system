using MoneyWay.Domain.MarketData;

namespace MoneyWay.Domain.UnitTests.MarketData;

public sealed class CandleTests
{
    private static readonly MarketDataProviderId ProviderId = new("historical-fixture");
    private static readonly MarketSymbol Symbol = new("EUR/USD");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset OpenTimeUtc = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CloseTimeUtc = new(2026, 9, 1, 10, 5, 0, TimeSpan.Zero);

    [Fact]
    public void ValidValuesArePreserved()
    {
        var candle = Create(open: 100m, high: 110m, low: 95m, close: 108m, volume: 42.125m);

        Assert.Same(ProviderId, candle.ProviderId);
        Assert.Same(Symbol, candle.Symbol);
        Assert.Same(Timeframe, candle.Timeframe);
        Assert.Equal(OpenTimeUtc, candle.OpenTimeUtc);
        Assert.Equal(CloseTimeUtc, candle.CloseTimeUtc);
        Assert.Equal(100m, candle.Open);
        Assert.Equal(110m, candle.High);
        Assert.Equal(95m, candle.Low);
        Assert.Equal(108m, candle.Close);
        Assert.Equal(42.125m, candle.Volume);
    }

    [Theory]
    [InlineData("bullish", 100, 110, 95, 108)]
    [InlineData("bearish", 108, 110, 95, 100)]
    [InlineData("doji", 100, 105, 95, 100)]
    [InlineData("flat", 100, 100, 100, 100)]
    [InlineData("negative", -100, -90, -110, -95)]
    public void StructurallyConsistentOhlcIsAccepted(string _, int open, int high, int low, int close)
    {
        var candle = Create(open: open, high: high, low: low, close: close);

        Assert.Equal(open, candle.Open);
        Assert.Equal(high, candle.High);
        Assert.Equal(low, candle.Low);
        Assert.Equal(close, candle.Close);
    }

    [Theory]
    [InlineData(100, 99, 90, 95)]
    [InlineData(95, 99, 90, 100)]
    [InlineData(100, 110, 101, 105)]
    [InlineData(105, 110, 101, 100)]
    [InlineData(100, 90, 95, 100)]
    public void InconsistentOhlcIsRejected(int open, int high, int low, int close)
    {
        Assert.Throws<ArgumentException>(() => Create(open: open, high: high, low: low, close: close));
    }

    [Fact]
    public void NullMetadataIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Candle(null!, Symbol, Timeframe, OpenTimeUtc, CloseTimeUtc, 100m, 110m, 90m, 105m, null));
        Assert.Throws<ArgumentNullException>(() =>
            new Candle(ProviderId, null!, Timeframe, OpenTimeUtc, CloseTimeUtc, 100m, 110m, 90m, 105m, null));
        Assert.Throws<ArgumentNullException>(() =>
            new Candle(ProviderId, Symbol, null!, OpenTimeUtc, CloseTimeUtc, 100m, 110m, 90m, 105m, null));
    }

    [Fact]
    public void NonUtcOpenTimeIsRejected()
    {
        var timestamp = new DateTimeOffset(2026, 9, 1, 5, 0, 0, TimeSpan.FromHours(-5));
        Assert.Throws<ArgumentException>(() => Create(openTimeUtc: timestamp));
    }

    [Fact]
    public void NonUtcCloseTimeIsRejected()
    {
        var timestamp = new DateTimeOffset(2026, 9, 1, 5, 5, 0, TimeSpan.FromHours(-5));
        Assert.Throws<ArgumentException>(() => Create(closeTimeUtc: timestamp));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonLaterCloseTimeIsRejected(int minuteOffset)
    {
        Assert.Throws<ArgumentException>(() => Create(closeTimeUtc: OpenTimeUtc.AddMinutes(minuteOffset)));
    }

    [Theory]
    [MemberData(nameof(ValidVolumes))]
    public void NonNegativeOrAbsentVolumeIsAccepted(decimal? volume)
    {
        var candle = Create(volume: volume);
        Assert.Equal(volume, candle.Volume);
    }

    [Fact]
    public void NegativeVolumeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(volume: -0.01m));
    }

    [Fact]
    public void InstancesDoNotUseIdentityBasedValueEquality()
    {
        Assert.NotEqual(Create(), Create());
    }

    public static TheoryData<decimal?> ValidVolumes => new()
    {
        null,
        0m,
        10.5m,
    };

    private static Candle Create(
        MarketDataProviderId? providerId = null,
        MarketSymbol? symbol = null,
        Timeframe? timeframe = null,
        DateTimeOffset? openTimeUtc = null,
        DateTimeOffset? closeTimeUtc = null,
        decimal open = 100m,
        decimal high = 110m,
        decimal low = 90m,
        decimal close = 105m,
        decimal? volume = null) =>
        new(providerId ?? ProviderId, symbol ?? Symbol, timeframe ?? Timeframe,
            openTimeUtc ?? OpenTimeUtc, closeTimeUtc ?? CloseTimeUtc, open, high, low, close, volume);
}
