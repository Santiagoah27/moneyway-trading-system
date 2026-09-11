using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Immutable typed evidence bounded to one replay step and optional progression instance.</summary>
public sealed class StrategyReplayLifecycleEvidenceSnapshot
{
    private readonly IReadOnlyDictionary<Type, IStrategyReplayLifecycleEvidence> evidenceByType;

    public StrategyReplayLifecycleEvidenceSnapshot(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        int step,
        DateTimeOffset asOfUtc,
        StrategyReplayLifecycleEvidenceScope scope,
        StrategyReplayProgressionInstanceId? instanceId,
        IEnumerable<IStrategyReplayLifecycleEvidence> evidence)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(evidence);

        var items = evidence.ToArray();
        if (items.Any(item => item is null)) throw new ArgumentException("Evidence cannot contain null items.", nameof(evidence));
        if (items.Any(item => item.ObservedAtUtc.Offset != TimeSpan.Zero || item.ObservedAtUtc > asOfUtc))
            throw new ArgumentException("Evidence must be UTC and observable no later than AsOfUtc.", nameof(evidence));
        if (items.GroupBy(item => item.GetType()).Any(group => group.Count() > 1))
            throw new ArgumentException("Only one evidence payload per runtime type is allowed.", nameof(evidence));
        if (scope == StrategyReplayLifecycleEvidenceScope.Empty && (instanceId is not null || items.Length != 0))
            throw new ArgumentException("Empty evidence cannot have an instance or payloads.", nameof(scope));
        if (scope == StrategyReplayLifecycleEvidenceScope.ActivationCandidate && instanceId is not null)
            throw new ArgumentException("Activation-candidate evidence cannot be bound to an instance.", nameof(instanceId));
        if (scope == StrategyReplayLifecycleEvidenceScope.ActiveInstance && instanceId is null)
            throw new ArgumentException("Active-instance evidence requires an instance identifier.", nameof(instanceId));
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));

        Step = step;
        AsOfUtc = asOfUtc;
        Scope = scope;
        InstanceId = instanceId;
        var ordered = items.OrderBy(item => item.GetType().FullName, StringComparer.Ordinal).ToArray();
        Evidence = new ReadOnlyCollection<IStrategyReplayLifecycleEvidence>(ordered);
        evidenceByType = new ReadOnlyDictionary<Type, IStrategyReplayLifecycleEvidence>(ordered.ToDictionary(item => item.GetType()));
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public StrategyReplayLifecycleEvidenceScope Scope { get; }
    public StrategyReplayProgressionInstanceId? InstanceId { get; }
    public IReadOnlyList<IStrategyReplayLifecycleEvidence> Evidence { get; }

    public bool TryGet<T>(out T? evidence) where T : class, IStrategyReplayLifecycleEvidence
    {
        if (evidenceByType.TryGetValue(typeof(T), out var value))
        {
            evidence = (T)value;
            return true;
        }

        evidence = null;
        return false;
    }

    public static StrategyReplayLifecycleEvidenceSnapshot Empty(StrategyReplayContextObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new(observation.StrategyId, observation.StrategyVersion, observation.ProviderId, observation.Symbol,
            observation.Step, observation.AsOfUtc, StrategyReplayLifecycleEvidenceScope.Empty, null, []);
    }

    internal StrategyReplayLifecycleEvidenceSnapshot BindTo(StrategyReplayProgressionInstanceId instanceId) =>
        new(StrategyId, StrategyVersion, ProviderId, Symbol, Step, AsOfUtc,
            StrategyReplayLifecycleEvidenceScope.ActiveInstance, instanceId, Evidence);

    internal StrategyReplayLifecycleEvidenceSnapshot MergeFor(
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionInstanceId instanceId,
        StrategyReplayLifecycleEvidenceSnapshot previous)
    {
        var merged = previous.Evidence.Concat(Evidence)
            .GroupBy(item => item.GetType())
            .Select(group => group.Last());
        return new(observation.StrategyId, observation.StrategyVersion, observation.ProviderId, observation.Symbol,
            observation.Step, observation.AsOfUtc, StrategyReplayLifecycleEvidenceScope.ActiveInstance, instanceId, merged);
    }
}
