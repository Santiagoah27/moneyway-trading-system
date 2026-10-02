namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes only an already-consumed CandidateContinues decision without re-evaluating the candle.</summary>
public sealed class NasdaqPostCompletionCandidateContinuationReducer
{
    public NasdaqPostCompletionCandidateState Reduce(NasdaqPostCompletionCandidateLifecycleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Decision is not NasdaqCandidateLifecycleDecision.CandidateContinues continuation)
            throw new ArgumentException("Only CandidateContinues can be materialized by this reducer.", nameof(result));
        var source = result.SourceState;
        if (continuation.CandidateSide != source.CandidateSide
            || !ReferenceEquals(continuation.CandidateGeometry, source.CandidateGeometry)
            || !ReferenceEquals(continuation.FrozenTerminal, source.ActivePair.ActiveExtremeGeometry)
            || !ReferenceEquals(continuation.PreviousCursor, source.MarketCursor))
            throw new ArgumentException("The continuation decision must retain the exact source candidate facts and cursor.", nameof(result));
        return new(result);
    }
}
