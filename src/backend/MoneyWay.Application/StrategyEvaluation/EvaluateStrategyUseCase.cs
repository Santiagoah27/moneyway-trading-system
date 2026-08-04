using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.StrategyEvaluation;

/// <summary>
/// Coordinates the generic domain evaluator without evaluating markets or knowing Forex or Nasdaq rules.
/// </summary>
public sealed class EvaluateStrategyUseCase
{
    private readonly SequentialStrategyEvaluator evaluator = new();

    public EvaluateStrategyResult Execute(EvaluateStrategyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outcome = evaluator.Evaluate(request.Evaluations);
        var orderedEvaluations = request.Evaluations.OrderBy(static evaluation => evaluation.Sequence).ToArray();

        return new EvaluateStrategyResult(
            request.StrategyId,
            request.StrategyVersion,
            outcome,
            orderedEvaluations);
    }
}
