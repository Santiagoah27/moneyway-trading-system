using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Checks required coverage in the transitional single-timeframe pipeline retained for backward compatibility with
/// earlier MoneyWay backtesting infrastructure. New strategy-rule and backtesting features must use the canonical
/// <see cref="StrategyReplayContext"/> multi-timeframe pipeline.
/// </summary>
public sealed class EvaluateStrategyReplayFrameOutcomeUseCase
{
    private readonly SequentialStrategyEvaluator evaluator = new();

    public StrategyReplayFrameOutcome Execute(
        StrategyDefinition strategyDefinition,
        StrategyReplayFrameObservation observation)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.StrategyId != strategyDefinition.StrategyId
            || observation.StrategyVersion != strategyDefinition.Version)
        {
            throw new InvalidOperationException("Observation strategy identity does not match the definition.");
        }

        var definitions = strategyDefinition.Rules.ToDictionary(rule => rule.RuleId);
        foreach (var evaluation in observation.Evaluations)
        {
            if (!definitions.TryGetValue(evaluation.RuleId, out var definition)
                || evaluation.Sequence != definition.Sequence
                || evaluation.IsRequired != definition.IsRequired
                || evaluation.DefinitionStatus != definition.DefinitionStatus
                || evaluation.EvaluatedAtUtc != observation.AsOfUtc)
            {
                throw new InvalidOperationException("Observation evaluation metadata does not match the strategy definition.");
            }
        }

        var evaluatedRuleIds = observation.Evaluations.Select(evaluation => evaluation.RuleId).ToHashSet();
        var missing = strategyDefinition.Rules
            .Where(rule => rule.IsRequired && !evaluatedRuleIds.Contains(rule.RuleId))
            .Select(rule => rule.RuleId)
            .ToArray();

        if (missing.Length > 0)
        {
            return new StrategyReplayFrameOutcome(
                observation,
                false,
                missing,
                StrategyVerdict.DataUnavailable,
                StrategyReplayFrameOutcome.IncompleteCoverageReason,
                null);
        }

        var outcome = evaluator.Evaluate(observation.Evaluations);
        return new StrategyReplayFrameOutcome(
            observation,
            true,
            [],
            outcome.Verdict,
            outcome.Reason,
            outcome);
    }
}
