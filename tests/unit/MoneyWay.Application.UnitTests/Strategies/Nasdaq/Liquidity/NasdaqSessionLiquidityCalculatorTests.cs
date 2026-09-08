using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.Liquidity;

public sealed class NasdaqSessionLiquidityCalculatorTests
{
    private static readonly DateOnly TradingDay = new(2026, 1, 15);
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly TimeZoneInfo Bogota = ResolveBogota();
    private readonly NasdaqSessionLiquidityCalculator calculator = new();

    [Fact]
    public void ResultAvailabilityIsDerivedFromLevelsAndReasonIsValidated()
    {
        var levels = new NasdaqSessionLiquidityLevels(TradingDay, 500m, 10m, 700m, 20m);
        var available = new NasdaqSessionLiquidityCalculationResult(levels, "Available.");
        var unavailable = new NasdaqSessionLiquidityCalculationResult(null, "Unavailable.");

        Assert.True(available.IsAvailable);
        Assert.Same(levels, available.Levels);
        Assert.Equal("Available.", available.Reason);
        Assert.False(unavailable.IsAvailable);
        Assert.Null(unavailable.Levels);
        Assert.Equal("Unavailable.", unavailable.Reason);
        Assert.Throws<ArgumentNullException>(() => new NasdaqSessionLiquidityCalculationResult(null, null!));
        Assert.Throws<ArgumentException>(() => new NasdaqSessionLiquidityCalculationResult(null, " "));
        Assert.Throws<ArgumentException>(() => new NasdaqSessionLiquidityCalculationResult(null, " reason"));
    }

