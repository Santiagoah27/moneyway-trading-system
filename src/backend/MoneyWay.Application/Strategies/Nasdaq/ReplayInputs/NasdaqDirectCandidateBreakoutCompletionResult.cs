using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Preserves one original candidate and its later non-migrating validating breakout.</summary>
public sealed record NasdaqDirectCandidateBreakoutCompletionResult
{
    internal NasdaqDirectCandidateBreakoutCompletionResult(
        NasdaqPostInvalidationCandidateState candidate,
        Candle validatingCandle,
        StructuralCandidateValidationResult validatedCandidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(validatingCandle);
        ArgumentNullException.ThrowIfNull(validatedCandidate);

        if (!validatedCandidate.IsValidated
            || validatedCandidate.CandidateSide != candidate.CandidateSide
            || !ReferenceEquals(validatedCandidate.CandidateGeometry, candidate.CandidateGeometry)
            || !ReferenceEquals(validatedCandidate.BreakObservation.Candle, validatingCandle))
        {
            throw new ArgumentException("The result must retain the existing candidate geometry and its validating breakout.", nameof(validatedCandidate));
        }

        Candidate = candidate;
        ValidatingCandle = validatingCandle;
        ValidatedCandidate = validatedCandidate;
    }

    public NasdaqPostInvalidationCandidateState Candidate { get; }
    public Candle ValidatingCandle { get; }
    public StructuralCandidateValidationResult ValidatedCandidate { get; }
    public Candle LastProcessedCandle => ValidatingCandle;
}
