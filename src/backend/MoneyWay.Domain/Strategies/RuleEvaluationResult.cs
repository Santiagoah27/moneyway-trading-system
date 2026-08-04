namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Describes the outcome of evaluating one rule in a specific context, independently of its definition status.
/// </summary>
public enum RuleEvaluationResult
{
    Passed,
    Failed,
    Waiting,
    NotApplicable,
    HumanValidationRequired,
    DataUnavailable,
}
