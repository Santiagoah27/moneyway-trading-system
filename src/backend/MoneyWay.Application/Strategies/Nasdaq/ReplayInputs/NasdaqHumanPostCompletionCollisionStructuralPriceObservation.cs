using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One human price assertion for an exact post-completion 008 collision; supplies no wick or geometry facts.</summary>
public sealed class NasdaqHumanPostCompletionCollisionStructuralPriceObservation : IStrategyReplayInputObservation,
    IEquatable<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>
{
    public NasdaqHumanPostCompletionCollisionStructuralPriceObservation(
        NasdaqPostCompletionBreakoutAwaitingCompletionState awaitingState, decimal structuralPrice,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(awaitingState);
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < awaitingState.CollisionCandle.CloseTimeUtc)
            throw new ArgumentException("Observation must be UTC and cannot precede the collision close.", nameof(observedAtUtc));
        Context = awaitingState.EvidenceContext;
        StructuralPrice = structuralPrice;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqPostCompletionCollisionStructuralPriceContext Context { get; }
    public StrategyId StrategyId => Context.Episode.PreviousCompletedEpisode.StrategyId;
    public StrategyVersion StrategyVersion => Context.Episode.PreviousCompletedEpisode.StrategyVersion;
    public MarketDataProviderId ProviderId => Context.Episode.PreviousCompletedEpisode.ProviderId;
    public MarketSymbol Symbol => Context.Episode.PreviousCompletedEpisode.Symbol;
    public Timeframe Timeframe => Context.Episode.PreviousCompletedEpisode.Timeframe;
    public decimal StructuralPrice { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    public bool Equals(NasdaqHumanPostCompletionCollisionStructuralPriceObservation? other) => other is not null
        && Context == other.Context && StructuralPrice == other.StructuralPrice
        && ObservedAtUtc == other.ObservedAtUtc && SourceReference == other.SourceReference;
    public override bool Equals(object? obj) => Equals(obj as NasdaqHumanPostCompletionCollisionStructuralPriceObservation);
    public override int GetHashCode() => HashCode.Combine(Context, StructuralPrice, ObservedAtUtc, SourceReference);
}
