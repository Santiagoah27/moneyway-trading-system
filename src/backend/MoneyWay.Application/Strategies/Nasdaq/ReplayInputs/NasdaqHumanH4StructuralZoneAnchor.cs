using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human initial-context zone only. Its bounds are not a structural scalar or executable price.</summary>
/// <remarks>The enclosing H4 observation retains the original source/feed provenance and availability.</remarks>
public sealed class NasdaqHumanH4StructuralZoneAnchor : IEquatable<NasdaqHumanH4StructuralZoneAnchor>, IComparable<NasdaqHumanH4StructuralZoneAnchor>
{
    public NasdaqHumanH4StructuralZoneAnchor(NasdaqHumanH4StructuralRole role, decimal lowerBound, decimal upperBound,
        NasdaqHumanH4ZoneBoundaryPrecision boundaryPrecision, IEnumerable<DateTimeOffset> memberOpenTimesUtc,
        DateTimeOffset? validatingCandleOpenTimeUtc = null)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (!Enum.IsDefined(boundaryPrecision)) throw new ArgumentOutOfRangeException(nameof(boundaryPrecision));
        if (lowerBound > upperBound) throw new ArgumentException("Zone lower bound cannot exceed its upper bound.", nameof(lowerBound));
        ArgumentNullException.ThrowIfNull(memberOpenTimesUtc);
        var members = memberOpenTimesUtc.ToArray();
        if (members.Length == 0 || members.Any(item => item.Offset != TimeSpan.Zero) || members.Distinct().Count() != members.Length)
            throw new ArgumentException("Members must be a non-empty set of distinct UTC H4 open timestamps.", nameof(memberOpenTimesUtc));
        if (validatingCandleOpenTimeUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Validation candle timestamp must be UTC.", nameof(validatingCandleOpenTimeUtc));
        Role = role;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        BoundaryPrecision = boundaryPrecision;
        MemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(members.OrderBy(item => item).ToArray());
        ValidatingCandleOpenTimeUtc = validatingCandleOpenTimeUtc;
    }

    public NasdaqHumanH4StructuralRole Role { get; }
    public decimal LowerBound { get; }
    public decimal UpperBound { get; }
    public NasdaqHumanH4ZoneBoundaryPrecision BoundaryPrecision { get; }
    public IReadOnlyList<DateTimeOffset> MemberOpenTimesUtc { get; }
    public DateTimeOffset? ValidatingCandleOpenTimeUtc { get; }

    public int CompareTo(NasdaqHumanH4StructuralZoneAnchor? other)
    {
        if (other is null) return 1;
        var comparison = Role.CompareTo(other.Role);
        if (comparison != 0) return comparison;
        comparison = LowerBound.CompareTo(other.LowerBound);
        if (comparison != 0) return comparison;
        comparison = UpperBound.CompareTo(other.UpperBound);
        if (comparison != 0) return comparison;
        comparison = BoundaryPrecision.CompareTo(other.BoundaryPrecision);
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

    public bool Equals(NasdaqHumanH4StructuralZoneAnchor? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanH4StructuralZoneAnchor);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Role); hash.Add(LowerBound); hash.Add(UpperBound); hash.Add(BoundaryPrecision); hash.Add(ValidatingCandleOpenTimeUtc);
        foreach (var member in MemberOpenTimesUtc) hash.Add(member);
        return hash.ToHashCode();
    }
}

/// <summary>Precision asserted by the human source; Approximate defines no tolerance or arithmetic rule.</summary>
public enum NasdaqHumanH4ZoneBoundaryPrecision { Exact, Approximate }
