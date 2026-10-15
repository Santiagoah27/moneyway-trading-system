using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Evaluates human reversal evidence for one explicitly selected Scenario 2 setup. No entry or branch workflow is established.</summary>
public sealed class MoneyWayNasdaqHumanWickfillScenario2ReversalEvaluator : IReplayRuleEvaluator
{
    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
    public RuleId RuleId { get; } = new("NQ-WF2-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        // Reuse canonical terminal/window/setup guards, including direct-call protection; never calculate a cutoff here.
        var blocked = NasdaqFvgReplayPrerequisites.Check(context, RuleId);
        if (blocked is not null) return blocked;
        var active = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression?.ActiveInstance;
        var owner = active is null ? null : context.PriorObservations.SingleOrDefault(o => o.AsOfUtc == active.StartedAtUtc
            && o.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId);
        if (owner is null) return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-LIQ-003");
        NasdaqLiquidityTakeRuleFact take;
        try { take = NasdaqHumanBranchApplicabilityObservation.RequireInitialTake(owner); }
        catch (ArgumentException) { return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-LIQ-003"); }
        if (!take.Session.Matches(context)) return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-LIQ-003");

        var applicability = new NasdaqHumanBranchApplicabilityObservationSelector().Select(context, owner);
        if (applicability is not NasdaqHumanBranchApplicabilitySelection.Unique applicable)
            return new(RuleEvaluationResult.HumanValidationRequired, "The exact established setup requires an unambiguous human path selection.",
                JsonSerializer.Serialize(new
                {
                    Setup = take.InitiatingTake,
                    Alternatives = (applicability as NasdaqHumanBranchApplicabilitySelection.Conflict)?.Alternatives,
                }));
        var applicabilityProof = JsonSerializer.Serialize(new { Applicability = applicable.SupportingObservations, Setup = take.InitiatingTake });
        // Standard never requests Scenario 2-specific evidence. NotApplicable is not a fabricated Passed fact.
        if (applicable.Fact.Path == NasdaqStrategyPath.Standard)
            return new(RuleEvaluationResult.NotApplicable, "The exact canonical setup is explicitly classified Standard.", applicabilityProof);

        var contextual = new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(context, take.Session);
        var contextProof = JsonSerializer.Serialize(new
        {
            Applicability = applicable.SupportingObservations,
            ContextEvidence = contextual is NasdaqHumanWickfillScenario2ContextSelection.Unique u ? u.SupportingObservations
                : (contextual as NasdaqHumanWickfillScenario2ContextSelection.Conflict)?.Alternatives,
            contextual.UnavailableSourceObservations,
        });
        if (contextual is NasdaqHumanWickfillScenario2ContextSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "Scenario 2 contextual reviews conflict.", contextProof);
        if (contextual is not NasdaqHumanWickfillScenario2ContextSelection.Unique selectedContext)
            return new(contextual.UnavailableSourceObservations.Count > 0 ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
                "No usable reviewed Scenario 2 pre-open context is available.", contextProof);
        if (selectedContext.UnavailableSourceObservations.Any(o => !o.SameFact(selectedContext.Fact)))
            return new(RuleEvaluationResult.DataUnavailable, "A competing contextual assertion has unavailable named sources.", contextProof);

        var sources = new NasdaqHumanLiquidityTakeObservationSelector().Select(context, take.Take.Reference);
        if (sources.UnavailableSourceObservations.Any(o => sources is not NasdaqHumanLiquidityTakeSelection.Unique u || !o.SameFact(u.Fact)))
            return new(RuleEvaluationResult.DataUnavailable, "Required opening-sweep source evidence is unavailable.",
                JsonSerializer.Serialize(new { Context = contextProof, sources.UnavailableSourceObservations }));
        NasdaqWickfillScenario2OpeningSweepContext sweep;
        try { sweep = NasdaqWickfillScenario2OpeningSweepContext.From(context, selectedContext, owner); }
        catch (ArgumentException)
        {
            return new(RuleEvaluationResult.Waiting, "The exact setup has no usable canonical Scenario 2 opening-sweep composition.", contextProof);
        }
        var reversal = new NasdaqHumanWickfillScenario2PostSweepReversalObservationSelector().Select(context, sweep);
        var proof = JsonSerializer.Serialize(new
        {
            Applicability = applicable.SupportingObservations,
            OpeningSweep = sweep,
            ReversalEvidence = reversal is NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique uniqueReversal ? uniqueReversal.SupportingObservations
                : (reversal as NasdaqHumanWickfillScenario2PostSweepReversalSelection.Conflict)?.Alternatives,
            reversal.UnavailableSourceObservations,
        });
        if (reversal is NasdaqHumanWickfillScenario2PostSweepReversalSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "Human post-sweep reversal assertions conflict; no winner is selected.", proof);
        if (reversal is NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique selected)
        {
            if (selected.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable, "A competing reversal has unavailable named sources.", proof);
            return new(RuleEvaluationResult.Passed, "Human-reviewed SELL reversal is established after this Scenario 2 opening sweep; no entry eligibility is established.",
                proof, NasdaqHumanWickfillScenario2ReversalRuleFact.Create(context, applicable, selected));
        }
        return new(reversal.UnavailableSourceObservations.Count > 0 ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human post-sweep bearish reversal is available for this exact setup.", proof);
    }
}
