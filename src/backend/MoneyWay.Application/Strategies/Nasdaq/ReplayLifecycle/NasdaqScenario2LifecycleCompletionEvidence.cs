using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;

/// <summary>Verified prior canonical completion for one active initial-take setup, never an executed trade.</summary>
public sealed class NasdaqScenario2LifecycleCompletionEvidence : IStrategyReplayLifecycleEvidence
{
    private NasdaqScenario2LifecycleCompletionEvidence(DateTimeOffset observedAtUtc,
        StrategyReplayContextObservation? startOwner, StrategyReplayContextObservation? eligibilityOwner,
        NasdaqWickfillScenario2PreEntryEligibilityRuleFact? eligibility)
    { ObservedAtUtc = observedAtUtc; StartOwner = startOwner; EligibilityOwner = eligibilityOwner; Eligibility = eligibility; }

    public DateTimeOffset ObservedAtUtc { get; }
    [JsonIgnore]
    public StrategyReplayContextObservation? StartOwner { get; }
    [JsonIgnore]
    public StrategyReplayContextObservation? EligibilityOwner { get; }
    public NasdaqWickfillScenario2PreEntryEligibilityRuleFact? Eligibility { get; }
    public DateTimeOffset? StartAtUtc => StartOwner?.AsOfUtc;
    public DateTimeOffset? EligibilityEvaluatedAtUtc => EligibilityOwner?.AsOfUtc;

