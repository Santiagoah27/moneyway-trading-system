using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Represents the completed observation record of a historical replay.
/// It is an after-the-fact artifact and must not be supplied to per-frame strategy evaluators during replay.
/// </summary>
public sealed class BacktestRun
{
    public BacktestRun(MarketDataProviderId providerId, MarketSymbol symbol, Timeframe timeframe, IEnumerable<BacktestObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(timeframe);
        ArgumentNullException.ThrowIfNull(observations);

        var snapshot = observations.ToArray();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var observation = snapshot[index]
                ?? throw new ArgumentException("Observations cannot contain null elements.", nameof(observations));
            if (observation.CurrentCandle.ProviderId != providerId
                || observation.CurrentCandle.Symbol != symbol
                || observation.CurrentCandle.Timeframe != timeframe)
            {
                throw new ArgumentException("Every observation must match the run metadata.", nameof(observations));
            }

            if (observation.Step != index + 1)
            {
                throw new ArgumentException("Observation steps must be consecutive from one.", nameof(observations));
            }

            if (index > 0 && observation.AsOfUtc <= snapshot[index - 1].AsOfUtc)
            {
                throw new ArgumentException("Observation timestamps must be strictly increasing.", nameof(observations));
            }
        }

        ProviderId = providerId;
        Symbol = symbol;
        Timeframe = timeframe;
        Observations = new ReadOnlyCollection<BacktestObservation>(snapshot);
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public Timeframe Timeframe { get; }
    public IReadOnlyList<BacktestObservation> Observations { get; }
    public int ObservationCount => Observations.Count;
    public DateTimeOffset? FirstAsOfUtc => ObservationCount == 0 ? null : Observations[0].AsOfUtc;
    public DateTimeOffset? LastAsOfUtc => ObservationCount == 0 ? null : Observations[^1].AsOfUtc;
}
