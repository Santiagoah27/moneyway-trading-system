using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A source-documented pre-entry risk assessment for exact eligibility and selected SL.</summary>
public sealed class NasdaqRiskExposureObservation : IStrategyReplayInputObservation
{
    public NasdaqRiskExposureObservation(NasdaqPreEntryEligibilityRuleFact preEntryEligibility,
        NasdaqHumanStructuralStopLossRuleFact stopLoss, NasdaqRiskExposure exposure,
        DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        PreEntryEligibility = preEntryEligibility ?? throw new ArgumentNullException(nameof(preEntryEligibility));
        StopLoss = stopLoss ?? throw new ArgumentNullException(nameof(stopLoss));
        Exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (!SameEligibility(preEntryEligibility, stopLoss.PreEntryEligibility))
            throw new ArgumentException("Risk eligibility and SL must retain the same exact setup ancestry.", nameof(stopLoss));
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc < stopLoss.EffectiveAtUtc
            || effectiveAtUtc < preEntryEligibility.Realignment.Selection.Fact.ObservedAtUtc
            || effectiveAtUtc < stopLoss.Selection.SupportingObservations.Min(o => o.ObservedAtUtc))
            throw new ArgumentException("Risk assessment must be UTC and follow availability of its selected parameters.", nameof(effectiveAtUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc)
            throw new ArgumentException("Assertion availability must be UTC and follow the risk assessment.", nameof(observedAtUtc));
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqHumanStructuralStopLossRuleFact StopLoss { get; }
    public NasdaqRiskExposure Exposure { get; }
    /// <summary>Documented pre-entry risk assessment time for the selected parameters.</summary>
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal static bool SameEligibility(NasdaqPreEntryEligibilityRuleFact one, NasdaqPreEntryEligibilityRuleFact other) =>
        one.Session == other.Session && one.Direction == other.Direction
        && one.Realignment.Selection.Fact.SameFact(other.Realignment.Selection.Fact);

    internal bool SameFact(NasdaqRiskExposureObservation other) =>
        SameEligibility(PreEntryEligibility, other.PreEntryEligibility)
        && StopLoss.Selection.Fact.SameFact(other.StopLoss.Selection.Fact)
        && Exposure.SameFact(other.Exposure) && EffectiveAtUtc == other.EffectiveAtUtc;
}
