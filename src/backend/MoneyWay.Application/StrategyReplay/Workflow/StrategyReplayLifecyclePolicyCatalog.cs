using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Validates and resolves one deterministic lifecycle policy per exact strategy version.</summary>
public sealed class StrategyReplayLifecyclePolicyCatalog
{
    public static StrategyReplayLifecyclePolicyCatalog Empty { get; } = new([], []);

    private readonly IReadOnlyDictionary<PolicyKey, IStrategyReplayLifecyclePolicy> policies;

    public StrategyReplayLifecyclePolicyCatalog(
        IEnumerable<StrategyDefinition> strategyDefinitions,
        IEnumerable<IStrategyReplayLifecyclePolicy> lifecyclePolicies)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinitions);
        ArgumentNullException.ThrowIfNull(lifecyclePolicies);
        var definitions = strategyDefinitions.ToArray();
        var configured = lifecyclePolicies.ToArray();
        if (definitions.Any(item => item is null))
            throw new ArgumentException("Strategy definitions cannot contain null.", nameof(strategyDefinitions));
        if (configured.Any(item => item is null))
            throw new ArgumentException("Lifecycle policies cannot contain null.", nameof(lifecyclePolicies));
        var known = definitions.Select(item => new PolicyKey(item.StrategyId, item.Version)).ToHashSet();
        var duplicate = configured.GroupBy(item => new PolicyKey(item.StrategyId, item.StrategyVersion)).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate lifecycle policy for strategy '{duplicate.Key.StrategyId}' version '{duplicate.Key.StrategyVersion}'.", nameof(lifecyclePolicies));
        var unknown = configured.FirstOrDefault(item => !known.Contains(new(item.StrategyId, item.StrategyVersion)));
        if (unknown is not null)
            throw new InvalidOperationException($"Lifecycle policy references unknown strategy '{unknown.StrategyId}' version '{unknown.StrategyVersion}'.");
        LifecyclePolicies = new ReadOnlyCollection<IStrategyReplayLifecyclePolicy>(configured);
        policies = configured.ToDictionary(item => new PolicyKey(item.StrategyId, item.StrategyVersion));
    }

    public IReadOnlyList<IStrategyReplayLifecyclePolicy> LifecyclePolicies { get; }

    public IStrategyReplayLifecyclePolicy? Find(StrategyId strategyId, StrategyVersion strategyVersion)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        return policies.GetValueOrDefault(new(strategyId, strategyVersion));
    }

    private sealed record PolicyKey(StrategyId StrategyId, StrategyVersion StrategyVersion);
}
