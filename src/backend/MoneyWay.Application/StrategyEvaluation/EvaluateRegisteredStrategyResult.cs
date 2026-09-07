using System.Collections.ObjectModel;

namespace MoneyWay.Application.StrategyEvaluation;

public sealed class EvaluateRegisteredStrategyResult
{
    private EvaluateRegisteredStrategyResult(
        StrategyEvaluationExecutionStatus status,
        EvaluateStrategyResult? evaluation,
        IEnumerable<StrategyEvaluationValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var snapshot = issues.ToArray();
        if (snapshot.Any(static issue => issue is null))
        {
            throw new ArgumentException("Issues cannot contain null elements.", nameof(issues));
        }

        if (status == StrategyEvaluationExecutionStatus.Success && (evaluation is null || snapshot.Length != 0))
        {
            throw new ArgumentException("Success requires an evaluation and no issues.", nameof(status));
        }

        if (status == StrategyEvaluationExecutionStatus.StrategyNotFound && evaluation is not null)
        {
            throw new ArgumentException("StrategyNotFound cannot contain an evaluation.", nameof(status));
        }

        if (status == StrategyEvaluationExecutionStatus.ValidationFailed
            && (evaluation is not null || snapshot.Length == 0))
        {
            throw new ArgumentException("ValidationFailed requires issues and no evaluation.", nameof(status));
        }

        Status = status;
        Evaluation = evaluation;
        Issues = new ReadOnlyCollection<StrategyEvaluationValidationIssue>(snapshot);
    }

    public StrategyEvaluationExecutionStatus Status { get; }

    public EvaluateStrategyResult? Evaluation { get; }

    public IReadOnlyList<StrategyEvaluationValidationIssue> Issues { get; }

    public static EvaluateRegisteredStrategyResult Success(EvaluateStrategyResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return new(StrategyEvaluationExecutionStatus.Success, evaluation, []);
    }

    public static EvaluateRegisteredStrategyResult StrategyNotFound(StrategyEvaluationValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new(StrategyEvaluationExecutionStatus.StrategyNotFound, null, [issue]);
    }

    public static EvaluateRegisteredStrategyResult ValidationFailed(
        IEnumerable<StrategyEvaluationValidationIssue> issues) =>
        new(StrategyEvaluationExecutionStatus.ValidationFailed, null, issues);
}
