namespace MoneyWay.Application.StrategyReplay.Observability;

/// <summary>Bounds when one fact is known to have occurred without inventing an order inside the interval.</summary>
public sealed record ReplayTemporalEvidenceWindow
{
    public ReplayTemporalEvidenceWindow(
        string evidenceId,
        DateTimeOffset earliestPossibleUtc,
        DateTimeOffset latestPossibleUtc)
    {
        ArgumentNullException.ThrowIfNull(evidenceId);
        if (string.IsNullOrWhiteSpace(evidenceId) || evidenceId != evidenceId.Trim())
            throw new ArgumentException("Evidence identifier must be non-empty and have no surrounding whitespace.", nameof(evidenceId));
        if (earliestPossibleUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Earliest timestamp must be UTC.", nameof(earliestPossibleUtc));
        if (latestPossibleUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Latest timestamp must be UTC.", nameof(latestPossibleUtc));
        if (latestPossibleUtc < earliestPossibleUtc)
            throw new ArgumentException("Latest timestamp cannot precede earliest timestamp.", nameof(latestPossibleUtc));

        EvidenceId = evidenceId;
        EarliestPossibleUtc = earliestPossibleUtc;
        LatestPossibleUtc = latestPossibleUtc;
    }

    public string EvidenceId { get; }
    public DateTimeOffset EarliestPossibleUtc { get; }
    public DateTimeOffset LatestPossibleUtc { get; }
}
