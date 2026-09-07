using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Domain.UnitTests.MarketData.Replay;

public sealed class MultiTimeframeCandleReplayCursorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Timeframe OneMinute = new(1, TimeframeUnit.Minute); private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);

    [Fact]
    public void ConstructorRejectsInvalidCollectionsAndMetadata()
    {
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeCandleReplayCursor(null!));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeCandleReplayCursor([]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeCandleReplayCursor(new CandleSeries[] { null! }));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeCandleReplayCursor([Series(Provider, Symbol, OneMinute), Series(new("other"), Symbol, FiveMinutes)]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeCandleReplayCursor([Series(Provider, Symbol, OneMinute), Series(Provider, new("OTHER"), FiveMinutes)]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeCandleReplayCursor([Series(Provider, Symbol, OneMinute), Series(Provider, Symbol, OneMinute)]));
    }

    [Fact]
    public void ConfiguredTimeframesAreStructurallyOrderedAndExactIdentitiesRemainDistinct()
    {
        var hour = new Timeframe(1, TimeframeUnit.Hour); var sixtyMinutes = new Timeframe(60, TimeframeUnit.Minute);
        var day = new Timeframe(1, TimeframeUnit.Day); var week = new Timeframe(1, TimeframeUnit.Week); var thirty = new Timeframe(30, TimeframeUnit.Minute);
        var cursor = new MultiTimeframeCandleReplayCursor([Series(Provider, Symbol, week), Series(Provider, Symbol, FiveMinutes), Series(Provider, Symbol, hour), Series(Provider, Symbol, thirty), Series(Provider, Symbol, day), Series(Provider, Symbol, sixtyMinutes)]);
        Assert.Equal([FiveMinutes, thirty, sixtyMinutes, hour, day, week], cursor.ConfiguredTimeframes);
        Assert.Contains(hour, cursor.ConfiguredTimeframes); Assert.Contains(sixtyMinutes, cursor.ConfiguredTimeframes);
    }

    [Fact]
    public void AllEmptyCompletesRepeatedlyWhileKeepingMetadata()
    {
        var cursor = new MultiTimeframeCandleReplayCursor([Series(Provider, Symbol, OneMinute), Series(Provider, Symbol, FiveMinutes)]);
        Assert.False(cursor.TryAdvance(out var first)); Assert.Null(first); Assert.False(cursor.TryAdvance(out var second)); Assert.Null(second);
        Assert.Equal(Provider, cursor.ProviderId); Assert.Equal(Symbol, cursor.Symbol); Assert.Equal(0, cursor.StepsCompleted);
    }

    [Fact]
    public void SimultaneousClosesAdvanceAtomicallyAndRetainLatestChildFrames()
    {
        var minute = Series(Provider, Symbol, OneMinute, Candle(OneMinute, 0, 1), Candle(OneMinute, 1, 2), Candle(OneMinute, 4, 5), Candle(OneMinute, 5, 6));
        var five = Series(Provider, Symbol, FiveMinutes, Candle(FiveMinutes, 0, 5));
        var cursor = new MultiTimeframeCandleReplayCursor([five, minute]); var frames = Consume(cursor);
        Assert.Equal([Start.AddMinutes(1), Start.AddMinutes(2), Start.AddMinutes(5), Start.AddMinutes(6)], frames.Select(x => x.AsOfUtc));
        var atomic = frames[2]; Assert.Equal(3, atomic.Step); Assert.Equal([OneMinute, FiveMinutes], atomic.UpdatedTimeframes);
        Assert.Equal(atomic.AsOfUtc, atomic.FramesByTimeframe[OneMinute].AsOfUtc); Assert.Equal(atomic.AsOfUtc, atomic.FramesByTimeframe[FiveMinutes].AsOfUtc);
        Assert.Equal(3, atomic.FramesByTimeframe[OneMinute].AvailableCandles.Count); Assert.Single(atomic.FramesByTimeframe[FiveMinutes].AvailableCandles);
        var later = frames[3]; Assert.Equal([OneMinute], later.UpdatedTimeframes); Assert.Equal(Start.AddMinutes(5), later.FramesByTimeframe[FiveMinutes].AsOfUtc);
    }

    [Fact]
    public void EmptyAndNotStartedTimeframesStayConfiguredButAbsent()
    {
        var minute = Series(Provider, Symbol, OneMinute, Candle(OneMinute, 0, 1));
        var laterFive = Series(Provider, Symbol, FiveMinutes, Candle(FiveMinutes, 60, 65));
        var emptyHour = Series(Provider, Symbol, new(1, TimeframeUnit.Hour));
        var cursor = new MultiTimeframeCandleReplayCursor([emptyHour, laterFive, minute]); cursor.TryAdvance(out var frame);
        Assert.Equal(3, frame!.ConfiguredTimeframes.Count); Assert.Single(frame.FramesByTimeframe); Assert.True(frame.FramesByTimeframe.ContainsKey(OneMinute));
    }

    [Fact]
    public void InputOrderIsDeterministicAndSnapshotsRemainStable()
    {
        var minute = Series(Provider, Symbol, OneMinute, Candle(OneMinute, 0, 1), Candle(OneMinute, 1, 2));
        var five = Series(Provider, Symbol, FiveMinutes, Candle(FiveMinutes, 0, 2));
        var firstCursor = new MultiTimeframeCandleReplayCursor([minute, five]); var secondCursor = new MultiTimeframeCandleReplayCursor([five, minute]);
        firstCursor.TryAdvance(out var saved); var savedKeys = saved!.FramesByTimeframe.Keys.ToArray(); var savedCount = saved.FramesByTimeframe[OneMinute].AvailableCandles.Count;
        var first = new List<MultiTimeframeReplayFrame> { saved }; first.AddRange(Consume(firstCursor)); var second = Consume(secondCursor);
        Assert.Equal(first.Select(Describe), second.Select(Describe)); Assert.Equal(savedKeys, saved.FramesByTimeframe.Keys); Assert.Equal(savedCount, saved.FramesByTimeframe[OneMinute].AvailableCandles.Count);
    }

    [Fact]
    public void FutureOhlcCannotChangeEarlierFramesAndIndependentCursorsDoNotShareState()
    {
        var visible = Candle(OneMinute, 0, 1); var a = Series(Provider, Symbol, OneMinute, visible, Candle(OneMinute, 1, 2, 110)); var b = Series(Provider, Symbol, OneMinute, visible, Candle(OneMinute, 1, 2, 900));
        var cursorA = new MultiTimeframeCandleReplayCursor([a]); var cursorB = new MultiTimeframeCandleReplayCursor([b]); cursorA.TryAdvance(out var frameA); cursorB.TryAdvance(out var frameB);
        Assert.Equal(Describe(frameA!), Describe(frameB!)); cursorA.TryAdvance(out _); Assert.Equal(1, cursorB.StepsCompleted);
        Assert.Equal(2, a.Count); Assert.Equal(2, b.Count);
    }

    private static List<MultiTimeframeReplayFrame> Consume(MultiTimeframeCandleReplayCursor cursor) { var result = new List<MultiTimeframeReplayFrame>(); while (cursor.TryAdvance(out var frame)) result.Add(frame!); return result; }
    private static object Describe(MultiTimeframeReplayFrame frame) => (frame.Step, frame.AsOfUtc, Configured: string.Join(',', frame.ConfiguredTimeframes), Updated: string.Join(',', frame.UpdatedTimeframes), Children: string.Join('|', frame.FramesByTimeframe.Select(x => $"{x.Key}:{x.Value.Step}:{x.Value.AsOfUtc}:{x.Value.CurrentCandle.Close}")));
    private static CandleSeries Series(MarketDataProviderId provider, MarketSymbol symbol, Timeframe timeframe, params Candle[] candles) => new(provider, symbol, timeframe, candles);
    private static Candle Candle(Timeframe timeframe, int openMinute, int closeMinute, decimal close = 100) => new(Provider, Symbol, timeframe, Start.AddMinutes(openMinute), Start.AddMinutes(closeMinute), 100, Math.Max(101, close), Math.Min(99, close), close, null);
}
