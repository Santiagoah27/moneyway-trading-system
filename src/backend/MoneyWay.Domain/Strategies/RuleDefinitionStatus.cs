namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Describes the evidence and definition quality of a strategy rule, independently of any evaluation result.
/// </summary>
public enum RuleDefinitionStatus
{
    Confirmed,
    Candidate,
    ContextSpecific,
    VisualOnly,
    HumanValidationRequired,
    Unresolved,
    RejectedAiInference,
}
