namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Records the immutable and auditable outcome of an individual rule evaluation.
/// </summary>
public sealed record RuleEvaluation
{
    public RuleEvaluation(
        RuleId ruleId,
        RuleDefinitionStatus definitionStatus,
        RuleEvaluationResult result,
        int sequence,
        bool isRequired,
        string reason,
        DateTimeOffset evaluatedAtUtc,
        string? evidenceReference)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(reason);

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim())
        {
            throw new ArgumentException("Reason must be non-empty and have no surrounding whitespace.", nameof(reason));
        }

        if (evaluatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Evaluation timestamp must have a UTC offset.", nameof(evaluatedAtUtc));
        }

        if (evidenceReference is not null
            && (string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference != evidenceReference.Trim()))
        {
            throw new ArgumentException(
                "Evidence reference must be non-empty and have no surrounding whitespace when provided.",
                nameof(evidenceReference));
        }

        RuleId = ruleId;
        DefinitionStatus = definitionStatus;
        Result = result;
        Sequence = sequence;
        IsRequired = isRequired;
        Reason = reason;
        EvaluatedAtUtc = evaluatedAtUtc;
        EvidenceReference = evidenceReference;
    }

    public RuleId RuleId { get; }

    public RuleDefinitionStatus DefinitionStatus { get; }

    public RuleEvaluationResult Result { get; }

    public int Sequence { get; }

    public bool IsRequired { get; }

    public string Reason { get; }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public string? EvidenceReference { get; }
}
