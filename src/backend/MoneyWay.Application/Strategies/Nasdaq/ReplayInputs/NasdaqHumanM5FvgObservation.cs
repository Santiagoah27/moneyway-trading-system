using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Explicit relevant-FVG selection for an exact trigger, not a rule-pass assertion.</summary>
public sealed class NasdaqHumanM5FvgObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanM5FvgObservation(NasdaqHumanM5TriggerObservation trigger, NasdaqHumanM5FvgEvent marketEvent,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        Event = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (marketEvent.Candle3.ProviderId != Session.ProviderId || marketEvent.Candle3.Symbol != Session.Symbol)
            throw new ArgumentException("FVG sources must match the exact trigger provider and instrument.");
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < EffectiveAtUtc || observedAtUtc < trigger.ObservedAtUtc)
            throw new ArgumentException("Availability must be UTC and no earlier than the FVG and relied-upon trigger evidence.");
        ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }
    public NasdaqHumanM5TriggerObservation Trigger { get; }
    public NasdaqHumanM5FvgEvent Event { get; }
    public NasdaqDemoSessionIdentity Session => Trigger.Session;
    public NasdaqHumanH4PermittedDirection Direction => Trigger.Event.Direction;
    public DateTimeOffset EffectiveAtUtc => Event.EffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    internal bool SameFact(NasdaqHumanM5FvgObservation other) => Trigger.SameFact(other.Trigger) && Event.SameFact(other.Event);
}
