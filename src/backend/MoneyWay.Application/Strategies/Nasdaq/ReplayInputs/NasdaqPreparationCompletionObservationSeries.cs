using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable canonical preparation input with at most one positive assertion per trading day.</summary>
public sealed class NasdaqPreparationCompletionObservationSeries
{
    public NasdaqPreparationCompletionObservationSeries(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        IEnumerable<NasdaqPreparationCompletionObservation> observations)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        if (strategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Preparation completion input must belong to MoneyWay Nasdaq.", nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        ArgumentNullException.ThrowIfNull(observations);
        var snapshot = observations.ToArray();
        if (snapshot.Any(item => item is null))
            throw new ArgumentException("Observations cannot contain null elements.", nameof(observations));
        if (snapshot.Any(item => item.StrategyId != strategyId || item.StrategyVersion != strategyVersion
            || item.ProviderId != providerId || item.Symbol != symbol))
            throw new ArgumentException("Observations must match the series identity.", nameof(observations));
        if (snapshot.GroupBy(item => item.TradingDay).Any(group => group.Count() > 1))
            throw new ArgumentException("Only one preparation completion observation per trading day is allowed.", nameof(observations));

        Observations = new ReadOnlyCollection<NasdaqPreparationCompletionObservation>(
            snapshot.OrderBy(item => item.ObservedAtUtc).ToArray());
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public IReadOnlyList<NasdaqPreparationCompletionObservation> Observations { get; }
}
