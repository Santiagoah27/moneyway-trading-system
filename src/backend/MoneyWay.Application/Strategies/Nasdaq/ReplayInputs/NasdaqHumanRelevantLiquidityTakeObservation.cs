using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human identification of the relevant initiating event, not a rule result or an automatic priority.</summary>
public sealed record NasdaqHumanRelevantLiquidityTakeObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanRelevantLiquidityTakeObservation(NasdaqHumanLiquidityTakeObservation take, DateTimeOffset observedAtUtc, string sourceReference, NasdaqHumanLiquidityTakeObservation? initiatingTake = null)
    {
        ArgumentNullException.ThrowIfNull(take);
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < take.ObservedAtUtc)
            throw new ArgumentException("Relevant selection cannot precede availability of the selected take.", nameof(observedAtUtc));
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Retained human selection provenance is required.", nameof(sourceReference));
        if (initiatingTake is not null && (initiatingTake.Session != take.Session || initiatingTake.EffectiveAtUtc >= take.EffectiveAtUtc
            || initiatingTake.ObservedAtUtc > observedAtUtc))
            throw new ArgumentException("A later pre-entry event must bind an earlier observable initiating take in the same session.", nameof(initiatingTake));
        InitiatingTake = initiatingTake;
        Take = take; ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }
    public NasdaqHumanLiquidityTakeObservation? InitiatingTake { get; }
    public NasdaqHumanLiquidityTakeObservation Take { get; }
    public NasdaqDemoSessionIdentity Session => Take.Session;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
}
