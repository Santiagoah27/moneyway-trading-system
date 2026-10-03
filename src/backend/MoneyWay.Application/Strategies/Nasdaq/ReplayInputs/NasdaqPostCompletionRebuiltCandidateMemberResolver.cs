using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves selected 009 human member identities without evidence reselection, geometry or market consumption.</summary>
public sealed class NasdaqPostCompletionRebuiltCandidateMemberResolver
{
    public NasdaqPostCompletionRebuiltCandidateMemberResolution Evaluate(
        NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady selection, StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);
        var pending = selection.PendingState;
        var identity = pending.Episode.PreviousCompletedEpisode;
        if (context.StrategyId != identity.StrategyId || context.StrategyVersion != identity.StrategyVersion
            || context.ProviderId != identity.ProviderId || context.Symbol != identity.Symbol)
            throw new ArgumentException("The selected rebuild must match the replay context identity.", nameof(context));
        if (context.AsOfUtc < selection.AsOfUtc || selection.SupportingObservations.Count == 0
            || selection.SemanticMemberOpenTimesUtc.Count == 0
            || selection.SemanticMemberOpenTimesUtc.Distinct().Count() != selection.SemanticMemberOpenTimesUtc.Count
            || !selection.SemanticMemberOpenTimesUtc.Contains(pending.MigrationCandle.OpenTimeUtc)
            || selection.SupportingObservations.Any(item => item.Context != pending.EvidenceContext
                || !item.SelectedMemberOpenTimesUtc.SequenceEqual(selection.SemanticMemberOpenTimesUtc)))
            throw new ArgumentException("The unique selection must retain its exact rebuild context and semantic membership.", nameof(selection));
        var (members, missing) = RebuiltCandidateMemberResolver.Resolve(selection.SemanticMemberOpenTimesUtc,
            pending.MigrationCandle, pending.CandidateSide, pending.KnownProtectionAnchor, context);
        return missing.Count > 0
            ? new NasdaqPostCompletionRebuiltCandidateMemberResolution.DataUnavailable(selection, missing)
            : new NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved(selection, members);
    }
}
