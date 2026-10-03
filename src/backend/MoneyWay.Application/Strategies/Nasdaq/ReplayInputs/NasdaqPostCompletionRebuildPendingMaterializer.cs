namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes only the canonical migration-only pending boundary, without recalculation or additional market consumption.</summary>
public sealed class NasdaqPostCompletionRebuildPendingMaterializer
{
    public NasdaqPostCompletionRebuildPendingState Materialize(NasdaqPostCompletionCandidateLifecycleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Decision is not NasdaqCandidateLifecycleDecision.RebuildPending pending)
            throw new ArgumentException("Only RebuildPending can enter this waiting boundary.", nameof(result));
        var source = result.SourceState;
        if (pending.CandidateSide != source.CandidateSide
            || !ReferenceEquals(pending.CandidateGeometry, source.CandidateGeometry)
            || !ReferenceEquals(pending.FrozenTerminal, source.ActivePair.ActiveExtremeGeometry)
            || !ReferenceEquals(pending.PreviousCursor, source.MarketCursor)
            || !pending.Migration.WasReplaced || pending.Breakout.IsConfirmed)
            throw new ArgumentException("The pending decision must retain its canonical source and migration-only facts.", nameof(result));
        return new(result, pending);
    }
}
