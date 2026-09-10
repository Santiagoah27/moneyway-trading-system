using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Provides immutable, strategy-owned workflow metadata for built-in MoneyWay strategy versions.</summary>
public static class MoneyWayReplayWorkflowDefinitions
{
    private static readonly StrategyDefinition Nasdaq = MoneyWayNasdaqStrategyDefinition.Instance;

    private static readonly IReadOnlyList<StrategyReplayWorkflowDefinition> Definitions =
        new ReadOnlyCollection<StrategyReplayWorkflowDefinition>(
        [
            new(
                Nasdaq.StrategyId,
                Nasdaq.Version,
                [
                    Requires("NQ-LIQ-003", "NQ-H4-001", "NQ-LIQ-002", "NQ-TIME-001"),
                    Requires("NQ-M5-001", "NQ-LIQ-003"),
                    Requires("NQ-FVG-001", "NQ-M5-001"),
                    Requires("NQ-M1-001", "NQ-FVG-001"),
                    Requires("NQ-M1-002", "NQ-M1-001"),
                    Requires("NQ-M1-003", "NQ-M1-002"),
                ]),
        ]);

    public static IReadOnlyList<StrategyReplayWorkflowDefinition> GetAll() => Definitions;

    private static StrategyReplayRulePrerequisite Requires(string downstreamRuleId, params string[] prerequisiteRuleIds) =>
        new(new RuleId(downstreamRuleId), prerequisiteRuleIds.Select(ruleId => new RuleId(ruleId)));
}
