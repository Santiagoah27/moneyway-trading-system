using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Validates and resolves immutable workflow metadata without inferring dependencies.</summary>
public sealed class StrategyReplayWorkflowCatalog
{
    public static StrategyReplayWorkflowCatalog Empty { get; } = new([], []);

    private readonly IReadOnlyDictionary<WorkflowKey, StrategyReplayWorkflowDefinition> workflows;

    public StrategyReplayWorkflowCatalog(
        IEnumerable<StrategyDefinition> strategyDefinitions,
        IEnumerable<StrategyReplayWorkflowDefinition> workflowDefinitions)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinitions);
        ArgumentNullException.ThrowIfNull(workflowDefinitions);
        var definitions = strategyDefinitions.ToArray();
        var configured = workflowDefinitions.ToArray();
        if (definitions.Any(definition => definition is null))
            throw new ArgumentException("Strategy definitions cannot contain null.", nameof(strategyDefinitions));
        if (configured.Any(workflow => workflow is null))
            throw new ArgumentException("Workflow definitions cannot contain null.", nameof(workflowDefinitions));
        var definitionMap = definitions.ToDictionary(definition => new WorkflowKey(definition.StrategyId, definition.Version));
        var duplicate = configured.GroupBy(workflow => new WorkflowKey(workflow.StrategyId, workflow.StrategyVersion))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException(
                $"Duplicate workflow definition for strategy '{duplicate.Key.StrategyId}' version '{duplicate.Key.StrategyVersion}'.",
                nameof(workflowDefinitions));

        foreach (var workflow in configured)
        {
            var key = new WorkflowKey(workflow.StrategyId, workflow.StrategyVersion);
            if (!definitionMap.TryGetValue(key, out var definition))
                throw new InvalidOperationException(
                    $"Workflow references unknown strategy '{workflow.StrategyId}' version '{workflow.StrategyVersion}'.");
            var knownRuleIds = definition.Rules.Select(rule => rule.RuleId).ToHashSet();
            foreach (var declaration in workflow.RulePrerequisites)
            {
                if (!knownRuleIds.Contains(declaration.DownstreamRuleId))
                    throw new InvalidOperationException(
                        $"Workflow '{workflow.StrategyId}' version '{workflow.StrategyVersion}' references unknown downstream rule '{declaration.DownstreamRuleId}'.");
                var unknownPrerequisite = declaration.PrerequisiteRuleIds.FirstOrDefault(ruleId => !knownRuleIds.Contains(ruleId));
                if (unknownPrerequisite is not null)
                    throw new InvalidOperationException(
                        $"Workflow '{workflow.StrategyId}' version '{workflow.StrategyVersion}' references unknown prerequisite rule '{unknownPrerequisite}' for downstream rule '{declaration.DownstreamRuleId}'.");
            }
        }

        WorkflowDefinitions = new ReadOnlyCollection<StrategyReplayWorkflowDefinition>(configured);
        workflows = configured.ToDictionary(workflow => new WorkflowKey(workflow.StrategyId, workflow.StrategyVersion));
    }

    public IReadOnlyList<StrategyReplayWorkflowDefinition> WorkflowDefinitions { get; }

    public StrategyReplayWorkflowDefinition? Find(StrategyId strategyId, StrategyVersion strategyVersion)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        return workflows.GetValueOrDefault(new(strategyId, strategyVersion));
    }

    private sealed record WorkflowKey(StrategyId StrategyId, StrategyVersion StrategyVersion);
}
