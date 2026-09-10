using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Validates exact rule-evaluation metadata and required coverage before delegating complete observations to
/// <see cref="SequentialStrategyEvaluator"/>. It fabricates no evaluations and manages no market data or cross-frame state.
/// </summary>
public sealed class EvaluateStrategyReplayContextOutcomeUseCase
{
    private readonly SequentialStrategyEvaluator evaluator = new();

    public StrategyReplayContextOutcome Execute(StrategyDefinition strategyDefinition, StrategyReplayContextObservation observation)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.StrategyId != strategyDefinition.StrategyId || observation.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Observation strategy identity does not match the definition.");
        var definitions = strategyDefinition.Rules.ToDictionary(rule => rule.RuleId);
        foreach (var evaluation in observation.Evaluations)
        {
            if (!definitions.TryGetValue(evaluation.RuleId, out var definition)
                || evaluation.Sequence != definition.Sequence
                || evaluation.IsRequired != definition.IsRequired
                || evaluation.DefinitionStatus != definition.DefinitionStatus
                || evaluation.EvaluatedAtUtc != observation.AsOfUtc)
                throw new InvalidOperationException("Observation evaluation metadata does not match the strategy definition.");
        }
        if (observation.MarketDataObservability.Any(item => !definitions.ContainsKey(item.RuleId)))
            throw new InvalidOperationException("Observation market-data observability references an unknown strategy rule.");
        var evaluatedRuleIds = observation.Evaluations.Select(x => x.RuleId).ToHashSet();
        var missing = strategyDefinition.Rules.Where(rule => rule.IsRequired && !evaluatedRuleIds.Contains(rule.RuleId)).Select(rule => rule.RuleId).ToArray();
        if (missing.Length > 0)
            return new(observation, false, missing, StrategyVerdict.DataUnavailable, StrategyReplayContextOutcome.IncompleteCoverageReason, null);
        var outcome = evaluator.Evaluate(observation.Evaluations);
        return new(observation, true, [], outcome.Verdict, outcome.Reason, outcome);
    }
}
