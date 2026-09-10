using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>
/// Selects a lifecycle transition using only the immutable replay-local state and current future-safe observation.
/// Implementations must be deterministic and free of external I/O, wall-clock access, randomness, and hidden state.
/// </summary>
public interface IStrategyReplayLifecyclePolicy
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    StrategyReplayLifecycleTransition Decide(StrategyReplayLifecyclePolicyContext context);
}
