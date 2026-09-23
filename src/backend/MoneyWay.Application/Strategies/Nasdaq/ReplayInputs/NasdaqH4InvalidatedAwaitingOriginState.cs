using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Verified invalidation retained while the origin vertex is not uniquely usable.</summary>
public sealed record NasdaqH4InvalidatedAwaitingOriginState
{
    public NasdaqH4InvalidatedAwaitingOriginState(
        NasdaqHumanOriginVertexEpisode episode,
        StructuralCandidateTurnBoundaryResult invalidation)
    {
        Episode = episode ?? throw new ArgumentNullException(nameof(episode));
        Invalidation = invalidation ?? throw new ArgumentNullException(nameof(invalidation));
        var candle = invalidation.CurrentCandle;
        if (invalidation.Kind != StructuralCandidateTurnBoundaryKind.StructureInvalidated
            || candle.ProviderId != episode.ProviderId
            || candle.Symbol != episode.Symbol
            || candle.Timeframe != episode.Timeframe
            || candle.OpenTimeUtc != episode.InvalidatingCandleOpenTimeUtc)
            throw new ArgumentException("The verified H4 invalidation must match its reconstruction episode.", nameof(invalidation));
    }

    public NasdaqHumanOriginVertexEpisode Episode { get; }
    public StructuralCandidateTurnBoundaryResult Invalidation { get; }
    public Candle InvalidatingCandle => Invalidation.CurrentCandle;
    public Candle LastProcessedCandle => InvalidatingCandle;
}
