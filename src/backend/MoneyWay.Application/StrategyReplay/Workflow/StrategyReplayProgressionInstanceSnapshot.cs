namespace MoneyWay.Application.StrategyReplay.Workflow;

public enum StrategyReplayProgressionTerminationKind
{
    Cancelled,
    Expired,
}

/// <summary>Captures the latest immutable state of one active or terminal progression instance.</summary>
public sealed class StrategyReplayProgressionInstanceSnapshot
{
    public StrategyReplayProgressionInstanceSnapshot(
        StrategyReplayProgressionInstanceId instanceId,
        DateTimeOffset startedAtUtc,
        StrategyReplayProgressionStateReference stateReference,
        StrategyReplayProgressionSnapshot workflowProgression,
        DateTimeOffset? terminatedAtUtc = null,
        StrategyReplayProgressionTerminationKind? terminationKind = null,
        StrategyReplayLifecycleEvidenceSnapshot? evidence = null)
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        if (startedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(startedAtUtc));
        StateReference = stateReference ?? throw new ArgumentNullException(nameof(stateReference));
        WorkflowProgression = workflowProgression ?? throw new ArgumentNullException(nameof(workflowProgression));
        if ((terminatedAtUtc is null) != (terminationKind is null))
            throw new ArgumentException("Termination timestamp and kind must be supplied together.");
        if (terminatedAtUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Timestamp must be UTC.", nameof(terminatedAtUtc));
        if (terminatedAtUtc < startedAtUtc)
            throw new ArgumentException("Termination cannot precede activation.", nameof(terminatedAtUtc));
        StartedAtUtc = startedAtUtc;
        TerminatedAtUtc = terminatedAtUtc;
        TerminationKind = terminationKind;
        Evidence = evidence ?? new(
            workflowProgression.StrategyId,
            workflowProgression.StrategyVersion,
            workflowProgression.ProviderId,
            workflowProgression.Symbol,
            workflowProgression.Step,
            workflowProgression.AsOfUtc,
            StrategyReplayLifecycleEvidenceScope.ActiveInstance,
            instanceId,
            []);
        if (Evidence.Scope != StrategyReplayLifecycleEvidenceScope.ActiveInstance || Evidence.InstanceId != instanceId
            || Evidence.StrategyId != workflowProgression.StrategyId || Evidence.StrategyVersion != workflowProgression.StrategyVersion
            || Evidence.ProviderId != workflowProgression.ProviderId || Evidence.Symbol != workflowProgression.Symbol
            || Evidence.Step < workflowProgression.Step || Evidence.AsOfUtc < workflowProgression.AsOfUtc
            || (terminatedAtUtc is null && (Evidence.Step != workflowProgression.Step || Evidence.AsOfUtc != workflowProgression.AsOfUtc))
            || (terminatedAtUtc is not null && Evidence.AsOfUtc > terminatedAtUtc))
            throw new ArgumentException("Lifecycle evidence must be bound to this progression instance.", nameof(evidence));
    }

    public StrategyReplayProgressionInstanceId InstanceId { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public StrategyReplayProgressionStateReference StateReference { get; }
    public StrategyReplayProgressionSnapshot WorkflowProgression { get; }
    public DateTimeOffset? TerminatedAtUtc { get; }
    public StrategyReplayProgressionTerminationKind? TerminationKind { get; }
    public StrategyReplayLifecycleEvidenceSnapshot Evidence { get; }
    public bool IsActive => TerminationKind is null;
}
