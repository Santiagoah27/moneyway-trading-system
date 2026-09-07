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
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-TIME-001", "The strategy timezone and daylight-saving basis remain unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-M5-004", "The deterministic IFVG geometry remains unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-SL-001", "The Stop Loss selection rule remains contradictory and unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-TP-001", "The general target and target-priority rules remain unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-TIME-002", "The strategy timezone and daylight-saving basis remain unresolved.", NasdaqSource),
    ];

    private static ReplayRuleEvaluationCapabilityDeclaration Blocked(string strategyId, string version, string ruleId, string reason, string source) =>
        new(new(strategyId), new(version), new(ruleId), ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, reason, source);
}
