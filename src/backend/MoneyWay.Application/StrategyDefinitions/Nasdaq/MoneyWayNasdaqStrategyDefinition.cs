using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyDefinitions.Nasdaq;

/// <summary>
/// Provides the audited MoneyWay Nasdaq draft definition without evaluating its rules.
/// </summary>
public static class MoneyWayNasdaqStrategyDefinition
{
    private const string CatalogReference = "docs/strategies/nasdaq/rule-catalog.md";

    public static StrategyDefinition Instance { get; } = new(
        new StrategyId("moneyway-nasdaq"),
        new StrategyVersion("nasdaq-0.1.0-draft"),
        "MoneyWay Nasdaq",
        "docs/strategies/nasdaq/strategy-specification.md",
        [
            Rule("NQ-H4-001", "4H-first context", "4H", 10, true, RuleDefinitionStatus.Confirmed,
                "At 08:00 America/Bogota, start preparation on 4H by reviewing HH/LL trend and Breakout, Wickfill, or Fakeout context through human validation."),
            Rule("NQ-H4-002", "Break classification", "4H", 20, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Break when a candle body closes beyond the human-selected structural level; exact thresholds remain undefined."),
            Rule("NQ-H4-003", "Wickfill classification", "4H", 30, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Wickfill through human validation without assigning automatic geometry."),
            Rule("NQ-H4-004", "Fakeout classification", "4H", 40, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Fakeout through human validation without assigning automatic geometry."),
            Rule("NQ-LIQ-001", "Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed,
                "Identify completed Asia and London session highs/lows from exact 1H candles selected by local America/Bogota OpenTime: Asia [D-1 17:00, D 02:00) and London [D 02:00, D 07:00). Each session high is the maximum Candle.High and each session low is the minimum Candle.Low."),
            Rule("NQ-LIQ-002", "Relevant liquidity", "Liquidity", 60, true, RuleDefinitionStatus.HumanValidationRequired,
                "Review human-validated structural points on 1H and 4H, especially where they coincide with Asia or London extrema, without an invented hierarchy or tolerance."),
            Rule("NQ-LIQ-003", "Liquidity sweep required", "Sweep", 70, true, RuleDefinitionStatus.Confirmed,
                "Require price to exceed a marked liquidity high or low before searching for a 5M setup; remaining sweep details require human validation."),
            Rule("NQ-LIQ-004", "Wick sweep alert", "Sweep", 80, false, RuleDefinitionStatus.Candidate,
                "Record a wick as a candidate alert for a possible liquidity take."),
            Rule("NQ-TIME-003", "Preparation start", "Schedule", 90, false, RuleDefinitionStatus.Confirmed,
                "Start preparation and analysis at 08:00 America/Bogota; this is not the trading-window start."),
            Rule("NQ-TIME-001", "Trading-window start", "Schedule", 100, true, RuleDefinitionStatus.Confirmed,
                "Do not open entries before the trading window starts at 08:30 America/Bogota."),
            Rule("NQ-M5-001", "Inversion alternatives", "5M inversion", 110, true, RuleDefinitionStatus.Confirmed,
                "Require traditional structural change or IFVG through human validation, whichever occurs first; both are not required and neither has fixed priority."),
            Rule("NQ-M5-002", "Buy structural change", "5M inversion", 120, false, RuleDefinitionStatus.HumanValidationRequired,
                "For a buy alternative, validate a body close beyond a human-selected bearish 5M structural swing."),
            Rule("NQ-M5-003", "Sell structural change", "5M inversion", 130, false, RuleDefinitionStatus.HumanValidationRequired,
                "For a sell alternative, validate a body close beyond a human-selected bullish 5M structural swing."),
            Rule("NQ-M5-004", "IFVG alternative", "5M inversion", 140, false, RuleDefinitionStatus.Unresolved,
                "Allow human review of prior-FVG invalidation as an alternative while IFVG geometry remains unresolved."),
            Rule("NQ-FVG-001", "Continuation FVG required", "5M continuation", 150, true, RuleDefinitionStatus.Confirmed,
                "Require a directional three-candle FVG after inversion, measured as space between candle 1 and candle 3 wicks."),
            Rule("NQ-FVG-002", "FVG quality", "5M continuation", 160, true, RuleDefinitionStatus.HumanValidationRequired,
                "Require human validation of clear FVG strength while minimum size remains undefined."),
            Rule("NQ-M1-001", "Corrective retracement", "1M entry", 170, true, RuleDefinitionStatus.HumanValidationRequired,
                "Wait for a human-validated 1M retracement against the new 5M move."),
            Rule("NQ-M1-002", "Entry swing realignment", "1M entry", 180, true, RuleDefinitionStatus.HumanValidationRequired,
                "Require break and body close beyond the human-selected last corrective 1M swing."),
            Rule("NQ-M1-003", "Entry mechanics", "Entry", 190, false, RuleDefinitionStatus.Unresolved,
                "Record that entry follows 1M confirmation while order type, timing, and slippage remain unresolved."),
            Rule("NQ-SL-001", "Stop Loss reference", "Stop Loss", 200, true, RuleDefinitionStatus.Unresolved,
                "For a buy, reference the structural 5M HL; for a sell, reference the structural 5M LH; exact detection and where the trade loses structural meaning remain unresolved."),
            Rule("NQ-BE-001", "Distinct BE swing", "Break Even", 210, false, RuleDefinitionStatus.Confirmed,
                "Keep the post-entry Break Even swing distinct from the corrective entry swing."),
            Rule("NQ-BE-002", "Break-and-close to BE", "Break Even", 220, false, RuleDefinitionStatus.HumanValidationRequired,
                "After entry, validate break and 1M close beyond a selected post-entry swing before moving Stop Loss to entry."),
            Rule("NQ-BE-003", "Universal BE use", "Break Even", 230, false, RuleDefinitionStatus.Candidate,
                "Preserve universal Break Even application as a candidate rather than a rule for every trade."),
            Rule("NQ-TP-001", "General target", "Take Profit", 240, false, RuleDefinitionStatus.Unresolved,
                "For a buy, reference important highs; for a sell, reference important lows; importance, priority, and management remain unresolved."),
            Rule("NQ-TP-002", "Observed liquidity targets", "Take Profit", 250, false, RuleDefinitionStatus.ContextSpecific,
                "Record session extremes only as context-specific examples that may be important directional targets, not universal targets."),
            Rule("NQ-TIME-002", "Trading-window end", "Schedule", 260, true, RuleDefinitionStatus.Confirmed,
                "Do not open new entries after the trading window ends at 11:30 America/Bogota; this does not require closing existing positions."),
            Rule("NQ-RISK-001", "Maximum risk per trade", "Risk", 270, true, RuleDefinitionStatus.Confirmed,
                "Record a maximum risk per trade of 1%; sizing implementation remains undefined."),
            Rule("NQ-RISK-002", "Daily loss limit", "Risk", 280, false, RuleDefinitionStatus.Candidate,
                "Preserve the mentioned 1% daily loss limit as a candidate pending evidence review."),
            Rule("NQ-RISK-003", "Additional risk controls", "Risk", 290, false, RuleDefinitionStatus.Unresolved,
                "Keep trade count, consecutive losses, risk reduction, and kill switch unresolved."),
            Rule("NQ-NEWS-001", "Observed news closure", "News", 300, false, RuleDefinitionStatus.ContextSpecific,
                "Record the observed 10:00 news and 09:45 close-all example without making it universal."),
            Rule("NQ-NEWS-002", "General news policy", "News", 310, false, RuleDefinitionStatus.Unresolved,
                "Keep universal event, window, position, and resumption policy unresolved."),
            Rule("NQ-REENTRY-001", "Reentry policy", "Reentry", 320, false, RuleDefinitionStatus.Unresolved,
                "Keep reentry scope and maximum attempts unresolved."),
        ]);

    private static StrategyRuleDefinition Rule(
        string id,
        string name,
        string stage,
        int sequence,
        bool isRequired,
        RuleDefinitionStatus status,
        string description) =>
        new(new RuleId(id), name, stage, sequence, isRequired, status, description, CatalogReference);
}
