using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-identified exact event evidence; neither branch infers a take or timestamp from OHLC.</summary>
public abstract class NasdaqHumanLiquidityTakeEvent
{
    private NasdaqHumanLiquidityTakeEvent() { }
    public abstract decimal ObservedPrice { get; }
    public abstract DateTimeOffset EffectiveAtUtc { get; }
    public abstract MarketDataProviderId ProviderId { get; }
    public abstract MarketSymbol Symbol { get; }
    internal abstract bool SameFact(NasdaqHumanLiquidityTakeEvent other);
    internal abstract string SortKey { get; }

    public sealed class Documented : NasdaqHumanLiquidityTakeEvent
    {
        public Documented(MarketDataProviderId providerId, MarketSymbol symbol, string eventId,
            decimal observedPrice, DateTimeOffset effectiveAtUtc, string sourceReference, IEnumerable<Candle>? supportingCandles = null)
        {
            ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            NasdaqStructuralLiquidityReference.ValidateProvenance(eventId);
            NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
            if (effectiveAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Exact documented market time must be UTC.", nameof(effectiveAtUtc));
            var sources = (supportingCandles ?? []).ToArray();
            if (sources.Any(c => c is null || c.ProviderId != providerId || c.Symbol != symbol))
                throw new ArgumentException("Supporting candles must match the exact event instrument/source.");
            EventId = eventId; ObservedPrice = observedPrice; EffectiveAtUtc = effectiveAtUtc; SourceReference = sourceReference;
            SupportingCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.OpenTimeUtc).ThenBy(c => c.Timeframe.ToString()).ToArray());
        }
        public override MarketDataProviderId ProviderId { get; }
        public override MarketSymbol Symbol { get; }
        public string EventId { get; }
        public override decimal ObservedPrice { get; }
        public override DateTimeOffset EffectiveAtUtc { get; }
        public string SourceReference { get; }
        public IReadOnlyList<Candle> SupportingCandles { get; }
        internal override bool SameFact(NasdaqHumanLiquidityTakeEvent other) => other is Documented d && ProviderId == d.ProviderId
            && Symbol == d.Symbol && EventId == d.EventId && ObservedPrice == d.ObservedPrice && EffectiveAtUtc == d.EffectiveAtUtc;
        internal override string SortKey => EventId;
    }

    public sealed class CanonicalPrice : NasdaqHumanLiquidityTakeEvent
    {
        public CanonicalPrice(HistoricalMarketPriceObservation observation) => Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        public HistoricalMarketPriceObservation Observation { get; }
        public override MarketDataProviderId ProviderId => Observation.ProviderId;
        public override MarketSymbol Symbol => Observation.Symbol;
        public override decimal ObservedPrice => Observation.ObservedPrice;
        public override DateTimeOffset EffectiveAtUtc => Observation.ObservedAtUtc;
        internal override bool SameFact(NasdaqHumanLiquidityTakeEvent other) => other is CanonicalPrice c && Observation == c.Observation;
        internal override string SortKey => $"{Observation.ObservationKind}:{Observation.SourceResolution}:{Observation.SourceSequence}";
    }
}
