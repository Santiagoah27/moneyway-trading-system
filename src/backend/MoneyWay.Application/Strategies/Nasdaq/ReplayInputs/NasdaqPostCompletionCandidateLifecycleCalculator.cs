using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Adapts the universal post-completion candidate to the canonical decision without materializing a next state.</summary>
public sealed class NasdaqPostCompletionCandidateLifecycleCalculator
{
    private readonly NasdaqCandidateLifecycleDecisionCalculator decisionCalculator = new();

    public NasdaqPostCompletionCandidateLifecycleResult Evaluate(NasdaqPostCompletionCandidateState candidate, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candle);
        var decision = decisionCalculator.Evaluate(candidate.CandidateSide, candidate.CandidateGeometry,
            candidate.ActivePair.ActiveExtremeGeometry, candidate.MarketCursor, candle);
        return new(candidate, decision);
    }
}
