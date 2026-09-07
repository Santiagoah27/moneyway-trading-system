using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class BacktestModelTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidObservationPreservesReplayValues()
    {
        var candle = CandleAt(Start);
        var observation = new BacktestObservation(1, candle.CloseTimeUtc, candle);
        Assert.Equal(1, observation.Step);
        Assert.Equal(candle.CloseTimeUtc, observation.AsOfUtc);
        Assert.Same(candle, observation.CurrentCandle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ObservationRejectsInvalidStep(int step)
    {
        var candle = CandleAt(Start);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BacktestObservation(step, candle.CloseTimeUtc, candle));
    }

    [Fact]
    public void ObservationRejectsNullCandle() =>
        Assert.Throws<ArgumentNullException>(() => new BacktestObservation(1, Start, null!));

    [Fact]
    public void ObservationRejectsNonUtcAndMismatchedTimestamp()
    {
        var candle = CandleAt(Start);
        Assert.Throws<ArgumentException>(() => new BacktestObservation(1, candle.CloseTimeUtc.ToOffset(TimeSpan.FromHours(-5)), candle));
        Assert.Throws<ArgumentException>(() => new BacktestObservation(1, candle.CloseTimeUtc.AddMinutes(1), candle));
    }

    [Fact]
    public void EmptyRunIsValid()
    {
        var run = new BacktestRun(Provider, Symbol, Timeframe, []);
        Assert.Equal(0, run.ObservationCount);
        Assert.Null(run.FirstAsOfUtc);
        Assert.Null(run.LastAsOfUtc);
    }

    [Fact]
    public void RunPreservesMetadataSequenceAndDefensiveCopy()
    {
        var source = new List<BacktestObservation> { Observation(1, Start), Observation(2, Start.AddMinutes(5)) };
        var run = new BacktestRun(Provider, Symbol, Timeframe, source);
        source.Clear();

        Assert.Same(Provider, run.ProviderId);
        Assert.Same(Symbol, run.Symbol);
        Assert.Same(Timeframe, run.Timeframe);
        Assert.Equal(2, run.ObservationCount);
        Assert.Equal(Start.AddMinutes(5), run.FirstAsOfUtc);
        Assert.Equal(Start.AddMinutes(10), run.LastAsOfUtc);
    }

    [Fact]
    public void RunRejectsNullCollectionAndElement()
    {
        Assert.Throws<ArgumentNullException>(() => new BacktestRun(Provider, Symbol, Timeframe, null!));
        Assert.Throws<ArgumentException>(() => new BacktestRun(Provider, Symbol, Timeframe, new BacktestObservation[] { null! }));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("timeframe")]
    public void RunRejectsMetadataMismatch(string field)
    {
        var provider = field == "provider" ? new MarketDataProviderId("other") : Provider;
        var symbol = field == "symbol" ? new MarketSymbol("OTHER") : Symbol;
        var timeframe = field == "timeframe" ? new Timeframe(15, TimeframeUnit.Minute) : Timeframe;
        var candle = new Candle(provider, symbol, timeframe, Start, Start.AddMinutes(5), 100, 101, 99, 100, null);
        var observation = new BacktestObservation(1, candle.CloseTimeUtc, candle);
        Assert.Throws<ArgumentException>(() => new BacktestRun(Provider, Symbol, Timeframe, [observation]));
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(1, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void RunRejectsInvalidStepSequences(int first, int second)
    {
        Assert.Throws<ArgumentException>(() => new BacktestRun(Provider, Symbol, Timeframe,
            [Observation(first, Start), Observation(second, Start.AddMinutes(5))]));
    }

    [Fact]
    public void RunRejectsNonIncreasingChronologyButAllowsGaps()
    {
        var later = Observation(1, Start.AddMinutes(5));
        var earlier = Observation(2, Start);
        Assert.Throws<ArgumentException>(() => new BacktestRun(Provider, Symbol, Timeframe, [later, earlier]));

        var gapRun = new BacktestRun(Provider, Symbol, Timeframe, [Observation(1, Start), Observation(2, Start.AddMinutes(15))]);
        Assert.Equal(2, gapRun.ObservationCount);
    }

    [Fact]
    public void UseCaseHandlesEmptyOneMultipleAndRepeatedRuns()
    {
        var useCase = new GenerateBacktestRunUseCase(new RunCandleReplayUseCase());
        Assert.Equal(0, useCase.Execute(Series([])).ObservationCount);
        Assert.Equal(1, useCase.Execute(Series([CandleAt(Start)])).ObservationCount);
        var candles = new[] { CandleAt(Start), CandleAt(Start.AddMinutes(5)), CandleAt(Start.AddMinutes(10)) };
        var series = Series(candles);
        var first = useCase.Execute(series);
        var second = useCase.Execute(series);

        Assert.Equal([1, 2, 3], first.Observations.Select(x => x.Step));
        Assert.Equal(candles, first.Observations.Select(x => x.CurrentCandle));
        Assert.All(first.Observations, x => Assert.Equal(x.CurrentCandle.CloseTimeUtc, x.AsOfUtc));
        Assert.Equal(first.Observations.Select(x => x.AsOfUtc), second.Observations.Select(x => x.AsOfUtc));
        Assert.Equal(candles, series.Candles);
        Assert.Same(Provider, first.ProviderId);
    }

    [Fact]
    public void FutureDifferenceDoesNotAffectEarlierObservations()
    {
        var common = new[] { CandleAt(Start), CandleAt(Start.AddMinutes(5)) };
        var useCase = new GenerateBacktestRunUseCase(new RunCandleReplayUseCase());
        var a = useCase.Execute(Series([.. common, CandleAt(Start.AddMinutes(10), 105)]));
        var b = useCase.Execute(Series([.. common, CandleAt(Start.AddMinutes(10), 999)]));

        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(a.Observations[index].Step, b.Observations[index].Step);
            Assert.Equal(a.Observations[index].AsOfUtc, b.Observations[index].AsOfUtc);
            Assert.Same(a.Observations[index].CurrentCandle, b.Observations[index].CurrentCandle);
        }
        Assert.NotEqual(a.Observations[2].CurrentCandle.Close, b.Observations[2].CurrentCandle.Close);
    }

    [Fact]
    public void UseCaseRejectsNullDependenciesAndSeries()
    {
        Assert.Throws<ArgumentNullException>(() => new GenerateBacktestRunUseCase(null!));
        Assert.Throws<ArgumentNullException>(() => new GenerateBacktestRunUseCase(new RunCandleReplayUseCase()).Execute(null!));
    }

    private static BacktestObservation Observation(int step, DateTimeOffset open) =>
        new(step, open.AddMinutes(5), CandleAt(open));

    private static CandleSeries Series(IEnumerable<Candle> candles) => new(Provider, Symbol, Timeframe, candles);

    private static Candle CandleAt(DateTimeOffset open, decimal close = 100) =>
        new(Provider, Symbol, Timeframe, open, open.AddMinutes(5), 100, Math.Max(101, close), 99, close, null);
}
