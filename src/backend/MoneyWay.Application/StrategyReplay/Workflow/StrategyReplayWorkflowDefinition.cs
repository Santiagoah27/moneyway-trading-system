using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Owns immutable prerequisite metadata for one exact strategy version.</summary>
public sealed class StrategyReplayWorkflowDefinition
{
    private readonly IReadOnlyDictionary<RuleId, StrategyReplayRulePrerequisite> prerequisitesByRule;

    public StrategyReplayWorkflowDefinition(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        IEnumerable<StrategyReplayRulePrerequisite> rulePrerequisites)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(rulePrerequisites);
        var declarations = rulePrerequisites.ToArray();
        if (declarations.Any(declaration => declaration is null))
            throw new ArgumentException("Workflow declarations cannot contain null.", nameof(rulePrerequisites));
        foreach (var declaration in declarations)
        {
            if (declaration.PrerequisiteRuleIds.Distinct().Count() != declaration.PrerequisiteRuleIds.Count)
                throw new ArgumentException(
                    $"Workflow '{strategyId}' version '{strategyVersion}' rule '{declaration.DownstreamRuleId}' contains a duplicate prerequisite.",
                    nameof(rulePrerequisites));
            if (declaration.PrerequisiteRuleIds.Contains(declaration.DownstreamRuleId))
                throw new ArgumentException(
                    $"Workflow '{strategyId}' version '{strategyVersion}' rule '{declaration.DownstreamRuleId}' cannot depend on itself.",
                    nameof(rulePrerequisites));
        }
        var duplicate = declarations.GroupBy(declaration => declaration.DownstreamRuleId).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException(
                $"Workflow '{strategyId}' version '{strategyVersion}' contains duplicate declarations for rule '{duplicate.Key}'.",
                nameof(rulePrerequisites));

        ValidateAcyclic(strategyId, strategyVersion, declarations);
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        RulePrerequisites = new ReadOnlyCollection<StrategyReplayRulePrerequisite>(declarations);
        prerequisitesByRule = declarations.ToDictionary(declaration => declaration.DownstreamRuleId);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public IReadOnlyList<StrategyReplayRulePrerequisite> RulePrerequisites { get; }

    public IReadOnlyList<RuleId> GetPrerequisiteRuleIds(RuleId downstreamRuleId)
    {
        ArgumentNullException.ThrowIfNull(downstreamRuleId);
        return prerequisitesByRule.TryGetValue(downstreamRuleId, out var declaration)
            ? declaration.PrerequisiteRuleIds
            : [];
    }

    private static void ValidateAcyclic(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        IReadOnlyList<StrategyReplayRulePrerequisite> declarations)
    {
        var byRule = declarations.ToDictionary(declaration => declaration.DownstreamRuleId);
        var states = new Dictionary<RuleId, VisitState>();
        foreach (var ruleId in declarations
            .SelectMany(declaration => declaration.PrerequisiteRuleIds.Prepend(declaration.DownstreamRuleId))
            .Distinct())
        {
            Visit(ruleId);
        }

        void Visit(RuleId ruleId)
        {
            if (states.TryGetValue(ruleId, out var state))
            {
                if (state == VisitState.Visiting)
                    throw new ArgumentException(
                        $"Workflow '{strategyId}' version '{strategyVersion}' contains a dependency cycle involving rule '{ruleId}'.",
                        nameof(declarations));
                return;
            }

            states[ruleId] = VisitState.Visiting;
            if (byRule.TryGetValue(ruleId, out var declaration))
            {
                foreach (var prerequisiteRuleId in declaration.PrerequisiteRuleIds)
                    Visit(prerequisiteRuleId);
            }
            states[ruleId] = VisitState.Visited;
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited,
    }
}
