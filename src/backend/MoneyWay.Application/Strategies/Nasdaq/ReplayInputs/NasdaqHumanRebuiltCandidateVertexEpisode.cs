using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Identity of one H4 post-migration rebuilt-candidate membership episode.</summary>
public sealed record NasdaqHumanRebuiltCandidateVertexEpisode
{
    public NasdaqHumanRebuiltCandidateVertexEpisode(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        DateTimeOffset invalidatingCandleOpenTimeUtc,
        DateTimeOffset migrationCandleOpenTimeUtc,
        StructuralCandidateExtremeSide candidateSide)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (invalidatingCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalidating candle timestamp must be UTC.", nameof(invalidatingCandleOpenTimeUtc));
        if (migrationCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Migration candle timestamp must be UTC.", nameof(migrationCandleOpenTimeUtc));
        if (!Enum.IsDefined(candidateSide))
            throw new ArgumentOutOfRangeException(nameof(candidateSide), "The candidate side is not supported.");

        InvalidatingCandleOpenTimeUtc = invalidatingCandleOpenTimeUtc;
        MigrationCandleOpenTimeUtc = migrationCandleOpenTimeUtc;
        CandidateSide = candidateSide;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe => NasdaqHumanRebuiltCandidateVertexObservation.H4;
    public DateTimeOffset InvalidatingCandleOpenTimeUtc { get; }
    public DateTimeOffset MigrationCandleOpenTimeUtc { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }

    public bool Matches(NasdaqHumanRebuiltCandidateVertexObservation observation) =>
        observation.StrategyId == StrategyId
        && observation.StrategyVersion == StrategyVersion
        && observation.ProviderId == ProviderId
        && observation.Symbol == Symbol
        && observation.Timeframe == Timeframe
        && observation.InvalidatingCandleOpenTimeUtc == InvalidatingCandleOpenTimeUtc
        && observation.MigrationCandleOpenTimeUtc == MigrationCandleOpenTimeUtc
        && observation.CandidateSide == CandidateSide;
}
