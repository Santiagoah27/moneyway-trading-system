using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Canonical documented parameters for one setup, without trade compliance or economic outcome claims.</summary>
public sealed class NasdaqHistoricalTradeSnapshot
{
    internal NasdaqHistoricalTradeSnapshot(DateTimeOffset asOfUtc, NasdaqPreEntryEligibilityRuleFact eligibility,
        NasdaqHistoricalObservedEntrySelection.Unique entry, NasdaqHumanStructuralStopLossRuleFact stopLoss,
        NasdaqHumanTakeProfitRuleFact takeProfit, NasdaqRiskExposureRuleFact risk)
    {
        AsOfUtc = asOfUtc;
        PreEntryEligibility = eligibility;
        ObservedEntry = entry;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        Risk = risk;
    }

    public DateTimeOffset AsOfUtc { get; }
    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    public NasdaqHistoricalObservedEntrySelection.Unique ObservedEntry { get; }
    public NasdaqHistoricalObservedEntry Entry => ObservedEntry.Fact.Entry;
    public NasdaqHumanStructuralStopLossRuleFact StopLoss { get; }
    public NasdaqHumanTakeProfitRuleFact TakeProfit { get; }
    public NasdaqRiskExposureRuleFact Risk { get; }
    public decimal EntryPrice => Entry.EntryPrice;
    public DateTimeOffset EntryEffectiveAtUtc => Entry.EntryEffectiveAtUtc;
    public decimal StopPrice => StopLoss.StopPrice;
    public decimal TakeProfitPrice => TakeProfit.TakeProfitPrice;
}
