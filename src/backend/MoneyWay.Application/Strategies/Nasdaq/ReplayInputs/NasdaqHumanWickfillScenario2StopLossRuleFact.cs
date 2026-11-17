using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical native Scenario 2 SELL protection; never a Standard 5M LH, sizing approval or execution.</summary>
public sealed record NasdaqHumanWickfillScenario2StopLossRuleFact : IReplayRuleFact
{
    internal NasdaqHumanWickfillScenario2StopLossRuleFact(NasdaqWickfillScenario2PreEntryEligibilityRuleFact eligibility,
        NasdaqHumanWickfillScenario2StopLossSelection.Unique selection)
    { PreEntryEligibility = eligibility; Selection = selection; }
    public NasdaqWickfillScenario2PreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqHumanWickfillScenario2StopLossSelection.Unique Selection { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    public decimal StopPrice => Selection.Fact.StopPrice;
    public DateTimeOffset EffectiveAtUtc => Selection.Fact.EffectiveAtUtc;
    internal static NasdaqHumanWickfillScenario2StopLossRuleFact RequireOwner(StrategyReplayContextObservation owner)
    {
        var fact = owner.RuleFacts.Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanWickfillScenario2StopLossRuleFact>().SingleOrDefault();
        if (fact is null || fact.Direction != NasdaqHumanH4PermittedDirection.Sell
            || fact.Session.StrategyId != owner.StrategyId || fact.Session.StrategyVersion != owner.StrategyVersion
            || fact.Session.ProviderId != owner.ProviderId || fact.Session.Symbol != owner.Symbol
            || owner.Evaluations.SingleOrDefault(e => e.RuleId.Value == "NQ-SL-001")?.Result != RuleEvaluationResult.Passed
            || owner.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-SL-001")?.EstablishesProgression != true
            || owner.LifecycleProgression?.ActiveInstance?.StartedAtUtc != fact.PreEntryEligibility.Reversal.Applicability.Fact.InitialTakeObservation.AsOfUtc)
            throw new ArgumentException("An actual established Scenario 2 SELL canonical SL owner is required.", nameof(owner));
        return fact;
    }
    internal static bool HasScenario2Ancestry(StrategyReplayContext context)
    {
        var active = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression?.ActiveInstance;
        return active is not null && context.PriorObservations.Any(o => o.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId
            && o.RuleFacts.Any(f => f.RuleId.Value == "NQ-WF2-002" && f.Fact is NasdaqWickfillScenario2PreEntryEligibilityRuleFact eligibility
                && eligibility.Session.Matches(context)
                && eligibility.Reversal.Applicability.Fact.InitialTakeObservation.AsOfUtc == active.StartedAtUtc));
    }
}
