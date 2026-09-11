namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Applies one strategy-owned lifecycle decision to the canonical chronological progression fold.</summary>
public sealed class AdvanceStrategyReplayLifecycleUseCase
{
    private readonly AdvanceStrategyReplayProgressionUseCase progressionUseCase;

    public AdvanceStrategyReplayLifecycleUseCase(AdvanceStrategyReplayProgressionUseCase progressionUseCase)
    {
        this.progressionUseCase = progressionUseCase ?? throw new ArgumentNullException(nameof(progressionUseCase));
    }

    public StrategyReplayLifecycleAdvanceResult Execute(
        StrategyReplayWorkflowDefinition workflow,
        IStrategyReplayLifecyclePolicy policy,
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleSnapshot? previousSnapshot)
        => ExecuteCore(workflow, policy, observation, previousSnapshot, null, null, null);

    public StrategyReplayLifecycleAdvanceResult Execute(
        StrategyReplayWorkflowDefinition workflow,
        IStrategyReplayLifecyclePolicy policy,
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleSnapshot? previousSnapshot,
        StrategyReplayProgressionSnapshot? previousProgression)
        => ExecuteCore(workflow, policy, observation, previousSnapshot, previousProgression, null, null);

    public StrategyReplayLifecycleAdvanceResult Execute(
        StrategyReplayWorkflowDefinition workflow,
        IStrategyReplayLifecyclePolicy policy,
        StrategyReplayContext replayContext,
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleSnapshot? previousSnapshot,
        StrategyReplayProgressionSnapshot? previousProgression,
        IStrategyReplayLifecycleEvidenceProducer evidenceProducer)
        => ExecuteCore(workflow, policy, observation, previousSnapshot, previousProgression,
            replayContext ?? throw new ArgumentNullException(nameof(replayContext)),
            evidenceProducer ?? throw new ArgumentNullException(nameof(evidenceProducer)));

    private StrategyReplayLifecycleAdvanceResult ExecuteCore(
        StrategyReplayWorkflowDefinition workflow,
        IStrategyReplayLifecyclePolicy policy,
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleSnapshot? previousSnapshot,
        StrategyReplayProgressionSnapshot? previousProgression,
        StrategyReplayContext? replayContext,
        IStrategyReplayLifecycleEvidenceProducer? evidenceProducer)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(observation);
        ValidateIdentityAndChronology(workflow, policy, observation, previousSnapshot);

        var previousActive = previousSnapshot?.ActiveInstance;
        var candidateProgression = previousActive is not null
            ? progressionUseCase.Execute(workflow, observation, previousActive.WorkflowProgression)
            : previousSnapshot is null
                || previousSnapshot.Instances.Count == 0
                || !WasTerminatedOnSnapshot(previousSnapshot)
                ? progressionUseCase.Execute(workflow, observation, previousProgression)
                : progressionUseCase.StartInstance(workflow, observation);
        var evidence = StrategyReplayLifecycleEvidenceSnapshot.Empty(observation);
        if (evidenceProducer is not null)
        {
            if (evidenceProducer.StrategyId != observation.StrategyId || evidenceProducer.StrategyVersion != observation.StrategyVersion)
                throw new InvalidOperationException("Lifecycle evidence producer identity must exactly match the strategy observation.");
            evidence = evidenceProducer.Capture(new(replayContext!, observation, candidateProgression, previousSnapshot))
                ?? throw new InvalidOperationException("Lifecycle evidence producer returned null.");
        }
        var policyContext = new StrategyReplayLifecyclePolicyContext(observation, candidateProgression, previousSnapshot, evidence);
        var transition = policy.Decide(policyContext) ?? throw new InvalidOperationException("Lifecycle policy returned null.");
        var instances = previousSnapshot?.Instances.ToList() ?? [];
        var history = previousSnapshot?.TransitionHistory.ToList() ?? [];

