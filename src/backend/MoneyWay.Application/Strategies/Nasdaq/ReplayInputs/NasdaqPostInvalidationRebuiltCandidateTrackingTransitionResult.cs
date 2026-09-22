namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exactly one outcome of processing a later closed H4 candle during rebuilt candidate tracking.</summary>
public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult
{
    private NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult(
        NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind kind,
        NasdaqPostInvalidationRebuiltCandidateTrackingState? tracking,
        NasdaqPostInvalidationCandidateRebuildPendingState? pending,
        NasdaqPostInvalidationCandidateRebuildBreakoutState? breakout)
    {
        Kind = kind;
        Tracking = tracking;
        Pending = pending;
        Breakout = breakout;
    }

    public NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind Kind { get; }
    public NasdaqPostInvalidationRebuiltCandidateTrackingState? Tracking { get; }
    public NasdaqPostInvalidationCandidateRebuildPendingState? Pending { get; }
    public NasdaqPostInvalidationCandidateRebuildBreakoutState? Breakout { get; }

    internal static NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult Continue(NasdaqPostInvalidationRebuiltCandidateTrackingState tracking) =>
        new(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingContinues, tracking, null, null);

    internal static NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult Reset(NasdaqPostInvalidationCandidateRebuildPendingState pending) =>
        new(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.PendingReset, null, pending, null);

    internal static NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult Restart(NasdaqPostInvalidationRebuiltCandidateTrackingState tracking) =>
        new(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingRestarted, tracking, null, null);

    internal static NasdaqPostInvalidationRebuiltCandidateTrackingTransitionResult DetectBreakout(NasdaqPostInvalidationCandidateRebuildBreakoutState breakout) =>
        new(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.BreakoutDetected, null, null, breakout);
}
