using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Folds one raw chronological observation into an immutable replay-local progression snapshot.</summary>
public sealed class AdvanceStrategyReplayProgressionUseCase
{
    public StrategyReplayProgressionSnapshot Execute(
        StrategyReplayWorkflowDefinition workflow,
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionSnapshot? previousSnapshot)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(observation);
        if (workflow.StrategyId != observation.StrategyId || workflow.StrategyVersion != observation.StrategyVersion)
            throw new InvalidOperationException("Workflow identity must exactly match the strategy observation.");
        ValidateChronology(observation, previousSnapshot);

        var previouslyEstablished = previousSnapshot?.EstablishedRuleIds.ToHashSet() ?? [];
        var eligibility = new List<StrategyReplayRuleEligibility>(observation.EvaluationCount);
        var newlyEstablished = new List<RuleId>();
        foreach (var evaluation in observation.Evaluations)
        {
            var prerequisites = workflow.GetPrerequisiteRuleIds(evaluation.RuleId);
            var missing = prerequisites.Where(ruleId => !previouslyEstablished.Contains(ruleId)).ToArray();
            var isEligible = missing.Length == 0;
            var establishesProgression = isEligible && evaluation.Result == RuleEvaluationResult.Passed;
            eligibility.Add(new(
                evaluation.RuleId,
                observation.AsOfUtc,
                isEligible,
                prerequisites,
                missing,
                establishesProgression));
            if (establishesProgression && !previouslyEstablished.Contains(evaluation.RuleId))
                newlyEstablished.Add(evaluation.RuleId);
        }

        var established = previousSnapshot is null
            ? newlyEstablished
            : previousSnapshot.EstablishedRuleIds.Concat(newlyEstablished).Distinct().ToList();
        return new(
            observation.StrategyId,
            observation.StrategyVersion,
            observation.ProviderId,
            observation.Symbol,
            observation.Step,
            observation.AsOfUtc,
            eligibility,
            established);
    }

    private static void ValidateChronology(
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionSnapshot? previousSnapshot)
    {
        if (previousSnapshot is null)
        {
            if (observation.Step != 1)
                throw new InvalidOperationException("The first progression observation must be replay step 1.");
            return;
        }

        if (previousSnapshot.StrategyId != observation.StrategyId
            || previousSnapshot.StrategyVersion != observation.StrategyVersion
            || previousSnapshot.ProviderId != observation.ProviderId
            || previousSnapshot.Symbol != observation.Symbol)
            throw new InvalidOperationException("Progression snapshot identity must exactly match the strategy observation.");
        if (observation.Step != previousSnapshot.Step + 1 || observation.AsOfUtc <= previousSnapshot.AsOfUtc)
            throw new InvalidOperationException("Progression observations must be consecutive and strictly chronological.");
    }
}
