namespace MoneyWay.Api.Contracts.StrategyEvaluation;

/// <summary>
/// Represents the complete, ordered, and auditable result of a manual evaluation.
/// </summary>
public sealed record ManualStrategyEvaluationResponse(
    string StrategyId,
    string StrategyVersion,
    string Verdict,
    string Reason,
    string? BlockingRuleId,
    int? BlockingSequence,
    string? BlockingResult,
    int EvaluationCount,
    int RequiredEvaluationCount,
    IReadOnlyList<RuleEvaluationResponse> Evaluations);
