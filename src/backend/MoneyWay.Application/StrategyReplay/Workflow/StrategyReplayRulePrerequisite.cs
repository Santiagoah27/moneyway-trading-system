using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Declares the original AND prerequisites and an optional prior-Passed OR route for one downstream rule.</summary>
public sealed class StrategyReplayRulePrerequisite
{
    public StrategyReplayRulePrerequisite(RuleId downstreamRuleId, IEnumerable<RuleId> prerequisiteRuleIds, bool requiresLatestPrerequisitePass = false,
        RuleId? alternativePrerequisiteRuleId = null)
    {
        ArgumentNullException.ThrowIfNull(downstreamRuleId);
        ArgumentNullException.ThrowIfNull(prerequisiteRuleIds);
        var prerequisites = prerequisiteRuleIds.ToArray();
        if (prerequisites.Any(ruleId => ruleId is null))
            throw new ArgumentException("Prerequisite rule identifiers cannot contain null.", nameof(prerequisiteRuleIds));

        RequiresLatestPrerequisitePass = requiresLatestPrerequisitePass;
        DownstreamRuleId = downstreamRuleId;
        PrerequisiteRuleIds = new ReadOnlyCollection<RuleId>(prerequisites);
        AlternativePrerequisiteRuleId = alternativePrerequisiteRuleId;
    }

    /// <summary>Recheck the immediately prior eligible pass instead of relying on historical completion alone.</summary>
    public bool RequiresLatestPrerequisitePass { get; }
    public RuleId DownstreamRuleId { get; }
    public IReadOnlyList<RuleId> PrerequisiteRuleIds { get; }
    /// <summary>Optional OR route, requiring its immediately prior eligible Passed, never same-frame completion.</summary>
    public RuleId? AlternativePrerequisiteRuleId { get; }
    public IEnumerable<RuleId> AllPrerequisiteRuleIds => AlternativePrerequisiteRuleId is { } alternative
        ? PrerequisiteRuleIds.Append(alternative) : PrerequisiteRuleIds;
}
