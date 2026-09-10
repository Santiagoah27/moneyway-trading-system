using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Captures immutable lifecycle history and the active progression, if any, after one replay frame.</summary>
public sealed class StrategyReplayLifecycleSnapshot
{
    public StrategyReplayLifecycleSnapshot(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        int step,
        DateTimeOffset asOfUtc,
        IEnumerable<StrategyReplayProgressionInstanceSnapshot> instances,
        IEnumerable<StrategyReplayLifecycleTransitionRecord> transitionHistory)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(transitionHistory);
        var instanceSnapshot = instances.ToArray();
        var historySnapshot = transitionHistory.ToArray();
        if (instanceSnapshot.Any(item => item is null)
            || instanceSnapshot.GroupBy(item => item.InstanceId).Any(group => group.Count() > 1)
            || instanceSnapshot.Count(item => item.IsActive) > 1
            || !instanceSnapshot.Select(item => item.InstanceId.Ordinal).SequenceEqual(Enumerable.Range(1, instanceSnapshot.Length))
            || instanceSnapshot.Any(item =>
                item.WorkflowProgression.StrategyId != strategyId
                || item.WorkflowProgression.StrategyVersion != strategyVersion
                || item.WorkflowProgression.ProviderId != providerId
                || item.WorkflowProgression.Symbol != symbol
                || item.WorkflowProgression.Step > step
                || item.WorkflowProgression.AsOfUtc > asOfUtc)
            || instanceSnapshot.Where(item => item.IsActive).Any(item => item.WorkflowProgression.Step != step))
            throw new ArgumentException("Lifecycle instances must be non-null, unique, and contain at most one active instance.", nameof(instances));
        var instanceIds = instanceSnapshot.Select(item => item.InstanceId).ToHashSet();
        if (historySnapshot.Any(item => item is null)
            || historySnapshot.Any(item => item.StrategyId != strategyId || item.StrategyVersion != strategyVersion || item.AsOfUtc > asOfUtc)
            || historySnapshot.Any(item => !instanceIds.Contains(item.InstanceId))
            || historySnapshot.Where((item, index) => index > 0 && item.AsOfUtc < historySnapshot[index - 1].AsOfUtc).Any())
            throw new ArgumentException("Lifecycle history must be identity-aligned and chronological.", nameof(transitionHistory));
        Step = step;
        AsOfUtc = asOfUtc;
        Instances = new ReadOnlyCollection<StrategyReplayProgressionInstanceSnapshot>(instanceSnapshot);
        TransitionHistory = new ReadOnlyCollection<StrategyReplayLifecycleTransitionRecord>(historySnapshot);
        ActiveInstance = instanceSnapshot.SingleOrDefault(item => item.IsActive);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<StrategyReplayProgressionInstanceSnapshot> Instances { get; }
    public IReadOnlyList<StrategyReplayLifecycleTransitionRecord> TransitionHistory { get; }
    public StrategyReplayProgressionInstanceSnapshot? ActiveInstance { get; }
}
