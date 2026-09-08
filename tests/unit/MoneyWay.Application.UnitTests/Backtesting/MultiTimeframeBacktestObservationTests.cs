using MoneyWay.Application.Backtesting;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class MultiTimeframeBacktestObservationTests
{
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);

    [Fact]
    public void ValidObservationPreservesValuesAndDefensivelyCopies()
    {
        var updated = new List<Timeframe> { Minute }; var available = new List<Timeframe> { Minute, Five };
        var value = new MultiTimeframeBacktestObservation(2, AsOf, updated, available); updated.Clear(); available.Clear();
        Assert.Equal(2, value.Step); Assert.Equal(AsOf, value.AsOfUtc); Assert.Equal([Minute], value.UpdatedTimeframes); Assert.Equal([Minute, Five], value.AvailableTimeframes);
    }

    [Fact]
    public void InvalidTemporalAndCollectionStatesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTimeframeBacktestObservation(0, AsOf, [], []));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeBacktestObservation(1, AsOf.ToOffset(TimeSpan.FromHours(-5)), [], []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeBacktestObservation(1, AsOf, null!, []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeBacktestObservation(1, AsOf, [], null!));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeBacktestObservation(1, AsOf, [Minute, Minute], [Minute]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeBacktestObservation(1, AsOf, [], [Minute, Minute]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeBacktestObservation(1, AsOf, [Five], [Minute]));
    }
}
