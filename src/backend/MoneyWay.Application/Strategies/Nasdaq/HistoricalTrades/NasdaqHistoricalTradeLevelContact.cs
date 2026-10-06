using MoneyWay.Application.MarketData.PriceLevels;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

public enum NasdaqHistoricalTradeLevelRole { StopLoss, TakeProfit }
public enum NasdaqHistoricalContactEntryRelation { BeforeEntry, OverlapsEntryBoundary, AfterEntry }
public enum NasdaqHistoricalContactOrder { Before, After, Unresolved }

/// <summary>Source-backed directional level contact, without fills, exit policy or invented intrabar timestamps.</summary>
public sealed class NasdaqHistoricalTradeLevelContact
{
    internal NasdaqHistoricalTradeLevelContact(NasdaqHistoricalTradeSnapshot snapshot, NasdaqHistoricalTradeLevelRole role,
        decimal levelPrice, ReplayTemporalEvidenceWindow window, DateTimeOffset observedAtUtc,
        Candle? candle = null, HistoricalMarketPriceObservation? price = null, HistoricalMarketPriceObservationGroup? group = null)
    {
        Snapshot = snapshot;
        Role = role;
        LevelPrice = levelPrice;
        EvidenceWindow = window;
        ObservedAtUtc = observedAtUtc;
        SourceCandle = candle;
        SourcePrice = price;
        SourcePriceGroup = group;
        EntryRelation = window.LatestPossibleUtc < snapshot.EntryEffectiveAtUtc ? NasdaqHistoricalContactEntryRelation.BeforeEntry
            : window.EarliestPossibleUtc > snapshot.EntryEffectiveAtUtc ? NasdaqHistoricalContactEntryRelation.AfterEntry
            : NasdaqHistoricalContactEntryRelation.OverlapsEntryBoundary;
    }

    public NasdaqHistoricalTradeSnapshot Snapshot { get; }
    public NasdaqHistoricalTradeLevelRole Role { get; }
    public decimal LevelPrice { get; }
    public ReplayTemporalEvidenceWindow EvidenceWindow { get; }
    /// <summary>Availability of the contact assertion bound to this snapshot in the current replay context.</summary>
    public DateTimeOffset ObservedAtUtc { get; }
    public Candle? SourceCandle { get; }
    public HistoricalMarketPriceObservation? SourcePrice { get; }
    public HistoricalMarketPriceObservationGroup? SourcePriceGroup { get; }
    public PriceLevelTouchEvidenceKind EvidenceKind => SourceCandle is null
        ? PriceLevelTouchEvidenceKind.MarketPriceObservation : PriceLevelTouchEvidenceKind.CandleRange;
    public Timeframe? SourceTimeframe => SourceCandle?.Timeframe;
    public string? SourceResolution => SourcePrice?.SourceResolution;
    public DateTimeOffset SourceAvailableAtUtc => SourceCandle?.CloseTimeUtc ?? SourcePrice!.ObservedAtUtc;
    public bool HasExactTimestamp => EvidenceWindow.EarliestPossibleUtc == EvidenceWindow.LatestPossibleUtc;
    public bool? HasAuthoritativeSourceOrder => SourcePriceGroup?.HasAuthoritativeOrder;
    public NasdaqHistoricalContactEntryRelation EntryRelation { get; }

    /// <summary>Only strict disjoint intervals or authoritative sequences within the same price group prove order.</summary>
    public NasdaqHistoricalContactOrder CompareOrder(NasdaqHistoricalTradeLevelContact other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!ReferenceEquals(Snapshot, other.Snapshot))
            throw new ArgumentException("Contacts must belong to the exact same historical snapshot.", nameof(other));
        if (EvidenceWindow.LatestPossibleUtc < other.EvidenceWindow.EarliestPossibleUtc) return NasdaqHistoricalContactOrder.Before;
        if (other.EvidenceWindow.LatestPossibleUtc < EvidenceWindow.EarliestPossibleUtc) return NasdaqHistoricalContactOrder.After;
        if (SourcePriceGroup is { HasAuthoritativeOrder: true } && ReferenceEquals(SourcePriceGroup, other.SourcePriceGroup)
            && SourcePrice?.SourceSequence is long left && other.SourcePrice?.SourceSequence is long right)
            return left < right ? NasdaqHistoricalContactOrder.Before : left > right ? NasdaqHistoricalContactOrder.After : NasdaqHistoricalContactOrder.Unresolved;
        return NasdaqHistoricalContactOrder.Unresolved;
    }
}
