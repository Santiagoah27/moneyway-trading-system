using System.Text.Json.Serialization;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Native documented planned exposure. Preserves the original canonical SL object; no size, conversion or fill.</summary>
public sealed class NasdaqWickfillScenario2RiskExposureObservation : IStrategyReplayInputObservation
{
    public NasdaqWickfillScenario2RiskExposureObservation(StrategyReplayContextObservation stopOwner,
        MarketDataProviderId exposureProviderId, MarketSymbol exposureSymbol, NasdaqRiskExposure exposure,
        DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        StopOwner = stopOwner ?? throw new ArgumentNullException(nameof(stopOwner));
        StopLoss = NasdaqHumanWickfillScenario2StopLossRuleFact.RequireOwner(stopOwner);
        Exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
        if (exposureProviderId != Session.ProviderId || exposureSymbol != Session.Symbol)
            throw new ArgumentException("Risk prices must belong to the exact native SL feed/instrument; USTEC is not US100.");
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc < StopLoss.EffectiveAtUtc
            || effectiveAtUtc < StopLoss.Selection.SupportingObservations.Min(o => o.ObservedAtUtc)
            || effectiveAtUtc < StopLoss.PreEntryEligibility.Reversal.Reversal.Fact.ObservedAtUtc)
            throw new ArgumentException("Original risk assessment must be UTC and follow availability of its selected parameters.", nameof(effectiveAtUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc)
            throw new ArgumentException("Original risk availability must be UTC and follow the assessment.", nameof(observedAtUtc));
        EffectiveAtUtc = effectiveAtUtc; ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }
    [JsonIgnore] public StrategyReplayContextObservation StopOwner { get; }
    public NasdaqHumanWickfillScenario2StopLossRuleFact StopLoss { get; }
    public NasdaqRiskExposure Exposure { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public NasdaqDemoSessionIdentity Session => StopLoss.Session;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    internal bool SameFact(NasdaqWickfillScenario2RiskExposureObservation other) =>
        ReferenceEquals(StopLoss.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation,
            other.StopLoss.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation)
        && StopLoss.Selection.Fact.SameFact(other.StopLoss.Selection.Fact)
        && Exposure.SameFact(other.Exposure) && EffectiveAtUtc == other.EffectiveAtUtc;
}
