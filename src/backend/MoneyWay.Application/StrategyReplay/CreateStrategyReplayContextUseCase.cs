using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Creates a strategy-bound observable market-data context from an already validated multi-timeframe replay frame.
/// It performs no strategy-rule evaluation.
/// </summary>
public sealed class CreateStrategyReplayContextUseCase
{
    public StrategyReplayContext Execute(StrategyDefinition strategyDefinition, MultiTimeframeReplayFrame replayFrame)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(replayFrame);
        return new StrategyReplayContext(strategyDefinition.StrategyId, strategyDefinition.Version, replayFrame);
    }

    public StrategyReplayContext Execute(StrategyDefinition strategyDefinition, MultiTimeframeReplayFrame replayFrame,
        NasdaqPreparationCompletionObservationSeries preparationObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(replayFrame);
        ArgumentNullException.ThrowIfNull(preparationObservations);
        ValidateIdentity(strategyDefinition, replayFrame.ProviderId, replayFrame.Symbol, preparationObservations);
        return new StrategyReplayContext(strategyDefinition.StrategyId, strategyDefinition.Version, replayFrame, preparationObservations.Observations);
    }

    public StrategyReplayContext ExecuteCanonical(StrategyDefinition strategyDefinition, CanonicalMultiTimeframeReplayFrame replayFrame)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(replayFrame);
        return new StrategyReplayContext(strategyDefinition.StrategyId, strategyDefinition.Version, replayFrame);
    }

    public StrategyReplayContext ExecuteCanonical(StrategyDefinition strategyDefinition, CanonicalMultiTimeframeReplayFrame replayFrame,
        NasdaqPreparationCompletionObservationSeries preparationObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(replayFrame);
        ArgumentNullException.ThrowIfNull(preparationObservations);
        ValidateIdentity(strategyDefinition, replayFrame.ProviderId, replayFrame.Symbol, preparationObservations);
        return new StrategyReplayContext(strategyDefinition.StrategyId, strategyDefinition.Version, replayFrame, preparationObservations.Observations);
    }

    private static void ValidateIdentity(StrategyDefinition definition, MarketDataProviderId providerId,
        MarketSymbol symbol, NasdaqPreparationCompletionObservationSeries observations)
    {
        if (observations.StrategyId != definition.StrategyId || observations.StrategyVersion != definition.Version
            || observations.ProviderId != providerId || observations.Symbol != symbol)
            throw new ArgumentException("Preparation observations must match the replay identity.", nameof(observations));
    }
}
