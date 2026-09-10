using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Declares the direct AND prerequisites for one downstream strategy rule.</summary>
public sealed class StrategyReplayRulePrerequisite
{
    public StrategyReplayRulePrerequisite(RuleId downstreamRuleId, IEnumerable<RuleId> prerequisiteRuleIds)
    {
        ArgumentNullException.ThrowIfNull(downstreamRuleId);
        ArgumentNullException.ThrowIfNull(prerequisiteRuleIds);
        var prerequisites = prerequisiteRuleIds.ToArray();
        if (prerequisites.Any(ruleId => ruleId is null))
            throw new ArgumentException("Prerequisite rule identifiers cannot contain null.", nameof(prerequisiteRuleIds));

        DownstreamRuleId = downstreamRuleId;
        PrerequisiteRuleIds = new ReadOnlyCollection<RuleId>(prerequisites);
    }

    public RuleId DownstreamRuleId { get; }
    public IReadOnlyList<RuleId> PrerequisiteRuleIds { get; }
}
