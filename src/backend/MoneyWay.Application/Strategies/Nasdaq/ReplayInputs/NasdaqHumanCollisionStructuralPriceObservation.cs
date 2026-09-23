using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Immutable human-reviewed definitive price assertion for one NQ-Q-H4-008 collision.
/// The collision state remains responsible for breakout and protection-anchor facts.
/// </summary>
public sealed class NasdaqHumanCollisionStructuralPriceObservation : IStrategyReplayInputObservation,
    IEquatable<NasdaqHumanCollisionStructuralPriceObservation>
{
    public NasdaqHumanCollisionStructuralPriceObservation(
        NasdaqHumanOriginVertexEpisode episode,
        DateTimeOffset collisionCandleOpenTimeUtc,
        StructuralCandidateExtremeSide candidateSide,
        decimal structuralPrice,
        DateTimeOffset observedAtUtc,
        string sourceReference)
    {
        Episode = episode ?? throw new ArgumentNullException(nameof(episode));
        if (episode.StrategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Collision StructuralPrice input must belong to MoneyWay Nasdaq.", nameof(episode));
        if (collisionCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Collision candle timestamp must be UTC.", nameof(collisionCandleOpenTimeUtc));
        if (!Enum.IsDefined(candidateSide))
            throw new ArgumentOutOfRangeException(nameof(candidateSide), "The candidate side is not supported.");
        if (observedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Observation timestamp must be UTC.", nameof(observedAtUtc));
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));
        if (observedAtUtc < collisionCandleOpenTimeUtc.AddHours(4))
            throw new ArgumentException("Observation cannot precede the close of its collision H4 candle.", nameof(observedAtUtc));

        CollisionCandleOpenTimeUtc = collisionCandleOpenTimeUtc;
        CandidateSide = candidateSide;
        StructuralPrice = structuralPrice;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHumanOriginVertexEpisode Episode { get; }
    public StrategyId StrategyId => Episode.StrategyId;
    public StrategyVersion StrategyVersion => Episode.StrategyVersion;
    public MarketDataProviderId ProviderId => Episode.ProviderId;
    public MarketSymbol Symbol => Episode.Symbol;
    public Timeframe Timeframe => Episode.Timeframe;
    public DateTimeOffset InvalidatingCandleOpenTimeUtc => Episode.InvalidatingCandleOpenTimeUtc;
    public DateTimeOffset CollisionCandleOpenTimeUtc { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
    public decimal StructuralPrice { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    public bool Equals(NasdaqHumanCollisionStructuralPriceObservation? other) =>
        other is not null
        && Episode == other.Episode
        && CollisionCandleOpenTimeUtc == other.CollisionCandleOpenTimeUtc
        && CandidateSide == other.CandidateSide
        && StructuralPrice == other.StructuralPrice
        && ObservedAtUtc == other.ObservedAtUtc
        && SourceReference == other.SourceReference;

    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanCollisionStructuralPriceObservation);

    public override int GetHashCode() => HashCode.Combine(
        Episode,
        CollisionCandleOpenTimeUtc,
        CandidateSide,
        StructuralPrice,
        ObservedAtUtc,
        SourceReference);
}
