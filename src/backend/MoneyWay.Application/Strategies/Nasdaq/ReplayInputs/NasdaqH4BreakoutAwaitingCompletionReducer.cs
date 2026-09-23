using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Routes one frozen H4 breakout to its single branch-specific completion reducer.</summary>
public sealed class NasdaqH4BreakoutAwaitingCompletionReducer
{
    private readonly NasdaqH4DirectionalBreakoutCompletionSnapshotReducer directionalReducer = new();
    private readonly NasdaqH4HumanStructuralPriceBreakoutEvidenceSnapshotReducer humanStructuralPriceReducer = new();
    private readonly NasdaqH4OrdinaryRebuiltBreakoutEvidenceSnapshotReducer ordinaryRebuiltReducer = new();

    public NasdaqH4BreakoutAwaitingCompletionReductionResult Reduce(
        NasdaqH4ReconstructionSnapshot snapshot,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion awaiting)
            throw new ArgumentException("Only a BreakoutAwaitingCompletion snapshot can be reduced.", nameof(snapshot));

        return awaiting.State.CollisionKind switch
        {
            NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.Directional007(
                    directionalReducer.Reduce(awaiting)),
            NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired =>
                ReduceHumanStructuralPrice(awaiting, context),
            NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None =>
                ReduceOrdinaryRebuilt(awaiting, context),
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot), "Unsupported breakout collision kind."),
        };
    }

    private NasdaqH4BreakoutAwaitingCompletionReductionResult ReduceHumanStructuralPrice(
        NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion snapshot,
        StrategyReplayContext context)
    {
        var reduction = humanStructuralPriceReducer.Reduce(snapshot, context);
        return reduction.Kind switch
        {
            NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Missing =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.HumanStructuralPrice008(reduction),
            NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Conflict =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict.HumanStructuralPrice008(reduction),
            NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Completed =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.HumanStructuralPrice008(reduction),
            _ => throw new ArgumentOutOfRangeException(nameof(reduction), "Unsupported NQ-Q-H4-008 reduction kind."),
        };
    }

    private NasdaqH4BreakoutAwaitingCompletionReductionResult ReduceOrdinaryRebuilt(
        NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion snapshot,
        StrategyReplayContext context)
    {
        var reduction = ordinaryRebuiltReducer.Reduce(snapshot, context);
        return reduction.Kind switch
        {
            NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Missing =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.OrdinaryRebuilt009(reduction),
            NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Conflict =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict.OrdinaryRebuilt009(reduction),
            NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Completed =>
                new NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.OrdinaryRebuilt009(reduction),
            _ => throw new ArgumentOutOfRangeException(nameof(reduction), "Unsupported NQ-Q-H4-009 reduction kind."),
        };
    }
}
