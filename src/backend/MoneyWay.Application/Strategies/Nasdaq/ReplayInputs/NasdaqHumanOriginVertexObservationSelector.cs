using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Classifies visible human origin-vertex evidence for one reconstruction episode without selecting authority,
/// resolving candles, or calculating geometry.
/// </summary>
public sealed class NasdaqHumanOriginVertexObservationSelector
{
    public NasdaqHumanOriginVertexObservationSelection Select(
        StrategyReplayContext context,
        NasdaqHumanOriginVertexEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(episode);

        var supporting = context.InputObservations
            .OfType<NasdaqHumanOriginVertexObservation>()
            .Where(episode.Matches)
            .OrderBy(item => item.ObservedAtUtc)
            .ThenBy(item => item.SourceReference, StringComparer.Ordinal)
            .ThenBy(item => item.SelectedMemberOpenTimesUtc, MemberSetComparer.Instance)
            .ToArray();

        var memberships = supporting
            .Select(item => item.SelectedMemberOpenTimesUtc)
            .Distinct(MemberSetComparer.Instance)
            .OrderBy(item => item, MemberSetComparer.Instance)
            .ToArray();

        return memberships.Length switch
        {
            0 => new(NasdaqHumanOriginVertexObservationSelectionKind.Missing, [], [], []),
            1 => new(NasdaqHumanOriginVertexObservationSelectionKind.Unique, memberships[0], supporting, memberships),
            _ => new(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, [], supporting, memberships),
        };
    }

    private sealed class MemberSetComparer : IEqualityComparer<IReadOnlyList<DateTimeOffset>>, IComparer<IReadOnlyList<DateTimeOffset>>
    {
        public static readonly MemberSetComparer Instance = new();

        public bool Equals(IReadOnlyList<DateTimeOffset>? x, IReadOnlyList<DateTimeOffset>? y) =>
            ReferenceEquals(x, y) || (x is not null && y is not null && x.SequenceEqual(y));

        public int GetHashCode(IReadOnlyList<DateTimeOffset> value)
        {
            var hash = new HashCode();
            foreach (var item in value) hash.Add(item);
            return hash.ToHashCode();
        }

        public int Compare(IReadOnlyList<DateTimeOffset>? x, IReadOnlyList<DateTimeOffset>? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            for (var index = 0; index < Math.Min(x.Count, y.Count); index++)
            {
                var comparison = x[index].CompareTo(y[index]);
                if (comparison != 0) return comparison;
            }
            return x.Count.CompareTo(y.Count);
        }
    }
}
