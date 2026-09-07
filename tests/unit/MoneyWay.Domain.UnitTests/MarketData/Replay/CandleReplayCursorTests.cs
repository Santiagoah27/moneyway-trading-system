using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Domain.UnitTests.MarketData.Replay;

public sealed class CandleReplayCursorTests
{
    private static readonly MarketDataProviderId ProviderId = new("historical-fixture");
    private static readonly MarketSymbol Symbol = new("EUR/USD");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset StartUtc = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptySeriesImmediatelyAndRepeatedlyReturnsEnd()
    {
        var cursor = new CandleReplayCursor(new CandleSeries(ProviderId, Symbol, Timeframe, []));
        Assert.False(cursor.TryAdvance(out var first));
        Assert.Null(first);
        Assert.False(cursor.TryAdvance(out var repeated));
        Assert.Null(repeated);
        Assert.Equal(0, cursor.StepsCompleted);
    }

    [Fact]
    public void NullSeriesIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new CandleReplayCursor(null!));

    [Fact]
    public void FirstFrameContainsOnlyFirstClosedCandle()
    {
        var candles = CreateThreeCandles();
        var cursor = new CandleReplayCursor(CreateSeries(candles));

        Assert.True(cursor.TryAdvance(out var frame));

        Assert.NotNull(frame);
        Assert.Equal(1, frame.Step);
        Assert.Same(ProviderId, frame.ProviderId);
        Assert.Same(Symbol, frame.Symbol);
        Assert.Same(Timeframe, frame.Timeframe);
        Assert.Same(candles[0], frame.CurrentCandle);
        Assert.Equal(candles[0].CloseTimeUtc, frame.AsOfUtc);
        Assert.Equal([candles[0]], frame.AvailableCandles);
        Assert.DoesNotContain(candles[1], frame.AvailableCandles);
        Assert.DoesNotContain(candles[2], frame.AvailableCandles);
    }

    [Fact]
    public void SecondFrameContainsExactlyFirstTwoCandles()
    {
        var candles = CreateThreeCandles();
        var cursor = new CandleReplayCursor(CreateSeries(candles));
        cursor.TryAdvance(out _);

        Assert.True(cursor.TryAdvance(out var frame));

        Assert.Equal(2, frame!.Step);
        Assert.Same(candles[1], frame.CurrentCandle);
        Assert.Equal(candles[1].CloseTimeUtc, frame.AsOfUtc);
        Assert.Equal(candles.Take(2), frame.AvailableCandles);
        Assert.DoesNotContain(candles[2], frame.AvailableCandles);
    }

    [Fact]
    public void CompleteReplayProducesEveryCandleOnceAndThenRemainsEnded()
    {
        var candles = CreateThreeCandles();
        var cursor = new CandleReplayCursor(CreateSeries(candles));
        var frames = Consume(cursor);

        Assert.Equal([1, 2, 3], frames.Select(static frame => frame.Step));
        Assert.Equal(candles, frames.Select(static frame => frame.CurrentCandle));
        Assert.Equal(3, cursor.StepsCompleted);
        Assert.False(cursor.TryAdvance(out var firstEnd));
        Assert.Null(firstEnd);
        Assert.False(cursor.TryAdvance(out var repeatedEnd));
        Assert.Null(repeatedEnd);
    }

    [Fact]
    public void PreviousFrameRemainsStableAfterReplayAdvances()
    {
        var candles = CreateThreeCandles();
        var cursor = new CandleReplayCursor(CreateSeries(candles));
        cursor.TryAdvance(out var firstFrame);

        Consume(cursor);

        Assert.Equal(1, firstFrame!.Step);
        Assert.Equal([candles[0]], firstFrame.AvailableCandles);
        Assert.Same(candles[0], firstFrame.CurrentCandle);
    }

    [Fact]
    public void FrameCollectionIsReadOnly()
    {
        var cursor = new CandleReplayCursor(CreateSeries(CreateThreeCandles()));
        cursor.TryAdvance(out var frame);
        Assert.False(frame!.AvailableCandles is List<Candle>);
        var collection = Assert.IsAssignableFrom<ICollection<Candle>>(frame.AvailableCandles);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    [Fact]
    public void DifferentFutureValuesDoNotAffectEarlierFrames()
    {
        var common = CreateThreeCandles();
        var changedFuture = CreateCandle(StartUtc.AddMinutes(10), 1000m, 1100m, 900m, 1050m);
        var cursorA = new CandleReplayCursor(CreateSeries(common));
        var cursorB = new CandleReplayCursor(CreateSeries([common[0], common[1], changedFuture]));

        cursorA.TryAdvance(out var firstA);
        cursorB.TryAdvance(out var firstB);
        cursorA.TryAdvance(out var secondA);
        cursorB.TryAdvance(out var secondB);
        cursorA.TryAdvance(out var thirdA);
        cursorB.TryAdvance(out var thirdB);

        AssertEquivalent(firstA!, firstB!);
        AssertEquivalent(secondA!, secondB!);
        Assert.NotEqual(thirdA!.CurrentCandle.Close, thirdB!.CurrentCandle.Close);
    }

    [Fact]
    public void GapsArePreservedWithoutSyntheticCandles()
    {
        var first = CreateCandle(StartUtc);
        var afterGap = CreateCandle(StartUtc.AddMinutes(15));
        var frames = Consume(new CandleReplayCursor(CreateSeries([first, afterGap])));

        Assert.Equal(2, frames.Count);
        Assert.Same(first, frames[0].CurrentCandle);
        Assert.Same(afterGap, frames[1].CurrentCandle);
        Assert.Equal(afterGap.CloseTimeUtc, frames[1].AsOfUtc);
    }

    [Fact]
    public void CursorsOverSameSeriesAdvanceIndependently()
    {
        var series = CreateSeries(CreateThreeCandles());
        var cursorA = new CandleReplayCursor(series);
        var cursorB = new CandleReplayCursor(series);
        cursorA.TryAdvance(out _);
        cursorA.TryAdvance(out _);

        Assert.Equal(2, cursorA.StepsCompleted);
        Assert.Equal(0, cursorB.StepsCompleted);
        Assert.True(cursorB.TryAdvance(out var firstFrameB));
        Assert.Equal(1, firstFrameB!.Step);
    }

    [Fact]
    public void ReplayDoesNotModifySourceSeries()
    {
        var candles = CreateThreeCandles();
        var series = CreateSeries(candles);
        Consume(new CandleReplayCursor(series));
        Assert.Equal(candles, series.Candles);
    }

    [Fact]
    public void EveryFrameMaintainsChronologyAndClosedCandleSemantics()
    {
        var frames = Consume(new CandleReplayCursor(CreateSeries(CreateThreeCandles())));

        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            Assert.Equal(index + 1, frame.Step);
            Assert.Same(frame.CurrentCandle, frame.AvailableCandles[^1]);
            Assert.Equal(frame.CurrentCandle.CloseTimeUtc, frame.AsOfUtc);
            Assert.All(frame.AvailableCandles, candle => Assert.True(candle.CloseTimeUtc <= frame.AsOfUtc));
            if (index > 0)
            {
                Assert.True(frame.AsOfUtc >= frames[index - 1].AsOfUtc);
            }
        }

        Assert.Equal(StartUtc.AddMinutes(5), frames[0].AsOfUtc);
        Assert.NotEqual(StartUtc, frames[0].AsOfUtc);
    }

    private static void AssertEquivalent(ReplayFrame expected, ReplayFrame actual)
    {
        Assert.Equal(expected.Step, actual.Step);
        Assert.Equal(expected.AsOfUtc, actual.AsOfUtc);
        Assert.Same(expected.CurrentCandle, actual.CurrentCandle);
        Assert.Equal(expected.AvailableCandles, actual.AvailableCandles);
        Assert.Equal(expected.ProviderId, actual.ProviderId);
        Assert.Equal(expected.Symbol, actual.Symbol);
        Assert.Equal(expected.Timeframe, actual.Timeframe);
    }

    private static List<ReplayFrame> Consume(CandleReplayCursor cursor)
    {
        var frames = new List<ReplayFrame>();
        while (cursor.TryAdvance(out var frame))
        {
            frames.Add(frame!);
        }

        return frames;
    }

    private static Candle[] CreateThreeCandles() =>
    [
        CreateCandle(StartUtc),
        CreateCandle(StartUtc.AddMinutes(5)),
        CreateCandle(StartUtc.AddMinutes(10)),
    ];

    private static CandleSeries CreateSeries(IEnumerable<Candle> candles) =>
        new(ProviderId, Symbol, Timeframe, candles);

    private static Candle CreateCandle(
        DateTimeOffset openTimeUtc,
        decimal open = 100m,
        decimal high = 110m,
        decimal low = 90m,
        decimal close = 105m) =>
        new(ProviderId, Symbol, Timeframe, openTimeUtc, openTimeUtc.AddMinutes(5),
            open, high, low, close, 10m);
}
