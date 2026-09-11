namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Marks one immutable, typed fact available to a replay lifecycle policy.</summary>
public interface IStrategyReplayLifecycleEvidence
{
    DateTimeOffset ObservedAtUtc { get; }
}
