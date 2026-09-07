using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Contains only evaluator-owned output; structural metadata comes from the strategy definition.</summary>
public sealed class ReplayRuleEvaluationDecision
{
    public ReplayRuleEvaluationDecision(RuleEvaluationResult result, string reason, string? evidenceReference)
    {
        Validate(reason, nameof(reason));
        if (evidenceReference is not null) Validate(evidenceReference, nameof(evidenceReference));
        Result = result;
        Reason = reason;
        EvidenceReference = evidenceReference;
    }
    public RuleEvaluationResult Result { get; }
    public string Reason { get; }
    public string? EvidenceReference { get; }
    private static void Validate(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("Value must be non-empty and have no surrounding whitespace.", name);
    }
}
