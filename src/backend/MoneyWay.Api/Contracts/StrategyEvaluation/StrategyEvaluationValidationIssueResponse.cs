namespace MoneyWay.Api.Contracts.StrategyEvaluation;

public sealed record StrategyEvaluationValidationIssueResponse(string Code, string Message, string? RuleId);
