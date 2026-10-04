using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable semantic assertion for an exact take, with authentic review availability and retained provenance.</summary>
public sealed class NasdaqHumanM5TriggerObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanM5TriggerObservation(NasdaqHumanLiquidityTakeObservation decisiveTake, NasdaqHumanM5TriggerEvent marketEvent,
        DateTimeOffset observedAtUtc, string sourceReference,
        NasdaqHumanM5TakeTriggerOrder sameCandleOrder = NasdaqHumanM5TakeTriggerOrder.Unspecified)
    {
        DecisiveTake = decisiveTake ?? throw new ArgumentNullException(nameof(decisiveTake));
        Event = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (!Enum.IsDefined(sameCandleOrder)) throw new ArgumentOutOfRangeException(nameof(sameCandleOrder));
        if (marketEvent.ConfirmationCandle.ProviderId != Session.ProviderId || marketEvent.ConfirmationCandle.Symbol != Session.Symbol)
            throw new ArgumentException("Trigger must match the exact take source and instrument.");
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < EffectiveAtUtc || observedAtUtc < decisiveTake.ObservedAtUtc)
            throw new ArgumentException("Human availability must be UTC and no earlier than the trigger and relied-upon take evidence.");
        ObservedAtUtc = observedAtUtc; SourceReference = sourceReference; SameCandleOrder = sameCandleOrder;
    }
    public NasdaqHumanLiquidityTakeObservation DecisiveTake { get; }
    public NasdaqHumanM5TriggerEvent Event { get; }
    public NasdaqDemoSessionIdentity Session => DecisiveTake.Session;
    public DateTimeOffset EffectiveAtUtc => Event.EffectiveAtUtc;
    public NasdaqHumanM5TakeTriggerOrder SameCandleOrder { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    internal bool SameFact(NasdaqHumanM5TriggerObservation other) => DecisiveTake.SameFact(other.DecisiveTake)
        && Event.SameFact(other.Event) && SameCandleOrder == other.SameCandleOrder;
}
