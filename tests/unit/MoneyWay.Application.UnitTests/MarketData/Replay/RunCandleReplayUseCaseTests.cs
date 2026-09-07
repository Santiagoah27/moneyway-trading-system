using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class RunCandleReplayUseCaseTests
{
    private static readonly MarketDataProviderId ProviderId = new("Provider-A");
    private static readonly MarketSymbol Symbol = new("EUR/USD");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset StartUtc = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly RunCandleReplayUseCase useCase = new();

    [Fact]
    public void NullArgumentsAreRejected()
    {
        var series = CreateSeries([]);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(series, null!));
    }

    [Fact]
    public void EmptySeriesCompletesWithoutInvokingConsumer()
    {
        var invocations = 0;

        var result = useCase.Execute(CreateSeries([]), _ => invocations++);

        Assert.Equal(0, invocations);
        Assert.Equal(0, result.FramesProcessed);
        Assert.Null(result.FirstAsOfUtc);
        Assert.Null(result.LastAsOfUtc);
        Assert.Same(ProviderId, result.ProviderId);
        Assert.Same(Symbol, result.Symbol);
        Assert.Same(Timeframe, result.Timeframe);
    }

    [Fact]
    public void OneCandleProducesOneClosedCandleFrame()
    {
        var candle = CreateCandle(StartUtc);
        ReplayFrame? consumed = null;

        var result = useCase.Execute(CreateSeries([candle]), frame => consumed = frame);

        Assert.NotNull(consumed);
        Assert.Equal(1, consumed.Step);
        Assert.Same(candle, consumed.CurrentCandle);
        Assert.Equal([candle], consumed.AvailableCandles);
        Assert.Equal(1, result.FramesProcessed);
        Assert.Equal(consumed.AsOfUtc, result.FirstAsOfUtc);
        Assert.Equal(consumed.AsOfUtc, result.LastAsOfUtc);
        Assert.Equal(candle.CloseTimeUtc, consumed.AsOfUtc);
    }

    [Fact]
    public void MultipleCandlesAreConsumedOnceInSequentialOrder()
    {
        var candles = CreateThreeCandles();
        var frames = new List<ReplayFrame>();

        var result = useCase.Execute(CreateSeries(candles), frames.Add);

        Assert.Equal(3, frames.Count);
        Assert.Equal([1, 2, 3], frames.Select(static frame => frame.Step));
        Assert.Equal(candles, frames.Select(static frame => frame.CurrentCandle));
        Assert.Equal([1, 2, 3], frames.Select(static frame => frame.AvailableCandles.Count));
        Assert.Equal(3, result.FramesProcessed);
        Assert.Equal(candles[0].CloseTimeUtc, result.FirstAsOfUtc);
        Assert.Equal(candles[2].CloseTimeUtc, result.LastAsOfUtc);
    }

    [Fact]
    public void ConsumerObservesOnlyCurrentPrefix()
    {
        var observedCounts = new List<int>();
        useCase.Execute(CreateSeries(CreateThreeCandles()), frame => observedCounts.Add(frame.AvailableCandles.Count));
        Assert.Equal([1, 2, 3], observedCounts);
    }

    [Fact]
    public void DifferentFutureValuesDoNotAffectPriorFrames()
    {
        var common = CreateThreeCandles();
        var changedFuture = CreateCandle(StartUtc.AddMinutes(10), 1000m, 1100m, 900m, 1050m);
        var framesA = new List<ReplayFrame>();
        var framesB = new List<ReplayFrame>();

        useCase.Execute(CreateSeries(common), framesA.Add);
        useCase.Execute(CreateSeries([common[0], common[1], changedFuture]), framesB.Add);

        AssertEquivalent(framesA[0], framesB[0]);
        AssertEquivalent(framesA[1], framesB[1]);
        Assert.NotEqual(framesA[2].CurrentCandle.Close, framesB[2].CurrentCandle.Close);
    }

    [Fact]
    public void ConsumerExceptionPropagatesAndStopsReplay()
    {
        var expected = new InvalidOperationException("Known consumer failure.");
        var steps = new List<int>();

        var actual = Assert.Throws<InvalidOperationException>(() =>
            useCase.Execute(CreateSeries(CreateThreeCandles()), frame =>
            {
                steps.Add(frame.Step);
                if (frame.Step == 2)
                {
                    throw expected;
                }
            }));

        Assert.Same(expected, actual);
        Assert.Equal([1, 2], steps);
    }

    [Fact]
    public void RepeatedExecutionsStartAtStepOneAndUseIndependentConsumers()
    {
        var series = CreateSeries(CreateThreeCandles());
        var firstRunFrames = new List<ReplayFrame>();
        var secondRunSteps = new List<int>();

        var firstResult = useCase.Execute(series, firstRunFrames.Add);
        var secondResult = useCase.Execute(series, frame => secondRunSteps.Add(frame.Step));

        Assert.Equal([1, 2, 3], firstRunFrames.Select(static frame => frame.Step));
        Assert.Equal([1, 2, 3], secondRunSteps);
        Assert.Equal(3, firstResult.FramesProcessed);
        Assert.Equal(3, secondResult.FramesProcessed);
        Assert.Equal([1, 2, 3], firstRunFrames.Select(static frame => frame.AvailableCandles.Count));
    }

    [Fact]
    public void GapsPassThroughWithoutSyntheticFrames()
    {
        var first = CreateCandle(StartUtc);
        var afterGap = CreateCandle(StartUtc.AddMinutes(15));
        var frames = new List<ReplayFrame>();

        var result = useCase.Execute(CreateSeries([first, afterGap]), frames.Add);

        Assert.Equal(2, result.FramesProcessed);
        Assert.Equal([first, afterGap], frames.Select(static frame => frame.CurrentCandle));
        Assert.Equal([first.CloseTimeUtc, afterGap.CloseTimeUtc], frames.Select(static frame => frame.AsOfUtc));
    }

    [Fact]
    public void SourceSeriesAndExactMetadataArePreserved()
    {
        var candles = CreateThreeCandles();
        var series = CreateSeries(candles);

        var result = useCase.Execute(series, _ => { });

        Assert.Equal(candles, series.Candles);
        Assert.Same(series.ProviderId, result.ProviderId);
        Assert.Same(series.Symbol, result.Symbol);
        Assert.Same(series.Timeframe, result.Timeframe);
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
