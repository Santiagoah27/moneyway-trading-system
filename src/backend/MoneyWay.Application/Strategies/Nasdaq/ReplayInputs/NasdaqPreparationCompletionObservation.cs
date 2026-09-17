using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Positive source-backed assertion that both mandatory preparation steps completed for one Bogota trading day.</summary>
public sealed record NasdaqPreparationCompletionObservation : IStrategyReplayInputObservation
{
    public NasdaqPreparationCompletionObservation(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        DateOnly tradingDay,
        DateTimeOffset observedAtUtc,
        string sourceReference)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        if (strategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Preparation completion input must belong to MoneyWay Nasdaq.", nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (observedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Observation timestamp must be UTC.", nameof(observedAtUtc));
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));

        TradingDay = tradingDay;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public DateOnly TradingDay { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
}
