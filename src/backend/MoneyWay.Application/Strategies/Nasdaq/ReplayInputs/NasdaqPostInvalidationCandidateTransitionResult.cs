namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One mutually exclusive outcome of processing a single closed H4 candle for an active candidate.</summary>
public abstract record NasdaqPostInvalidationCandidateTransitionResult
{
    private NasdaqPostInvalidationCandidateTransitionResult() { }

    public abstract NasdaqPostInvalidationCandidateTransitionKind Kind { get; }

    public sealed record CandidateContinues(NasdaqPostInvalidationCandidateState State)
        : NasdaqPostInvalidationCandidateTransitionResult
    {
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues;
    }

    public sealed record DirectCompleted(NasdaqDirectCandidateBreakoutCompletionResult Result)
        : NasdaqPostInvalidationCandidateTransitionResult
    {
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.DirectCompleted;
    }

    public sealed record RebuildPending(NasdaqPostInvalidationCandidateRebuildPendingState State)
        : NasdaqPostInvalidationCandidateTransitionResult
    {
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.RebuildPending;
    }

    public sealed record RebuiltTracking(NasdaqPostInvalidationRebuiltCandidateTrackingState State)
        : NasdaqPostInvalidationCandidateTransitionResult
    {
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.RebuiltTracking;
    }

    public sealed record CollisionBreakout(NasdaqPostInvalidationCandidateRebuildBreakoutState State)
        : NasdaqPostInvalidationCandidateTransitionResult
    {
        public override NasdaqPostInvalidationCandidateTransitionKind Kind => NasdaqPostInvalidationCandidateTransitionKind.CollisionBreakout;
    }
}

public enum NasdaqPostInvalidationCandidateTransitionKind
{
    CandidateContinues,
    DirectCompleted,
    RebuildPending,
    RebuiltTracking,
    CollisionBreakout,
}
