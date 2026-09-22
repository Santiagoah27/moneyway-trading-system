using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable semantic selection outcome for visible rebuilt-candidate evidence in one episode.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexObservationSelection
{
    internal NasdaqHumanRebuiltCandidateVertexObservationSelection(
        NasdaqHumanRebuiltCandidateVertexObservationSelectionKind kind,
        IEnumerable<DateTimeOffset> semanticMemberOpenTimesUtc,
        IEnumerable<NasdaqHumanRebuiltCandidateVertexObservation> supportingObservations,
        IEnumerable<IReadOnlyList<DateTimeOffset>> distinctMemberships)
    {
        Kind = kind;
        SemanticMemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(semanticMemberOpenTimesUtc.ToArray());
        SupportingObservations = new ReadOnlyCollection<NasdaqHumanRebuiltCandidateVertexObservation>(supportingObservations.ToArray());
        DistinctMemberships = new ReadOnlyCollection<IReadOnlyList<DateTimeOffset>>(
            distinctMemberships.Select(members => (IReadOnlyList<DateTimeOffset>)
                new ReadOnlyCollection<DateTimeOffset>(members.ToArray())).ToArray());
    }

    public NasdaqHumanRebuiltCandidateVertexObservationSelectionKind Kind { get; }
    public IReadOnlyList<DateTimeOffset> SemanticMemberOpenTimesUtc { get; }
    public IReadOnlyList<NasdaqHumanRebuiltCandidateVertexObservation> SupportingObservations { get; }
    public IReadOnlyList<IReadOnlyList<DateTimeOffset>> DistinctMemberships { get; }
}
