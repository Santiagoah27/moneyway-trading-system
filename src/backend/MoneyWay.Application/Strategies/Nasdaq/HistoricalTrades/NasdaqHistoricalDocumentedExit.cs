using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

public enum NasdaqHistoricalDocumentedExitScope { Full, Partial }

/// <summary>One source-documented historical close/execution event, without fill inference or economic classification.</summary>
public sealed class NasdaqHistoricalDocumentedExit
{
    public NasdaqHistoricalDocumentedExit(NasdaqHistoricalTradeSnapshot snapshot, string executionId,
        decimal exitPrice, DateTimeOffset exitEffectiveAtUtc, string executionSourceReference,
        NasdaqHistoricalEntryBeforeExitEvidence? entryBeforeExitEvidence = null,
        string? documentedReason = null, NasdaqHistoricalDocumentedExitScope? documentedScope = null,
        decimal? quantity = null, string? quantityUnit = null, IEnumerable<Candle>? supportingCandles = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        NasdaqStructuralLiquidityReference.ValidateProvenance(executionId);
        NasdaqStructuralLiquidityReference.ValidateProvenance(executionSourceReference);
        if (exitEffectiveAtUtc.Offset != TimeSpan.Zero || exitEffectiveAtUtc < snapshot.EntryEffectiveAtUtc)
            throw new ArgumentException("Documented exit must be UTC and cannot precede entry.", nameof(exitEffectiveAtUtc));
        if (entryBeforeExitEvidence is not null && (!ReferenceEquals(entryBeforeExitEvidence.Snapshot, snapshot)
            || entryBeforeExitEvidence.ExitExecutionId != executionId || entryBeforeExitEvidence.ObservedAtUtc < exitEffectiveAtUtc))
            throw new ArgumentException("Causal proof must bind to this exact snapshot/exit and be available after the event.", nameof(entryBeforeExitEvidence));
        if (documentedReason is not null) NasdaqStructuralLiquidityReference.ValidateProvenance(documentedReason);
        if (documentedScope is not null && !Enum.IsDefined(documentedScope.Value))
            throw new ArgumentException("Documented scope must be a defined value.", nameof(documentedScope));
        if (quantity is <= 0 || quantity.HasValue != (quantityUnit is not null))
            throw new ArgumentException("Documented quantity must be positive and have an explicit unit.", nameof(quantity));
        if (quantityUnit is not null) NasdaqStructuralLiquidityReference.ValidateProvenance(quantityUnit);
        var sources = (supportingCandles ?? []).ToArray();
        if (sources.Any(c => c is null || c.ProviderId != snapshot.Session.ProviderId || c.Symbol != snapshot.Session.Symbol))
            throw new ArgumentException("Supporting records must match the exact source/instrument.", nameof(supportingCandles));
        ExecutionId = executionId;
        ExitPrice = exitPrice;
        ExitEffectiveAtUtc = exitEffectiveAtUtc;
        ExecutionSourceReference = executionSourceReference;
        EntryBeforeExitEvidence = entryBeforeExitEvidence;
        DocumentedReason = documentedReason;
        DocumentedScope = documentedScope;
        Quantity = quantity;
        QuantityUnit = quantityUnit;
        SupportingCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.OpenTimeUtc).ThenBy(c => c.Timeframe.ToString()).ToArray());
    }

    public NasdaqHistoricalTradeSnapshot Snapshot { get; }
    public string ExecutionId { get; }
    public decimal ExitPrice { get; }
    public DateTimeOffset ExitEffectiveAtUtc { get; }
    public string ExecutionSourceReference { get; }
    public NasdaqHistoricalEntryBeforeExitEvidence? EntryBeforeExitEvidence { get; }
    /// <summary>Literal source description, never inferred from the execution price.</summary>
    public string? DocumentedReason { get; }
    public NasdaqHistoricalDocumentedExitScope? DocumentedScope { get; }
    public decimal? Quantity { get; }
    public string? QuantityUnit { get; }
    public IReadOnlyList<Candle> SupportingCandles { get; }

    internal bool SameFact(NasdaqHistoricalDocumentedExit other) => ReferenceEquals(Snapshot, other.Snapshot)
        && ExecutionId == other.ExecutionId && ExitPrice == other.ExitPrice && ExitEffectiveAtUtc == other.ExitEffectiveAtUtc
        && DocumentedReason == other.DocumentedReason && DocumentedScope == other.DocumentedScope
        && Quantity == other.Quantity && QuantityUnit == other.QuantityUnit
        && (EntryBeforeExitEvidence is null ? other.EntryBeforeExitEvidence is null
            : other.EntryBeforeExitEvidence is not null && EntryBeforeExitEvidence.SameFact(other.EntryBeforeExitEvidence));
}
