using System.Collections.ObjectModel;

namespace MoneyWay.Application.MarketData.Replay;

/// <summary>
/// Groups observations sharing one source timestamp. Collection order is causal only when
/// <see cref="HasAuthoritativeOrder"/> is true; otherwise the observations are an explicitly unordered source group.
/// </summary>
public sealed class HistoricalMarketPriceObservationGroup
{
    internal HistoricalMarketPriceObservationGroup(IEnumerable<HistoricalMarketPriceObservation> observations)
    {
        var snapshot = observations.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("An observation group cannot be empty.", nameof(observations));
        ObservedAtUtc = snapshot[0].ObservedAtUtc;
        if (snapshot.Any(item => item.ObservedAtUtc != ObservedAtUtc))
            throw new ArgumentException("Grouped observations must share one timestamp.", nameof(observations));

        var hasCompleteSourceSequence = snapshot.All(item => item.SourceSequence.HasValue);
        HasAuthoritativeOrder = snapshot.Length == 1 || hasCompleteSourceSequence;
        if (snapshot.Length > 1 && hasCompleteSourceSequence)
        {
            var sequences = snapshot.Select(item => item.SourceSequence!.Value).ToArray();
            if (sequences.Distinct().Count() != sequences.Length
                || sequences.Where((value, index) => index > 0 && value <= sequences[index - 1]).Any())
                throw new ArgumentException("Authoritatively ordered observations require unique ascending source sequences.", nameof(observations));
        }

        Observations = new ReadOnlyCollection<HistoricalMarketPriceObservation>(snapshot);
    }

    public DateTimeOffset ObservedAtUtc { get; }
    public bool HasAuthoritativeOrder { get; }
    public IReadOnlyList<HistoricalMarketPriceObservation> Observations { get; }
}
