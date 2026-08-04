namespace MoneyWay.Domain.Strategies.Evaluation;

/// <summary>
/// Deterministically derives a strategy verdict from already completed rule evaluations in sequence order.
/// </summary>
public sealed class SequentialStrategyEvaluator
{
    private const string ReadyReason = "All required rule evaluations passed or were not applicable.";
    private const string EmptyReason = "No rule evaluations were supplied.";

    public StrategyEvaluationOutcome Evaluate(IEnumerable<RuleEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);

        var materialized = new List<RuleEvaluation>();
        var sequences = new HashSet<int>();
        var requiredEvaluationCount = 0;

        foreach (var evaluation in evaluations)
        {
            if (evaluation is null)
            {
                throw new ArgumentException("The collection cannot contain null evaluations.", nameof(evaluations));
            }

            if (!sequences.Add(evaluation.Sequence))
            {
                throw new ArgumentException($"Sequence {evaluation.Sequence} is duplicated.", nameof(evaluations));
            }

            materialized.Add(evaluation);
            if (evaluation.IsRequired)
            {
                requiredEvaluationCount++;
            }
        }

        if (materialized.Count == 0)
        {
            return new StrategyEvaluationOutcome(
                StrategyVerdict.DataUnavailable,
                null,
                null,
                null,
                EmptyReason,
                0,
                0);
        }

        materialized.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));

        foreach (var evaluation in materialized)
        {
            if (!evaluation.IsRequired
                || evaluation.Result is RuleEvaluationResult.Passed or RuleEvaluationResult.NotApplicable)
            {
                continue;
            }

            return new StrategyEvaluationOutcome(
                MapBlockingVerdict(evaluation.Result),
                evaluation.RuleId,
                evaluation.Sequence,
                evaluation.Result,
                evaluation.Reason,
                materialized.Count,
                requiredEvaluationCount);
        }

        return new StrategyEvaluationOutcome(
            StrategyVerdict.Ready,
            null,
            null,
            null,
            ReadyReason,
            materialized.Count,
            requiredEvaluationCount);
    }

    private static StrategyVerdict MapBlockingVerdict(RuleEvaluationResult result)
    {
        return result switch
        {
            RuleEvaluationResult.Waiting => StrategyVerdict.Wait,
            RuleEvaluationResult.Failed => StrategyVerdict.NoTrade,
            RuleEvaluationResult.HumanValidationRequired => StrategyVerdict.HumanValidationRequired,
            RuleEvaluationResult.DataUnavailable => StrategyVerdict.DataUnavailable,
            _ => throw new InvalidOperationException($"Rule result {result} is not blocking."),
        };
    }
}
