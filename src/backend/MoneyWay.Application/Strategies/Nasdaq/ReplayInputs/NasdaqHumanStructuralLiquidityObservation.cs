using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human selection of an exact structural reference set for one Demo session, not a rule override.</summary>
public sealed class NasdaqHumanStructuralLiquidityObservation : IStrategyReplayInputObservation, IEquatable<NasdaqHumanStructuralLiquidityObservation>
{
    public NasdaqHumanStructuralLiquidityObservation(NasdaqDemoSessionIdentity session,
        IEnumerable<NasdaqStructuralLiquidityReference> references, DateTimeOffset effectiveAtUtc,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(references);
        if (session.StrategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Structural liquidity must belong to Nasdaq.", nameof(session));
        if (effectiveAtUtc.Offset != TimeSpan.Zero || observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc)
            throw new ArgumentException("Effective time and availability must be UTC, with effective time no later than availability.");
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        var snapshot = references.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(item => item is null) || snapshot.Distinct().Count() != snapshot.Length)
            throw new ArgumentException("A nonempty set of distinct non-null structural references is required.", nameof(references));
        if (snapshot.Any(item => !item.MatchesSession(session)))
            throw new ArgumentException("All sources and structural episodes must match the exact session identity.", nameof(references));
        Session = session;
        References = new ReadOnlyCollection<NasdaqStructuralLiquidityReference>(snapshot.OrderBy(item => item).ToArray());
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqDemoSessionIdentity Session { get; }
    public IReadOnlyList<NasdaqStructuralLiquidityReference> References { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    internal string ProvenanceKey => System.Text.Json.JsonSerializer.Serialize(References.Select(item => item.ProvenanceKey));

    public bool Equals(NasdaqHumanStructuralLiquidityObservation? other) => other is not null && Session == other.Session
        && ObservedAtUtc == other.ObservedAtUtc && SourceReference == other.SourceReference && CompareFact(other) == 0
        && ProvenanceKey == other.ProvenanceKey;
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanStructuralLiquidityObservation);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Session); hash.Add(EffectiveAtUtc); hash.Add(ObservedAtUtc); hash.Add(SourceReference); hash.Add(ProvenanceKey);
        foreach (var reference in References) hash.Add(reference);
        return hash.ToHashCode();
    }

    internal int CompareFact(NasdaqHumanStructuralLiquidityObservation other)
    {
        var comparison = EffectiveAtUtc.CompareTo(other.EffectiveAtUtc);
        if (comparison != 0) return comparison;
        for (var index = 0; index < Math.Min(References.Count, other.References.Count); index++)
        {
            comparison = References[index].CompareTo(other.References[index]);
            if (comparison != 0) return comparison;
        }
        return References.Count.CompareTo(other.References.Count);
    }
}
