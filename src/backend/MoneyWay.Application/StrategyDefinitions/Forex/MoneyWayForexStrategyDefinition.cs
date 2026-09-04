using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyDefinitions.Forex;

/// <summary>
/// Provides the audited MoneyWay Forex draft definition without evaluating its rules.
/// </summary>
public static class MoneyWayForexStrategyDefinition
{
    private const string CatalogReference = "docs/strategies/forex/rule-catalog.md";

    public static StrategyDefinition Instance { get; } = new(
        new StrategyId("moneyway-forex"),
        new StrategyVersion("forex-0.1.0-draft"),
        "MoneyWay Forex",
        "docs/strategies/forex/strategy-specification.md",
        [
            Rule("FX-W-002", "Weekly direction", "Weekly", 10, true, RuleDefinitionStatus.HumanValidationRequired,
                "Determine weekly direction or context before reviewing lower timeframes."),
            Rule("FX-W-003", "Weekly zone", "Weekly", 20, true, RuleDefinitionStatus.HumanValidationRequired,
                "Identify a relevant weekly zone through human validation."),
            Rule("FX-W-004", "Weekly-zone interaction", "Weekly", 30, true, RuleDefinitionStatus.Unresolved,
                "Wait for price to reach or interact with the approved weekly zone; exact tolerance remains unresolved."),
            Rule("FX-D-001", "Daily review after interaction", "Daily", 40, true, RuleDefinitionStatus.Confirmed,
                "Review the daily timeframe only after weekly-zone interaction."),
            Rule("FX-D-002", "Daily-weekly alignment", "Daily", 50, true, RuleDefinitionStatus.HumanValidationRequired,
                "Require human validation that daily context aligns with weekly context."),
            Rule("FX-H4-001", "Allowed pattern group", "4H", 60, true, RuleDefinitionStatus.VisualOnly,
                "Require one permitted 4H pattern: Head and Shoulders, inverse Head and Shoulders, Double Top, Triple Top, Double Bottom, or Triple Bottom."),
            Rule("FX-H4-002", "Pattern completion", "4H", 70, true, RuleDefinitionStatus.HumanValidationRequired,
                "Require human validation that the selected 4H pattern is complete before continuation."),
            Rule("FX-H4-003", "Macro location", "4H / Weekly", 80, false, RuleDefinitionStatus.HumanValidationRequired,
                "Record human review of macro location without inventing mathematical range boundaries or symmetric rules."),
            Rule("FX-H4-004", "Setup maturity", "4H / Weekly", 90, false, RuleDefinitionStatus.Candidate,
                "Record human review of first-clean-impulse preference, setup maturity, and extended movement without quantitative thresholds."),
            Rule("FX-BO-001", "Relevant-level breakout", "Breakout", 100, true, RuleDefinitionStatus.Unresolved,
                "Require human validation that the relevant zone or level broke; exact breakout geometry remains unresolved."),
            Rule("FX-BO-002", "No breakout-candle entry", "Breakout", 110, true, RuleDefinitionStatus.Confirmed,
                "Do not enter during the breakout candle."),
            Rule("FX-RT-001", "Retest required", "Retest", 120, true, RuleDefinitionStatus.Unresolved,
                "Wait for a human-validated retest after breakout; exact tolerances and invalidation remain unresolved."),
            Rule("FX-ENTRY-001", "Allowed signal timeframes", "Entry", 130, true, RuleDefinitionStatus.Confirmed,
                "Accept one human-validated signal from 2H, 1H, or 30M without automatic timeframe priority."),
            Rule("FX-ENTRY-003", "Signal candle close", "Entry", 140, true, RuleDefinitionStatus.Confirmed,
                "Wait for the validated signal candle to close before entry."),
            Rule("FX-SL-001", "Buy Stop Loss structure", "Stop Loss", 150, true, RuleDefinitionStatus.HumanValidationRequired,
                "For a buy, require a human-approved Stop Loss referencing a structural 4H Higher Low."),
            Rule("FX-SL-002", "Sell Stop Loss structure", "Stop Loss", 160, true, RuleDefinitionStatus.HumanValidationRequired,
                "For a sell, require a human-approved Stop Loss referencing a structural 4H Lower High."),
            Rule("FX-TP-001", "Reference reward/risk", "Take Profit", 170, true, RuleDefinitionStatus.Unresolved,
                "Record 2:1 as the reference Take Profit ratio while the exact exit policy remains unresolved."),
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
