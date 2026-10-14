using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Explicit human selection of one corrective event for one exact M5 trigger with optional approved FVG support.</summary>
public sealed class NasdaqHumanM1CorrectiveRetracementObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanM1CorrectiveRetracementObservation(NasdaqHumanM5FvgQualityObservation approvedQuality,
        NasdaqHumanM1CorrectiveRetracementEvent marketEvent, DateTimeOffset observedAtUtc, string sourceReference)
        : this((approvedQuality ?? throw new ArgumentNullException(nameof(approvedQuality))).Fact.Fvg.Trigger,
            marketEvent, observedAtUtc, sourceReference, approvedQuality)
    { }

    public NasdaqHumanM1CorrectiveRetracementObservation(NasdaqHumanM5TriggerObservation trigger,
        NasdaqHumanM1CorrectiveRetracementEvent marketEvent, DateTimeOffset observedAtUtc, string sourceReference,
        NasdaqHumanM5FvgQualityObservation? approvedQuality = null)
    {
        Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        ApprovedQuality = approvedQuality;
        Event = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (approvedQuality is not null && (approvedQuality.Fact.Decision != NasdaqHumanM5FvgQualityDecision.Approved
            || !approvedQuality.Fact.Fvg.Trigger.SameFact(trigger)))
            throw new ArgumentException("The exact FVG quality must be Approved.", nameof(approvedQuality));
        if (marketEvent.SourceCandles.Any(c => c.ProviderId != Session.ProviderId || c.Symbol != Session.Symbol))
            throw new ArgumentException("1M sources must match the selected M5 trigger provider and instrument.", nameof(marketEvent));
        if (marketEvent.PullbackEffectiveAtUtc < trigger.EffectiveAtUtc
            || approvedQuality is not null && marketEvent.PullbackEffectiveAtUtc < approvedQuality.EffectiveAtUtc)
            throw new ArgumentException("Pullback confirmation cannot precede its M5 trigger or relied-upon quality approval.", nameof(marketEvent));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < marketEvent.PullbackEffectiveAtUtc
            || observedAtUtc < trigger.ObservedAtUtc
            || approvedQuality is not null && observedAtUtc < approvedQuality.ObservedAtUtc)
            throw new ArgumentException("Availability must be UTC and follow the pullback and relied-upon trigger/support evidence.", nameof(observedAtUtc));
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHumanM5TriggerObservation Trigger { get; }
    public NasdaqHumanM5FvgQualityObservation? ApprovedQuality { get; }
    public NasdaqHumanM1CorrectiveRetracementEvent Event { get; }
    public NasdaqDemoSessionIdentity Session => Trigger.Session;
    public NasdaqHumanH4PermittedDirection SetupDirection => Trigger.Event.Direction;
    public DateTimeOffset PullbackEffectiveAtUtc => Event.PullbackEffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal bool SameFact(NasdaqHumanM1CorrectiveRetracementObservation other) =>
        Trigger.SameFact(other.Trigger) && SameSupport(ApprovedQuality, other.ApprovedQuality) && SetupDirection == other.SetupDirection
        && Event.SameFact(other.Event);
    private static bool SameSupport(NasdaqHumanM5FvgQualityObservation? one, NasdaqHumanM5FvgQualityObservation? other) =>
        one is null ? other is null : other is not null && one.Fact.SameFact(other.Fact);
}
