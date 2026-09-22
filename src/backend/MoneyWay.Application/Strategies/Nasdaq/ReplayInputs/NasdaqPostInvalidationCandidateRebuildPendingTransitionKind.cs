namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqPostInvalidationCandidateRebuildPendingTransitionKind
{
    PendingContinues = 0,
    PendingReset = 1,
    TrackingStarted = 2,
    BreakoutDetected = 3,
}
