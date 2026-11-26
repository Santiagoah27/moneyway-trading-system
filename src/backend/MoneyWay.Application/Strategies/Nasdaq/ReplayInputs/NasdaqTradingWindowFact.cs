using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Replay window supplied by the canonical TIME owners and shared by the pre-entry consumers.</summary>
public sealed record NasdaqTradingWindowFact : IReplayRuleFact
{
    private static readonly TimeOnly LegacyStart = new(8, 30);
    private static readonly TimeOnly LegacyEnd = new(11, 0);

    internal NasdaqTradingWindowFact(DateTimeOffset asOfUtc, TimeZoneInfo zone)
        : this(asOfUtc, zone, LegacyStart, LegacyEnd) { }

    internal NasdaqTradingWindowFact(DateTimeOffset asOfUtc, TimeZoneInfo zone, TimeOnly start, TimeOnly end)
    {
        var local = TimeZoneInfo.ConvertTime(asOfUtc, zone);
        var date = local.Date;
        DayStartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date, zone));
        StartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.Add(start.ToTimeSpan()), zone));
        EndUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.Add(end.ToTimeSpan()), zone));
    }
    internal static NasdaqTradingWindowFact ForReplay(StrategyReplayContext context)
    {
        var zone = NasdaqSupervisedSessionPolicy.Zone;
        var legacy = new NasdaqTradingWindowFact(context.AsOfUtc, zone);
        if (context.StrategyVersion != MoneyWayNasdaqWindow1130Candidate.Version) return legacy;

        if (HasReviewedWickfillSetup(context, legacy.DayStartUtc))
            return legacy;

        return new(context.AsOfUtc, zone, NasdaqTradingWindowAlignment.Opening, NasdaqTradingWindowAlignment.Closing);
    }

    private static bool HasReviewedWickfillSetup(StrategyReplayContext context, DateTimeOffset dayStartUtc)
    {
        if (!context.InputObservations.OfType<NasdaqHumanBranchApplicabilityObservation>().Any()) return false;
        // Keep the exact Start owner visible after expiry so the same WF2 take cannot start again.
        var owner = context.PriorObservations.LastOrDefault(o => o.AsOfUtc >= dayStartUtc
            && o.LifecycleProgression?.ActiveInstance?.StartedAtUtc == o.AsOfUtc);
        return owner is not null
            && new NasdaqHumanBranchApplicabilityObservationSelector().Select(context, owner)
                is NasdaqHumanBranchApplicabilitySelection.Unique reviewed
            && reviewed.Fact.Path == NasdaqStrategyPath.WickfillScenario2;
    }

    public DateTimeOffset DayStartUtc { get; }
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }
    public bool Contains(DateTimeOffset effectiveAtUtc) => effectiveAtUtc >= StartUtc && effectiveAtUtc < EndUtc;
}
