using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Operational window supplied by the canonical TIME owners, never derived by liquidity evaluators.</summary>
public sealed record NasdaqTradingWindowFact : IReplayRuleFact
{
    internal NasdaqTradingWindowFact(DateTimeOffset asOfUtc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(asOfUtc, zone);
        var date = local.Date;
        DayStartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date, zone));
        StartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.AddHours(8).AddMinutes(30), zone));
        EndUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.AddHours(11), zone));
    }
    public DateTimeOffset DayStartUtc { get; }
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }
    public bool Contains(DateTimeOffset effectiveAtUtc) => effectiveAtUtc >= StartUtc && effectiveAtUtc < EndUtc;
}
