using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces exactly one Candidate snapshot through one closed H4 candle.</summary>
public sealed class NasdaqH4CandidateSnapshotReducer
{
    private readonly NasdaqPostInvalidationCandidateTransitionCalculator transitionCalculator = new();

    public NasdaqH4ReconstructionSnapshot Reduce(NasdaqH4ReconstructionSnapshot snapshot, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candle);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.Candidate candidate)
            throw new ArgumentException("Only a Candidate snapshot can be reduced by this reducer.", nameof(snapshot));

        return transitionCalculator.Evaluate(candidate.State, candle) switch
        {
            NasdaqPostInvalidationCandidateTransitionResult.CandidateContinues result =>
                new NasdaqH4ReconstructionSnapshot.Candidate(result.State),
            NasdaqPostInvalidationCandidateTransitionResult.DirectCompleted result =>
                new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(result.Result),
            NasdaqPostInvalidationCandidateTransitionResult.RebuildPending result =>
                new NasdaqH4ReconstructionSnapshot.RebuildPending(result.State),
            NasdaqPostInvalidationCandidateTransitionResult.RebuiltTracking result =>
                new NasdaqH4ReconstructionSnapshot.RebuiltTracking(result.State),
            NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout result =>
                new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(result.State),
            _ => throw new InvalidOperationException("The candidate transition result is not supported."),
        };
    }
}
