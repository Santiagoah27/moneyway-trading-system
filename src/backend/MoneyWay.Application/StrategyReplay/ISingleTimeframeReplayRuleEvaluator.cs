using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Transitional single-timeframe evaluator contract retained for backward compatibility with earlier MoneyWay
/// backtesting infrastructure. New strategy-rule and backtesting features must use the canonical
/// <see cref="StrategyReplayContext"/> multi-timeframe pipeline through <see cref="IReplayRuleEvaluator"/>.
/// </summary>
public interface ISingleTimeframeReplayRuleEvaluator
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    RuleId RuleId { get; }
    ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame);
}
