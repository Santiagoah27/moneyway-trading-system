using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Retains the human price evidence separately from the deterministic wick and breakout evidence.</summary>
public sealed record NasdaqHumanStructuralPriceBreakoutCompletionResult
{
    internal NasdaqHumanStructuralPriceBreakoutCompletionResult(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        NasdaqHumanCollisionStructuralPriceObservationSelection humanPriceSelection,
        StructuralTurnGeometryResult candidateGeometry,
        StructuralCandidateValidationResult validatedCandidate)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        ArgumentNullException.ThrowIfNull(humanPriceSelection);
        ArgumentNullException.ThrowIfNull(candidateGeometry);
        ArgumentNullException.ThrowIfNull(validatedCandidate);
        if (humanPriceSelection.Kind != NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Unique
            || humanPriceSelection.StructuralPrice != candidateGeometry.StructuralPrice
            || candidateGeometry.ProtectionAnchor != breakout.EffectiveProtectionAnchor
            || !validatedCandidate.IsValidated
            || validatedCandidate.CandidateSide != breakout.CandidateSide
            || !ReferenceEquals(validatedCandidate.CandidateGeometry, candidateGeometry)
            || !ReferenceEquals(validatedCandidate.BreakObservation.Candle, breakout.ValidatingCandle))
        {
            throw new ArgumentException("The result must retain the human price, migrated anchor, and validating candle.", nameof(validatedCandidate));
        }

        Breakout = breakout;
        HumanPriceSelection = humanPriceSelection;
        CandidateGeometry = candidateGeometry;
        ValidatedCandidate = validatedCandidate;
    }

    public NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout { get; }
    public NasdaqHumanCollisionStructuralPriceObservationSelection HumanPriceSelection { get; }
    public StructuralTurnGeometryResult CandidateGeometry { get; }
    public StructuralCandidateValidationResult ValidatedCandidate { get; }
    public Candle LastProcessedCandle => Breakout.ValidatingCandle;
}
