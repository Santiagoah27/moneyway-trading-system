using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.StrategyEvaluation;

/// <summary>
/// Preserves the complete ordered evaluation snapshot and its domain outcome for auditability.
/// Ready does not authorize execution.
/// </summary>
public sealed class EvaluateStrategyResult
{
    public EvaluateStrategyResult(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        StrategyEvaluationOutcome outcome,
        IEnumerable<RuleEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(evaluations);

        var snapshot = evaluations.ToArray();
        ValidateEvaluations(snapshot, outcome);

        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        Outcome = outcome;
        Evaluations = new ReadOnlyCollection<RuleEvaluation>(snapshot);
    }

    public StrategyId StrategyId { get; }

    public StrategyVersion StrategyVersion { get; }

    public StrategyEvaluationOutcome Outcome { get; }

    public IReadOnlyList<RuleEvaluation> Evaluations { get; }

    private static void ValidateEvaluations(
        IReadOnlyList<RuleEvaluation> evaluations,
        StrategyEvaluationOutcome outcome)
    {
        var requiredCount = 0;

        for (var index = 0; index < evaluations.Count; index++)
        {
            var evaluation = evaluations[index]
                ?? throw new ArgumentException("Evaluations cannot contain null elements.", nameof(evaluations));

            if (index > 0 && evaluations[index - 1].Sequence >= evaluation.Sequence)
            {
                throw new ArgumentException("Evaluations must have unique sequences in ascending order.", nameof(evaluations));
            }

            if (evaluation.IsRequired)
            {
                requiredCount++;
            }
        }

        if (outcome.EvaluationCount != evaluations.Count)
        {
            throw new ArgumentException("Outcome evaluation count does not match the evaluation snapshot.", nameof(outcome));
        }

        if (outcome.RequiredEvaluationCount != requiredCount)
        {
            throw new ArgumentException("Outcome required evaluation count does not match the evaluation snapshot.", nameof(outcome));
        }

        if (outcome.BlockingRuleId is null)
        {
            return;
        }

        var matches = evaluations.Where(evaluation =>
            evaluation.RuleId == outcome.BlockingRuleId
            && evaluation.Sequence == outcome.BlockingSequence).ToArray();

        if (matches.Length != 1)
        {
            throw new ArgumentException("The blocking rule must identify exactly one evaluation.", nameof(outcome));
        }

        var blockingEvaluation = matches[0];
        if (!blockingEvaluation.IsRequired
            || blockingEvaluation.Result != outcome.BlockingResult
            || blockingEvaluation.Reason != outcome.Reason)
        {
            throw new ArgumentException("The blocking outcome does not match its required evaluation.", nameof(outcome));
        }
    }
}
