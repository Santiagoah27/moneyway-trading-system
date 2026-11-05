using System.Text.Json.Serialization;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Complete source-backed initial SL assertion for Scenario 2 SELL only; not a rule fact or order.
/// Incomplete documentary testimony remains outside this evaluable input. No cross-feed reconciliation is provided.</summary>
/// <remarks>Effective/Observed times retain original selection/parameter chronology, even before canonical eligibility.
/// The selector independently checks the later actual prior owner; timestamps are never clamped to its emission.</remarks>
public sealed class NasdaqHumanWickfillScenario2StopLossObservation : IStrategyReplayInputObservation
{
    private static readonly TimeZoneInfo StrategyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public NasdaqHumanWickfillScenario2StopLossObservation(StrategyReplayContextObservation eligibilityOwner,
        NasdaqHumanWickfillScenario2StopLossReference protectiveReference, decimal? stopPrice,
        DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(eligibilityOwner);
        ProtectiveReference = protectiveReference ?? throw new ArgumentNullException(nameof(protectiveReference));
        PreEntryEligibility = RequireEligibility(eligibilityOwner);
        EligibilityOwner = eligibilityOwner;
        StartOwner = PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation;
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (protectiveReference.ProviderId != ProviderId || protectiveReference.Symbol != Symbol)
            throw new ArgumentException("The protective price must belong to the exact native setup feed; visual USTEC is not US100.", nameof(protectiveReference));
        if (stopPrice is null || stopPrice <= protectiveReference.Price)
            throw new ArgumentException("An independently documented stop strictly above the selected sweep peak is required.", nameof(stopPrice));
        if (effectiveAtUtc == default || effectiveAtUtc.Offset != TimeSpan.Zero
            || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(effectiveAtUtc, StrategyTimeZone).DateTime) != Session.TradingDay)
            throw new ArgumentException("Authentic selection/parameter time must be specified in UTC for the exact session.", nameof(effectiveAtUtc));
        if (observedAtUtc == default || observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc
            || observedAtUtc < protectiveReference.ObservedAtUtc)
            throw new ArgumentException("Authentic UTC availability must follow the assertion and its protective source.", nameof(observedAtUtc));
        StopPrice = stopPrice.Value;
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    [JsonIgnore] public StrategyReplayContextObservation EligibilityOwner { get; }
    [JsonIgnore] public StrategyReplayContextObservation StartOwner { get; }
    public NasdaqWickfillScenario2PreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqHumanWickfillScenario2StopLossReference ProtectiveReference { get; }
    public decimal StopPrice { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqStrategyPath Path => PreEntryEligibility.Reversal.Applicability.Fact.Path;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal static NasdaqWickfillScenario2PreEntryEligibilityRuleFact RequireEligibility(StrategyReplayContextObservation owner)
    {
        var fact = owner.RuleFacts.Where(f => f.RuleId.Value == "NQ-WF2-002").Select(f => f.Fact)
            .OfType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>().SingleOrDefault();
        if (fact is null || fact.Direction != NasdaqHumanH4PermittedDirection.Sell
            || fact.Reversal.Applicability.Fact.Path != NasdaqStrategyPath.WickfillScenario2
            || owner.Evaluations.SingleOrDefault(e => e.RuleId.Value == "NQ-WF2-002")?.Result != RuleEvaluationResult.Passed
            || owner.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-WF2-002")?.EstablishesProgression != true
            || fact.Session.StrategyId != owner.StrategyId || fact.Session.StrategyVersion != owner.StrategyVersion
            || fact.Session.ProviderId != owner.ProviderId || fact.Session.Symbol != owner.Symbol
            || fact.EligibilityEffectiveAtUtc > owner.AsOfUtc)
            throw new ArgumentException("An established canonical Scenario 2 SELL eligibility owner is required.", nameof(owner));
        var start = fact.Reversal.Applicability.Fact.InitialTakeObservation;
        var initial = NasdaqHumanBranchApplicabilityObservation.RequireInitialTake(start);
        if (!ReferenceEquals(start, fact.Reversal.Reversal.Fact.OpeningSweep.TakeObservation)
            || !ReferenceEquals(initial, fact.Reversal.Reversal.Fact.OpeningSweep.Take)
            || initial.Take.Reference is not NasdaqLiquidityTakeReference.SessionLevel { Endpoint: NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh }
            || owner.LifecycleProgression?.ActiveInstance is not { } active
            || active.InstanceId != start.LifecycleProgression!.ActiveInstance!.InstanceId || active.StartedAtUtc != start.AsOfUtc)
            throw new ArgumentException("Eligibility must retain the exact initiating London High take and surviving Start owner.", nameof(owner));
        return fact;
    }

    internal bool SameFact(NasdaqHumanWickfillScenario2StopLossObservation other) =>
        ReferenceEquals(StartOwner, other.StartOwner)
        && PreEntryEligibility.Reversal.Reversal.Fact.SameFact(other.PreEntryEligibility.Reversal.Reversal.Fact)
        && ProtectiveReference.SameFact(other.ProtectiveReference) && StopPrice == other.StopPrice && EffectiveAtUtc == other.EffectiveAtUtc;
}
