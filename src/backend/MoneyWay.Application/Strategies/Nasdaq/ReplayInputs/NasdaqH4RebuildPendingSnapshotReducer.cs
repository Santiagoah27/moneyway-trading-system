using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces exactly one RebuildPending snapshot through one closed H4 candle.</summary>
public sealed class NasdaqH4RebuildPendingSnapshotReducer
{
    private readonly NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator transitionCalculator = new();

    public NasdaqH4ReconstructionSnapshot Reduce(NasdaqH4ReconstructionSnapshot snapshot, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candle);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.RebuildPending pending)
            throw new ArgumentException("Only a RebuildPending snapshot can be reduced by this reducer.", nameof(snapshot));

        var result = transitionCalculator.Evaluate(pending.State, candle);
        return result.Kind switch
        {
            NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.PendingContinues or
                NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.PendingReset =>
                new NasdaqH4ReconstructionSnapshot.RebuildPending(
                    result.Pending ?? throw new InvalidOperationException("The pending transition result requires a pending state.")),
            NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.TrackingStarted =>
                new NasdaqH4ReconstructionSnapshot.RebuiltTracking(
                    result.Tracking ?? throw new InvalidOperationException("The tracking transition result requires a tracking state.")),
            NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.BreakoutDetected =>
                new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(
                    result.Breakout ?? throw new InvalidOperationException("The breakout transition result requires a breakout state.")),
            _ => throw new InvalidOperationException("The pending transition result is not supported."),
        };
    }
}