    internal static StrategyReplayLifecycleEvidenceSnapshot Capture(StrategyReplayLifecycleEvidenceProductionContext production)
    {
        ArgumentNullException.ThrowIfNull(production);
        var context = production.ReplayContext;
        var active = production.PreviousLifecycle?.ActiveInstance;
        if (active is null) return StrategyReplayLifecycleEvidenceSnapshot.Empty(production.Observation);
        var start = context.PriorObservations.SingleOrDefault(o => o.AsOfUtc == active.StartedAtUtc
            && o.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId);
        NasdaqLiquidityTakeRuleFact initial;
        try { initial = NasdaqHumanBranchApplicabilityObservation.RequireInitialTake(start!); }
        catch (ArgumentException) { return StrategyReplayLifecycleEvidenceSnapshot.Empty(production.Observation); }
        var applicability = new NasdaqHumanBranchApplicabilityObservationSelector().Select(context, start!);
        if (applicability is NasdaqHumanBranchApplicabilitySelection.Unique standard && standard.Fact.Path == NasdaqStrategyPath.Standard)
            return StrategyReplayLifecycleEvidenceSnapshot.Empty(production.Observation);

        StrategyReplayContextObservation? owner = null;
        NasdaqWickfillScenario2PreEntryEligibilityRuleFact? fact = null;
        // Completed pre-entry setups retain Standard's cutoff treatment. Post-cutoff TIME rejections do not
        // erase a genuine earlier completion; current incompatible reviews and terminal evidence still do.
        var window = start!.RuleFacts.Where(f => f.RuleId.Value is "NQ-TIME-001" or "NQ-TIME-002")
            .Select(f => f.Fact).OfType<NasdaqTradingWindowFact>().FirstOrDefault(w => w.Contains(start.AsOfUtc));
        var history = context.PriorObservations.Where(o => o.AsOfUtc >= start.AsOfUtc && window?.Contains(o.AsOfUtc) == true).ToArray();
        var candidate = history.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-002"));
        var candidateFact = candidate?.RuleFacts.Where(f => f.RuleId.Value == "NQ-WF2-002")
            .Select(f => f.Fact).OfType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>().SingleOrDefault();
        var reversalOwner = candidate is null ? null : history.LastOrDefault(o => o.AsOfUtc < candidate.AsOfUtc
            && o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001"));
        var reversal = reversalOwner?.RuleFacts.Where(f => f.RuleId.Value == "NQ-WF2-001")
            .Select(f => f.Fact).OfType<NasdaqHumanWickfillScenario2ReversalRuleFact>().SingleOrDefault();
        var latestReversalOwner = history.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001"));
        var latestReversal = latestReversalOwner?.RuleFacts.Where(f => f.RuleId.Value == "NQ-WF2-001")
            .Select(f => f.Fact).OfType<NasdaqHumanWickfillScenario2ReversalRuleFact>().SingleOrDefault();
        var currentOwnersCompatible = window?.Contains(context.AsOfUtc) != true
            || production.Observation.Evaluations.Where(e => e.RuleId.Value is "NQ-WF2-001" or "NQ-WF2-002")
                .All(e => e.Result == RuleEvaluationResult.Passed);
        if (currentOwnersCompatible && candidateFact is not null && reversal is not null && latestReversal is not null
            && applicability is NasdaqHumanBranchApplicabilitySelection.Unique selected
            && selected.Fact.Path == NasdaqStrategyPath.WickfillScenario2
            && selected.Fact.Direction == NasdaqHumanH4PermittedDirection.Sell
            && initial.Session.Matches(context) && candidateFact.Session.Matches(context)
            && candidateFact.Direction == NasdaqHumanH4PermittedDirection.Sell
            && candidateFact.Reversal.Applicability.Fact.Path == NasdaqStrategyPath.WickfillScenario2
            && ReferenceEquals(candidateFact.Reversal, reversal)
            && candidate!.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId
            && candidate.LifecycleProgression.ActiveInstance.StartedAtUtc == active.StartedAtUtc
            && reversalOwner!.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId
            && reversalOwner.LifecycleProgression.ActiveInstance.StartedAtUtc == active.StartedAtUtc
            && latestReversalOwner!.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId
            && latestReversalOwner.LifecycleProgression.ActiveInstance.StartedAtUtc == active.StartedAtUtc
            && reversal.Applicability.Fact.InitialTakeObservation.AsOfUtc == start.AsOfUtc
            && reversal.Reversal.Fact.OpeningSweep.TakeObservation.AsOfUtc == start.AsOfUtc
            && active.WorkflowProgression.EstablishedRuleIds.Contains(new RuleId("NQ-WF2-002"))
            && Established(candidate, "NQ-WF2-002") && Established(reversalOwner!, "NQ-WF2-001")
            && Established(latestReversalOwner!, "NQ-WF2-001")
            && latestReversal.Reversal.Fact.SameFact(reversal.Reversal.Fact)
            && NasdaqHumanBranchApplicabilityObservation.SameSetup(initial, selected.Fact.InitialTake)
            && NasdaqHumanBranchApplicabilityObservation.SameSetup(initial, reversal.Applicability.Fact.InitialTake)
            && NasdaqHumanBranchApplicabilityObservation.SameSetup(initial, reversal.Reversal.Fact.OpeningSweep.Take)
            && candidateFact.EligibilityEffectiveAtUtc <= candidate.AsOfUtc
            && reversal.Applicability.SupportingObservations.All(o => o.ObservedAtUtc <= candidate.AsOfUtc)
            && reversal.Reversal.SupportingObservations.All(o => o.ObservedAtUtc <= candidate.AsOfUtc)
            && !context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact).OfType<NasdaqLiquidityTakeRuleFact>()
                .Any(f => f.IsSessionInvalidated && f.Session.Matches(context)))
        {
            try
            {
                var sweep = NasdaqWickfillScenario2OpeningSweepContext.From(context, reversal.Reversal.Fact.OpeningSweep.PreOpenContext, start);
                var current = new NasdaqHumanWickfillScenario2PostSweepReversalObservationSelector().Select(context, sweep);
                if (current is NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique usable
                    && usable.Fact.SameFact(reversal.Reversal.Fact)
                    && !usable.UnavailableSourceObservations.Any(o => !o.SameFact(usable.Fact)))
                { owner = candidate; fact = candidateFact; }
            }
            catch (ArgumentException) { /* The retained sources/selection no longer compose for this canonical Start. */ }
        }
        var proof = new NasdaqScenario2LifecycleCompletionEvidence(context.AsOfUtc, start, owner, fact);
        return new(context.StrategyId, context.StrategyVersion, context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
            StrategyReplayLifecycleEvidenceScope.ActiveInstance, active.InstanceId, [proof]);
    }

    private static bool Established(StrategyReplayContextObservation owner, string rule) =>
        owner.Evaluations.SingleOrDefault(e => e.RuleId.Value == rule)?.Result == RuleEvaluationResult.Passed
        && owner.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == rule)?.EstablishesProgression == true;
}
