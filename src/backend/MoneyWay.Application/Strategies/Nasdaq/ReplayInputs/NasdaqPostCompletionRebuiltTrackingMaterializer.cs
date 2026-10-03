namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes the established first-turn event without recalculation or additional market consumption.</summary>
public sealed class NasdaqPostCompletionRebuiltTrackingMaterializer
{
    public NasdaqPostCompletionRebuiltTrackingState Materialize(NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return new(transition);
    }
}
