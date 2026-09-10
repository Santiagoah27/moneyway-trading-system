using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.PriceLevels;

/// <summary>
/// Reports whether evidence at one canonical replay boundary establishes an absolute price-level touch.
/// It does not represent an order fill, strategy result, lifecycle transition, or trading instruction.
/// </summary>
public sealed record PriceLevelTouchResult
{
    internal PriceLevelTouchResult(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        decimal targetPrice,
        PriceLevelDirection direction,
        bool isTouched,
        PriceLevelTouchEvidenceKind? evidenceKind = null,
        ReplayTemporalEvidenceWindow? evidenceWindow = null,
        Timeframe? candleTimeframe = null,
        bool? hasAuthoritativeSourceOrder = null,
        long? sourceSequence = null)
    {
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (evidenceKind is not null && !Enum.IsDefined(evidenceKind.Value))
            throw new ArgumentOutOfRangeException(nameof(evidenceKind));
        if (isTouched && (evidenceKind is null || evidenceWindow is null))
            throw new ArgumentException("Touched results require evidence.");
        if (evidenceKind == PriceLevelTouchEvidenceKind.CandleRange
            && (candleTimeframe is null || hasAuthoritativeSourceOrder is not null || sourceSequence is not null))
            throw new ArgumentException("Candle evidence requires a timeframe and cannot claim source-event ordering.");
        if (evidenceKind == PriceLevelTouchEvidenceKind.MarketPriceObservation
            && (candleTimeframe is not null || hasAuthoritativeSourceOrder is null
                || (!hasAuthoritativeSourceOrder.Value && sourceSequence is not null)))
            throw new ArgumentException("Market-price evidence must preserve its actual source ordering capability.");
        if (!isTouched && (evidenceKind is not null || evidenceWindow is not null || candleTimeframe is not null
            || hasAuthoritativeSourceOrder is not null || sourceSequence is not null))
            throw new ArgumentException("Untouched results cannot contain touch evidence metadata.");

        TargetPrice = targetPrice;
        Direction = direction;
        IsTouched = isTouched;
        EvidenceKind = evidenceKind;
        EvidenceWindow = evidenceWindow;
        CandleTimeframe = candleTimeframe;
        HasAuthoritativeSourceOrder = hasAuthoritativeSourceOrder;
        SourceSequence = sourceSequence;
    }

    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public decimal TargetPrice { get; }
    public PriceLevelDirection Direction { get; }
    public bool IsTouched { get; }
    public PriceLevelTouchEvidenceKind? EvidenceKind { get; }
    public ReplayTemporalEvidenceWindow? EvidenceWindow { get; }
    public Timeframe? CandleTimeframe { get; }
    public bool? HasAuthoritativeSourceOrder { get; }
    public long? SourceSequence { get; }
    public bool HasExactTimestamp => EvidenceWindow is not null
        && EvidenceWindow.EarliestPossibleUtc == EvidenceWindow.LatestPossibleUtc;
}
