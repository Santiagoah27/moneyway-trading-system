using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Evaluates one versioned strategy rule using only market data observable in the supplied replay frame.</summary>
public interface IReplayRuleEvaluator
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    RuleId RuleId { get; }
    ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame);
}
