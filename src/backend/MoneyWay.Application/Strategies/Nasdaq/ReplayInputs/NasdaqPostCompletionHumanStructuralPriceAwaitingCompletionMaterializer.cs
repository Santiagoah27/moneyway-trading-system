namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes only the already-consumed 008 waiting boundary, without evidence selection or recalculation.</summary>
public sealed class NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer
{
    public NasdaqPostCompletionBreakoutAwaitingCompletionState Materialize(NasdaqPostCompletionCandidateLifecycleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Decision is not NasdaqCandidateLifecycleDecision.CollisionBreakout collision
            || collision.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired
            || collision.CandidateResolution is not NasdaqCollisionCandidateResolution.HumanStructuralPriceRequired resolution)
            throw new ArgumentException("Only a human-StructuralPrice-required 008 collision can enter this waiting boundary.", nameof(result));
        var source = result.SourceState;
        if (collision.CandidateSide != source.CandidateSide
            || !ReferenceEquals(collision.CandidateGeometry, source.CandidateGeometry)
            || !ReferenceEquals(collision.FrozenTerminal, source.ActivePair.ActiveExtremeGeometry)
            || !ReferenceEquals(collision.PreviousCursor, source.MarketCursor))
            throw new ArgumentException("The collision must retain the exact source candidate and frozen terminal.", nameof(result));
        return new(result, collision, resolution);
    }
}
