using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Frozen market provenance required to resolve one ADR 0009 rebuilt-candidate membership.</summary>
public sealed class NasdaqHumanRebuiltCandidateVertexResolutionContext
{
    private NasdaqHumanRebuiltCandidateVertexResolutionContext(
        NasdaqHumanRebuiltCandidateVertexEpisode episode,
        Candle migrationCandle,
        decimal knownProtectionAnchor)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(migrationCandle);
        if (migrationCandle.ProviderId != episode.ProviderId || migrationCandle.Symbol != episode.Symbol
            || migrationCandle.Timeframe != episode.Timeframe
            || migrationCandle.OpenTimeUtc != episode.MigrationCandleOpenTimeUtc)
        {
            throw new ArgumentException("The migration candle must match the rebuilt-candidate episode.", nameof(migrationCandle));
        }

        var expectedAnchor = episode.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? migrationCandle.Low
            : migrationCandle.High;
        if (knownProtectionAnchor != expectedAnchor)
            throw new ArgumentException("The known protection anchor must be the effective migration wick.", nameof(knownProtectionAnchor));

        Episode = episode;
        MigrationCandle = migrationCandle;
        KnownProtectionAnchor = knownProtectionAnchor;
    }

    public NasdaqHumanRebuiltCandidateVertexEpisode Episode { get; }
    public StructuralCandidateExtremeSide CandidateSide => Episode.CandidateSide;
    public Candle MigrationCandle { get; }
    public decimal KnownProtectionAnchor { get; }

    public static NasdaqHumanRebuiltCandidateVertexResolutionContext From(
        NasdaqPostInvalidationCandidateRebuildPendingState pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        return new(
            new NasdaqHumanRebuiltCandidateVertexEpisode(
                pending.Episode.StrategyId,
                pending.Episode.StrategyVersion,
                pending.Episode.ProviderId,
                pending.Episode.Symbol,
                pending.Episode.InvalidatingCandleOpenTimeUtc,
                pending.MigrationCandle.OpenTimeUtc,
                pending.CandidateSide),
            pending.MigrationCandle,
            pending.KnownProtectionAnchor);
    }

    public static NasdaqHumanRebuiltCandidateVertexResolutionContext From(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout)
    {
        ArgumentNullException.ThrowIfNull(breakout);
        if (breakout.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None
            || breakout.HasStrictMigration
            || breakout.Origin is not NasdaqPostInvalidationBreakoutOrigin.Rebuild rebuild)
        {
            throw new ArgumentException("Only an ordinary Rebuild-origin breakout has rebuilt-membership provenance.", nameof(breakout));
        }

        return new(
            new NasdaqHumanRebuiltCandidateVertexEpisode(
                breakout.Episode.StrategyId,
                breakout.Episode.StrategyVersion,
                breakout.Episode.ProviderId,
                breakout.Episode.Symbol,
                breakout.Episode.InvalidatingCandleOpenTimeUtc,
                rebuild.PriorMigrationCandle.OpenTimeUtc,
                breakout.CandidateSide),
            rebuild.PriorMigrationCandle,
            breakout.PreviousProtectionAnchor);
    }
}
