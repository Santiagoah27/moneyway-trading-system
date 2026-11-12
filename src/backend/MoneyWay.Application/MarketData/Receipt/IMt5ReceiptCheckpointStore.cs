namespace MoneyWay.Application.MarketData.Receipt;

/// <summary>Explicit durable acknowledgment of receipt only, never downstream exactly-once processing.</summary>
public interface IMt5ReceiptCheckpointStore
{
    Mt5CheckpointResult Load();

    /// <summary>Null revision means create only; otherwise compare with the last loaded file revision.</summary>
    Mt5CheckpointResult Commit(Mt5ReceiptCheckpoint progress, string? expectedRevision);
}

public enum Mt5CheckpointStatus
{
    Missing, Loaded, Committed, Unchanged, Busy, Conflict, Invalid, DataUnavailable, PublicationUncertain
}

/// <summary>Checkpoint is committed progress. Verification may propose additional, uncommitted receipts.</summary>
public sealed record Mt5CheckpointResult(Mt5CheckpointStatus Status, Mt5ReceiptCheckpoint? Checkpoint,
    string? Revision, Mt5ReceiptBatch? Verification, IReadOnlyList<string> Diagnostics)
{
    public bool CanonicalReady => false;
}
