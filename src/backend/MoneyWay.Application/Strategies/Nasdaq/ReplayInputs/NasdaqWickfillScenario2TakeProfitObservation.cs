using System.Text.Json.Serialization;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human target selection for the original canonical WF2 SELL stop. No target discovery or adjustment.</summary>
public sealed class NasdaqWickfillScenario2TakeProfitObservation : IStrategyReplayInputObservation
{
    public NasdaqWickfillScenario2TakeProfitObservation(StrategyReplayContextObservation stopOwner,
        NasdaqHumanTakeProfitTarget target, DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        StopOwner = stopOwner ?? throw new ArgumentNullException(nameof(stopOwner));
        StopLoss = NasdaqHumanWickfillScenario2StopLossRuleFact.RequireOwner(stopOwner);
        Target = target ?? throw new ArgumentNullException(nameof(target));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (target.Reference.Session != Session || target.Side != NasdaqStructuralLiquiditySide.Low)
            throw new ArgumentException("Scenario 2 SELL requires a human-selected Low in the exact native session; USTEC is not US100.", nameof(target));
        // Same target-selection/availability contract as Standard: selection may precede SL emission.
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc < target.Reference.AvailableAtUtc
            || target.Reference.Sources.Any(c => c.CloseTimeUtc > effectiveAtUtc))
            throw new ArgumentException("Target selection must be UTC and follow observable target sources.", nameof(effectiveAtUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc
            || observedAtUtc < StopLoss.Selection.SupportingObservations.Min(o => o.ObservedAtUtc))
            throw new ArgumentException("Assertion availability must follow selection and upstream evidence availability.", nameof(observedAtUtc));
        EffectiveAtUtc = effectiveAtUtc; ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }
    [JsonIgnore] public StrategyReplayContextObservation StopOwner { get; }
    public NasdaqHumanWickfillScenario2StopLossRuleFact StopLoss { get; }
    public NasdaqHumanTakeProfitTarget Target { get; }
    public NasdaqDemoSessionIdentity Session => StopLoss.Session;
    public NasdaqHumanH4PermittedDirection Direction => StopLoss.Direction;
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    internal bool SameFact(NasdaqWickfillScenario2TakeProfitObservation other) =>
        ReferenceEquals(StopLoss.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation,
            other.StopLoss.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation)
        && StopLoss.Selection.Fact.SameFact(other.StopLoss.Selection.Fact)
        && Target.SameFact(other.Target) && EffectiveAtUtc == other.EffectiveAtUtc;
}
