using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Classifies visible human rebuilt-candidate evidence without resolving members or assigning authority.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexObservationSelector
{
    public NasdaqHumanRebuiltCandidateVertexObservationSelection Select(
        StrategyReplayContext context,
        NasdaqHumanRebuiltCandidateVertexEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(episode);

        var (supporting, memberships) = HumanMembershipEvidenceSelector.Select(context.InputObservations
            .OfType<NasdaqHumanRebuiltCandidateVertexObservation>().Where(episode.Matches),
            item => item.SelectedMemberOpenTimesUtc, item => item.SourceReference);

        return memberships.Count switch
        {
            0 => new(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Missing, [], [], []),
            1 => new(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique, memberships[0], supporting, memberships),
            _ => new(NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Conflict, [], supporting, memberships),
        };
    }

}
