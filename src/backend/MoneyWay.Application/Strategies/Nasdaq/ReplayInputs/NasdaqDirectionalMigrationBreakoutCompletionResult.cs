using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Retains the same candle as definitive vertex source and causal confirmation event.</summary>
public sealed record NasdaqDirectionalMigrationBreakoutCompletionResult
{
    internal NasdaqDirectionalMigrationBreakoutCompletionResult(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        StructuralTurnGeometryResult candidateGeometry,
        StructuralCandidateValidationResult validatedCandidate)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        ArgumentNullException.ThrowIfNull(candidateGeometry);
        ArgumentNullException.ThrowIfNull(validatedCandidate);
        if (!validatedCandidate.IsValidated
            || validatedCandidate.CandidateSide != breakout.CandidateSide
            || !ReferenceEquals(validatedCandidate.CandidateGeometry, candidateGeometry)
            || !ReferenceEquals(validatedCandidate.BreakObservation.Candle, breakout.ValidatingCandle))
        {
            throw new ArgumentException("The result must retain same-candle geometry and confirmation.", nameof(validatedCandidate));
        }

        Breakout = breakout;
        CandidateGeometry = candidateGeometry;
        ValidatedCandidate = validatedCandidate;
    }

    public NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout { get; }
    public StructuralTurnGeometryResult CandidateGeometry { get; }
    public StructuralCandidateValidationResult ValidatedCandidate { get; }
    public Candle LastProcessedCandle => Breakout.ValidatingCandle;
}
