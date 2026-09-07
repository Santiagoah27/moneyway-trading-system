using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Capabilities;

/// <summary>Explicitly documents a non-implemented evaluation limitation for one exact strategy rule version. Implemented capability requires a registered evaluator.</summary>
public sealed class ReplayRuleEvaluationCapabilityDeclaration
{
    public ReplayRuleEvaluationCapabilityDeclaration(StrategyId strategyId, StrategyVersion strategyVersion, RuleId ruleId, ReplayRuleEvaluationCapabilityStatus status, string reason, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion); ArgumentNullException.ThrowIfNull(ruleId);
        if (status == ReplayRuleEvaluationCapabilityStatus.Implemented) throw new ArgumentException("Implemented capability requires a registered evaluator.", nameof(status));
        Validate(reason, nameof(reason)); Validate(sourceReference, nameof(sourceReference));
        StrategyId = strategyId; StrategyVersion = strategyVersion; RuleId = ruleId; Status = status; Reason = reason; SourceReference = sourceReference;
    }
    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public RuleId RuleId { get; }
    public ReplayRuleEvaluationCapabilityStatus Status { get; }
    public string Reason { get; }
    public string SourceReference { get; }
    private static void Validate(string value, string name) { ArgumentNullException.ThrowIfNull(value, name); if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("Value must be non-empty and trimmed.", name); }
}
