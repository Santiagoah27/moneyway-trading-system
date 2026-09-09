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
            Rule("NQ-TIME-003", "Preparation start", "Schedule", 5, false, RuleDefinitionStatus.Confirmed,
                "Start preparation and analysis at 08:00 America/Bogota; this is not the trading-window start."),
            Rule("NQ-H4-001", "4H-first context", "4H", 10, true, RuleDefinitionStatus.Confirmed,
                "At 08:00 America/Bogota, use the closed 4H candle to review HH/HL bullish or LL/LH bearish context, classify Breakout, Wickfill, or Fakeout using OR, and determine the permitted direction. Structural breaks require a body close, not a wick alone, and HL/LH confirmation is retrospective; bootstrap from a major visible structural extreme remains visual/contextual rather than a deterministic raw-candle algorithm."),
            Rule("NQ-H4-002", "Break classification", "4H", 20, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Break when a candle body closes beyond the human-selected structural level; exact thresholds remain undefined."),
            Rule("NQ-H4-003", "Wickfill classification", "4H", 30, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Wickfill through human validation without assigning automatic geometry."),
            Rule("NQ-H4-004", "Fakeout classification", "4H", 40, false, RuleDefinitionStatus.HumanValidationRequired,
                "Classify Fakeout through human validation without assigning automatic geometry."),
            Rule("NQ-LIQ-001", "Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed,
                "Identify completed Asia and London session highs/lows from exact 1H candles selected by local America/Bogota OpenTime: Asia [D-1 17:00, D 02:00) and London [D 02:00, D 07:00). Each session high is the maximum Candle.High and each session low is the minimum Candle.Low."),
            Rule("NQ-LIQ-002", "Relevant liquidity", "Liquidity", 60, true, RuleDefinitionStatus.HumanValidationRequired,
                "Review 1H/4H structural liquidity references as Step-2 levels and note possible coincidence with Asia or London highs/lows, without inferring a structural-point algorithm, tolerance, or priority."),
            Rule("NQ-TIME-001", "Trading-window start", "Schedule", 65, true, RuleDefinitionStatus.Confirmed,
                "Do not open entries before the trading window starts at 08:30 America/Bogota."),
            Rule("NQ-LIQ-003", "Liquidity take required", "Sweep", 70, true, RuleDefinitionStatus.Confirmed,
                "From the 08:30 operational start and before 11:00 America/Bogota, a valid liquidity-take event occurs when price exceeds a selected relevant High or goes below a selected relevant Low. It is the prerequisite that enables waiting for the downstream Step-4 5M trigger, not Step-4 confirmation or entry eligibility, while the active setup remains uncancelled."),
            Rule("NQ-LIQ-004", "Wick sweep alert", "Sweep", 80, false, RuleDefinitionStatus.Candidate,
                "Record a wick as a candidate alert for a possible liquidity take."),
            Rule("NQ-M5-001", "Inversion alternatives", "5M inversion", 110, true, RuleDefinitionStatus.Confirmed,
                "Within an active pre-entry setup before 11:00 America/Bogota and only after its valid liquidity take, Step 4 requires 5M Structural Change OR IFVG, whichever occurs first; neither alternative bypasses the separate Step-5 FVG confirmation. Structural Change requires a body close beyond the active 5M structural reference; a wick-only break or continued manipulation-direction movement remains pending and does not alone cancel the setup, and a newer extreme may update that reference before confirmation."),
            Rule("NQ-M5-002", "Buy structural change", "5M inversion", 120, false, RuleDefinitionStatus.HumanValidationRequired,
                "After downside liquidity is taken, validate a strong 5M body close beyond the human-selected bearish structural swing in the bullish direction; exact swing and strength geometry remain unresolved."),
            Rule("NQ-M5-003", "Sell structural change", "5M inversion", 130, false, RuleDefinitionStatus.HumanValidationRequired,
                "After upside liquidity is taken, validate a strong 5M body close beyond the human-selected bullish structural swing in the bearish direction; exact swing and strength geometry remain unresolved."),
            Rule("NQ-M5-004", "IFVG alternative", "5M inversion", 140, false, RuleDefinitionStatus.Unresolved,
                "After liquidity is taken, human review may validate IFVG as the Step-4 alternative to Structural Change; it is not a direct entry and does not automatically satisfy Step 5, while IFVG geometry remains unresolved."),
            Rule("NQ-FVG-001", "Continuation FVG required", "5M continuation", 150, true, RuleDefinitionStatus.Confirmed,
                "After the Step-4 trigger, require a separate directional 5M FVG confirmation before Step 6; a Step-4 IFVG does not automatically satisfy this mandatory gate, and exact FVG geometry, quality, and lifecycle remain unresolved."),
            Rule("NQ-FVG-002", "FVG quality", "5M continuation", 160, true, RuleDefinitionStatus.HumanValidationRequired,
                "Require human validation of clear FVG strength while minimum size remains undefined."),
            Rule("NQ-M1-001", "Corrective retracement", "1M entry", 170, true, RuleDefinitionStatus.HumanValidationRequired,
                "Only after the Step-5 FVG, validate a countertrend 1M pullback toward or into that relevant directional 5M FVG: bearish for a buy or bullish for a sell."),
            Rule("NQ-M1-002", "Entry swing realignment", "1M entry", 180, true, RuleDefinitionStatus.HumanValidationRequired,
                "After the pullback interacts with the relevant 5M FVG, require 1M structural realignment and a body-close break in the intended direction: bullish for a buy or bearish for a sell. Exact corrective-swing geometry remains unresolved."),
            Rule("NQ-M1-003", "Entry mechanics", "Entry", 190, false, RuleDefinitionStatus.Unresolved,
                "Entry eligibility requires all mandatory Steps 1 through 6 to complete in chronological order within the active pre-entry setup before 11:00 America/Bogota. A late isolated 1M signal cannot complete an expired setup, and a target consumed before eligibility cancels the setup under the do-not-chase-price principle; runtime gate-status mapping and order mechanics remain unresolved."),
            Rule("NQ-SL-001", "Stop Loss reference", "Stop Loss", 200, true, RuleDefinitionStatus.HumanValidationRequired,
                "For a buy, place Stop Loss below or beyond the wick of the latest relevant structural 5M HL; for a sell, above or beyond the wick of the latest relevant structural 5M LH. Swing detection and the exact offset remain unresolved."),
            Rule("NQ-TP-001", "Important liquidity target", "Take Profit", 210, false, RuleDefinitionStatus.HumanValidationRequired,
                "Use important upside liquidity or highs for buy targets and important downside liquidity or lows for sell targets; relevance, priority, and management remain human-validated or unresolved."),
            Rule("NQ-TP-002", "Asia/London liquidity targets", "Take Profit", 220, false, RuleDefinitionStatus.HumanValidationRequired,
                "Relevant Asia High or London High are buy target candidates, and relevant Asia Low or London Low are sell target candidates; neither session has automatic priority."),
            Rule("NQ-BE-001", "Distinct BE swing", "Break Even", 230, false, RuleDefinitionStatus.Unresolved,
                "Earlier evidence distinguished a post-entry swing from the entry swing, but its relationship to the authoritative first-important-liquidity Break-Even trigger remains unresolved; do not substitute one for the other."),
            Rule("NQ-BE-002", "First-liquidity touch to BE", "Break Even", 240, false, RuleDefinitionStatus.HumanValidationRequired,
                "After entry, reaching or touching the first important favorable upside liquidity for a buy or downside liquidity for a sell moves Stop Loss conceptually to entry; target selection, trigger geometry, and exact Break-Even price remain unresolved."),
            Rule("NQ-BE-003", "Universal BE use", "Break Even", 250, false, RuleDefinitionStatus.Candidate,
                "Preserve universal Break Even application as a candidate rather than a rule for every trade."),
            Rule("NQ-TIME-002", "Trading-window end", "Schedule", 260, true, RuleDefinitionStatus.Confirmed,
                "Treat local times before 11:00 America/Bogota as inside the Nasdaq pre-entry operational period; at 11:00 and later, the setup/entry cutoff has been reached. This does not require closing existing positions or end post-entry management."),
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
