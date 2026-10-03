namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes the already-consumed canonical 007 validation without recalculation or next-cycle handoff.</summary>
public sealed class NasdaqPostCompletionDirectionalCollisionCompletionMaterializer
{
    public NasdaqPostCompletionStructuralCompletion Materialize(NasdaqPostCompletionCandidateLifecycleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Decision is not NasdaqCandidateLifecycleDecision.CollisionBreakout collision
            || collision.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
            || collision.CandidateResolution is not NasdaqCollisionCandidateResolution.Directional directional)
            throw new ArgumentException("Only a directional 007 collision can be materialized by this component.", nameof(result));
        var source = result.SourceState;
        if (collision.CandidateSide != source.CandidateSide
            || !ReferenceEquals(collision.CandidateGeometry, source.CandidateGeometry)
            || !ReferenceEquals(collision.FrozenTerminal, source.ActivePair.ActiveExtremeGeometry)
            || !ReferenceEquals(collision.PreviousCursor, source.MarketCursor)
            || !ReferenceEquals(directional.Member, result.MarketCursor))
            throw new ArgumentException("The collision must retain the exact source candidate, frozen terminal and consumed candle.", nameof(result));
        return new(result, directional.Validation);
    }
}
