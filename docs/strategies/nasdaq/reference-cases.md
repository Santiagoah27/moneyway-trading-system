# MoneyWay Nasdaq reference cases

These cases preserve observed or conceptual evidence. They do not establish success rates or fill missing thresholds.

## NQ-CASE-001 — Conceptual valid setup

- Case ID: `NQ-CASE-001`.
- Scenario: ideal conceptual sequence with liquidity sweep, 5M inversion, continuation FVG, 1M retracement, realignment and entry.
- Rules demonstrated: mandatory stage order and the distinction between setup evidence and entry confirmation.
- Rules not demonstrated: empirical success rate, exact swings, FVG threshold, order type, deterministic Stop Loss geometry, deterministic important-high/important-low selection or complete risk sizing.
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

## NQ-CASE-004 — Stop Loss source reconciliation

- Case ID: `NQ-CASE-004`.
- Status: `clarified_reference_case`.
- Scenario: an earlier interpretation treated the sweep extreme and structural 5M HL/LH as competing Stop Loss references.
- Reconciliation: manual source-video re-verification resolved the conceptual rule in favor of the structural 5M HL for buys and structural 5M LH for sells.
- Rules demonstrated: directional structural Stop Loss guidance and the requirement that the stop represent where the trade idea loses structural meaning.
- Rules not demonstrated: deterministic HL/LH detection, structural swing geometry, buffer, spread or maximum-distance treatment.
- Generalization risks: inventing a swing detector or a numeric buffer from the conceptual guidance.
- Human-review notes: structural selection remains `human_validation_required` until its geometry is formally defined.
- Automation impact: the prior source contradiction is resolved, but deterministic Stop Loss placement remains blocked.

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

## NQ-CASE-007 — Bearish reset/invalidation collision

- Case ID: `NQ-CASE-007`.
- Status: `context_specific_reference_case`.
- Scenario: one formally closed candle prints `Low < current correction-origin floor` and closes with `Close > prior validated LH`.
- Structural rules demonstrated: both observations remain true, prior-LH invalidation dominates the transition, the candidate LH is destroyed, the candle is excluded from the old bearish correction and candidate geometry, and no new bearish correction starts. The candle belongs to the new bullish impulse context from its causal close.
- Lower-extreme role: the collision `Low` remains a lower price extreme, bullish-origin reference and wick-based protection evidence. It is not automatically a validated HL, Structural Low or executable Stop Loss.
- Context-specific Step-3/Step-4 evidence: the mentor interprets the lower wick/new Low as liquidity take and the body close above prior LH as structural change, with the candle beginning the bullish impulse.
- Rules not demonstrated: universal mapping of a structural collision to `NQ-LIQ-003` or `NQ-M5-001`, selected-liquidity qualification, 4H alignment, timeframe satisfaction, first-valid-liquidity selection, same-frame prerequisite propagation or permission to skip any workflow gate.
- Gating discrepancy: canonical progression currently determines eligibility from prerequisites established before the current replay frame. If future human validation maps both events to their canonical gates at the same causal boundary, Step 4 cannot become eligible in that same frame under current behavior.
- Generalization risks: treating every lower wick as Step 3, every prior-LH close as canonical Step 4, or using the reviewed candle to bypass workflow prerequisites.
- Human-review notes: retain the structural facts and narrow interpretation without inventing intrabar chronology or changing prior snapshots.
- Automation impact: useful as a structural collision and gating-review case; insufficient to authorize RuleId mapping or runtime changes.

## NQ-CASE-008 — Distinct extreme candle before correction

- Case ID: `NQ-CASE-008`.
- Status: `confirmed_structural_boundary_reference_case`.
- Bullish: candle A produces the current HH with a bullish body, then distinct candle B is the first bearish body. A belongs to the preceding bullish impulse; the candidate HL correction turn begins with B, and A contributes no OHLC to that turn's structural price or protection anchor.
- Bullish exact-doji variant: candle A produces the current HH with `Close == Open`; distinct candle B is the first bearish body. A remains outside the correction at the prior impulse boundary; B starts it.
- Bearish mirror: candle A produces the current LL with a bearish body, then distinct candle B is the first bullish body. A belongs to the preceding bearish impulse; the candidate LH correction turn begins with B.
- Same-candle boundary: if the new-High/new-Low reset candle itself has the opposite body direction, existing reset-and-start rules include that candle as the first member of the new turn; this distinct-candle case does not change that rule.
- Rules not demonstrated: near-doji threshold, initial structural anchor selection and historical scan termination.
- Automation impact: defines membership for these reviewed pre-start cases; supplied-turn geometry primitives do not select the turn automatically.

