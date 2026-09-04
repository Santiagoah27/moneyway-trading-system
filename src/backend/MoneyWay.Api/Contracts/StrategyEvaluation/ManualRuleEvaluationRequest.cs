namespace MoneyWay.Api.Contracts.StrategyEvaluation;

/// <summary>
/// Represents one rule evaluation already determined by the caller.
/// </summary>
public sealed record ManualRuleEvaluationRequest(
    string? RuleId,
    string? DefinitionStatus,
    string? Result,
    int Sequence,
    bool IsRequired,
    string? Reason,
    DateTimeOffset EvaluatedAtUtc,
    string? EvidenceReference);
