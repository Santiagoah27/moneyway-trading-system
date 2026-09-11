using MoneyWay.Application.StrategyReplay.Observability;

namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Transports a neutral temporal-ordering assessment without selecting a lifecycle transition.</summary>
public sealed record StrategyReplayLifecycleTemporalObservabilityEvidence : IStrategyReplayLifecycleEvidence
{
    public StrategyReplayLifecycleTemporalObservabilityEvidence(
        DateTimeOffset observedAtUtc,
        ReplayTemporalEvidenceWindow first,
        ReplayTemporalEvidenceWindow second,
        ReplayMarketDataObservabilityStatus status)
    {
        if (observedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(observedAtUtc));
        First = first ?? throw new ArgumentNullException(nameof(first));
        Second = second ?? throw new ArgumentNullException(nameof(second));
        if (first.LatestPossibleUtc > observedAtUtc || second.LatestPossibleUtc > observedAtUtc)
            throw new ArgumentException("Temporal evidence cannot extend beyond the observation timestamp.");
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        ObservedAtUtc = observedAtUtc;
        Status = status;
    }

    public DateTimeOffset ObservedAtUtc { get; }
    public ReplayTemporalEvidenceWindow First { get; }
    public ReplayTemporalEvidenceWindow Second { get; }
    public ReplayMarketDataObservabilityStatus Status { get; }
}
