using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Deterministic identity of one post-completion migration-only rebuild, distinct from reconstruction identity.</summary>
public sealed record NasdaqPostCompletionRebuildContext
{
    internal NasdaqPostCompletionRebuildContext(NasdaqPostCompletionEpisode episode,
        DateTimeOffset migrationCandleOpenTimeUtc, StructuralCandidateExtremeSide candidateSide)
    {
        Episode = episode;
        MigrationCandleOpenTimeUtc = migrationCandleOpenTimeUtc;
        CandidateSide = candidateSide;
    }

    public NasdaqPostCompletionEpisode Episode { get; }
    public DateTimeOffset MigrationCandleOpenTimeUtc { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
}
