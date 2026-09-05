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
                "Start analysis on 4H and establish macro context."),
            Rule("NQ-H4-002", "Break classification", "4H", 20, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Break when a candle body closes beyond the human-selected structural level; exact thresholds remain undefined."),
            Rule("NQ-H4-003", "Wick classification", "4H", 30, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Wick through human validation without assigning automatic Wickfill geometry."),
            Rule("NQ-H4-004", "Fake classification", "4H", 40, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify an attempted break returning to the prior range through human validation."),
            Rule("NQ-LIQ-001", "Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed,
                "Mark Asia High, Asia Low, London High, and London Low without session priority."),
            Rule("NQ-LIQ-002", "Relevant liquidity", "Liquidity", 60, true, RuleDefinitionStatus.HumanValidationRequired,
                "Mark other relevant liquidity through human validation without an invented hierarchy."),
            Rule("NQ-LIQ-003", "Liquidity sweep required", "Sweep", 70, true, RuleDefinitionStatus.Confirmed,
                "Require one human-validated traversal of a marked high or low before searching for a 5M setup."),
            Rule("NQ-LIQ-004", "Wick sweep alert", "Sweep", 80, false, RuleDefinitionStatus.Candidate,
                "Record a wick as a candidate alert for a possible liquidity take."),
            Rule("NQ-TIME-003", "Pre-analysis around 08:00", "Schedule", 90, false, RuleDefinitionStatus.Candidate,
                "Record the candidate practice of preparing context before operations; exact start and timezone remain undefined."),
            Rule("NQ-TIME-001", "Entry start", "Schedule", 100, true, RuleDefinitionStatus.Confirmed,
                "Do not open entries before 08:30 in the unresolved strategy timezone."),
            Rule("NQ-M5-001", "Inversion alternatives", "5M inversion", 110, true, RuleDefinitionStatus.Confirmed,
                "Require either traditional structural change or IFVG through human validation; both are not required."),
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
                "Require a human-selected Stop Loss while selection between sweep extreme and structural 5M HL/LH remains unresolved."),
            Rule("NQ-BE-001", "Distinct BE swing", "Break Even", 210, false, RuleDefinitionStatus.Confirmed,
                "Keep the post-entry Break Even swing distinct from the corrective entry swing."),
            Rule("NQ-BE-002", "Break-and-close to BE", "Break Even", 220, false, RuleDefinitionStatus.HumanValidationRequired,
                "After entry, validate break and 1M close beyond a selected post-entry swing before moving Stop Loss to entry."),
            Rule("NQ-BE-003", "Universal BE use", "Break Even", 230, false, RuleDefinitionStatus.Candidate,
                "Preserve universal Break Even application as a candidate rather than a rule for every trade."),
            Rule("NQ-TP-001", "General target", "Take Profit", 240, false, RuleDefinitionStatus.Unresolved,
                "Require human target selection because no general target, priority, or fixed ratio is defined."),
            Rule("NQ-TP-002", "Observed liquidity targets", "Take Profit", 250, false, RuleDefinitionStatus.ContextSpecific,
                "Record session extremes and important liquidity only as context-specific target examples."),
            Rule("NQ-TIME-002", "Latest new entry", "Schedule", 260, true, RuleDefinitionStatus.Confirmed,
                "Do not open new entries after 11:30 in the unresolved strategy timezone."),
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
