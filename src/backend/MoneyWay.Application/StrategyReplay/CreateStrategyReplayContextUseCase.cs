using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Creates a strategy-bound observable market-data context from an already validated multi-timeframe replay frame.
/// It performs no strategy-rule evaluation.
/// </summary>
public sealed class CreateStrategyReplayContextUseCase
{
    public StrategyReplayContext Execute(StrategyDefinition strategyDefinition, MultiTimeframeReplayFrame replayFrame)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(replayFrame);
        return new StrategyReplayContext(strategyDefinition.StrategyId, strategyDefinition.Version, replayFrame);
    }

    public StrategyReplayContext ExecuteCanonical(StrategyDefinition strategyDefinition, CanonicalMultiTimeframeReplayFrame replayFrame)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(replayFrame);
        return new StrategyReplayContext(strategyDefinition.StrategyId, strategyDefinition.Version, replayFrame);
    }
}
