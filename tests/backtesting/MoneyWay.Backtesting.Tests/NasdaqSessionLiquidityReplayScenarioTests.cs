using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Backtesting.Tests;

public sealed class NasdaqSessionLiquidityReplayScenarioTests
{
    private static readonly DateOnly TradingDay = new(2026, 1, 15);
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly TimeZoneInfo Bogota = ResolveBogota();
    private readonly NasdaqSessionLiquidityCalculator calculator = new();

    [Fact]
    public void CanonicalSynchronizedReplayCalculatesSessionLevelsAtLondonCompletion()
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var context = ContextAt(
            LocalUtc(TradingDay, 7),
            new CandleSeries(Provider, Symbol, Hour, CompleteSessionCandles()),
            new CandleSeries(Provider, Symbol, minute,
            [
                CandleAtUtc(LocalUtc(TradingDay, 6, 59), LocalUtc(TradingDay, 7), 50_000m, -50_000m, minute),
            ]));

        var result = calculator.Calculate(context);

        Assert.True(result.IsAvailable);
        Assert.Equal(new NasdaqSessionLiquidityLevels(TradingDay, 500m, 10m, 700m, 20m), result.Levels);
    }

    [Fact]
    public void CanonicalReplayOutputAtSameTimestampIgnoresDifferentFutureCandles()
    {
        var futureA = CandleAt(TradingDay, 7, 1_000m, -1_000m);
        var futureB = CandleAt(TradingDay, 7, 9_000m, -9_000m);
        var contextA = ContextAt(
            LocalUtc(TradingDay, 7),
            new CandleSeries(Provider, Symbol, Hour, CompleteSessionCandles().Append(futureA)));
        var contextB = ContextAt(
            LocalUtc(TradingDay, 7),
            new CandleSeries(Provider, Symbol, Hour, CompleteSessionCandles().Append(futureB)));

        var resultA = calculator.Calculate(contextA);
        var resultB = calculator.Calculate(contextB);

        Assert.Equal(resultA, resultB);
    }

    private static StrategyReplayContext ContextAt(DateTimeOffset asOfUtc, params CandleSeries[] series)
    {
        StrategyReplayContext? context = null;
        var factory = new CreateStrategyReplayContextUseCase();
        new RunMultiTimeframeReplayUseCase().Execute(series, frame =>
        {
            if (frame.AsOfUtc == asOfUtc)
            {
                context = factory.Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame);
            }
        });
        return context ?? throw new InvalidOperationException("The requested replay timestamp was not produced.");
    }

    private static Candle[] CompleteSessionCandles()
    {
        var candles = new List<Candle>();
        for (var hour = 17; hour < 24; hour++)
        {
            candles.Add(CandleAt(TradingDay.AddDays(-1), hour, hour == 17 ? 500m : 200m + hour, 50m));
        }

        candles.Add(CandleAt(TradingDay, 0, 210m, 40m));
        candles.Add(CandleAt(TradingDay, 1, 220m, 10m));
        candles.Add(CandleAt(TradingDay, 2, 700m, 70m));
        candles.Add(CandleAt(TradingDay, 3, 300m, 60m));
        candles.Add(CandleAt(TradingDay, 4, 310m, 50m));
        candles.Add(CandleAt(TradingDay, 5, 320m, 40m));
        candles.Add(CandleAt(TradingDay, 6, 330m, 20m));
        return candles.ToArray();
    }

    private static Candle CandleAt(DateOnly day, int hour, decimal high, decimal low)
    {
        var open = LocalUtc(day, hour);
        return CandleAtUtc(open, open.AddHours(1), high, low, Hour);
    }

    private static Candle CandleAtUtc(
        DateTimeOffset open,
        DateTimeOffset close,
        decimal high,
        decimal low,
        Timeframe timeframe)
    {
        var middle = (high + low) / 2m;
        return new(Provider, Symbol, timeframe, open, close, middle, high, low, middle, null);
    }

    private static DateTimeOffset LocalUtc(DateOnly day, int hour, int minute = 0)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified);
        return new(TimeZoneInfo.ConvertTimeToUtc(local, Bogota), TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveBogota()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId("America/Bogota", out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
