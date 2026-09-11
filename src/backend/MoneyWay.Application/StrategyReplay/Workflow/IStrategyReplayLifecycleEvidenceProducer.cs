using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Produces current, bounded lifecycle evidence without retaining replay state.</summary>
public interface IStrategyReplayLifecycleEvidenceProducer
{
    StrategyId StrategyId { get; }
    StrategyVersion StrategyVersion { get; }
    StrategyReplayLifecycleEvidenceSnapshot Capture(StrategyReplayLifecycleEvidenceProductionContext context);
}
