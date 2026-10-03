using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-selected H4 identity and structural price; it computes neither geometry nor wick protection.</summary>
public sealed class NasdaqHumanH4StructuralAnchor : IEquatable<NasdaqHumanH4StructuralAnchor>, IComparable<NasdaqHumanH4StructuralAnchor>
{
    public NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole role, decimal structuralPrice,
        IEnumerable<DateTimeOffset> memberOpenTimesUtc, DateTimeOffset? validatingCandleOpenTimeUtc = null)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        ArgumentNullException.ThrowIfNull(memberOpenTimesUtc);
        var members = memberOpenTimesUtc.ToArray();
        if (members.Length == 0 || members.Any(item => item.Offset != TimeSpan.Zero)
            || members.Distinct().Count() != members.Length)
            throw new ArgumentException("Members must be a non-empty set of distinct UTC H4 open timestamps.", nameof(memberOpenTimesUtc));
        if (validatingCandleOpenTimeUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Validation candle timestamp must be UTC.", nameof(validatingCandleOpenTimeUtc));
        Role = role;
        StructuralPrice = structuralPrice;
        MemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(members.OrderBy(item => item).ToArray());
        ValidatingCandleOpenTimeUtc = validatingCandleOpenTimeUtc;
    }

    public NasdaqHumanH4StructuralRole Role { get; }
    public decimal StructuralPrice { get; }
    public IReadOnlyList<DateTimeOffset> MemberOpenTimesUtc { get; }
    public DateTimeOffset? ValidatingCandleOpenTimeUtc { get; }

    public int CompareTo(NasdaqHumanH4StructuralAnchor? other)
    {
        if (other is null) return 1;
        var comparison = Role.CompareTo(other.Role);
        if (comparison != 0) return comparison;
        comparison = StructuralPrice.CompareTo(other.StructuralPrice);
        if (comparison != 0) return comparison;
        comparison = Nullable.Compare(ValidatingCandleOpenTimeUtc, other.ValidatingCandleOpenTimeUtc);
        if (comparison != 0) return comparison;
        for (var index = 0; index < Math.Min(MemberOpenTimesUtc.Count, other.MemberOpenTimesUtc.Count); index++)
        {
            comparison = MemberOpenTimesUtc[index].CompareTo(other.MemberOpenTimesUtc[index]);
            if (comparison != 0) return comparison;
        }
        return MemberOpenTimesUtc.Count.CompareTo(other.MemberOpenTimesUtc.Count);
    }

    public bool Equals(NasdaqHumanH4StructuralAnchor? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanH4StructuralAnchor);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Role); hash.Add(StructuralPrice); hash.Add(ValidatingCandleOpenTimeUtc);
        foreach (var member in MemberOpenTimesUtc) hash.Add(member);
        return hash.ToHashCode();
    }
}
