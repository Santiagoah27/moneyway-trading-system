using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Provides immutable, strategy-owned lifecycle policies for built-in MoneyWay strategy versions.</summary>
public static class MoneyWayReplayLifecyclePolicies
{
    private static readonly IReadOnlyList<IStrategyReplayLifecyclePolicy> Policies =
        new ReadOnlyCollection<IStrategyReplayLifecyclePolicy>(
        [
            new MoneyWayNasdaqReplayLifecyclePolicy(),
        ]);

    public static IReadOnlyList<IStrategyReplayLifecyclePolicy> GetAll() => Policies;
}
