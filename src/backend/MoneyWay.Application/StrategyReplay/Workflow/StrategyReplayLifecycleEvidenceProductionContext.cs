namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Supplies one producer with immutable inputs from the current canonical replay step.</summary>
public sealed class StrategyReplayLifecycleEvidenceProductionContext
{
    public StrategyReplayLifecycleEvidenceProductionContext(
        StrategyReplayContext replayContext,
        StrategyReplayContextObservation observation,
        StrategyReplayProgressionSnapshot candidateProgression,
        StrategyReplayLifecycleSnapshot? previousLifecycle)
    {
        ReplayContext = replayContext ?? throw new ArgumentNullException(nameof(replayContext));
        Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        CandidateProgression = candidateProgression ?? throw new ArgumentNullException(nameof(candidateProgression));
        PreviousLifecycle = previousLifecycle;
        if (replayContext.StrategyId != observation.StrategyId || replayContext.StrategyVersion != observation.StrategyVersion
            || replayContext.ProviderId != observation.ProviderId || replayContext.Symbol != observation.Symbol
            || replayContext.Step != observation.Step || replayContext.AsOfUtc != observation.AsOfUtc
            || candidateProgression.StrategyId != observation.StrategyId || candidateProgression.StrategyVersion != observation.StrategyVersion
            || candidateProgression.ProviderId != observation.ProviderId || candidateProgression.Symbol != observation.Symbol
            || candidateProgression.Step != observation.Step || candidateProgression.AsOfUtc != observation.AsOfUtc)
            throw new ArgumentException("Evidence production inputs must describe the same replay step.");
    }

    public StrategyReplayContext ReplayContext { get; }
    public StrategyReplayContextObservation Observation { get; }
    public StrategyReplayProgressionSnapshot CandidateProgression { get; }
    public StrategyReplayLifecycleSnapshot? PreviousLifecycle { get; }
    public StrategyReplayLifecycleEvidenceSnapshot? PreviousActiveInstanceEvidence => PreviousLifecycle?.ActiveInstance?.Evidence;
    public DateTimeOffset AsOfUtc => ReplayContext.AsOfUtc;
}
