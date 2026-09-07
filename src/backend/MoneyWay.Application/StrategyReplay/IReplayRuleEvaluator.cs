using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Evaluates one exact versioned strategy rule using only the synchronized multi-timeframe market data observable in
/// <see cref="StrategyReplayContext"/>. This is the canonical evaluator contract for new rule implementations.
/// </summary>
public interface IReplayRuleEvaluator
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    RuleId RuleId { get; }
    ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context);
}
