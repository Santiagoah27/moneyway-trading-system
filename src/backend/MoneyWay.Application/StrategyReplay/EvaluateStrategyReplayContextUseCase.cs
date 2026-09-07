using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Coordinates canonical multi-timeframe rule evaluators for one strategy definition and replay context. Structural
/// metadata comes from the definition and evaluation time from <see cref="StrategyReplayContext.AsOfUtc"/>. It does
/// not calculate a strategy verdict.
/// </summary>
public sealed class EvaluateStrategyReplayContextUseCase
{
    private readonly IReadOnlyDictionary<EvaluatorKey, IReplayRuleEvaluator> evaluators;

    public EvaluateStrategyReplayContextUseCase(IEnumerable<IReplayRuleEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        var registry = new Dictionary<EvaluatorKey, IReplayRuleEvaluator>();
        foreach (var evaluator in evaluators)
        {
            if (evaluator is null) throw new ArgumentException("Evaluators cannot contain null.", nameof(evaluators));
            if (!registry.TryAdd(new(evaluator.StrategyId, evaluator.StrategyVersion, evaluator.RuleId), evaluator))
                throw new ArgumentException("Duplicate evaluator registration.", nameof(evaluators));
        }
        this.evaluators = registry;
    }

    public StrategyReplayContextObservation Execute(StrategyDefinition strategyDefinition, StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != strategyDefinition.StrategyId || context.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Strategy context identity must exactly match the strategy definition.");
        var matching = evaluators.Where(x => x.Key.StrategyId == strategyDefinition.StrategyId && x.Key.Version == strategyDefinition.Version).ToArray();
        var rules = strategyDefinition.Rules.ToDictionary(x => x.RuleId);
        if (matching.Any(x => !rules.ContainsKey(x.Key.RuleId)))
            throw new InvalidOperationException("An evaluator is registered for an unknown rule.");
        var results = new List<RuleEvaluation>();
        foreach (var rule in strategyDefinition.Rules)
        {
            if (!evaluators.TryGetValue(new(strategyDefinition.StrategyId, strategyDefinition.Version, rule.RuleId), out var evaluator)) continue;
            var decision = evaluator.Evaluate(context) ?? throw new InvalidOperationException("Evaluator returned null.");
            results.Add(new(rule.RuleId, rule.DefinitionStatus, decision.Result, rule.Sequence, rule.IsRequired, decision.Reason, context.AsOfUtc, decision.EvidenceReference));
        }
        return new(strategyDefinition.StrategyId, strategyDefinition.Version, context.ProviderId, context.Symbol, context.Step, context.AsOfUtc, results);
    }

    private sealed record EvaluatorKey(StrategyId StrategyId, StrategyVersion Version, RuleId RuleId);
}
