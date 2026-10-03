using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Deterministic evidence key for a post-completion collision, distinct from reconstruction identity.</summary>
public sealed record NasdaqPostCompletionCollisionStructuralPriceContext
{
    internal NasdaqPostCompletionCollisionStructuralPriceContext(NasdaqPostCompletionEpisode episode,
        DateTimeOffset collisionCandleOpenTimeUtc, StructuralCandidateExtremeSide candidateSide)
    {
        Episode = episode;
        CollisionCandleOpenTimeUtc = collisionCandleOpenTimeUtc;
        CandidateSide = candidateSide;
    }

    public NasdaqPostCompletionEpisode Episode { get; }
    public DateTimeOffset CollisionCandleOpenTimeUtc { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
}
