using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Identity of one H4 NQ-Q-H4-008 collision requiring a human StructuralPrice answer.</summary>
public sealed record NasdaqHumanCollisionStructuralPriceEpisode
{
    public NasdaqHumanCollisionStructuralPriceEpisode(
        NasdaqHumanOriginVertexEpisode reconstructionEpisode,
        DateTimeOffset collisionCandleOpenTimeUtc,
        StructuralCandidateExtremeSide candidateSide)
    {
        ReconstructionEpisode = reconstructionEpisode ?? throw new ArgumentNullException(nameof(reconstructionEpisode));
        if (collisionCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Collision candle timestamp must be UTC.", nameof(collisionCandleOpenTimeUtc));
        if (!Enum.IsDefined(candidateSide))
            throw new ArgumentOutOfRangeException(nameof(candidateSide), "The candidate side is not supported.");

        CollisionCandleOpenTimeUtc = collisionCandleOpenTimeUtc;
        CandidateSide = candidateSide;
    }

    public NasdaqHumanOriginVertexEpisode ReconstructionEpisode { get; }
    public DateTimeOffset CollisionCandleOpenTimeUtc { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }

    public bool Matches(NasdaqHumanCollisionStructuralPriceObservation observation) =>
        observation is not null
        && observation.Episode == ReconstructionEpisode
        && observation.CollisionCandleOpenTimeUtc == CollisionCandleOpenTimeUtc
        && observation.CandidateSide == CandidateSide;
}
