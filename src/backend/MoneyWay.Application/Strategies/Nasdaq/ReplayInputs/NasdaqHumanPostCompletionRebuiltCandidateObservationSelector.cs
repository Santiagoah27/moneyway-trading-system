using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects visible exact-context human member sets without resolving candles or consuming market steps.</summary>
public sealed class NasdaqHumanPostCompletionRebuiltCandidateObservationSelector
{
    public NasdaqHumanPostCompletionRebuiltCandidateSelection Select(
        NasdaqPostCompletionRebuildPendingState pendingState, StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(context);
        var episode = pendingState.Episode.PreviousCompletedEpisode;
        if (context.StrategyId != episode.StrategyId || context.StrategyVersion != episode.StrategyVersion
            || context.ProviderId != episode.ProviderId || context.Symbol != episode.Symbol
            || context.AsOfUtc < pendingState.MigrationCandle.CloseTimeUtc)
            throw new ArgumentException("Replay context must match the closed migration and its exact strategy/market identity.", nameof(context));
        var (observations, memberships) = HumanMembershipEvidenceSelector.Select(context.InputObservations
            .OfType<NasdaqHumanPostCompletionRebuiltCandidateObservation>()
            .Where(item => item.Context == pendingState.EvidenceContext),
            item => item.SelectedMemberOpenTimesUtc, item => item.SourceReference);
        return memberships.Count switch
        {
            0 => new NasdaqHumanPostCompletionRebuiltCandidateSelection.Missing(pendingState, context.AsOfUtc),
            1 => new NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady(pendingState, context.AsOfUtc, observations, memberships[0]),
            _ => new NasdaqHumanPostCompletionRebuiltCandidateSelection.Conflict(pendingState, context.AsOfUtc, observations, memberships),
        };
    }
}
