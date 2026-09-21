using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable semantic selection outcome for visible origin-vertex evidence in one episode.</summary>
public sealed class NasdaqHumanOriginVertexObservationSelection
{
    internal NasdaqHumanOriginVertexObservationSelection(
        NasdaqHumanOriginVertexObservationSelectionKind kind,
        IEnumerable<DateTimeOffset> semanticMemberOpenTimesUtc,
        IEnumerable<NasdaqHumanOriginVertexObservation> supportingObservations,
        IEnumerable<IReadOnlyList<DateTimeOffset>> distinctMemberships)
    {
        Kind = kind;
        SemanticMemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(semanticMemberOpenTimesUtc.ToArray());
        SupportingObservations = new ReadOnlyCollection<NasdaqHumanOriginVertexObservation>(supportingObservations.ToArray());
        DistinctMemberships = new ReadOnlyCollection<IReadOnlyList<DateTimeOffset>>(
            distinctMemberships.Select(members => (IReadOnlyList<DateTimeOffset>)
                new ReadOnlyCollection<DateTimeOffset>(members.ToArray())).ToArray());
    }

    public NasdaqHumanOriginVertexObservationSelectionKind Kind { get; }
    public IReadOnlyList<DateTimeOffset> SemanticMemberOpenTimesUtc { get; }
    public IReadOnlyList<NasdaqHumanOriginVertexObservation> SupportingObservations { get; }
    public IReadOnlyList<IReadOnlyList<DateTimeOffset>> DistinctMemberships { get; }
}
