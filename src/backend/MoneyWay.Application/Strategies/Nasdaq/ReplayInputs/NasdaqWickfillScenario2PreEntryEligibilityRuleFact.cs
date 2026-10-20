using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Scoped SELL eligibility derived from one canonical Scenario 2 reversal; no execution is established.</summary>
public sealed class NasdaqWickfillScenario2PreEntryEligibilityRuleFact : IReplayRuleFact
{
    private NasdaqWickfillScenario2PreEntryEligibilityRuleFact(NasdaqHumanWickfillScenario2ReversalRuleFact reversal)
    { Reversal = reversal; }

    public NasdaqHumanWickfillScenario2ReversalRuleFact Reversal { get; }
    public NasdaqDemoSessionIdentity Session => Reversal.Applicability.Fact.Session;
    public NasdaqHumanH4PermittedDirection Direction => Reversal.Applicability.Fact.Direction;
    public DateTimeOffset EligibilityEffectiveAtUtc => Reversal.EffectiveAtUtc;

    internal static NasdaqWickfillScenario2PreEntryEligibilityRuleFact Create(StrategyReplayContext context,
        StrategyReplayContextObservation predecessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(predecessor);
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001"));
        var reversal = predecessor.RuleFacts.Where(f => f.RuleId.Value == "NQ-WF2-001").Select(f => f.Fact)
            .OfType<NasdaqHumanWickfillScenario2ReversalRuleFact>().SingleOrDefault();
        var active = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression?.ActiveInstance;
        var initialOwner = active is null ? null : context.PriorObservations.SingleOrDefault(o => o.AsOfUtc == active.StartedAtUtc
            && o.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId);
        var initialTake = initialOwner is null ? null : NasdaqHumanBranchApplicabilityObservation.RequireInitialTake(initialOwner);
        if (!ReferenceEquals(latest, predecessor) || !context.PriorObservations.Contains(predecessor)
            || predecessor.AsOfUtc >= context.AsOfUtc || reversal is null || active is null || initialOwner is null
            || predecessor.LifecycleProgression?.ActiveInstance?.InstanceId != active.InstanceId
            || predecessor.Evaluations.SingleOrDefault(e => e.RuleId.Value == "NQ-WF2-001")?.Result != RuleEvaluationResult.Passed
            || predecessor.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-WF2-001")?.EstablishesProgression != true
            || !reversal.Applicability.Fact.Session.Matches(context)
            || reversal.Applicability.Fact.Path != NasdaqStrategyPath.WickfillScenario2
            || reversal.Applicability.Fact.Direction != NasdaqHumanH4PermittedDirection.Sell
            || initialTake is null || !NasdaqHumanBranchApplicabilityObservation.SameSetup(reversal.Applicability.Fact.InitialTake, initialTake)
            || !NasdaqHumanBranchApplicabilityObservation.SameSetup(reversal.Reversal.Fact.OpeningSweep.Take, initialTake)
            || !NasdaqHumanBranchApplicabilityObservation.SameSetup(reversal.Applicability.Fact.InitialTake, reversal.Reversal.Fact.OpeningSweep.Take)
            || reversal.EffectiveAtUtc > predecessor.AsOfUtc
            || reversal.Applicability.SupportingObservations.Any(o => o.ObservedAtUtc > predecessor.AsOfUtc)
            || reversal.Reversal.SupportingObservations.Any(o => o.ObservedAtUtc > predecessor.AsOfUtc)
            || reversal.Reversal.UnavailableSourceObservations.Any(o => !o.SameFact(reversal.Reversal.Fact))
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact).OfType<IReplayRuleGateFact>()
                .Any(g => g.Blocks(context, new RuleId("NQ-WF2-002"))))
            throw new ArgumentException("The latest observable Passed canonical SELL reversal must belong to the exact surviving Scenario 2 setup.", nameof(predecessor));
        return new(reversal);
    }
}
