using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Identity of one H4 post-invalidation origin-vertex reconstruction episode.</summary>
public sealed record NasdaqHumanOriginVertexEpisode
{
    public NasdaqHumanOriginVertexEpisode(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        DateTimeOffset invalidatingCandleOpenTimeUtc)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (invalidatingCandleOpenTimeUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalidating candle timestamp must be UTC.", nameof(invalidatingCandleOpenTimeUtc));
        InvalidatingCandleOpenTimeUtc = invalidatingCandleOpenTimeUtc;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe => NasdaqHumanOriginVertexObservation.H4;
    public DateTimeOffset InvalidatingCandleOpenTimeUtc { get; }

    public bool Matches(NasdaqHumanOriginVertexObservation observation) =>
        observation.StrategyId == StrategyId
        && observation.StrategyVersion == StrategyVersion
        && observation.ProviderId == ProviderId
        && observation.Symbol == Symbol
        && observation.Timeframe == Timeframe
        && observation.InvalidatingCandleOpenTimeUtc == InvalidatingCandleOpenTimeUtc;
}
