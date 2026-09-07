using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Backtesting.Tests;

public sealed class NeutralBacktestRunTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly GenerateBacktestRunUseCase useCase = new(new RunCandleReplayUseCase());

    [Fact]
    public void FiveCandleScenarioProducesNeutralReproducibleRun()
    {
        var candles = Enumerable.Range(0, 5).Select(index => CandleAt(Start.AddMinutes(index * 5))).ToArray();
        var series = new CandleSeries(Provider, Symbol, Timeframe, candles);
        var first = useCase.Execute(series);
        var second = useCase.Execute(series);

        Assert.Equal(5, first.ObservationCount);
        Assert.Equal([1, 2, 3, 4, 5], first.Observations.Select(x => x.Step));
        Assert.Equal(candles[0].CloseTimeUtc, first.FirstAsOfUtc);
        Assert.Equal(candles[^1].CloseTimeUtc, first.LastAsOfUtc);
        Assert.Same(Provider, first.ProviderId);
        Assert.Same(Symbol, first.Symbol);
        Assert.Same(Timeframe, first.Timeframe);
        Assert.Equal(first.Observations.Select(x => x.AsOfUtc), second.Observations.Select(x => x.AsOfUtc));
    }

    [Fact]
    public void GapProducesOnlyRealCandleObservations()
    {
        var candles = new[] { CandleAt(Start), CandleAt(Start.AddMinutes(15)) };
        var run = useCase.Execute(new CandleSeries(Provider, Symbol, Timeframe, candles));
        Assert.Equal(2, run.ObservationCount);
        Assert.Equal(candles.Select(x => x.CloseTimeUtc), run.Observations.Select(x => x.AsOfUtc));
    }

    private static Candle CandleAt(DateTimeOffset open) =>
        new(Provider, Symbol, Timeframe, open, open.AddMinutes(5), 100, 101, 99, 100.5m, null);
}
