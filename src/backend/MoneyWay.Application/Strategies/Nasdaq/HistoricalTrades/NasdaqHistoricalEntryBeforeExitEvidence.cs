using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>An authoritative source assertion that this snapshot's entry precedes the named exit execution.</summary>
public sealed class NasdaqHistoricalEntryBeforeExitEvidence
{
    public NasdaqHistoricalEntryBeforeExitEvidence(NasdaqHistoricalTradeSnapshot snapshot, string exitExecutionId,
        string evidenceId, DateTimeOffset observedAtUtc, string sourceReference)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        NasdaqStructuralLiquidityReference.ValidateProvenance(exitExecutionId);
        NasdaqStructuralLiquidityReference.ValidateProvenance(evidenceId);
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < snapshot.EntryEffectiveAtUtc)
            throw new ArgumentException("Causal assertion availability must be UTC and follow entry.", nameof(observedAtUtc));
        ExitExecutionId = exitExecutionId;
        EvidenceId = evidenceId;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHistoricalTradeSnapshot Snapshot { get; }
    public string EntryExecutionId => Snapshot.Entry.ExecutionId;
    public string ExitExecutionId { get; }
    /// <summary>Stable source-qualified identity of the audited Entry-before-Exit relationship.</summary>
    public string EvidenceId { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    /// <summary>Retained authoritative material establishing the relationship; no external lookup.</summary>
    public string SourceReference { get; }

    internal bool SameFact(NasdaqHistoricalEntryBeforeExitEvidence other) => ReferenceEquals(Snapshot, other.Snapshot)
        && ExitExecutionId == other.ExitExecutionId && EvidenceId == other.EvidenceId;
}
