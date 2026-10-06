using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Artifact availability, never a rule status or economic result. Entry diagnostics are retained losslessly.</summary>
public abstract class NasdaqHistoricalTradeSnapshotResult
{
    private NasdaqHistoricalTradeSnapshotResult(NasdaqHistoricalObservedEntrySelection entrySelection) => EntrySelection = entrySelection;
    public NasdaqHistoricalObservedEntrySelection EntrySelection { get; }

    public sealed class Available : NasdaqHistoricalTradeSnapshotResult
    {
        internal Available(NasdaqHistoricalTradeSnapshot snapshot) : base(snapshot.ObservedEntry) => Snapshot = snapshot;
        public NasdaqHistoricalTradeSnapshot Snapshot { get; }
    }

    public sealed class Unavailable : NasdaqHistoricalTradeSnapshotResult
    {
        internal Unavailable(NasdaqHistoricalObservedEntrySelection entrySelection, string reason) : base(entrySelection) => Reason = reason;
        public string Reason { get; }
    }
}
