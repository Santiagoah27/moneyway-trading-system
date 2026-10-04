using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Explicit human selection of one corrective event for one exact approved FVG.</summary>
public sealed class NasdaqHumanM1CorrectiveRetracementObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanM1CorrectiveRetracementObservation(NasdaqHumanM5FvgQualityObservation approvedQuality,
        NasdaqHumanM1CorrectiveRetracementEvent marketEvent, DateTimeOffset observedAtUtc, string sourceReference)
    {
        ApprovedQuality = approvedQuality ?? throw new ArgumentNullException(nameof(approvedQuality));
        Event = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (approvedQuality.Fact.Decision != NasdaqHumanM5FvgQualityDecision.Approved)
            throw new ArgumentException("The exact FVG quality must be Approved.", nameof(approvedQuality));
        if (marketEvent.SourceCandles.Any(c => c.ProviderId != Session.ProviderId || c.Symbol != Session.Symbol))
            throw new ArgumentException("1M sources must match the approved FVG provider and instrument.", nameof(marketEvent));
        if (marketEvent.PullbackEffectiveAtUtc < approvedQuality.EffectiveAtUtc)
            throw new ArgumentException("Pullback confirmation cannot precede quality approval.", nameof(marketEvent));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < marketEvent.PullbackEffectiveAtUtc
            || observedAtUtc < approvedQuality.ObservedAtUtc)
            throw new ArgumentException("Availability must be UTC and follow the pullback and relied-upon quality evidence.", nameof(observedAtUtc));
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHumanM5FvgQualityObservation ApprovedQuality { get; }
    public NasdaqHumanM1CorrectiveRetracementEvent Event { get; }
    public NasdaqDemoSessionIdentity Session => ApprovedQuality.Session;
    public NasdaqHumanH4PermittedDirection SetupDirection => ApprovedQuality.Fact.Fvg.Direction;
    public DateTimeOffset PullbackEffectiveAtUtc => Event.PullbackEffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal bool SameFact(NasdaqHumanM1CorrectiveRetracementObservation other) =>
        ApprovedQuality.Fact.SameFact(other.ApprovedQuality.Fact) && SetupDirection == other.SetupDirection
        && Event.SameFact(other.Event);
}
