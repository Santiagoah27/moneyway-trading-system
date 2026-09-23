using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Preserves the source-backed evidence and validated candidate for one ordinary rebuilt breakout.</summary>
public sealed record NasdaqOrdinaryRebuiltBreakoutCompletionResult
{
    internal NasdaqOrdinaryRebuiltBreakoutCompletionResult(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        NasdaqHumanRebuiltCandidateVertexMemberResolution memberResolution,
        StructuralTurnGeometryResult candidateGeometry,
        StructuralCandidateValidationResult validatedCandidate)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        ArgumentNullException.ThrowIfNull(memberResolution);
        ArgumentNullException.ThrowIfNull(candidateGeometry);
        ArgumentNullException.ThrowIfNull(validatedCandidate);

        if (!validatedCandidate.IsValidated
            || validatedCandidate.CandidateSide != breakout.CandidateSide
            || !ReferenceEquals(validatedCandidate.CandidateGeometry, candidateGeometry)
            || !ReferenceEquals(validatedCandidate.BreakObservation.Candle, breakout.ValidatingCandle))
        {
            throw new ArgumentException("The completion result must preserve the rebuilt candidate and its validating breakout.", nameof(validatedCandidate));
        }

        Breakout = breakout;
        MemberResolution = memberResolution;
        CandidateGeometry = candidateGeometry;
        ValidatedCandidate = validatedCandidate;
    }

    public NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout { get; }
    public NasdaqHumanRebuiltCandidateVertexMemberResolution MemberResolution { get; }
    public StructuralTurnGeometryResult CandidateGeometry { get; }
    public StructuralCandidateValidationResult ValidatedCandidate { get; }
    public Candle LastProcessedCandle => Breakout.ValidatingCandle;
}
