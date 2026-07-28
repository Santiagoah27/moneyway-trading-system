# MoneyWay Nasdaq reference cases

These cases preserve observed or conceptual evidence. They do not establish success rates or fill missing thresholds.

## NQ-CASE-001 — Conceptual valid setup

- Case ID: `NQ-CASE-001`.
- Scenario: ideal conceptual sequence with liquidity sweep, 5M inversion, continuation FVG, 1M retracement, realignment and entry.
- Rules demonstrated: mandatory stage order and the distinction between setup evidence and entry confirmation.
- Rules not demonstrated: empirical success rate, exact swings, FVG threshold, order type, Stop Loss selection, Take Profit or complete risk sizing.
- Context-specific decisions: none promoted to a general rule.
- Generalization risks: treating an ideal diagram as performance evidence or a deterministic detector.
- Human-review notes: each subjective stage still requires validation.
- Automation impact: useful for manual sequence review; insufficient for full automation.

## NQ-CASE-002 — Missing 1M confirmation

- Case ID: `NQ-CASE-002`.
- Status: `rejected_setup_reference_case`.
- Scenario: price took liquidity; 5M inversion appeared valid and displacement existed, but 1M produced no entry trigger.
- Rules demonstrated: liquidity and 5M confirmation do not bypass the required 1M retracement/realignment.
- Rules not demonstrated: exact 1M timeout, entry order, Stop Loss or target.
- Context-specific decisions: rejecting the observed setup due to missing 1M confirmation.
- Generalization risks: entering from the liquidity sweep, displacement or 5M FVG alone.
- Human-review notes: preserve `waiting` until confirmation; if realignment never occurs, use `no_trade`.
- Automation impact: provides a negative sequence case, but swing detection remains subjective.

## NQ-CASE-003 — FVG insufficient quality

- Case ID: `NQ-CASE-003`.
- Status: `human_validation_reference_case`.
- Scenario: a one- or two-point FVG was considered insufficient to demonstrate strength.
- Rules demonstrated: continuation FVG quality is mandatory and qualitative.
- Rules not demonstrated: a three-point minimum, ATR ratio, percentage or universal size threshold.
- Context-specific decisions: human rejection of the observed small FVG.
- Generalization risks: converting the observation into `minimum_size_points: 3`.
- Human-review notes: `minimum_size_points: null`; assess clear/evident space manually.
- Automation impact: blocks deterministic FVG-quality approval.

## NQ-CASE-004 — Stop Loss ambiguity

- Case ID: `NQ-CASE-004`.
- Status: `unresolved_reference_case`.
- Scenario: sweep extreme and structural 5M HL/LH were both reported as Stop Loss references, with opposing wide/short selection versions.
- Rules demonstrated: both references exist and human validation is required.
- Rules not demonstrated: which version is correct or any sweep-size threshold.
- Context-specific decisions: neither version selected.
- Generalization risks: forcing Stop always at wick or always at 5M HL/LH.
- Human-review notes: verify original timestamps and obtain unambiguous cases.
- Automation impact: blocks fully automatic demo execution.

## NQ-CASE-005 — News example

- Case ID: `NQ-CASE-005`.
- Status: `context_specific_reference_case`.
- Scenario: for a news event observed at 10:00, the action observed at 09:45 was `close_all`.
- Rules demonstrated: news can affect active-position management in a specific case.
- Rules not demonstrated: universal 15-minute close, event scope, calendar, no-trade day or restart policy.
- Context-specific decisions: `observed_news_time: "10:00"`, `observed_close_time: "09:45"`, `observed_action: close_all`.
- Generalization risks: applying the same window to every news event.
- Human-review notes: retain `universal_close_minutes_before: null`.
- Automation impact: no general news automation is permitted.

## NQ-CASE-006 — Break Even clarification

- Case ID: `NQ-CASE-006`.
- Status: `clarified_reference_case`.
- Scenario: the entry swing differs from the post-entry Break Even swing. An initial “touch” explanation was later clarified as break and 1M candle close beyond the post-entry swing.
- Rules demonstrated: distinct swings and precedence of the more specific break-and-close explanation.
- Rules not demonstrated: post-entry swing algorithm, universal BE use, cost adjustment or subsequent management.
- Context-specific decisions: later clarification retained without deleting the historical contradiction.
- Generalization risks: using the entry swing for BE or applying BE universally without evidence.
- Human-review notes: `mandatory_for_every_trade: candidate`.
- Automation impact: event logic is clearer, but subjective swing selection still blocks automation.

## Cross-case boundaries

- No case supplies missing dates, prices, instruments, profits or losses.
- No conceptual case is performance evidence.
- Context-specific decisions remain non-universal.
- Human validation is mandatory wherever thresholds, swing selection or contradictory evidence remain unresolved.
