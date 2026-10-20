using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Resolves the active canonical Start before any Standard-only evidence is requested.</summary>
internal static class NasdaqStandardBranchApplicability
{
    internal static ReplayRuleEvaluationDecision? Check(StrategyReplayContext context, string prerequisiteRuleId,
        bool preserveM5WindowSelection = false)
    {
        var active = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression?.ActiveInstance;
        var owner = active is null ? null : context.PriorObservations.SingleOrDefault(o => o.AsOfUtc == active.StartedAtUtc
            && o.LifecycleProgression?.ActiveInstance?.InstanceId == active.InstanceId);
        if (owner is null) return NasdaqFvgReplayPrerequisites.Waiting(context, prerequisiteRuleId);
        NasdaqLiquidityTakeRuleFact take;
        try { take = NasdaqHumanBranchApplicabilityObservation.RequireInitialTake(owner); }
        catch (ArgumentException) { return NasdaqFvgReplayPrerequisites.Waiting(context, prerequisiteRuleId); }

        var selection = new NasdaqHumanBranchApplicabilityObservationSelector().Select(context, owner);
        var proof = JsonSerializer.Serialize(new
        {
            InitialTake = take.InitiatingTake,
            Applicability = selection is NasdaqHumanBranchApplicabilitySelection.Unique unique ? unique.SupportingObservations : null,
            Alternatives = (selection as NasdaqHumanBranchApplicabilitySelection.Conflict)?.Alternatives,
        });
        if (selection is not NasdaqHumanBranchApplicabilitySelection.Unique applicable)
            return new(RuleEvaluationResult.HumanValidationRequired,
                "The exact active setup requires an unambiguous human branch classification.", proof);
        if (applicable.Fact.Path == NasdaqStrategyPath.Standard) return null;

        // M5 previously maps an out-of-window selector to missing human trigger evidence, not Failed.
        // M1's existing terminal/TIME guard runs before this helper. Do not unify their mappings.
        if (preserveM5WindowSelection && !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc))
            return new(RuleEvaluationResult.HumanValidationRequired,
                "The canonical TIME-owned window does not permit Standard exclusion for this setup.", proof);
        return new(RuleEvaluationResult.NotApplicable,
            "The exact active setup is explicitly classified WickfillScenario2 SELL; this Standard-only rule does not apply.", proof);
    }
}
