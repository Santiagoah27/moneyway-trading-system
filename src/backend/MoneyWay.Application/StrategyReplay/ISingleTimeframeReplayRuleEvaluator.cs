using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Transitional evaluator contract retained for the legacy single-timeframe backtesting pipeline.
/// New strategy rule implementations must use <see cref="IReplayRuleEvaluator"/> instead.
/// </summary>
public interface ISingleTimeframeReplayRuleEvaluator
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    RuleId RuleId { get; }
    ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame);
}
