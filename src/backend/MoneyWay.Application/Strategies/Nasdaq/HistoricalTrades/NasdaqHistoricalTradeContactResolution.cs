using System.Collections.ObjectModel;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>One supported pairwise comparison, including unresolved source ordering.</summary>
public sealed class NasdaqHistoricalTradeContactPairOrder
{
    internal NasdaqHistoricalTradeContactPairOrder(NasdaqHistoricalTradeLevelContact left, NasdaqHistoricalTradeLevelContact right)
    {
        Left = left;
        Right = right;
        Order = left.CompareOrder(right);
    }
    public NasdaqHistoricalTradeLevelContact Left { get; }
    public NasdaqHistoricalTradeLevelContact Right { get; }
    public NasdaqHistoricalContactOrder Order { get; }
}

/// <summary>Closed contact-resolution states, without fill, exit, strategy verdict or economic result semantics.</summary>
public abstract class NasdaqHistoricalTradeContactResolution
{
    private NasdaqHistoricalTradeContactResolution(NasdaqHistoricalTradeSnapshot snapshot, DateTimeOffset asOfUtc,
        IEnumerable<NasdaqHistoricalTradeLevelContact> contacts, IEnumerable<NasdaqHistoricalTradeContactPairOrder> pairs,
        NasdaqHistoricalTradeLevelContact? earliest)
    {
        Snapshot = snapshot;
        AsOfUtc = asOfUtc;
        Contacts = new ReadOnlyCollection<NasdaqHistoricalTradeLevelContact>(contacts.ToArray());
        PairwiseOrders = new ReadOnlyCollection<NasdaqHistoricalTradeContactPairOrder>(pairs.ToArray());
        BeforeEntryContacts = Select(NasdaqHistoricalContactEntryRelation.BeforeEntry);
        EntryBoundaryContacts = Select(NasdaqHistoricalContactEntryRelation.OverlapsEntryBoundary);
        StrictlyPostEntryContacts = Select(NasdaqHistoricalContactEntryRelation.AfterEntry);
        UnresolvedPairs = new ReadOnlyCollection<NasdaqHistoricalTradeContactPairOrder>(PairwiseOrders
            .Where(p => p.Order == NasdaqHistoricalContactOrder.Unresolved).ToArray());
        EarliestStrictlyPostEntryContact = earliest;
    }
    public NasdaqHistoricalTradeSnapshot Snapshot { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<NasdaqHistoricalTradeLevelContact> Contacts { get; }
    public IReadOnlyList<NasdaqHistoricalTradeLevelContact> BeforeEntryContacts { get; }
    public IReadOnlyList<NasdaqHistoricalTradeLevelContact> EntryBoundaryContacts { get; }
    public IReadOnlyList<NasdaqHistoricalTradeLevelContact> StrictlyPostEntryContacts { get; }
    public IReadOnlyList<NasdaqHistoricalTradeContactPairOrder> PairwiseOrders { get; }
    public IReadOnlyList<NasdaqHistoricalTradeContactPairOrder> UnresolvedPairs { get; }
    /// <summary>Qualified projection among AfterEntry contacts only; entry-boundary ambiguity is never removed by this property.</summary>
    public NasdaqHistoricalTradeLevelContact? EarliestStrictlyPostEntryContact { get; }

    private IReadOnlyList<NasdaqHistoricalTradeLevelContact> Select(NasdaqHistoricalContactEntryRelation relation) =>
        new ReadOnlyCollection<NasdaqHistoricalTradeLevelContact>(Contacts.Where(c => c.EntryRelation == relation).ToArray());

    public sealed class NoRelevantContacts : NasdaqHistoricalTradeContactResolution
    {
        internal NoRelevantContacts(NasdaqHistoricalTradeSnapshot snapshot, DateTimeOffset asOfUtc,
            IEnumerable<NasdaqHistoricalTradeLevelContact> contacts, IEnumerable<NasdaqHistoricalTradeContactPairOrder> pairs)
            : base(snapshot, asOfUtc, contacts, pairs, null) { }
    }
    public sealed class EarliestProvenContact : NasdaqHistoricalTradeContactResolution
    {
        internal EarliestProvenContact(NasdaqHistoricalTradeSnapshot snapshot, DateTimeOffset asOfUtc,
            IEnumerable<NasdaqHistoricalTradeLevelContact> contacts, IEnumerable<NasdaqHistoricalTradeContactPairOrder> pairs,
            NasdaqHistoricalTradeLevelContact earliest) : base(snapshot, asOfUtc, contacts, pairs, earliest) { }
        public NasdaqHistoricalTradeLevelContact FirstContact => EarliestStrictlyPostEntryContact!;
    }
    public sealed class EntryBoundaryAmbiguous : NasdaqHistoricalTradeContactResolution
    {
        internal EntryBoundaryAmbiguous(NasdaqHistoricalTradeSnapshot snapshot, DateTimeOffset asOfUtc,
            IEnumerable<NasdaqHistoricalTradeLevelContact> contacts, IEnumerable<NasdaqHistoricalTradeContactPairOrder> pairs,
            NasdaqHistoricalTradeLevelContact? earliest) : base(snapshot, asOfUtc, contacts, pairs, earliest) { }
    }
    public sealed class OrderingUnresolved : NasdaqHistoricalTradeContactResolution
    {
        internal OrderingUnresolved(NasdaqHistoricalTradeSnapshot snapshot, DateTimeOffset asOfUtc,
            IEnumerable<NasdaqHistoricalTradeLevelContact> contacts, IEnumerable<NasdaqHistoricalTradeContactPairOrder> pairs)
            : base(snapshot, asOfUtc, contacts, pairs, null) { }
    }
}
