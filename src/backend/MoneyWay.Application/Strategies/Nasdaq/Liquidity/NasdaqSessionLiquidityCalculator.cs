using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.Liquidity;

/// <summary>
/// Calculates the MoneyWay Nasdaq NQ-LIQ-001 extrema from observable closed candles on the exact 1H timeframe.
/// For local day D in <c>America/Bogota</c>, Asia is [D-1 17:00, D 02:00) and London is [D 02:00, D 07:00).
/// This primitive does not produce a strategy verdict, evaluate a sweep, or execute a trade.
/// </summary>
public sealed class NasdaqSessionLiquidityCalculator
{
    public const string AvailableReason = "Completed Nasdaq Asia and London session liquidity levels are available.";
    public const string SessionNotCompletedReason = "Completed Nasdaq session liquidity is unavailable before 07:00 America/Bogota.";
    public const string ExactHourNotConfiguredReason = "Completed Nasdaq session liquidity requires the exact 1H timeframe.";
    public const string ExactHourFrameUnavailableReason = "The configured exact 1H replay frame is not observable at this replay step.";
    public const string IncompleteSessionDataReason = "Completed Nasdaq session liquidity cannot be determined because required exact-1H session candle data is incomplete.";

    private const string TimeZoneId = "America/Bogota";
    private static readonly Timeframe ExactHour = new(1, TimeframeUnit.Hour);
    private static readonly TimeOnly AsiaStart = new(17, 0);
    private static readonly TimeOnly AsiaEnd = new(2, 0);
    private static readonly TimeOnly LondonStart = AsiaEnd;
    private static readonly TimeOnly LondonEnd = new(7, 0);
    private static readonly TimeZoneInfo StrategyTimeZone = ResolveStrategyTimeZone();

    public NasdaqSessionLiquidityCalculationResult Calculate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var localAsOf = TimeZoneInfo.ConvertTime(context.AsOfUtc, StrategyTimeZone);
        var tradingDay = DateOnly.FromDateTime(localAsOf.DateTime);

        if (TimeOnly.FromDateTime(localAsOf.DateTime) < LondonEnd)
        {
            return Unavailable(SessionNotCompletedReason);
        }

        if (!context.IsConfigured(ExactHour))
        {
            return Unavailable(ExactHourNotConfiguredReason);
        }

        if (!context.TryGetFrame(ExactHour, out var frame) || frame is null)
        {
            return Unavailable(ExactHourFrameUnavailableReason);
        }

        var asiaOpenTimes = ExpectedOpenTimes(
            tradingDay.AddDays(-1), AsiaStart,
            tradingDay, AsiaEnd);
        var londonOpenTimes = ExpectedOpenTimes(
            tradingDay, LondonStart,
            tradingDay, LondonEnd);
        var candlesByOpenTime = frame.AvailableCandles.ToDictionary(candle => candle.OpenTimeUtc);

        if (!TrySelect(candlesByOpenTime, asiaOpenTimes, out var asiaCandles) ||
            !TrySelect(candlesByOpenTime, londonOpenTimes, out var londonCandles))
        {
            return Unavailable(IncompleteSessionDataReason);
        }

        var levels = new NasdaqSessionLiquidityLevels(
            tradingDay,
            asiaCandles.Max(candle => candle.High),
            asiaCandles.Min(candle => candle.Low),
            londonCandles.Max(candle => candle.High),
            londonCandles.Min(candle => candle.Low));

        return new(levels, AvailableReason);
    }

    private static IReadOnlyList<DateTimeOffset> ExpectedOpenTimes(
        DateOnly startDay,
        TimeOnly startTime,
        DateOnly endDay,
        TimeOnly endTime)
    {
        var start = LocalDateTime(startDay, startTime);
        var end = LocalDateTime(endDay, endTime);
        var result = new List<DateTimeOffset>();

        for (var current = start; current < end; current = current.AddHours(1))
        {
            result.Add(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(current, StrategyTimeZone), TimeSpan.Zero));
        }

        return result;
    }

    private static bool TrySelect(
        IReadOnlyDictionary<DateTimeOffset, Candle> candlesByOpenTime,
        IReadOnlyList<DateTimeOffset> expectedOpenTimes,
        out IReadOnlyList<Candle> candles)
    {
        var selected = new List<Candle>(expectedOpenTimes.Count);

        foreach (var openTime in expectedOpenTimes)
        {
            if (!candlesByOpenTime.TryGetValue(openTime, out var candle))
            {
                candles = [];
                return false;
            }

            selected.Add(candle);
        }

        candles = selected;
        return true;
    }

    private static DateTime LocalDateTime(DateOnly day, TimeOnly time) =>
        DateTime.SpecifyKind(day.ToDateTime(time), DateTimeKind.Unspecified);

    private static NasdaqSessionLiquidityCalculationResult Unavailable(string reason) => new(null, reason);

    private static TimeZoneInfo ResolveStrategyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(TimeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