    [Fact]
    public void CalculatesCompletedSessionsUsingExactBoundariesAndIgnoresObservableOutsideCandles()
    {
        var candles = CompleteSessionCandles(TradingDay).Concat(
        [
            CandleAt(TradingDay.AddDays(-1), 16, 9_000m, -9_000m),
            CandleAt(TradingDay, 7, 8_000m, -8_000m),
            CandleAt(TradingDay, 8, 7_000m, -7_000m),
        ]).OrderBy(candle => candle.OpenTimeUtc).ToArray();
        var context = ContextAt(LocalUtc(TradingDay, 9), Series(Hour, candles));

        var result = calculator.Calculate(context);

        Assert.True(result.IsAvailable);
        Assert.Equal(NasdaqSessionLiquidityCalculator.AvailableReason, result.Reason);
        Assert.Equal(new NasdaqSessionLiquidityLevels(TradingDay, 500m, 10m, 700m, 20m), result.Levels);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 59)]
    public void ReturnsUnavailableBeforeLondonSessionIsComplete(int localHour, int localMinute)
    {
        var asOf = LocalUtc(TradingDay, localHour, localMinute);
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var anchor = Series(minute, CandleAtUtc(asOf.AddMinutes(-1), asOf, 100m, 90m, minute));
        var context = ContextAt(asOf, Series(Hour, CompleteSessionCandles(TradingDay)), anchor);

        var result = calculator.Calculate(context);

        Assert.False(result.IsAvailable);
        Assert.Null(result.Levels);
        Assert.Equal(NasdaqSessionLiquidityCalculator.SessionNotCompletedReason, result.Reason);
    }

    [Fact]
    public void RequiresExactOneHourConfigurationWithoutDurationNormalization()
    {
        var sixtyMinutes = new Timeframe(60, TimeframeUnit.Minute);
        var context = ContextAt(
            LocalUtc(TradingDay, 7),
            Series(sixtyMinutes, CandleAt(TradingDay, 6, 100m, 90m, sixtyMinutes)));

        var result = calculator.Calculate(context);

        Assert.False(result.IsAvailable);
        Assert.Equal(NasdaqSessionLiquidityCalculator.ExactHourNotConfiguredReason, result.Reason);
    }

    [Fact]
    public void ReturnsUnavailableWhenExactHourIsConfiguredButNotYetObservable()
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var context = ContextAt(
            LocalUtc(TradingDay, 7),
            Series(Hour, CandleAt(TradingDay, 7, 100m, 90m)),
            Series(minute, Candle(TradingDay, 6, 59, 7, 0, 100m, 90m, minute)));

        var result = calculator.Calculate(context);

        Assert.False(result.IsAvailable);
        Assert.Equal(NasdaqSessionLiquidityCalculator.ExactHourFrameUnavailableReason, result.Reason);
    }

    [Theory]
    [InlineData(-1, 21)]
    [InlineData(0, 4)]
    public void RejectsIncompleteSessionsWithoutPartialExtrema(int dayOffset, int localHour)
    {
        var missingOpen = LocalUtc(TradingDay.AddDays(dayOffset), localHour);
        var candles = CompleteSessionCandles(TradingDay)
            .Where(candle => candle.OpenTimeUtc != missingOpen)
            .ToArray();
        var context = ContextAt(LocalUtc(TradingDay, 7), Series(Hour, candles));

        var result = calculator.Calculate(context);

        Assert.False(result.IsAvailable);
        Assert.Null(result.Levels);
        Assert.Equal(NasdaqSessionLiquidityCalculator.IncompleteSessionDataReason, result.Reason);
    }

    [Fact]
    public void SelectsEachTradingDayIndependentlyOfOlderSessionsAndCallOrder()
    {
        var nextDay = TradingDay.AddDays(1);
        var older = CompleteSessionCandles(TradingDay.AddDays(-1), 10_000m);
        var currentContext = ContextAt(
            LocalUtc(TradingDay, 7),
            Series(Hour, older.Concat(CompleteSessionCandles(TradingDay)).OrderBy(candle => candle.OpenTimeUtc).ToArray()));
        var nextContext = ContextAt(LocalUtc(nextDay, 7), Series(Hour, CompleteSessionCandles(nextDay, 1_000m)));

        var nextFirst = calculator.Calculate(nextContext);
        var currentSecond = calculator.Calculate(currentContext);
        var currentAgain = calculator.Calculate(currentContext);
        var nextAgain = calculator.Calculate(nextContext);

        Assert.Equal(TradingDay, currentSecond.Levels!.TradingDay);
        Assert.Equal(new NasdaqSessionLiquidityLevels(TradingDay, 500m, 10m, 700m, 20m), currentSecond.Levels);
        Assert.Equal(nextDay, nextFirst.Levels!.TradingDay);
        Assert.Equal(new NasdaqSessionLiquidityLevels(nextDay, 1_500m, 1_010m, 1_700m, 1_020m), nextFirst.Levels);
        Assert.Equal(currentSecond, currentAgain);
        Assert.Equal(nextFirst, nextAgain);
    }

    [Fact]
    public void FutureAndUnrelatedTimeframesCannotChangeOutputOrMutateObservableHistory()
    {
        var asOf = LocalUtc(TradingDay, 7);
        var futureA = CandleAt(TradingDay, 7, 1_000m, -1_000m);
        var futureB = CandleAt(TradingDay, 7, 9_000m, -9_000m);
        var contextA = ContextAt(asOf, Series(Hour, CompleteSessionCandles(TradingDay).Append(futureA).ToArray()));
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var fiveMinutes = new Timeframe(5, TimeframeUnit.Minute);
        var fourHours = new Timeframe(4, TimeframeUnit.Hour);
        var contextB = ContextAt(
            asOf,
            Series(Hour, CompleteSessionCandles(TradingDay).Append(futureB).ToArray()),
            Series(minute, Candle(TradingDay, 6, 59, 7, 0, 90_000m, -90_000m, minute)),
            Series(fiveMinutes, Candle(TradingDay, 6, 55, 7, 0, 80_000m, -80_000m, fiveMinutes)),
            Series(fourHours, Candle(TradingDay, 3, 0, 7, 0, 70_000m, -70_000m, fourHours)));
        contextA.TryGetFrame(Hour, out var frame);
        var before = frame!.AvailableCandles.ToArray();

        var resultA = calculator.Calculate(contextA);
        var resultB = calculator.Calculate(contextB);

        Assert.Equal(resultA.IsAvailable, resultB.IsAvailable);
        Assert.Equal(resultA.Levels, resultB.Levels);
        Assert.Equal(resultA.Reason, resultB.Reason);
        Assert.Equal(before, frame.AvailableCandles);
    }

    [Fact]
    public void RejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => calculator.Calculate(null!));
    }

    private static Candle[] CompleteSessionCandles(DateOnly tradingDay, decimal adjustment = 0m)
    {
        var candles = new List<Candle>();

        for (var hour = 17; hour < 24; hour++)
        {
            var high = hour == 17 ? 500m + adjustment : 200m + adjustment + hour;
            candles.Add(CandleAt(tradingDay.AddDays(-1), hour, high, 50m + adjustment));
        }

        candles.Add(CandleAt(tradingDay, 0, 210m + adjustment, 40m + adjustment));
        candles.Add(CandleAt(tradingDay, 1, 220m + adjustment, 10m + adjustment));
        candles.Add(CandleAt(tradingDay, 2, 700m + adjustment, 70m + adjustment));
        candles.Add(CandleAt(tradingDay, 3, 300m + adjustment, 60m + adjustment));
        candles.Add(CandleAt(tradingDay, 4, 310m + adjustment, 50m + adjustment));
        candles.Add(CandleAt(tradingDay, 5, 320m + adjustment, 40m + adjustment));
        candles.Add(CandleAt(tradingDay, 6, 330m + adjustment, 20m + adjustment));
        return candles.ToArray();
    }

    private static StrategyReplayContext ContextAt(DateTimeOffset asOfUtc, params CandleSeries[] series)
    {
        StrategyReplayContext? context = null;
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var factory = new CreateStrategyReplayContextUseCase();

        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc)
            {
                context = factory.Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame);
            }
        }

        return context ?? throw new InvalidOperationException("The requested replay timestamp was not produced.");
    }

    private static CandleSeries Series(Timeframe timeframe, params Candle[] candles) =>
        new(Provider, Symbol, timeframe, candles);

    private static Candle CandleAt(
        DateOnly day,
        int hour,
        decimal high,
        decimal low,
        Timeframe? timeframe = null) =>
        Candle(day, hour, 0, hour + 1, 0, high, low, timeframe ?? Hour);

    private static Candle Candle(
        DateOnly day,
        int openHour,
        int openMinute,
        int closeHour,
        int closeMinute,
        decimal high,
        decimal low,
        Timeframe timeframe)
    {
        var closeDay = closeHour == 24 ? day.AddDays(1) : day;
        var normalizedCloseHour = closeHour == 24 ? 0 : closeHour;
        var open = LocalUtc(day, openHour, openMinute);
        var close = LocalUtc(closeDay, normalizedCloseHour, closeMinute);
        var middle = (high + low) / 2m;
        return new(Provider, Symbol, timeframe, open, close, middle, high, low, middle, null);
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
