using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class CandleReplayRunResultTests
{
    private static readonly MarketDataProviderId ProviderId = new("Provider-A");
    private static readonly MarketSymbol Symbol = new("EUR/USD");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset FirstUtc = new(2026, 9, 1, 10, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastUtc = new(2026, 9, 1, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyResultIsValidAndPreservesMetadata()
    {
        var result = Create(0, null, null);

        Assert.Same(ProviderId, result.ProviderId);
        Assert.Same(Symbol, result.Symbol);
        Assert.Same(Timeframe, result.Timeframe);
        Assert.Equal(0, result.FramesProcessed);
        Assert.Null(result.FirstAsOfUtc);
        Assert.Null(result.LastAsOfUtc);
    }

    [Fact]
    public void NonEmptyResultIsValid()
    {
        var result = Create(3, FirstUtc, LastUtc);

        Assert.Equal(3, result.FramesProcessed);
        Assert.Equal(FirstUtc, result.FirstAsOfUtc);
        Assert.Equal(LastUtc, result.LastAsOfUtc);
    }

    [Fact]
    public void NullMetadataIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CandleReplayRunResult(null!, Symbol, Timeframe, 0, null, null));
        Assert.Throws<ArgumentNullException>(() =>
            new CandleReplayRunResult(ProviderId, null!, Timeframe, 0, null, null));
        Assert.Throws<ArgumentNullException>(() =>
            new CandleReplayRunResult(ProviderId, Symbol, null!, 0, null, null));
    }

    [Fact]
    public void NegativeFrameCountIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(-1, null, null));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EmptyResultRejectsTimestamps(bool hasFirst, bool hasLast)
    {
        Assert.Throws<ArgumentException>(() =>
            Create(0, hasFirst ? FirstUtc : null, hasLast ? LastUtc : null));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void NonEmptyResultRequiresBothTimestamps(bool hasFirst, bool hasLast)
    {
        Assert.Throws<ArgumentException>(() =>
            Create(1, hasFirst ? FirstUtc : null, hasLast ? LastUtc : null));
    }

    [Fact]
    public void NonUtcFirstTimestampIsRejected()
    {
        var nonUtc = new DateTimeOffset(2026, 9, 1, 5, 5, 0, TimeSpan.FromHours(-5));
        Assert.Throws<ArgumentException>(() => Create(1, nonUtc, LastUtc));
    }

    [Fact]
    public void NonUtcLastTimestampIsRejected()
    {
        var nonUtc = new DateTimeOffset(2026, 9, 1, 5, 15, 0, TimeSpan.FromHours(-5));
        Assert.Throws<ArgumentException>(() => Create(1, FirstUtc, nonUtc));
    }

    [Fact]
    public void LastTimestampBeforeFirstIsRejected() =>
        Assert.Throws<ArgumentException>(() => Create(2, LastUtc, FirstUtc));

    private static CandleReplayRunResult Create(
        int framesProcessed,
        DateTimeOffset? firstAsOfUtc,
        DateTimeOffset? lastAsOfUtc) =>
        new(ProviderId, Symbol, Timeframe, framesProcessed, firstAsOfUtc, lastAsOfUtc);
}
