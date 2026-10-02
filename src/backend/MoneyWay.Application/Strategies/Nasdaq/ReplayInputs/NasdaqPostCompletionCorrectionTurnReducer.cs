using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Materializes canonical correction facts without evaluating or consuming any candle.</summary>
public sealed class NasdaqPostCompletionCorrectionTurnReducer
{
    public NasdaqPostCompletionCorrectionTurnReductionResult Reduce(NasdaqPostCompletionCorrectionTurnResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        switch (result)
        {
            case NasdaqPostCompletionCorrectionTurnResult.ContinuingCorrection continuing:
                Validate(continuing, continuing.TurnResult, true);
                return new NasdaqPostCompletionCorrectionTurnReductionResult.ActiveCorrection(new(continuing));
            case NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional candidate:
                Validate(candidate, candidate.TurnResult, false);
                return new NasdaqPostCompletionCorrectionTurnReductionResult.Candidate(new(candidate));
            default:
                throw new InvalidOperationException("The correction turn result is not supported.");
        }
    }

    private static void Validate(NasdaqPostCompletionCorrectionTurnResult result, CorrectionCandidateTurnResult facts, bool continuing)
    {
        var source = result.SourceState;
        if (facts.CandidateSide != source.ActivePair.ProtectedTurn.CandidateSide
            || facts.Geometry.Side != source.ActivePair.ProtectedTurnGeometry.Side
            || !ReferenceEquals(facts.MarketCursor, result.MarketCursor))
            throw new ArgumentException("The canonical correction facts must match the source side and consumed candle.", nameof(result));
        var prior = source.CorrectionTurnCandles;
        var members = facts.CorrectionTurnCandles;
        if (members.Count != prior.Count + (continuing ? 1 : 0)
            || prior.Where((member, index) => !ReferenceEquals(member, members[index])).Any()
            || (continuing && !ReferenceEquals(members[^1], result.MarketCursor)))
            throw new ArgumentException("The canonical correction membership must preserve prior members and single consumption.", nameof(result));
    }
}
