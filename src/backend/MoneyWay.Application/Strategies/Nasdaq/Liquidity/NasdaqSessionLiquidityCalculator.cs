using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;

namespace MoneyWay.Application.Strategies.Nasdaq.Liquidity;

/// <summary>
/// Calculates the MoneyWay Nasdaq NQ-LIQ-001 extrema from observable closed candles on the exact 1H timeframe.
/// Published versions retain Asia [D-1 17:00, D 02:00) and London [D 02:00, D 07:00) in America/Bogota.
/// The explicit-session candidate alone uses London end D 08:00 and retains source/coverage/tie evidence.
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

        if (MoneyWayNasdaqWindow1130Candidate.UsesExplicitSessions(context.StrategyVersion))
            return CalculateExplicit(context);

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

        return new(levels, AvailableReason, context);
    }

    /// <summary>Explicitly identified sources only. Non-H1 calculations are documentary comparisons, never canonical fallback.</summary>
    public IReadOnlyList<NasdaqSessionReference> CalculateExplicitSessions(CandleSeries source, DateOnly operatingDate, DateTimeOffset cutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (cutoffUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Explicit UTC cutoff required.");
        var minutes = source.Timeframe == ExactHour ? 60 : source.Timeframe.Unit == TimeframeUnit.Minute && source.Timeframe.Amount is 1 or 5 or 30
            ? source.Timeframe.Amount : throw new ArgumentException("Exact H1 or explicit documentary M30/M5/M1 sources only.");
        var duration = TimeSpan.FromMinutes(minutes);
        var asiaStart = Utc(operatingDate.AddDays(-1), AsiaStart);
        var asiaEnd = Utc(operatingDate, AsiaEnd);
        var londonEnd = Utc(operatingDate, new TimeOnly(8, 0));
        NasdaqSessionReference Window(string name, DateTimeOffset start, DateTimeOffset end)
        {
            var expected = new List<DateTimeOffset>();
            for (var t = start; t < end; t += duration) expected.Add(t);
            var visible = source.Candles.Where(c => c.CloseTimeUtc <= cutoffUtc && c.OpenTimeUtc < end && c.CloseTimeUtc > start).ToArray();
            var invalid = visible.Any(c => c.OpenTimeUtc < start || c.CloseTimeUtc > end || c.CloseTimeUtc - c.OpenTimeUtc != duration || !expected.Contains(c.OpenTimeUtc));
            var eligible = visible.Where(c => c.OpenTimeUtc >= start && c.CloseTimeUtc <= end && c.CloseTimeUtc - c.OpenTimeUtc == duration && expected.Contains(c.OpenTimeUtc)).ToArray();
            var missing = expected.Except(eligible.Select(c => c.OpenTimeUtc)).ToArray();
            var status = cutoffUtc < end ? "SessionNotCompleted" : invalid ? "InvalidSourceIntervals" : missing.Length > 0 ? "IncompleteCoverage" : "Complete";
            decimal? high = status == "Complete" ? eligible.Max(c => c.High) : null;
            decimal? low = status == "Complete" ? eligible.Min(c => c.Low) : null;
            return new(MoneyWayNasdaqSessionCandidate.Version.Value, operatingDate, name, source.ProviderId.Value, source.Symbol.Value, source.Timeframe.ToString(),
                start, end, cutoffUtc, status, expected.Count, Array.AsReadOnly(eligible), Array.AsReadOnly(missing), high, low,
                Array.AsReadOnly(eligible.Where(c => c.High == high).ToArray()), Array.AsReadOnly(eligible.Where(c => c.Low == low).ToArray()));
        }
        return Array.AsReadOnly(new[] { Window("Asia", asiaStart, asiaEnd), Window("London", asiaEnd, londonEnd) });
    }

    private NasdaqSessionLiquidityCalculationResult CalculateExplicit(StrategyReplayContext context)
    {
        if (context.StrategyId != MoneyWayNasdaqSessionCandidate.Definition.StrategyId) throw new ArgumentException("Exact Nasdaq candidate identity required.");
        if (!context.IsConfigured(ExactHour)) return Unavailable(ExactHourNotConfiguredReason);
        if (!context.TryGetFrame(ExactHour, out var frame) || frame is null) return Unavailable(ExactHourFrameUnavailableReason);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.AsOfUtc, StrategyTimeZone).DateTime);
        var source = new CandleSeries(context.ProviderId, context.Symbol, ExactHour, frame.AvailableCandles);
        var sessions = CalculateExplicitSessions(source, day, context.AsOfUtc);
        if (sessions.Any(s => !s.IsComplete)) return new(null, "Candidate requires completed, continuously covered exact-H1 Asia [17:00,02:00) and London [02:00,08:00) America/Bogota windows.", context, sessions);
        var asia = sessions[0]; var london = sessions[1];
        return new(new NasdaqSessionLiquidityLevels(day, asia.High!.Value, asia.Low!.Value, london.High!.Value, london.Low!.Value), AvailableReason, context, sessions);
    }

    private static DateTimeOffset Utc(DateOnly day, TimeOnly time) => new(TimeZoneInfo.ConvertTimeToUtc(LocalDateTime(day, time), StrategyTimeZone), TimeSpan.Zero);

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
