using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Immutable human-reviewed H4 membership assertion for one post-migration rebuilt candidate vertex.
/// It stores source identities only; deterministic consumers own structural geometry.
/// </summary>
public sealed class NasdaqHumanRebuiltCandidateVertexObservation : IStrategyReplayInputObservation,
    IEquatable<NasdaqHumanRebuiltCandidateVertexObservation>
{
    public static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;

    public NasdaqHumanRebuiltCandidateVertexObservation(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        DateTimeOffset invalidatingCandleOpenTimeUtc,
        DateTimeOffset migrationCandleOpenTimeUtc,
        StructuralCandidateExtremeSide candidateSide,
        IEnumerable<DateTimeOffset> selectedMemberOpenTimesUtc,
        DateTimeOffset observedAtUtc,
        string sourceReference)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        if (strategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Rebuilt candidate vertex input must belong to MoneyWay Nasdaq.", nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (invalidatingCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalidating candle timestamp must be UTC.", nameof(invalidatingCandleOpenTimeUtc));
        if (migrationCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Migration candle timestamp must be UTC.", nameof(migrationCandleOpenTimeUtc));
        if (!Enum.IsDefined(candidateSide))
            throw new ArgumentOutOfRangeException(nameof(candidateSide), "The candidate side is not supported.");
        ArgumentNullException.ThrowIfNull(selectedMemberOpenTimesUtc);
        if (observedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Observation timestamp must be UTC.", nameof(observedAtUtc));
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));

        var members = selectedMemberOpenTimesUtc.ToArray();
        if (members.Length == 0)
            throw new ArgumentException("At least one selected rebuilt candidate vertex member is required.", nameof(selectedMemberOpenTimesUtc));
        if (members.Any(item => item.Offset != TimeSpan.Zero))
            throw new ArgumentException("Selected member timestamps must be UTC.", nameof(selectedMemberOpenTimesUtc));
        if (members.Distinct().Count() != members.Length)
            throw new ArgumentException("Selected rebuilt candidate vertex members must be distinct.", nameof(selectedMemberOpenTimesUtc));
        if (!members.Contains(migrationCandleOpenTimeUtc))
            throw new ArgumentException("Selected members must include the migration candle.", nameof(selectedMemberOpenTimesUtc));

        var latestClose = members.Append(migrationCandleOpenTimeUtc).Max().AddHours(4);
        if (observedAtUtc < latestClose)
            throw new ArgumentException("Observation cannot precede the close of its selected H4 members.", nameof(observedAtUtc));

        InvalidatingCandleOpenTimeUtc = invalidatingCandleOpenTimeUtc;
        MigrationCandleOpenTimeUtc = migrationCandleOpenTimeUtc;
        CandidateSide = candidateSide;
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
    public DateTimeOffset MigrationCandleOpenTimeUtc { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
    public IReadOnlyList<DateTimeOffset> SelectedMemberOpenTimesUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    public bool Equals(NasdaqHumanRebuiltCandidateVertexObservation? other) =>
        other is not null
        && StrategyId == other.StrategyId
        && StrategyVersion == other.StrategyVersion
        && ProviderId == other.ProviderId
        && Symbol == other.Symbol
        && InvalidatingCandleOpenTimeUtc == other.InvalidatingCandleOpenTimeUtc
        && MigrationCandleOpenTimeUtc == other.MigrationCandleOpenTimeUtc
        && CandidateSide == other.CandidateSide
        && ObservedAtUtc == other.ObservedAtUtc
        && SourceReference == other.SourceReference
        && SelectedMemberOpenTimesUtc.SequenceEqual(other.SelectedMemberOpenTimesUtc);

    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanRebuiltCandidateVertexObservation);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StrategyId); hash.Add(StrategyVersion); hash.Add(ProviderId); hash.Add(Symbol);
        hash.Add(InvalidatingCandleOpenTimeUtc); hash.Add(MigrationCandleOpenTimeUtc); hash.Add(CandidateSide);
        hash.Add(ObservedAtUtc); hash.Add(SourceReference);
        foreach (var member in SelectedMemberOpenTimesUtc) hash.Add(member);
        return hash.ToHashCode();
    }
}
