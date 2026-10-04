using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanM5FvgQualityObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanM5FvgQualityObservation(NasdaqHumanM5FvgQualityFact fact, DateTimeOffset observedAtUtc, string sourceReference)
    {
        Fact = fact ?? throw new ArgumentNullException(nameof(fact));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < fact.EffectiveAtUtc || observedAtUtc < fact.Fvg.ObservedAtUtc)
            throw new ArgumentException("Quality availability must be UTC and no earlier than the review and relied-upon FVG evidence.");
        ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }
    public NasdaqHumanM5FvgQualityFact Fact { get; }
    public NasdaqDemoSessionIdentity Session => Fact.Fvg.Session;
    public DateTimeOffset EffectiveAtUtc => Fact.EffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
}
