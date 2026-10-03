namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Unifies either consumed rebuild breakout without another market input or downstream evidence processing.</summary>
public sealed class NasdaqPostCompletionRebuiltCandidateBreakoutMaterializer
{
    public NasdaqPostCompletionRebuiltCandidateBreakout Materialize(NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(new NasdaqPostCompletionRebuildBreakoutSource.Pending(result));
    }

    public NasdaqPostCompletionRebuiltCandidateBreakout Materialize(NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(new NasdaqPostCompletionRebuildBreakoutSource.RebuiltTracking(result));
    }
}
