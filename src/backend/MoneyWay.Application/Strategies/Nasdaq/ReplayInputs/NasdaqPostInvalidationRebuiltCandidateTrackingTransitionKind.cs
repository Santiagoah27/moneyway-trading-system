namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind
{
    TrackingContinues = 0,
    PendingReset = 1,
    TrackingRestarted = 2,
    BreakoutDetected = 3,
}
