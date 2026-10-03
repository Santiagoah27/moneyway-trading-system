using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact human assertion, including unresolved reviews; not a validated lifecycle state or a rule result.</summary>
public sealed class NasdaqHumanH4ContextFact : IEquatable<NasdaqHumanH4ContextFact>, IComparable<NasdaqHumanH4ContextFact>
{
    public NasdaqHumanH4ContextFact(NasdaqHumanH4PermittedDirection permittedDirection, NasdaqHumanH4ContextKind contextKind,
        DateTimeOffset contextCandleOpenTimeUtc, DateTimeOffset effectiveAtUtc, IEnumerable<NasdaqHumanH4StructuralAnchor> anchors)
    {
        if (!Enum.IsDefined(permittedDirection)) throw new ArgumentOutOfRangeException(nameof(permittedDirection));
        if (!Enum.IsDefined(contextKind)) throw new ArgumentOutOfRangeException(nameof(contextKind));
        if (contextCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Context candle timestamp must be UTC.", nameof(contextCandleOpenTimeUtc));
        if (effectiveAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Effective timestamp must be UTC.", nameof(effectiveAtUtc));
        ArgumentNullException.ThrowIfNull(anchors);
        var snapshot = anchors.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(item => item is null) || snapshot.Distinct().Count() != snapshot.Length)
            throw new ArgumentException("At least one distinct non-null structural anchor is required.", nameof(anchors));
        PermittedDirection = permittedDirection;
        ContextKind = contextKind;
        ContextCandleOpenTimeUtc = contextCandleOpenTimeUtc;
        EffectiveAtUtc = effectiveAtUtc;
        Anchors = new ReadOnlyCollection<NasdaqHumanH4StructuralAnchor>(snapshot.OrderBy(item => item).ToArray());
    }

    public NasdaqHumanH4PermittedDirection PermittedDirection { get; }
    public NasdaqHumanH4ContextKind ContextKind { get; }
    public DateTimeOffset ContextCandleOpenTimeUtc { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public IReadOnlyList<NasdaqHumanH4StructuralAnchor> Anchors { get; }

    public int CompareTo(NasdaqHumanH4ContextFact? other)
    {
        if (other is null) return 1;
        var comparison = EffectiveAtUtc.CompareTo(other.EffectiveAtUtc);
        if (comparison != 0) return comparison;
        comparison = ContextCandleOpenTimeUtc.CompareTo(other.ContextCandleOpenTimeUtc);
        if (comparison != 0) return comparison;
        comparison = PermittedDirection.CompareTo(other.PermittedDirection);
        if (comparison != 0) return comparison;
        comparison = ContextKind.CompareTo(other.ContextKind);
        if (comparison != 0) return comparison;
        for (var index = 0; index < Math.Min(Anchors.Count, other.Anchors.Count); index++)
        {
            comparison = Anchors[index].CompareTo(other.Anchors[index]);
            if (comparison != 0) return comparison;
        }
        return Anchors.Count.CompareTo(other.Anchors.Count);
    }

    public bool Equals(NasdaqHumanH4ContextFact? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanH4ContextFact);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PermittedDirection); hash.Add(ContextKind); hash.Add(ContextCandleOpenTimeUtc); hash.Add(EffectiveAtUtc);
        foreach (var anchor in Anchors) hash.Add(anchor);
        return hash.ToHashCode();
    }
}
