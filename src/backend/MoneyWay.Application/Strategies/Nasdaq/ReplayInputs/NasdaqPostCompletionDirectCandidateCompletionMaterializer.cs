namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes an already-consumed direct candidate completion without recalculation or next-cycle handoff.</summary>
public sealed class NasdaqPostCompletionDirectCandidateCompletionMaterializer
{
    public NasdaqPostCompletionStructuralCompletion Materialize(NasdaqPostCompletionCandidateLifecycleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Decision is not NasdaqCandidateLifecycleDecision.DirectCompleted direct)
            throw new ArgumentException("Only DirectCompleted can be materialized by this component.", nameof(result));
        var source = result.SourceState;
        if (direct.CandidateSide != source.CandidateSide
            || !ReferenceEquals(direct.CandidateGeometry, source.CandidateGeometry)
            || !ReferenceEquals(direct.FrozenTerminal, source.ActivePair.ActiveExtremeGeometry)
            || !ReferenceEquals(direct.PreviousCursor, source.MarketCursor)
            || !ReferenceEquals(direct.Validation.CandidateGeometry, source.CandidateGeometry))
            throw new ArgumentException("The direct completion must retain the exact source candidate and frozen terminal.", nameof(result));
        return new(result, direct.Validation);
    }
}
