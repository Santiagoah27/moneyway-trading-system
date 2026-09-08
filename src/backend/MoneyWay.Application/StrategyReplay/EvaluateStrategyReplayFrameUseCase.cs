using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Coordinates registered rule evaluators in the transitional single-timeframe pipeline retained for backward
/// compatibility with earlier MoneyWay backtesting infrastructure. New strategy-rule and backtesting features must
/// use the canonical <see cref="StrategyReplayContext"/> multi-timeframe pipeline.
/// </summary>
public sealed class EvaluateStrategyReplayFrameUseCase
{
    private readonly IReadOnlyDictionary<EvaluatorKey, ISingleTimeframeReplayRuleEvaluator> evaluators;
    public EvaluateStrategyReplayFrameUseCase(IEnumerable<ISingleTimeframeReplayRuleEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        var registry = new Dictionary<EvaluatorKey, ISingleTimeframeReplayRuleEvaluator>();
        foreach (var evaluator in evaluators)
        {
            if (evaluator is null) throw new ArgumentException("Evaluators cannot contain null.", nameof(evaluators));
            if (!registry.TryAdd(new(evaluator.StrategyId, evaluator.StrategyVersion, evaluator.RuleId), evaluator)) throw new ArgumentException("Duplicate evaluator registration.", nameof(evaluators));
        }
        this.evaluators = registry;
    }
    public StrategyReplayFrameObservation Execute(StrategyDefinition definition, ReplayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(definition); ArgumentNullException.ThrowIfNull(frame);
        var matching = evaluators.Where(x => x.Key.StrategyId == definition.StrategyId && x.Key.Version == definition.Version).ToArray();
        var rules = definition.Rules.ToDictionary(x => x.RuleId);
        if (matching.Any(x => !rules.ContainsKey(x.Key.RuleId))) throw new InvalidOperationException("An evaluator is registered for an unknown rule.");
        var results = new List<RuleEvaluation>();
        foreach (var rule in definition.Rules)
        {
            if (!evaluators.TryGetValue(new(definition.StrategyId, definition.Version, rule.RuleId), out var evaluator)) continue;
            var decision = evaluator.Evaluate(frame) ?? throw new InvalidOperationException("Evaluator returned null.");
            results.Add(new RuleEvaluation(rule.RuleId, rule.DefinitionStatus, decision.Result, rule.Sequence, rule.IsRequired, decision.Reason, frame.AsOfUtc, decision.EvidenceReference));
        }
        return new(definition.StrategyId, definition.Version, frame.Step, frame.AsOfUtc, frame.CurrentCandle, results);
    }
    private sealed record EvaluatorKey(StrategyId StrategyId, StrategyVersion Version, RuleId RuleId);
}
