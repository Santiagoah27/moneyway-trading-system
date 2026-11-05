using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-documented initiating sweep peak in an explicit native price domain.
/// This is an exact retained level/event reference, not a candle selector or calculated maximum.</summary>
public sealed class NasdaqHumanWickfillScenario2StopLossReference
{
    public NasdaqHumanWickfillScenario2StopLossReference(MarketDataProviderId providerId, MarketSymbol symbol,
        string referenceId, decimal price, DateTimeOffset observedAtUtc, string sourceReference,
        IEnumerable<Candle>? supportingCandles = null)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        NasdaqStructuralLiquidityReference.ValidateProvenance(referenceId);
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (observedAtUtc == default || observedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Authentic protective-reference availability must be specified in UTC.", nameof(observedAtUtc));
        var sources = (supportingCandles ?? []).ToArray();
        if (sources.Any(c => c is null || c.ProviderId != providerId || c.Symbol != symbol || c.CloseTimeUtc > observedAtUtc)
            || sources.Select(c => (c.Timeframe, c.OpenTimeUtc)).Distinct().Count() != sources.Length)
            throw new ArgumentException("Optional source candles must be distinct, native and closed by reference availability.", nameof(supportingCandles));
        ReferenceId = referenceId;
        Price = price;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
        SupportingCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.Timeframe.Unit)
            .ThenBy(c => c.Timeframe.Amount).ThenBy(c => c.OpenTimeUtc).ToArray());
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public string ReferenceId { get; }
    public decimal Price { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public IReadOnlyList<Candle> SupportingCandles { get; }

    internal bool SameFact(NasdaqHumanWickfillScenario2StopLossReference other) =>
        ProviderId == other.ProviderId && Symbol == other.Symbol && ReferenceId == other.ReferenceId && Price == other.Price
        && SupportingCandles.Count == other.SupportingCandles.Count
        && SupportingCandles.Zip(other.SupportingCandles).All(p => NasdaqStructuralLiquidityReference.SameCandle(p.First, p.Second));
}
