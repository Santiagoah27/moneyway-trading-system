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
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-H4-001", "Deterministic 4H structural context evaluation remains blocked, although bullish candidate-HL reconstruction is partially defined: the prior HH is the upper boundary, the first bearish candle after HH production stops starts the correction, deeper lows replace shallower candidates while it remains active, and only a formally closed Close > prior HH validates the candidate from that causal AsOfUtc. Within a selected turn, the lowest Open or Close body price is source-supported and an isolated wick is insufficient. A reproducible visual bootstrap, Doji/gap handling, exact HH and confirming-candle inclusivity, confirming-candle price scanning, turn membership, equal-coordinate candle identity, candidate-LH selection, bearish symmetry, and other structural coordinates remain unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-M5-004", "The deterministic IFVG geometry remains unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-SL-001", "Deterministic 5M HL/LH detection and structural invalidation geometry remain unresolved.", NasdaqSource),
        Blocked("moneyway-nasdaq", "nasdaq-0.1.0-draft", "NQ-TP-001", "Session target candidates, distinct first-encountered priority, and equal-target merge are source-defined. Structural fallback is partially defined: its candidate families include relevant unmitigated 1H/4H HL/LL support or Previous Day Low for sells and 1H/4H LH/HH resistance or Previous Day High for buys; the reviewed 1H-first/4H-extended relationship is source-supported, not universal. Bullish candidate-HL reconstruction is partially defined: the prior HH is the upper boundary, the first bearish candle after HH production stops starts the correction, deeper lows replace shallower candidates while it remains active, and only a formally closed Close > prior HH makes the candidate usable from that causal AsOfUtc. Within a selected turn, the lowest Open or Close body price is source-supported and an isolated wick is insufficient. Doji/gap handling, exact HH and confirming-candle inclusivity, confirming-candle price scanning, turn membership, equal-coordinate candle identity, candidate-LH selection, bearish symmetry, other structural-point selection and coordinates, exact OHLC mitigation test, PDH/PDL cross-class ranking, and ties or coincidences remain unresolved. General final Take Profit hierarchy outside the reviewed cases and universal full-exit behavior also remain unresolved.", NasdaqSource),
    ];

    private static ReplayRuleEvaluationCapabilityDeclaration Blocked(string strategyId, string version, string ruleId, string reason, string source) =>
        new(new(strategyId), new(version), new(ruleId), ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, reason, source);
}
