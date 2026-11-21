using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;

namespace MoneyWay.Application.Research.Nasdaq;

public enum NasdaqPreparationClockPhase { BeforePreparation, PreparationPhase, PreparationIntervalElapsed }
public enum NasdaqWindowClockPhase { BeforeTradingWindow, InsideTradingWindow, AtTradingWindowEnd, AfterTradingWindow }
public enum NasdaqWindowEvidenceStatus { Observable, MissingTimestamp, InvalidTimestamp, EventNotYetObservable }

/// <summary>Versioned 11:30 research configuration, independent from the canonical definition registry.</summary>
public static class NasdaqTradingWindowAlignment
{
    public const string ConfigurationId = "EXP-NQ-TRADING-WINDOW-1130-V1";
    public const string LifecycleStatus = "CandidateConfiguration_NotActivated";
    public static TimeOnly Preparation { get; } = new(8, 0);
    public static TimeOnly Opening { get; } = new(8, 30);
    public static TimeOnly Closing { get; } = new(11, 30);

    public static NasdaqTradingWindowComparison Evaluate(DateTimeOffset? asOfUtc, DateTimeOffset? eventUtc)
    {
        var evidence = asOfUtc is null || eventUtc is null ? NasdaqWindowEvidenceStatus.MissingTimestamp :
            asOfUtc.Value.Offset != TimeSpan.Zero || eventUtc.Value.Offset != TimeSpan.Zero ||
            asOfUtc == default(DateTimeOffset) || eventUtc == default(DateTimeOffset) ? NasdaqWindowEvidenceStatus.InvalidTimestamp :
            eventUtc > asOfUtc ? NasdaqWindowEvidenceStatus.EventNotYetObservable : NasdaqWindowEvidenceStatus.Observable;
        var validCutoff = asOfUtc is { Offset: var offset } && offset == TimeSpan.Zero && asOfUtc != default(DateTimeOffset)
            ? asOfUtc : null;
        if (evidence != NasdaqWindowEvidenceStatus.Observable)
            return new(validCutoff, null, null, null, evidence, null, null, null);

        var at = eventUtc!.Value;
        var zone = NasdaqSupervisedSessionPolicy.Zone;
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var day = DateOnly.FromDateTime(local.Date);
        DateTimeOffset Utc(TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(time), zone);
        var preparation = Utc(Preparation);
        var opening = Utc(Opening);
        var closing = Utc(Closing);
        var legacy = new NasdaqTradingWindowFact(at, zone);
        var clockPreparation = at < preparation ? NasdaqPreparationClockPhase.BeforePreparation :
            at < opening ? NasdaqPreparationClockPhase.PreparationPhase : NasdaqPreparationClockPhase.PreparationIntervalElapsed;
        return new(validCutoff, at, local, preparation, evidence, clockPreparation,
            Timing(legacy.StartUtc, legacy.EndUtc, legacy.Contains(at)),
            Timing(opening, closing, at >= opening && at < closing));

        NasdaqWindowTiming Timing(DateTimeOffset start, DateTimeOffset end, bool inside) =>
            new(start, end, at < start ? NasdaqWindowClockPhase.BeforeTradingWindow :
                at == end ? NasdaqWindowClockPhase.AtTradingWindowEnd :
                at > end ? NasdaqWindowClockPhase.AfterTradingWindow : NasdaqWindowClockPhase.InsideTradingWindow, inside);
    }
}

public sealed record NasdaqWindowTiming(DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    NasdaqWindowClockPhase Phase, bool WithinNewSetupAndEntryTimeWindow);

public sealed record NasdaqTradingWindowComparison(DateTimeOffset? AsOfUtc, DateTimeOffset? EventUtc,
    DateTimeOffset? EventBogota, DateTimeOffset? PreparationUtc, NasdaqWindowEvidenceStatus EvidenceStatus,
    NasdaqPreparationClockPhase? PreparationPhase, NasdaqWindowTiming? PublishedRuntime, NasdaqWindowTiming? Candidate)
{
    public string CandidateConfigurationId => NasdaqTradingWindowAlignment.ConfigurationId;
    public string LifecycleStatus => NasdaqTradingWindowAlignment.LifecycleStatus;
    public string TimeZone => "America/Bogota";
    public string Inclusion => "StartInclusive_EndExclusive";
    public IReadOnlyList<string> LegacyVersions { get; } = Array.AsReadOnly(new[]
    {
        MoneyWayNasdaqStrategyDefinition.Instance.Version.Value,
        MoneyWayNasdaqM5StrictCloseCandidate.Version.Value,
        MoneyWayNasdaqSessionCandidate.Version.Value
    });
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public bool DefinesPositionManagement => false;
    public bool TerminatesSession => false;
    public bool ForcesPositionClose => false;
}
