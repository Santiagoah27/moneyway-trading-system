using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Observability;

/// <summary>Records an immutable, strategy-neutral assessment of temporal evidence at one canonical replay boundary.</summary>
public sealed record ReplayMarketDataObservabilityAssessment
{
    public ReplayMarketDataObservabilityAssessment(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        RuleId ruleId,
        int step,
        DateTimeOffset asOfUtc,
        ReplayTemporalEvidenceWindow leftEvidence,
        ReplayTemporalEvidenceWindow rightEvidence,
        ReplayMarketDataObservabilityStatus status,
        string reason)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Assessment timestamp must be UTC.", nameof(asOfUtc));
        LeftEvidence = leftEvidence ?? throw new ArgumentNullException(nameof(leftEvidence));
        RightEvidence = rightEvidence ?? throw new ArgumentNullException(nameof(rightEvidence));
        if (leftEvidence.EvidenceId == rightEvidence.EvidenceId)
            throw new ArgumentException("Temporal evidence identifiers must be distinct.", nameof(rightEvidence));
        if (leftEvidence.LatestPossibleUtc > asOfUtc || rightEvidence.LatestPossibleUtc > asOfUtc)
            throw new ArgumentException("Temporal evidence cannot extend beyond the assessment timestamp.");
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        ArgumentNullException.ThrowIfNull(reason);
        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim())
            throw new ArgumentException("Reason must be non-empty and have no surrounding whitespace.", nameof(reason));

        Step = step;
        AsOfUtc = asOfUtc;
        Status = status;
        Reason = reason;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public RuleId RuleId { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public ReplayTemporalEvidenceWindow LeftEvidence { get; }
    public ReplayTemporalEvidenceWindow RightEvidence { get; }
    public ReplayMarketDataObservabilityStatus Status { get; }
    public string Reason { get; }
}
