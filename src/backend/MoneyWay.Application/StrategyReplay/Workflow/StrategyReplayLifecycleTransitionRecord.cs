using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Records an applied lifecycle transition without strategy-specific interpretation.</summary>
public sealed class StrategyReplayLifecycleTransitionRecord
{
    public StrategyReplayLifecycleTransitionRecord(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        StrategyReplayProgressionInstanceId instanceId,
        DateTimeOffset asOfUtc,
        StrategyReplayLifecycleTransitionKind kind,
        StrategyReplayProgressionInstanceId? priorActiveInstanceId,
        StrategyReplayProgressionInstanceId? resultingActiveInstanceId,
        StrategyReplayProgressionStateReference? priorStateReference,
        StrategyReplayProgressionStateReference? resultingStateReference)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ValidateTransition(instanceId, kind, priorActiveInstanceId, resultingActiveInstanceId, priorStateReference, resultingStateReference);
        AsOfUtc = asOfUtc;
        Kind = kind;
        PriorActiveInstanceId = priorActiveInstanceId;
        ResultingActiveInstanceId = resultingActiveInstanceId;
        PriorStateReference = priorStateReference;
        ResultingStateReference = resultingStateReference;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public StrategyReplayProgressionInstanceId InstanceId { get; }
    public DateTimeOffset AsOfUtc { get; }
    public StrategyReplayLifecycleTransitionKind Kind { get; }
    public StrategyReplayProgressionInstanceId? PriorActiveInstanceId { get; }
    public StrategyReplayProgressionInstanceId? ResultingActiveInstanceId { get; }
    public StrategyReplayProgressionStateReference? PriorStateReference { get; }
    public StrategyReplayProgressionStateReference? ResultingStateReference { get; }

    private static void ValidateTransition(
        StrategyReplayProgressionInstanceId instanceId,
        StrategyReplayLifecycleTransitionKind kind,
        StrategyReplayProgressionInstanceId? priorActiveInstanceId,
        StrategyReplayProgressionInstanceId? resultingActiveInstanceId,
        StrategyReplayProgressionStateReference? priorStateReference,
        StrategyReplayProgressionStateReference? resultingStateReference)
    {
        var isValid = kind switch
        {
            StrategyReplayLifecycleTransitionKind.Start => priorActiveInstanceId is null
                && resultingActiveInstanceId == instanceId
                && priorStateReference is null
                && resultingStateReference is not null,
            StrategyReplayLifecycleTransitionKind.ReplaceActiveState => priorActiveInstanceId == instanceId
                && resultingActiveInstanceId == instanceId
                && priorStateReference is not null
                && resultingStateReference is not null,
            StrategyReplayLifecycleTransitionKind.Cancel or StrategyReplayLifecycleTransitionKind.Expire =>
                priorActiveInstanceId == instanceId
                && resultingActiveInstanceId is null
                && priorStateReference is not null
                && resultingStateReference is null,
            StrategyReplayLifecycleTransitionKind.None => false,
            _ => false,
        };
        if (!isValid) throw new ArgumentException("Lifecycle transition audit metadata is inconsistent with its kind.");
    }
}
