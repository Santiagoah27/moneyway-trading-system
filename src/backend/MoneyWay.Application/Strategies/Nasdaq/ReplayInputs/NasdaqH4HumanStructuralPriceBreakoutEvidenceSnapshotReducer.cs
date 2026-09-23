using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces visible human evidence for one frozen NQ-Q-H4-008 breakout without consuming another candle.</summary>
public sealed class NasdaqH4HumanStructuralPriceBreakoutEvidenceSnapshotReducer
{
    private readonly NasdaqHumanCollisionStructuralPriceObservationSelector selector = new();
    private readonly NasdaqHumanStructuralPriceBreakoutCompletionCalculator completionCalculator = new();

    public NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult Reduce(
        NasdaqH4ReconstructionSnapshot snapshot,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion awaiting
            || awaiting.State.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)
        {
            throw new ArgumentException("Only an NQ-Q-H4-008 BreakoutAwaitingCompletion snapshot can be reduced.", nameof(snapshot));
        }
        if (snapshot.StrategyId != context.StrategyId || snapshot.StrategyVersion != context.StrategyVersion
            || snapshot.ProviderId != context.ProviderId || snapshot.Symbol != context.Symbol
            || context.AsOfUtc < awaiting.State.ValidatingCandle.CloseTimeUtc)
        {
            throw new ArgumentException("Replay context must match the closed breakout and its strategy/market identity.", nameof(context));
        }

        var breakout = awaiting.State;
        var episode = new NasdaqHumanCollisionStructuralPriceEpisode(
            breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide);
        var selection = selector.Select(context, episode);
        if (selection.Kind != NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique)
            return NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult.Pending(awaiting, selection);

        var completion = completionCalculator.Evaluate(breakout, selection);
        return NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult.Completed(
            new NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice(completion));
    }
}
