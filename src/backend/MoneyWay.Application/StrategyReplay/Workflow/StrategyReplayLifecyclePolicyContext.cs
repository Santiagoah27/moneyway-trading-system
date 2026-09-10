namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Supplies immutable current-frame facts to a strategy-owned lifecycle policy.</summary>
public sealed class StrategyReplayLifecyclePolicyContext
{
    public StrategyReplayLifecyclePolicyContext(
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionSnapshot candidateProgression,
        StrategyReplayLifecycleSnapshot? previousLifecycle)
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
    }

    public StrategyReplayContextObservation Observation { get; }
    public StrategyReplayProgressionSnapshot CandidateProgression { get; }
    public StrategyReplayLifecycleSnapshot? PreviousLifecycle { get; }
    public DateTimeOffset AsOfUtc => Observation.AsOfUtc;
}
