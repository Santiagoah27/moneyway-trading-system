using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Immutable human-reviewed membership assertion for one post-invalidation H4 origin vertex.
/// It carries source identities only; structural geometry remains calculator-owned.
/// </summary>
public sealed class NasdaqHumanOriginVertexObservation : IStrategyReplayInputObservation,
    IEquatable<NasdaqHumanOriginVertexObservation>
{
    public static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);

    public NasdaqHumanOriginVertexObservation(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        DateTimeOffset invalidatingCandleOpenTimeUtc,
        IEnumerable<DateTimeOffset> selectedMemberOpenTimesUtc,
        DateTimeOffset observedAtUtc,
        string sourceReference)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        if (strategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Origin vertex input must belong to MoneyWay Nasdaq.", nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (invalidatingCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalidating candle timestamp must be UTC.", nameof(invalidatingCandleOpenTimeUtc));
        ArgumentNullException.ThrowIfNull(selectedMemberOpenTimesUtc);
        if (observedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Observation timestamp must be UTC.", nameof(observedAtUtc));
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));

        var members = selectedMemberOpenTimesUtc.ToArray();
        if (members.Length == 0)
            throw new ArgumentException("At least one selected origin vertex member is required.", nameof(selectedMemberOpenTimesUtc));
        if (members.Any(item => item.Offset != TimeSpan.Zero))
            throw new ArgumentException("Selected member timestamps must be UTC.", nameof(selectedMemberOpenTimesUtc));
        if (members.Any(item => item >= invalidatingCandleOpenTimeUtc))
            throw new ArgumentException("Selected members must precede the invalidating candle.", nameof(selectedMemberOpenTimesUtc));
        if (members.Distinct().Count() != members.Length)
            throw new ArgumentException("Selected origin vertex members must be distinct.", nameof(selectedMemberOpenTimesUtc));

        InvalidatingCandleOpenTimeUtc = invalidatingCandleOpenTimeUtc;
        SelectedMemberOpenTimesUtc = new ReadOnlyCollection<DateTimeOffset>(members.OrderBy(item => item).ToArray());
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe => H4;
    public DateTimeOffset InvalidatingCandleOpenTimeUtc { get; }
    public IReadOnlyList<DateTimeOffset> SelectedMemberOpenTimesUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    public bool Equals(NasdaqHumanOriginVertexObservation? other) =>
        other is not null
        && StrategyId == other.StrategyId
        && StrategyVersion == other.StrategyVersion
        && ProviderId == other.ProviderId
        && Symbol == other.Symbol
        && InvalidatingCandleOpenTimeUtc == other.InvalidatingCandleOpenTimeUtc
        && ObservedAtUtc == other.ObservedAtUtc
        && SourceReference == other.SourceReference
        && SelectedMemberOpenTimesUtc.SequenceEqual(other.SelectedMemberOpenTimesUtc);

    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanOriginVertexObservation);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StrategyId); hash.Add(StrategyVersion); hash.Add(ProviderId); hash.Add(Symbol);
        hash.Add(InvalidatingCandleOpenTimeUtc); hash.Add(ObservedAtUtc); hash.Add(SourceReference);
        foreach (var member in SelectedMemberOpenTimesUtc) hash.Add(member);
        return hash.ToHashCode();
    }
}
