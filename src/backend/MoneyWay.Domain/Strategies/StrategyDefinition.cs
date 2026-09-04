using System.Collections.ObjectModel;

namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Represents a versioned, auditable description of a strategy without implying automatic evaluability.
/// </summary>
public sealed class StrategyDefinition
{
    public StrategyDefinition(
        StrategyId strategyId,
        StrategyVersion version,
        string displayName,
        string specificationReference,
        IEnumerable<StrategyRuleDefinition> rules)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(version);
        ValidateText(displayName, nameof(displayName));
        ValidateText(specificationReference, nameof(specificationReference));
        ArgumentNullException.ThrowIfNull(rules);

        var ruleSnapshot = rules.ToArray();
        if (ruleSnapshot.Length == 0)
        {
            throw new ArgumentException("At least one rule is required.", nameof(rules));
        }

        if (ruleSnapshot.Any(rule => rule is null))
        {
            throw new ArgumentException("Rules cannot contain null elements.", nameof(rules));
        }

        if (ruleSnapshot.GroupBy(rule => rule.RuleId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Rule identifiers must be unique.", nameof(rules));
        }

        if (ruleSnapshot.GroupBy(rule => rule.Sequence).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Rule sequences must be unique.", nameof(rules));
        }

        if (ruleSnapshot.Any(rule => rule.DefinitionStatus == RuleDefinitionStatus.RejectedAiInference))
        {
            throw new ArgumentException("Rejected AI inferences cannot be active strategy rules.", nameof(rules));
        }

        StrategyId = strategyId;
        Version = version;
        DisplayName = displayName;
        SpecificationReference = specificationReference;
        Rules = new ReadOnlyCollection<StrategyRuleDefinition>(ruleSnapshot.OrderBy(rule => rule.Sequence).ToArray());
    }

    public StrategyId StrategyId { get; }

    public StrategyVersion Version { get; }

    public string DisplayName { get; }

    public string SpecificationReference { get; }

    public IReadOnlyList<StrategyRuleDefinition> Rules { get; }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Value must be non-empty and have no surrounding whitespace.", parameterName);
        }
    }
}
