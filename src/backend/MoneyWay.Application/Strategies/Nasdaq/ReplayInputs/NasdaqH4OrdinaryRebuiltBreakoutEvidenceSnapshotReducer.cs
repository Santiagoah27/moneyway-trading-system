using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces visible human membership evidence for one frozen ordinary NQ-Q-H4-009 breakout.</summary>
public sealed class NasdaqH4OrdinaryRebuiltBreakoutEvidenceSnapshotReducer
{
    private readonly NasdaqHumanRebuiltCandidateVertexObservationSelector selector = new();
    private readonly NasdaqHumanRebuiltCandidateVertexMemberResolver memberResolver = new();
    private readonly NasdaqHumanRebuiltCandidateVertexGeometryCalculator geometryCalculator = new();
    private readonly NasdaqOrdinaryRebuiltBreakoutCompletionCalculator completionCalculator = new();

    public NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult Reduce(
        NasdaqH4ReconstructionSnapshot snapshot,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion awaiting
            || awaiting.State.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None
            || awaiting.State.HasStrictMigration
            || awaiting.State.Origin is not NasdaqPostInvalidationBreakoutOrigin.Rebuild rebuild)
        {
            throw new ArgumentException("Only an ordinary Rebuild-origin BreakoutAwaitingCompletion snapshot can be reduced.", nameof(snapshot));
        }
        if (snapshot.StrategyId != context.StrategyId || snapshot.StrategyVersion != context.StrategyVersion
            || snapshot.ProviderId != context.ProviderId || snapshot.Symbol != context.Symbol
            || context.AsOfUtc < awaiting.State.ValidatingCandle.CloseTimeUtc)
        {
            throw new ArgumentException("Replay context must match the closed breakout and its strategy/market identity.", nameof(context));
        }

        var breakout = awaiting.State;
        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(
            breakout.Episode.StrategyId, breakout.Episode.StrategyVersion,
            breakout.Episode.ProviderId, breakout.Episode.Symbol,
            breakout.Episode.InvalidatingCandleOpenTimeUtc,
            rebuild.PriorMigrationCandle.OpenTimeUtc, breakout.CandidateSide);
        var selection = selector.Select(context, episode);
        if (selection.Kind != NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique)
            return NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult.Pending(awaiting, selection);

        var resolutionContext = NasdaqHumanRebuiltCandidateVertexResolutionContext.From(breakout);
        var members = memberResolver.Evaluate(resolutionContext, selection, context);
        var geometry = geometryCalculator.Evaluate(members);
        var completion = completionCalculator.Evaluate(breakout, members, geometry);
        return NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult.Completed(
            new NasdaqH4ReconstructionSnapshot.Completed.Ordinary(completion), selection);
    }
}
