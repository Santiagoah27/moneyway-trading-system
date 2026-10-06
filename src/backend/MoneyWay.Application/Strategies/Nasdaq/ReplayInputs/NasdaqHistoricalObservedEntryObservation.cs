using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Authentic replay availability of one documented historical execution.</summary>
public sealed class NasdaqHistoricalObservedEntryObservation : IStrategyReplayInputObservation
{
    public NasdaqHistoricalObservedEntryObservation(NasdaqHistoricalObservedEntry entry,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < entry.EntryEffectiveAtUtc
            || entry.SupportingCandles.Any(c => c.CloseTimeUtc > observedAtUtc))
            throw new ArgumentException("Assertion availability must be UTC and follow execution and supporting records.", nameof(observedAtUtc));
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHistoricalObservedEntry Entry { get; }
    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility => Entry.PreEntryEligibility;
    public NasdaqDemoSessionIdentity Session => Entry.Session;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
}
