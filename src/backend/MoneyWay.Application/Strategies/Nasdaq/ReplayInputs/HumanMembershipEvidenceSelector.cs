using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Shared exact-member-set compatibility and provenance ordering for visible, identity-matched observations.</summary>
internal static class HumanMembershipEvidenceSelector
{
    internal static (IReadOnlyList<T> Observations, IReadOnlyList<IReadOnlyList<DateTimeOffset>> Memberships) Select<T>(
        IEnumerable<T> observations, Func<T, IReadOnlyList<DateTimeOffset>> members, Func<T, string> sourceReference)
        where T : IStrategyReplayInputObservation
    {
        var supporting = observations.OrderBy(item => item.ObservedAtUtc)
            .ThenBy(sourceReference, StringComparer.Ordinal)
            .ThenBy(members, MemberSetComparer.Instance).ToArray();
        var memberships = supporting.Select(members).Distinct(MemberSetComparer.Instance)
            .OrderBy(item => item, MemberSetComparer.Instance).ToArray();
        return (Array.AsReadOnly(supporting), Array.AsReadOnly(memberships));
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
