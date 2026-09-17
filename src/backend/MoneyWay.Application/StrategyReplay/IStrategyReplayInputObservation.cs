using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Immutable source-backed auxiliary input visible to a strategy evaluator only from its causal time.</summary>
public interface IStrategyReplayInputObservation
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    MarketDataProviderId ProviderId { get; }
    MarketSymbol Symbol { get; }
    DateTimeOffset ObservedAtUtc { get; }
    string SourceReference { get; }
}
