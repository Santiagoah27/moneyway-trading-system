namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Supplies immutable current-frame facts to a strategy-owned lifecycle policy.</summary>
public sealed class StrategyReplayLifecyclePolicyContext
{
    public StrategyReplayLifecyclePolicyContext(
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionSnapshot candidateProgression,
        StrategyReplayLifecycleSnapshot? previousLifecycle)
        : this(observation, candidateProgression, previousLifecycle, StrategyReplayLifecycleEvidenceSnapshot.Empty(observation))
    {
    }

    public StrategyReplayLifecyclePolicyContext(
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionSnapshot candidateProgression,
        StrategyReplayLifecycleSnapshot? previousLifecycle,
        StrategyReplayLifecycleEvidenceSnapshot evidence)
    {
        Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        CandidateProgression = candidateProgression ?? throw new ArgumentNullException(nameof(candidateProgression));
        if (candidateProgression.StrategyId != observation.StrategyId
            || candidateProgression.StrategyVersion != observation.StrategyVersion
            || candidateProgression.ProviderId != observation.ProviderId
            || candidateProgression.Symbol != observation.Symbol
            || candidateProgression.Step != observation.Step
            || candidateProgression.AsOfUtc != observation.AsOfUtc)
            throw new ArgumentException("Candidate progression must match the current observation.", nameof(candidateProgression));
        PreviousLifecycle = previousLifecycle;
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        if (evidence.StrategyId != observation.StrategyId || evidence.StrategyVersion != observation.StrategyVersion
            || evidence.ProviderId != observation.ProviderId || evidence.Symbol != observation.Symbol
            || evidence.Step != observation.Step || evidence.AsOfUtc != observation.AsOfUtc)
            throw new ArgumentException("Lifecycle evidence must match the current observation.", nameof(evidence));
        var previousActive = previousLifecycle?.ActiveInstance;
        if (evidence.Scope == StrategyReplayLifecycleEvidenceScope.ActiveInstance
            && (previousActive is null || evidence.InstanceId != previousActive.InstanceId))
            throw new ArgumentException("Active-instance evidence must match the previous active progression.", nameof(evidence));
        if (evidence.Scope == StrategyReplayLifecycleEvidenceScope.ActivationCandidate && previousActive is not null)
            throw new ArgumentException("Activation-candidate evidence is only valid without an active progression.", nameof(evidence));
    }

    public StrategyReplayContextObservation Observation { get; }
    public StrategyReplayProgressionSnapshot CandidateProgression { get; }
    public StrategyReplayLifecycleSnapshot? PreviousLifecycle { get; }
    public StrategyReplayLifecycleEvidenceSnapshot Evidence { get; }
    public DateTimeOffset AsOfUtc => Observation.AsOfUtc;
}
