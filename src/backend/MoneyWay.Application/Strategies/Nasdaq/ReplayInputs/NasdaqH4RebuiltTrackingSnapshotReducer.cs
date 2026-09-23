using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces exactly one RebuiltTracking snapshot through one closed H4 candle.</summary>
public sealed class NasdaqH4RebuiltTrackingSnapshotReducer
{
    private readonly NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator transitionCalculator = new();

    public NasdaqH4ReconstructionSnapshot Reduce(NasdaqH4ReconstructionSnapshot snapshot, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candle);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.RebuiltTracking tracking)
            throw new ArgumentException("Only a RebuiltTracking snapshot can be reduced by this reducer.", nameof(snapshot));

        var result = transitionCalculator.Evaluate(tracking.State, candle);
        return result.Kind switch
        {
            NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingContinues or
                NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingRestarted =>
                new NasdaqH4ReconstructionSnapshot.RebuiltTracking(
                    result.Tracking ?? throw new InvalidOperationException("The tracking transition result requires a tracking state.")),
            NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.PendingReset =>
                new NasdaqH4ReconstructionSnapshot.RebuildPending(
                    result.Pending ?? throw new InvalidOperationException("The reset transition result requires a pending state.")),
            NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.BreakoutDetected =>
                new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(
                    result.Breakout ?? throw new InvalidOperationException("The breakout transition result requires a breakout state.")),
            _ => throw new InvalidOperationException("The tracking transition result is not supported."),
        };
    }
}
