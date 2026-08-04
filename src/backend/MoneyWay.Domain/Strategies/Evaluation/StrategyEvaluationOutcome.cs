namespace MoneyWay.Domain.Strategies.Evaluation;

/// <summary>
/// Captures the immutable and auditable outcome of aggregating rule evaluations.
/// Ready only means that the supplied evaluations contain no required blocker; it does not authorize an order.
/// </summary>
public sealed record StrategyEvaluationOutcome
{
    public StrategyEvaluationOutcome(
        StrategyVerdict verdict,
        RuleId? blockingRuleId,
        int? blockingSequence,
        RuleEvaluationResult? blockingResult,
        string reason,
        int evaluationCount,
        int requiredEvaluationCount)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim())
        {
            throw new ArgumentException("Reason must be non-empty and have no surrounding whitespace.", nameof(reason));
        }

        if (evaluationCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evaluationCount), evaluationCount, "Evaluation count cannot be negative.");
        }

        if (requiredEvaluationCount < 0 || requiredEvaluationCount > evaluationCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredEvaluationCount),
                requiredEvaluationCount,
                "Required evaluation count must be between zero and the total evaluation count.");
        }

        var hasAnyBlockingValue = blockingRuleId is not null || blockingSequence is not null || blockingResult is not null;
        var hasAllBlockingValues = blockingRuleId is not null && blockingSequence is not null && blockingResult is not null;

        if (hasAnyBlockingValue && !hasAllBlockingValues)
        {
            throw new ArgumentException("Blocking rule identifier, sequence, and result must be supplied together.");
        }

        if (verdict == StrategyVerdict.Ready)
        {
            if (hasAnyBlockingValue)
            {
                throw new ArgumentException("A ready outcome cannot contain a blocking rule.");
            }

            if (evaluationCount == 0)
            {
                throw new ArgumentException("A ready outcome requires at least one evaluation.", nameof(evaluationCount));
            }
        }
        else if (!hasAnyBlockingValue)
        {
            if (verdict != StrategyVerdict.DataUnavailable || evaluationCount != 0 || requiredEvaluationCount != 0)
            {
                throw new ArgumentException("Only an empty data-unavailable outcome can omit blocking rule details.");
            }
        }
        else
        {
            if (blockingSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockingSequence), blockingSequence, "Blocking sequence must be greater than zero.");
            }

            if (evaluationCount == 0 || requiredEvaluationCount == 0)
            {
                throw new ArgumentException("A blocking outcome requires received and required evaluations.");
            }
        }

        Verdict = verdict;
        BlockingRuleId = blockingRuleId;
        BlockingSequence = blockingSequence;
        BlockingResult = blockingResult;
        Reason = reason;
        EvaluationCount = evaluationCount;
        RequiredEvaluationCount = requiredEvaluationCount;
    }

    public StrategyVerdict Verdict { get; }

    public RuleId? BlockingRuleId { get; }

    public int? BlockingSequence { get; }

    public RuleEvaluationResult? BlockingResult { get; }

    public string Reason { get; }

    public int EvaluationCount { get; }

    public int RequiredEvaluationCount { get; }
}
