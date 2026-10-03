using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects exact human price evidence visible at the replay AsOf without consuming market data.</summary>
public sealed class NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelector
{
    public NasdaqHumanPostCompletionCollisionStructuralPriceSelection Select(
        NasdaqPostCompletionBreakoutAwaitingCompletionState awaitingState, StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(awaitingState);
        ArgumentNullException.ThrowIfNull(context);
        var episode = awaitingState.Episode.PreviousCompletedEpisode;
        if (context.StrategyId != episode.StrategyId || context.StrategyVersion != episode.StrategyVersion
            || context.ProviderId != episode.ProviderId || context.Symbol != episode.Symbol
            || context.AsOfUtc < awaitingState.CollisionCandle.CloseTimeUtc)
            throw new ArgumentException("Replay context must match the closed collision and its exact strategy/market identity.", nameof(context));
        var (observations, prices) = StructuralPriceEvidenceSelector.Select(context.InputObservations
            .OfType<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>()
            .Where(item => item.Context == awaitingState.EvidenceContext), item => item.StructuralPrice, item => item.SourceReference);
        return prices.Count switch
        {
            0 => new NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Missing(awaitingState, context.AsOfUtc),
            1 => new NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady(awaitingState, context.AsOfUtc, observations, prices[0]),
            _ => new NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Conflict(awaitingState, context.AsOfUtc, observations, prices),
        };
    }
}