        ApplyTransition(observation, transition, previousActive, candidateProgression, evidence, instances, history);
        return new(
            candidateProgression,
            new(
                observation.StrategyId,
                observation.StrategyVersion,
                observation.ProviderId,
                observation.Symbol,
                observation.Step,
                observation.AsOfUtc,
                instances,
                history));
    }

    private static void ApplyTransition(
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleTransition transition,
        StrategyReplayProgressionInstanceSnapshot? previousActive,
        StrategyReplayProgressionSnapshot candidateProgression,
        StrategyReplayLifecycleEvidenceSnapshot evidence,
        List<StrategyReplayProgressionInstanceSnapshot> instances,
        List<StrategyReplayLifecycleTransitionRecord> history)
    {
        switch (transition.Kind)
        {
            case StrategyReplayLifecycleTransitionKind.None:
                if (previousActive is not null)
                    ReplaceInstance(instances, new(previousActive.InstanceId, previousActive.StartedAtUtc, previousActive.StateReference,
                        candidateProgression, evidence: EvidenceForActive(observation, evidence, previousActive)));
                return;
            case StrategyReplayLifecycleTransitionKind.Start:
                if (previousActive is not null) throw new InvalidOperationException("A new progression cannot start while another progression is active.");
                var instanceId = new StrategyReplayProgressionInstanceId(instances.Count == 0 ? 1 : instances.Max(item => item.InstanceId.Ordinal) + 1);
                var started = new StrategyReplayProgressionInstanceSnapshot(instanceId, observation.AsOfUtc, transition.StateReference!,
                    candidateProgression, evidence: evidence.BindTo(instanceId));
                instances.Add(started);
                history.Add(Record(observation, instanceId, transition.Kind, null, instanceId, null, started.StateReference));
                return;
            case StrategyReplayLifecycleTransitionKind.ReplaceActiveState:
                EnsureActive(previousActive, transition.Kind);
                var updated = new StrategyReplayProgressionInstanceSnapshot(previousActive!.InstanceId, previousActive.StartedAtUtc,
                    transition.StateReference!, candidateProgression,
                    evidence: EvidenceForActive(observation, evidence, previousActive));
                ReplaceInstance(instances, updated);
                history.Add(Record(observation, updated.InstanceId, transition.Kind, updated.InstanceId, updated.InstanceId, previousActive.StateReference, updated.StateReference));
                return;
            case StrategyReplayLifecycleTransitionKind.Cancel:
            case StrategyReplayLifecycleTransitionKind.Expire:
                EnsureActive(previousActive, transition.Kind);
                var terminationKind = transition.Kind == StrategyReplayLifecycleTransitionKind.Cancel
                    ? StrategyReplayProgressionTerminationKind.Cancelled
                    : StrategyReplayProgressionTerminationKind.Expired;
                var terminated = new StrategyReplayProgressionInstanceSnapshot(
                    previousActive!.InstanceId,
                    previousActive.StartedAtUtc,
                    previousActive.StateReference,
                    previousActive.WorkflowProgression,
                    observation.AsOfUtc,
                    terminationKind,
                    EvidenceForActive(observation, evidence, previousActive));
                ReplaceInstance(instances, terminated);
                history.Add(Record(observation, terminated.InstanceId, transition.Kind, terminated.InstanceId, null, terminated.StateReference, null));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(transition));
        }
    }

    private static StrategyReplayLifecycleEvidenceSnapshot EvidenceForActive(
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleEvidenceSnapshot current,
        StrategyReplayProgressionInstanceSnapshot previousActive) =>
        current.MergeFor(observation, previousActive.InstanceId, previousActive.Evidence);

    private static StrategyReplayLifecycleTransitionRecord Record(
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionInstanceId instanceId,
        StrategyReplayLifecycleTransitionKind kind,
        StrategyReplayProgressionInstanceId? priorActiveInstanceId,
        StrategyReplayProgressionInstanceId? resultingActiveInstanceId,
        StrategyReplayProgressionStateReference? priorStateReference,
        StrategyReplayProgressionStateReference? resultingStateReference) => new(
            observation.StrategyId,
            observation.StrategyVersion,
            instanceId,
            observation.AsOfUtc,
            kind,
            priorActiveInstanceId,
            resultingActiveInstanceId,
            priorStateReference,
            resultingStateReference);

    private static void EnsureActive(StrategyReplayProgressionInstanceSnapshot? active, StrategyReplayLifecycleTransitionKind kind)
    {
        if (active is null) throw new InvalidOperationException($"Lifecycle transition '{kind}' requires an active progression.");
    }

    private static bool WasTerminatedOnSnapshot(StrategyReplayLifecycleSnapshot snapshot)
    {
        var lastTransition = snapshot.TransitionHistory.LastOrDefault();
        return lastTransition?.AsOfUtc == snapshot.AsOfUtc
            && lastTransition.Kind is StrategyReplayLifecycleTransitionKind.Cancel or StrategyReplayLifecycleTransitionKind.Expire;
    }

    private static void ReplaceInstance(
        List<StrategyReplayProgressionInstanceSnapshot> instances,
        StrategyReplayProgressionInstanceSnapshot replacement)
    {
        var index = instances.FindIndex(item => item.InstanceId == replacement.InstanceId);
        if (index < 0) throw new InvalidOperationException("Active progression instance is missing from lifecycle history.");
        instances[index] = replacement;
    }

    private static void ValidateIdentityAndChronology(
        StrategyReplayWorkflowDefinition workflow,
        IStrategyReplayLifecyclePolicy policy,
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleSnapshot? previousSnapshot)
    {
        if (workflow.StrategyId != observation.StrategyId || workflow.StrategyVersion != observation.StrategyVersion
            || policy.StrategyId != observation.StrategyId || policy.StrategyVersion != observation.StrategyVersion)
            throw new InvalidOperationException("Workflow, lifecycle policy, and observation identities must exactly match.");
        if (previousSnapshot is null) return;
        if (previousSnapshot.StrategyId != observation.StrategyId
            || previousSnapshot.StrategyVersion != observation.StrategyVersion
            || previousSnapshot.ProviderId != observation.ProviderId
            || previousSnapshot.Symbol != observation.Symbol)
            throw new InvalidOperationException("Lifecycle snapshot identity must exactly match the strategy observation.");
        if (observation.Step != previousSnapshot.Step + 1 || observation.AsOfUtc <= previousSnapshot.AsOfUtc)
            throw new InvalidOperationException("Lifecycle observations must be consecutive and strictly chronological.");
    }
}

public sealed record StrategyReplayLifecycleAdvanceResult(
    StrategyReplayProgressionSnapshot WorkflowProgression,
    StrategyReplayLifecycleSnapshot LifecycleProgression);
