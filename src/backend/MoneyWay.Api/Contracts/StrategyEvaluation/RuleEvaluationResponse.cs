namespace MoneyWay.Api.Contracts.StrategyEvaluation;

/// <summary>
/// Represents an unchanged rule evaluation returned for auditability.
/// </summary>
public sealed record RuleEvaluationResponse(
    string RuleId,
    string DefinitionStatus,
    string Result,
    int Sequence,
    bool IsRequired,
    string Reason,
    DateTimeOffset EvaluatedAtUtc,
    string? EvidenceReference);
