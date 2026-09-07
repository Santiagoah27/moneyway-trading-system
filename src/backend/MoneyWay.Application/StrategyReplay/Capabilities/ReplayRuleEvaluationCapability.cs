using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Capabilities;

/// <summary>Combines immutable strategy-rule metadata with its current evaluator capability.</summary>
public sealed class ReplayRuleEvaluationCapability
{
    public ReplayRuleEvaluationCapability(StrategyId strategyId, StrategyVersion strategyVersion, StrategyRuleDefinition rule, ReplayRuleEvaluationCapabilityStatus capabilityStatus, string capabilityReason, string? capabilitySourceReference)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion); ArgumentNullException.ThrowIfNull(rule);
        Validate(capabilityReason, nameof(capabilityReason)); if (capabilitySourceReference is not null) Validate(capabilitySourceReference, nameof(capabilitySourceReference));
        StrategyId = strategyId; StrategyVersion = strategyVersion; RuleId = rule.RuleId; Name = rule.Name; Stage = rule.Stage; Sequence = rule.Sequence; IsRequired = rule.IsRequired;
        DefinitionStatus = rule.DefinitionStatus; RuleSourceReference = rule.SourceReference; CapabilityStatus = capabilityStatus; CapabilityReason = capabilityReason; CapabilitySourceReference = capabilitySourceReference;
    }
    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public RuleId RuleId { get; }
    public string Name { get; }
    public string Stage { get; }
    public int Sequence { get; }
    public bool IsRequired { get; }
    public RuleDefinitionStatus DefinitionStatus { get; }
    public string RuleSourceReference { get; }
    public ReplayRuleEvaluationCapabilityStatus CapabilityStatus { get; }
    public string CapabilityReason { get; }
    public string? CapabilitySourceReference { get; }
    private static void Validate(string value, string name) { ArgumentNullException.ThrowIfNull(value, name); if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("Value must be non-empty and trimmed.", name); }
}
