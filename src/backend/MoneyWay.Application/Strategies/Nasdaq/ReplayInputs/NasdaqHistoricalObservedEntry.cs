using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One explicitly documented historical execution, never an inferred fill or an execution command.</summary>
public sealed class NasdaqHistoricalObservedEntry
{
    public NasdaqHistoricalObservedEntry(NasdaqPreEntryEligibilityRuleFact preEntryEligibility,
        string executionId, decimal entryPrice, DateTimeOffset entryEffectiveAtUtc,
        string executionSourceReference, decimal? quantity = null, string? quantityUnit = null,
        IEnumerable<Candle>? supportingCandles = null)
    {
        PreEntryEligibility = preEntryEligibility ?? throw new ArgumentNullException(nameof(preEntryEligibility));
        NasdaqStructuralLiquidityReference.ValidateProvenance(executionId);
        NasdaqStructuralLiquidityReference.ValidateProvenance(executionSourceReference);
        if (entryEffectiveAtUtc.Offset != TimeSpan.Zero || entryEffectiveAtUtc < preEntryEligibility.EligibilityEffectiveAtUtc)
            throw new ArgumentException("Documented execution must be UTC and cannot precede exact eligibility.", nameof(entryEffectiveAtUtc));
        if (quantity is <= 0 || quantity.HasValue != (quantityUnit is not null))
            throw new ArgumentException("Documented quantity must be positive and have an explicit unit.", nameof(quantity));
        if (quantityUnit is not null) NasdaqStructuralLiquidityReference.ValidateProvenance(quantityUnit);
        var sources = (supportingCandles ?? []).ToArray();
        if (sources.Any(c => c is null || c.ProviderId != Session.ProviderId || c.Symbol != Session.Symbol))
            throw new ArgumentException("Supporting candles must match the exact execution instrument/source.", nameof(supportingCandles));
        ExecutionId = executionId;
        EntryPrice = entryPrice;
        EntryEffectiveAtUtc = entryEffectiveAtUtc;
        ExecutionSourceReference = executionSourceReference;
        Quantity = quantity;
        QuantityUnit = quantityUnit;
        SupportingCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.OpenTimeUtc).ThenBy(c => c.Timeframe.ToString()).ToArray());
    }

    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    /// <summary>Exact source-qualified execution event identity, stable across supporting records.</summary>
    public string ExecutionId { get; }
    public decimal EntryPrice { get; }
    public DateTimeOffset EntryEffectiveAtUtc { get; }
    /// <summary>Reference to retained execution/mentor material; no runtime external lookup is performed.</summary>
    public string ExecutionSourceReference { get; }
    public decimal? Quantity { get; }
    public string? QuantityUnit { get; }
    /// <summary>Optional supporting market records, never used to calculate the execution price.</summary>
    public IReadOnlyList<Candle> SupportingCandles { get; }

    internal static bool SameEligibility(NasdaqPreEntryEligibilityRuleFact one, NasdaqPreEntryEligibilityRuleFact other) =>
        one.Session == other.Session && one.Direction == other.Direction
        && one.Realignment.Selection.Fact.SameFact(other.Realignment.Selection.Fact);

    internal bool SameFact(NasdaqHistoricalObservedEntry other) => SameEligibility(PreEntryEligibility, other.PreEntryEligibility)
        && ExecutionId == other.ExecutionId && EntryPrice == other.EntryPrice && EntryEffectiveAtUtc == other.EntryEffectiveAtUtc
        && Quantity == other.Quantity && QuantityUnit == other.QuantityUnit;
}
