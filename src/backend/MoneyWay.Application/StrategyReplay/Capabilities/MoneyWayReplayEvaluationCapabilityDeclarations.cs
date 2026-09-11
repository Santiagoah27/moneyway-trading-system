namespace MoneyWay.Application.StrategyReplay.Capabilities;

/// <summary>Provides explicit audited MoneyWay evaluation limitations without inferring them from rule definition status.</summary>
public static class MoneyWayReplayEvaluationCapabilityDeclarations
{
    private const string ForexSource = "docs/strategies/forex/rule-catalog.md";
    private const string NasdaqSource = "docs/strategies/nasdaq/rule-catalog.md";

    public static IReadOnlyList<ReplayRuleEvaluationCapabilityDeclaration> GetAll() =>
    [
        Blocked("moneyway-forex", "forex-0.1.0-draft", "FX-W-004", "The exact weekly-zone interaction tolerance remains unresolved.", ForexSource),
        Blocked("moneyway-forex", "forex-0.1.0-draft", "FX-BO-001", "The deterministic breakout geometry remains unresolved.", ForexSource),
        Blocked("moneyway-forex", "forex-0.1.0-draft", "FX-RT-001", "The deterministic retest tolerance and invalidation rules remain unresolved.", ForexSource),
        Blocked("moneyway-forex", "forex-0.1.0-draft", "FX-TP-001", "The deterministic exit policy for the reference reward/risk ratio remains unresolved.", ForexSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-H4-001", "Deterministic 4H structural context evaluation is blocked because the audited strategy does not define a reproducible method for selecting the previous structural High/Low and retracement pivots from closed candles.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-M5-004", "The deterministic IFVG geometry remains unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-SL-001", "Deterministic 5M HL/LH detection and structural invalidation geometry remain unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-TP-001", "Session target candidates, distinct first-encountered priority, and equal-target merge are source-defined. Structural 1H/4H fallback is source-confirmed, but deterministic structural-point identification, 1H-versus-4H priority, candidate ranking, and tie-breaking remain unresolved. Target consumption after 08:30 but before Step-3 activation, general final Take Profit hierarchy outside the reviewed cases, and universal full-exit behavior also remain unresolved.", NasdaqSource),
    ];

    private static ReplayRuleEvaluationCapabilityDeclaration Blocked(string strategyId, string version, string ruleId, string reason, string source) =>
        new(new(strategyId), new(version), new(ruleId), ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, reason, source);
}
