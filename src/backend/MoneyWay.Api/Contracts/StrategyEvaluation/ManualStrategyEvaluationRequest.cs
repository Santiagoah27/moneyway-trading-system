namespace MoneyWay.Api.Contracts.StrategyEvaluation;

/// <summary>
/// Represents a manual strategy evaluation request containing caller-determined rule evaluations.
/// </summary>
public sealed record ManualStrategyEvaluationRequest(
    string? StrategyId,
    string? StrategyVersion,
    IReadOnlyList<ManualRuleEvaluationRequest?>? Evaluations);
