namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Completes one already-detected directional NQ-Q-H4-007 breakout without consuming another candle.</summary>
public sealed class NasdaqH4DirectionalBreakoutCompletionSnapshotReducer
{
    private readonly NasdaqDirectionalMigrationBreakoutCompletionCalculator completionCalculator = new();

    public NasdaqH4ReconstructionSnapshot.Completed.Directional Reduce(NasdaqH4ReconstructionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion awaiting
            || awaiting.State.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)
        {
            throw new ArgumentException("Only an NQ-Q-H4-007 BreakoutAwaitingCompletion snapshot can be completed.", nameof(snapshot));
        }

        return new NasdaqH4ReconstructionSnapshot.Completed.Directional(completionCalculator.Evaluate(awaiting.State));
    }
}
