namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exactly one causal outcome of advancing a rebuilt-pending episode by one closed H4 candle.</summary>
public sealed class NasdaqPostInvalidationCandidateRebuildPendingTransitionResult
{
    private NasdaqPostInvalidationCandidateRebuildPendingTransitionResult(
        NasdaqPostInvalidationCandidateRebuildPendingTransitionKind kind,
        NasdaqPostInvalidationCandidateRebuildPendingState? pending,
        NasdaqPostInvalidationRebuiltCandidateTrackingState? tracking,
        NasdaqPostInvalidationCandidateRebuildBreakoutState? breakout)
    {
        Kind = kind;
        Pending = pending;
        Tracking = tracking;
        Breakout = breakout;
    }

    public NasdaqPostInvalidationCandidateRebuildPendingTransitionKind Kind { get; }
    public NasdaqPostInvalidationCandidateRebuildPendingState? Pending { get; }
    public NasdaqPostInvalidationRebuiltCandidateTrackingState? Tracking { get; }
    public NasdaqPostInvalidationCandidateRebuildBreakoutState? Breakout { get; }

    internal static NasdaqPostInvalidationCandidateRebuildPendingTransitionResult Continue(NasdaqPostInvalidationCandidateRebuildPendingState pending) =>
        new(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.PendingContinues, pending, null, null);

    internal static NasdaqPostInvalidationCandidateRebuildPendingTransitionResult Reset(NasdaqPostInvalidationCandidateRebuildPendingState pending) =>
        new(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.PendingReset, pending, null, null);

    internal static NasdaqPostInvalidationCandidateRebuildPendingTransitionResult StartTracking(NasdaqPostInvalidationRebuiltCandidateTrackingState tracking) =>
        new(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.TrackingStarted, null, tracking, null);

    internal static NasdaqPostInvalidationCandidateRebuildPendingTransitionResult DetectBreakout(NasdaqPostInvalidationCandidateRebuildBreakoutState breakout) =>
        new(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.BreakoutDetected, null, null, breakout);
}
