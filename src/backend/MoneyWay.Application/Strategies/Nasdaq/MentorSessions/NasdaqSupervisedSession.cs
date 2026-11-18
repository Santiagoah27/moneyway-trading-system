using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

public sealed record NasdaqSupervisedSessionOpen(string SessionId, DateOnly TradingDay, string ProviderId, string Symbol,
    string Path, string Reviewer, string SourceReference);
public sealed record NasdaqSupervisedSessionCommand(Guid SubmissionId, string Action, string Reviewer, string SourceReference)
{
    public string? SetupReference { get; init; }
    public NasdaqSupervisedExecutionRecordReference? Execution { get; init; }
    public bool MoneyWayOwnedExecutionConfirmed { get; init; }
    public bool MoneyWayTradeClosedConfirmed { get; init; }
    public string? ClosureKind { get; init; }
    public string? Reason { get; init; }
    public NasdaqCanonicalDiagnosticReference? CanonicalDiagnostic { get; init; }
}
public sealed record NasdaqSupervisedSessionEvent(string SessionKey, NasdaqDemoSessionIdentity Identity, string SessionId,
    string Path, int Revision, string? PreviousHash, DateTimeOffset RegisteredAtUtc, NasdaqSupervisedSessionCommand Command)
{
    public string Scope => "LocalSupervisedWorkflow_AccountScopeUnknown";
    public bool BrokerVerified => false;
}
public sealed record NasdaqSupervisedSessionState(string SessionKey, NasdaqDemoSessionIdentity Identity, string SessionId,
    string Path, string Status, string? ActiveSetup, NasdaqSupervisedSessionEvent? ExecutionConfirmation,
    NasdaqSupervisedSessionEvent? ClosureConfirmation, int Revision, string RevisionHash,
    bool Terminal, bool LocalEntryIntentPermitted, string LocalGuardReason, IReadOnlyList<NasdaqSupervisedSessionEvent> Events)
{
    public string TimeZone => "America/Bogota";
    public string AccountScope => "Unknown_NoCrossAccountOrCrossProviderEnforcementClaim";
    public string StrategyAuthorization => "NotEstablishedByOperationalWorkflow";
    public string CalendarEligibility => "NotEstablished_ExistingPreparationCalendarAndRiskRequirementsApply";
    public bool BrokerExecutionVerified => false;
    public bool CompleteSupervisedTrade => false;
    public string? CalculatedPnl => null;
    public DateTimeOffset? TerminalRegisteredAtUtc => ClosureConfirmation?.RegisteredAtUtc;
    public string TerminalTiming => "OperationalRegistrationOnly_NotAnExactHistoricalCloseInstant";
}

/// <summary>Pure local operational projection, separate from canonical setup cancellation/expiry and historical replay.</summary>
public static class NasdaqSupervisedSessionPolicy
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    public static DateOnly TradingDay(DateTimeOffset time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, Zone).DateTime);
    public static NasdaqSupervisedSessionState Project(IReadOnlyList<NasdaqSupervisedSessionEvent> events, string hash, DateTimeOffset now)
    {
        if (events.Count == 0) throw new ArgumentException("A persisted session-open event is required.");
        if (events[0].Revision != 1 || events[0].Command.Action != "Open") throw new ArgumentException("The first operational event must open the session.");
        var first = events[0]; string? active = null; NasdaqSupervisedSessionEvent? entry = null, closure = null;
        foreach (var e in events)
        {
            if (e.Identity != first.Identity || e.SessionId != first.SessionId || e.Path != first.Path || e.SessionKey != first.SessionKey)
                throw new ArgumentException("Mixed supervised session identities.");
            if (closure is not null) throw new ArgumentException("A terminal session cannot be reopened or receive another operational action.");
            switch (e.Command.Action)
            {
                case "Open": if (e.Revision != 1) throw new ArgumentException("Session is already open."); break;
                case "ActivateSetup": if (entry is not null) throw new ArgumentException("One actual trade per session."); active = e.Command.SetupReference; break;
                case "InvalidateSetup": if (entry is not null || active != e.Command.SetupReference) throw new ArgumentException("Only the current unexecuted setup can be invalidated."); active = null; break;
                case "EntryIntent": if (entry is not null) throw new ArgumentException("One actual trade per session."); break;
                case "ConfirmExecution": if (entry is not null || e.Command.Execution is null || !e.Command.MoneyWayOwnedExecutionConfirmed) throw new ArgumentException("Explicit first MoneyWay-owned execution required."); entry = e; break;
                case "ConfirmClosure": if (entry is null || e.Command.Execution?.RecordId != entry.Command.Execution?.RecordId || !e.Command.MoneyWayTradeClosedConfirmed) throw new ArgumentException("Closure must refer to the exact confirmed MoneyWay trade."); closure = e; active = null; break;
                default: throw new ArgumentException("Unsupported operational action.");
            }
        }
        var reason = closure is not null ? "TerminalSession_NoReentryNoNewSetupNoSecondTrade"
            : entry is not null ? "FirstMoneyWayTradeAlreadyRegistered_NoSecondTrade"
            : TradingDay(now) != first.Identity.TradingDay ? "NotCurrentBogotaSessionDate"
            : !new NasdaqTradingWindowFact(now, Zone).Contains(now) ? "OutsideExisting0830To1100BogotaEntryWindow"
            : "LocalTerminalAndTimeGuardPassed_NotCanonicalEntryAuthorization";
        return new(first.SessionKey, first.Identity, first.SessionId, first.Path, closure is not null ? "Terminal" : entry is not null ? "TradeRegistered_AwaitingClosure" : active is not null ? "SetupRegistered_NoTrade" : "Open_NoTrade",
            active, entry, closure, events[^1].Revision, hash, closure is not null,
            closure is null && entry is null && TradingDay(now) == first.Identity.TradingDay && new NasdaqTradingWindowFact(now, Zone).Contains(now), reason, Array.AsReadOnly(events.ToArray()));
    }
}
