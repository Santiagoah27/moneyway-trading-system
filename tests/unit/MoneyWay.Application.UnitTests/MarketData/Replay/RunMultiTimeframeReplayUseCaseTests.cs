using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class RunMultiTimeframeReplayUseCaseTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ResultValidatesShapeAndDefensivelyCopiesConfiguredTimeframes()
    {
        var configured = new List<Timeframe> { Minute, Five }; var result = new MultiTimeframeReplayRunResult(Provider, Symbol, configured, 0, null, null); configured.Clear();
        Assert.Equal([Minute, Five], result.ConfiguredTimeframes); Assert.Equal(0, result.GlobalFramesProcessed); Assert.Null(result.FirstAsOfUtc); Assert.Null(result.LastAsOfUtc);
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, null!, 0, null, null));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [], 0, null, null));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute, Minute], 0, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], -1, null, null));
    }

    [Fact]
    public void ResultValidatesTimestampState()
    {
        var end = Start.AddMinutes(5); var result = new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 2, Start, end);
        Assert.Equal(Start, result.FirstAsOfUtc); Assert.Equal(end, result.LastAsOfUtc);
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 0, Start, null));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 0, null, end));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 1, null, end));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 1, Start, null));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 1, Start.ToOffset(TimeSpan.FromHours(-5)), end));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 1, Start, end.ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeReplayRunResult(Provider, Symbol, [Minute], 1, end, Start));
    }

    [Fact]
    public void RunnerRejectsNullsAndDelegatesCollectionValidationToDomain()
    {
        var useCase = new RunMultiTimeframeReplayUseCase();
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute([Series(Minute)], null!));
        Assert.Throws<ArgumentException>(() => useCase.Execute([], _ => { }));
    }

    [Fact]
    public void AllEmptySeriesPreserveConfigurationAndProduceNoFrames()
    {
        var frames = new List<MultiTimeframeReplayFrame>(); var result = new RunMultiTimeframeReplayUseCase().Execute([Series(Five), Series(Minute)], frames.Add);
        Assert.Empty(frames); Assert.Equal(0, result.GlobalFramesProcessed); Assert.Equal([Minute, Five], result.ConfiguredTimeframes); Assert.Null(result.FirstAsOfUtc); Assert.Null(result.LastAsOfUtc);
    }

    [Fact]
    public void SimultaneousClosesAreOneGlobalEventAndGapsRemainGaps()
    {
        var minute = Series(Minute, 1, 2, 3, 4, 5); var five = Series(Five, 5); var frames = new List<MultiTimeframeReplayFrame>();
        var result = new RunMultiTimeframeReplayUseCase().Execute([minute, five], frames.Add);
        Assert.Equal(5, result.GlobalFramesProcessed); Assert.Equal(5, frames.Count); Assert.Equal(Enumerable.Range(1, 5), frames.Select(x => x.Step));
        Assert.Equal(Start.AddMinutes(1), result.FirstAsOfUtc); Assert.Equal(Start.AddMinutes(5), result.LastAsOfUtc);
        Assert.Equal([Minute, Five], frames[^1].UpdatedTimeframes); Assert.Equal(2, frames[^1].FramesByTimeframe.Count);
    }

    [Fact]
    public void InputOrderDoesNotChangeObservableReplayAndSourcesRemainUnchanged()
    {
        var minute = Series(Minute, 1, 3, 6); var five = Series(Five, 5); var before = minute.Candles.ToArray();
        var first = Capture([minute, five]); var second = Capture([five, minute]);
        Assert.Equal(first.Select(Signature), second.Select(Signature)); Assert.Equal(before, minute.Candles); Assert.Equal(3, minute.Count);
    }

    [Fact]
    public void CallbackExceptionPropagatesAndStopsReplay()
    {
        var calls = 0; var error = new InvalidDataException("stop");
        Assert.Same(error, Assert.Throws<InvalidDataException>(() => new RunMultiTimeframeReplayUseCase().Execute([Series(Minute, 1, 2, 3)], _ => { calls++; if (calls == 2) throw error; })));
        Assert.Equal(2, calls);
    }

    private static List<MultiTimeframeReplayFrame> Capture(CandleSeries[] series) { var frames = new List<MultiTimeframeReplayFrame>(); new RunMultiTimeframeReplayUseCase().Execute(series, frames.Add); return frames; }
    private static object Signature(MultiTimeframeReplayFrame frame) => (frame.Step, frame.AsOfUtc, string.Join(',', frame.UpdatedTimeframes), string.Join(',', frame.FramesByTimeframe.Keys));
    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => new(Provider, Symbol, timeframe, closes.Select(close => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(close - 1), Start.AddMinutes(close), 100, 101, 99, 100, null)));
}
