using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Immutable strategy-owned causal state explicitly supplied before rule evaluation.</summary>
public interface IStrategyReplayPreEvaluationState
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    MarketDataProviderId ProviderId { get; }
    MarketSymbol Symbol { get; }
}
