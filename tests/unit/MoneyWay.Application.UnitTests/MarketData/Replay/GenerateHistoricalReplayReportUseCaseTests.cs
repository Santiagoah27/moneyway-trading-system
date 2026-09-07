using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class GenerateHistoricalReplayReportUseCaseTests
{
    private static readonly MarketDataProviderId ProviderId = new("historical-fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset StartUtc = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly GenerateHistoricalReplayReportUseCase useCase = new();

    [Fact]
    public void NullSeriesIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!));

    [Fact]
    public void EmptySeriesProducesEmptyReport()
    {
        var report = useCase.Execute(CreateSeries([]));

        Assert.Equal(0, report.CandleCount);
        Assert.Equal(0, report.FramesProcessed);
        Assert.Null(report.DatasetStartTimeUtc);
        Assert.Null(report.DatasetEndTimeUtc);
        Assert.Null(report.FirstFrameAsOfUtc);
        Assert.Null(report.LastFrameAsOfUtc);
    }

    [Fact]
    public void OneCandleDistinguishesDatasetStartFromFirstFrameTime()
    {
        var candle = CreateCandle(StartUtc);

        var report = useCase.Execute(CreateSeries([candle]));

        Assert.Equal(1, report.CandleCount);
        Assert.Equal(1, report.FramesProcessed);
        Assert.Equal(candle.OpenTimeUtc, report.DatasetStartTimeUtc);
        Assert.Equal(candle.CloseTimeUtc, report.DatasetEndTimeUtc);
        Assert.Equal(candle.CloseTimeUtc, report.FirstFrameAsOfUtc);
        Assert.Equal(candle.CloseTimeUtc, report.LastFrameAsOfUtc);
        Assert.NotEqual(report.DatasetStartTimeUtc, report.FirstFrameAsOfUtc);
    }

    [Fact]
    public void MultipleCandlesProduceReplayAndDatasetBoundaries()
    {
        var candles = CreateCandles();

        var report = useCase.Execute(CreateSeries(candles));

        Assert.Equal(3, report.CandleCount);
        Assert.Equal(3, report.FramesProcessed);
        Assert.Equal(candles[0].OpenTimeUtc, report.DatasetStartTimeUtc);
        Assert.Equal(candles[^1].CloseTimeUtc, report.DatasetEndTimeUtc);
        Assert.Equal(candles[0].CloseTimeUtc, report.FirstFrameAsOfUtc);
        Assert.Equal(candles[^1].CloseTimeUtc, report.LastFrameAsOfUtc);
    }

    [Fact]
    public void GapsProduceNoSyntheticFrames()
    {
        var candles = new[] { CreateCandle(StartUtc), CreateCandle(StartUtc.AddMinutes(15)) };
        var report = useCase.Execute(CreateSeries(candles));

        Assert.Equal(2, report.CandleCount);
        Assert.Equal(2, report.FramesProcessed);
        Assert.Equal(candles[^1].CloseTimeUtc, report.LastFrameAsOfUtc);
    }

    [Fact]
    public void MetadataIsPreserved()
    {
        var report = useCase.Execute(CreateSeries(CreateCandles()));

        Assert.Same(ProviderId, report.ProviderId);
        Assert.Same(Symbol, report.Symbol);
        Assert.Same(Timeframe, report.Timeframe);
    }

    [Fact]
    public void RepeatedExecutionIsEquivalentAndDoesNotModifySeries()
    {
        var candles = CreateCandles();
        var series = CreateSeries(candles);

        var first = useCase.Execute(series);
        var second = useCase.Execute(series);

        Assert.Equal(first.CandleCount, second.CandleCount);
        Assert.Equal(first.DatasetStartTimeUtc, second.DatasetStartTimeUtc);
        Assert.Equal(first.DatasetEndTimeUtc, second.DatasetEndTimeUtc);
        Assert.Equal(first.FramesProcessed, second.FramesProcessed);
        Assert.Equal(first.FirstFrameAsOfUtc, second.FirstFrameAsOfUtc);
        Assert.Equal(first.LastFrameAsOfUtc, second.LastFrameAsOfUtc);
        Assert.Equal(candles, series.Candles);
    }

    private static Candle[] CreateCandles() =>
    [
        CreateCandle(StartUtc),
        CreateCandle(StartUtc.AddMinutes(5)),
        CreateCandle(StartUtc.AddMinutes(10)),
    ];

    private static CandleSeries CreateSeries(IEnumerable<Candle> candles) =>
        new(ProviderId, Symbol, Timeframe, candles);

    private static Candle CreateCandle(DateTimeOffset openTimeUtc) =>
        new(
            ProviderId,
            Symbol,
            Timeframe,
            openTimeUtc,
            openTimeUtc.AddMinutes(5),
            100m,
            101m,
            99m,
            100.5m,
            1000m);
}