## NQ-CASE-009 — Pre-start waiting candles

- Case ID: `NQ-CASE-009`.
- Status: `confirmed_structural_boundary_reference_case`.
- Normal bullish start: A produces the HH; B is bullish without a new High; C is an exact doji; D is the first bearish body. A belongs to the prior impulse, B/C remain pre-start waiting outside correction, and the correction turn begins `[D, ...]`. A/B/C contribute no OHLC to that turn's structural price or protection anchor.
- Extreme extension while waiting: after an upper extreme of `100`, a bullish B prints `High = 102` and a following exact doji C remains waiting. The upper price extreme/ceiling updates under existing rules, but B/C do not start or join a correction. The first later bearish body starts it. A bearish waiting candle with a strict lower `Low` mirrors this floor update before the first bullish body.
- Reset and await: a bullish-body new-High reset candle A terminates the old correction but starts no new one. Later bullish B and exact-doji C also remain outside. The first bearish D starts a new turn containing D, with no earlier reset/waiting candle included. The bearish-side mirror waits for the first bullish body.
- Active-turn distinction: after D starts the correction, in-range candles of either body direction and exact dojis may remain in that turn under existing rules.
- Rules not demonstrated: a near-doji threshold, numeric exhaustion-zone width, initial H4 anchor, historical scan termination or a fully automatic detector.
- Automation impact: the pre-start membership rule is deterministic when an extreme and direction are supplied; current start/lifecycle primitives consume these observations, but no complete detector assembles candidate turns from arbitrary history.

## NQ-CASE-010 — Prior structural validation boundary inside an active turn

- Case ID: `NQ-CASE-010`.
- Status: `confirmed_structural_boundary_reference_case`.
- Bullish: with an active candidate-HL turn and prior structural HH `100`, a closed candle with `High = 100` or `Close = 100` does not confirm. Nor does `High = 101, Close = 100`. If no separate reset/invalidation event occurs, each candle remains in the same turn and its OHLC is eligible for the selected turn's `StructuralPrice` and `ProtectionAnchor`; a later deeper low may replace the candidate.
- Distinct reset level: the current correction-origin ceiling can differ from prior HH. With ceiling `102`, `High = 101, Close = 100` is non-confirming and remains included absent invalidation. With `High = 103`, the existing strict ceiling reset governs even if `Close <= 100`; no confirmation-only membership rule overrides it.
- Confirmation: a formally closed `Close > 100` validates the preceding candidate from that close and excludes the confirming candle from the old turn and its selected-turn geometry.
- Bearish mirror: replace prior HH with prior LL, ceiling with floor, `High >` with `Low <`, and confirmation with a formally closed `Close < prior LL`. Equality and wick-only penetration remain included absent another terminal event; a strict floor reset still governs independently.
- No intrabar chronology, new geometry formula, target-touch rule or liquidity-take rule follows from these examples.

## NQ-CASE-011 — Bullish candidate confirmation and origin extension

- Case ID: `NQ-CASE-011`.
- Status: `confirmed_structural_boundary_reference_case` for old-candidate validation and active-pair rollover; next-turn membership remains `unresolved`.
- An active candidate-HL turn has prior structural HH `100` and current correction-origin ceiling `102`. A formally closed candle with `High = 105`, `Open = 104`, `Close = 103` satisfies both strict `Close > prior HH` and strict `High > ceiling`.
- The prior candidate HL validates from this close. Its existing selected-turn `StructuralPrice` and `ProtectionAnchor` exclude the confirming candle. That candle's upper extreme supplies the new HH-side reference; the operational pair becomes new HH plus newly validated HL. Generic reset does not discard the old candidate.
- The candle has a bearish body (`Close < Open`). Whether it also starts the *next* bullish-structure correction is unresolved; do not apply ordinary reset/start membership to this compound event.
- The bearish `Close < prior LL` plus `Low < origin floor` mirror lacks explicit collision precedence in the reviewed source. Reset plus invalidation remains a distinct, confirmed invalidation-dominant collision.
- The prices are illustrative; this case defines no intrabar chronology, new HH geometry algorithm or trading workflow mapping.

## Cross-case boundaries

- No case supplies missing dates, prices, instruments, profits or losses.
- No conceptual case is performance evidence.
- Context-specific decisions remain non-universal.
- Human validation is mandatory wherever thresholds, geometry or swing selection remain unresolved.
