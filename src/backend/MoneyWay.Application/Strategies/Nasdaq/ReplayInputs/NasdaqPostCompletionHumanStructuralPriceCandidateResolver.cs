using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves definitive 008 candidate facts from already-selected evidence without reselection or market consumption.</summary>
public sealed class NasdaqPostCompletionHumanStructuralPriceCandidateResolver
{
    private readonly NasdaqHumanStructuralPriceCandidateGeometryCalculator geometryCalculator = new();

    public NasdaqPostCompletionResolvedHumanStructuralPriceCandidate Resolve(
        NasdaqPostCompletionBreakoutAwaitingCompletionState awaitingState,
        NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady selection)
    {
        ArgumentNullException.ThrowIfNull(awaitingState);
        ArgumentNullException.ThrowIfNull(selection);
        if (!ReferenceEquals(selection.AwaitingState, awaitingState) || selection.Context != awaitingState.EvidenceContext
            || selection.SupportingObservations.Count == 0
            || selection.SupportingObservations.Any(item => item.Context != awaitingState.EvidenceContext
                || item.StructuralPrice != selection.StructuralPrice))
            throw new ArgumentException("Unique evidence must retain the exact awaiting state and compatible price assertions for its collision.", nameof(selection));
        var geometry = geometryCalculator.Evaluate(awaitingState.Collision.CandidateSide,
            awaitingState.EffectiveProtectionAnchor, selection.StructuralPrice);
        var validation = new StructuralCandidateValidationResult(awaitingState.Collision.CandidateSide, geometry, awaitingState.Breakout);
        return new(awaitingState, selection, validation);
    }
}
