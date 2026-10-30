# ADR 0026: Define Nasdaq Wickfill Scenario 2 applicability

## Status

Accepted for specification of a `confirmed`, `context_specific` SELL branch. Full branch workflow and entry support remain unimplemented. The limited human-assisted rule implementation update below supersedes earlier statements that no Scenario 2 evaluator exists; the original specification-only decisions remain historical.

## Date

2026-10-14.

## Context

The [audited Nasdaq draft](../strategies/nasdaq/strategy-specification.md) and [ADR 0025](0025-define-nasdaq-demo-human-evidence-contract.md) describe the implemented standard pre-entry chain: preparation/context, valid opening liquidity take, closed M5 StructuralChange OR IFVG, optional continuation FVG support, M1 corrective retracement and realignment, then derived bounded eligibility. Applying that chain to every historical Nasdaq execution treated Replay #2's approximately 08:32 mentor entry as necessarily anticipating the M5 confirmation at 08:35.

The new human-reviewed source distinguishes a Wickfill setup with pre-established higher-timeframe context. Its existence does not invalidate the standard sequence or justify a universal intrabar NQ-M5-001 trigger.

### Source provenance

Source S1 is the user's explicit human-reviewed evidence supplied in the task **“MoneyWay Trading System — Nasdaq Wickfill Scenario 2 Specification”**, received 2026-10-14. Retained attachment identity: `a348f363-cf13-4fd8-bb90-4bd329420656`; source text SHA256: `a25ab54c4b8b2d08ee1826ba7ad6e756c8c9f5aad79bf61e2e6b527b09a10702`.

The cited original is [JacoggFX's mentor video](https://www.youtube.com/watch?v=QcN8dE8ry6I). The supplied review gives these references:

| Original-video reference supplied by S1 | Tight paraphrase of the human-reviewed evidence | Scope |
|---|---|---|
| [06:40–06:55](https://www.youtube.com/watch?v=QcN8dE8ry6I&t=400s) | Taking a trade near the opening requires an already well-organized structure/context and clear prior corrective movement. | Context supporting early-session execution; no numerical detector |
| [38:02–39:40](https://www.youtube.com/watch?v=QcN8dE8ry6I&t=2282s) | Scenario 1 cannot immediately sell while price remains at the lower part of the range; it must first retrace to the relevant zone during the session. In Scenario 2 price is already at the relevant H4 zone before opening; the opening liquidity sweep and subsequent rejection/break remain necessary. | Historical S1 general-context attribution; not causal Replay #2 entry evidence. S3 below explicitly excludes 38:43–39:03 as entry-trigger or geometry evidence |
| Replay #2 opening/execution observations in S1; separate video offsets not supplied | The mentor wanted the opening move above London High and said he would not enter without that sweep. After the sweep/rejection he used a Sell Stop on M30, executing approximately 08:32. M1 and a pre-entry FVG or M5 SC/IFVG were not used; M5 was reviewed later around 08:35 as confluence. | Historical session observations, not universal order or M30 mechanics |

These are attributed human-reviewed paraphrases, not a newly extracted transcript. The earlier assistant access check retrieved video metadata but no usable captions/audiovisual content; it did **not** independently verify the full video. S1 is the current explicit human confirmation. This record makes no invented quotation, missing video offset or claim of independent audiovisual inspection.

Source S2 is the user's explicit clarification **"MoneyWay Trading System - Clarify Wickfill Scenario 2 Opening Sweep References"**, received 2026-10-14. Retained attachment identity: `e4171731-28e6-4016-916a-2c78df79ac13`; source text SHA256: `d82751292ffa97ec982787a140f37e742b6c968c82851800a416763a5f987ae1`. S2 confirms the current admissible endpoint set `{LondonHigh, LondonLow}`, with setup/session-specific human relevance selection. It supplies no new video quotation or autonomous selection algorithm. Section 2.1 records its explicit clarification of S1's reference scope; S1's historical Replay #2 observations remain intact.

Source S3 is the user's authoritative human-source reconciliation **"MoneyWay Trading System - Close Wickfill Scenario 2 Post-Sweep Human Evidence Audit"**, received 2026-10-15. The reviewer personally checked the original mentor video. Retained attachment identity: `4482ca53-78dc-4ad0-a995-20816f5d9878`; source text SHA256: `4dc3157d74f88ae29e88edff2bd24271ab935c8a46608c2d43747bd1e9b2717f`. S3 supplies reviewed paraphrases and source-scope corrections, not a new verbatim transcript or an independent audiovisual audit by the assistant. It supersedes the post-sweep conclusions of the prior AI-assisted report (attachment `8db55eb2-3912-4214-9aca-f2576797f45b`); that report is not the final source of truth. Section 2.2 defines the accepted evidence and limits. No prices, exact placement coordinates or universal no-close rule from that report are adopted.

Source S4 is the user's direct human-source decision **"MoneyWay Trading System - Confirm Wickfill Scenario 2 SELL Entry Eligibility Boundary"**, received 2026-10-15. Retained attachment identity: `a4f61769-b2df-4488-a978-1ff87e1c3bf3`; source text SHA256: `4f2ca689aa2382f5acb9430a70be5555332ac511902fccb202501febcd2a2f45`. The reviewer directly rechecked original-video 08:18–08:27 and confirms that the reviewed historical SELL followed London-high take/exceed -> bearish turn -> consideration and execution of entry. S4 explicitly closes the strategic sufficiency boundary for scoped SELL eligibility under section 2.3. This is an attributed human-reviewed paraphrase and decision, not a verbatim transcript or a new independent audiovisual inspection by the assistant. The video offset is a source location, not a UTC event/availability timestamp.

Source S5 is the user's explicit human review **"MoneyWay Trading System - Document Human-Reviewed Scenario 2 SELL Initial Stop Loss Evidence from Replay #2"**, provided 2026-10-20. Retained decision-text attachment identity: `2b2a3820-aa95-46ce-acf7-d434112d7fbe`; verified decision-text SHA256: `0aa95a610a6f13872bc93d6d69b2ea19721a486e340d9059f33fd31cf76a8682`. The reviewer personally examined the original session and reports annotated MT5 screenshots supplied in the ChatGPT conversation. The underlying screenshot is **external / not repository-resolved**: image bytes, attachment identity, filename and image hash have not been verified here. The text hash identifies the human decision only, not a screenshot or video. This records the explicit human decision and attributed visual observations, not independent audiovisual inspection by the assistant or canonical runtime evidence. S5 supplies no exact video offset, candle timestamp or native replay-source mapping. Section 2.4 records its historical scope; S1-S4 source history remains intact. Earlier Google AI analysis is corroborative at most; its unverified quotations, timestamps and candle identifications are not adopted.

Source S6 is the strategy owner's explicit decision **"MoneyWay Trading System - Confirm Scenario 2 SELL Sweep-Peak Initial Stop Loss Strategy Contract"**, dated 2026-10-20. Retained decision-text attachment identity: `b40859c9-8800-44a0-a93a-0b18d91f8293`; verified decision-text SHA256: `cff13968c5df63ded6010229456f48fb69ef2bc17027a53f226443ce5c9ff5bd`. This is direct human strategy authority, not AI-derived geometry or a new assistant audiovisual inspection. The owner additionally identifies S5's historically indicated wick as the peak of the initiating liquidity sweep. S5's original text, hash and provenance remain unchanged; its underlying screenshot remains external / not repository-resolved. This text hash verifies only the new decision text, not image/video bytes. Section 2.5 records the newly confirmed general SELL protective-reference concept and its unresolved deterministic limits.

Source S7 is the strategy owner's explicit approval **"MoneyWay Trading System - Confirm Deterministic Sweep Interval Boundaries for Scenario 2 SELL Stop Loss"**, dated 2026-10-21. Retained decision-text attachment identity: `8be61a97-18eb-4c3d-956c-4853a3afc940`; verified decision-text SHA256: `8c26d96ba85d4983ed796f96961a02f95104e081fb47ef2851f4e1bcfc5d4abf`. This is a human-approved operational formalization of the mentor's visual concept, **not a verbatim mentor algorithm**, AI inference or new assistant audiovisual review. Section 2.6 defines the measurement interval using the existing initiating-take and confirmed-reversal ancestry. The hash identifies this decision text only, not screenshot/video bytes. S5 and S6 identities, hashes, provenance and historical sections remain unchanged; S5's screenshot remains external / not repository-resolved.

Source S8 is the user's direct message **"MoneyWay — Register Human-Reviewed Replay #2 Pre-Open Evidence"**, received 2026-10-30. The user selected three screenshots from the original September 8 mentor recording and supplied their chart-clock, timeframe and content descriptions. This records attributed human-selected screenshot evidence, not an independent assistant inspection of image/video bytes or a verbatim transcript. The screenshots remain external / not repository-resolved: no image bytes, repository path, attachment identity or image hash are supplied for this record. No screenshot or decision-text hash is invented. S1–S7 provenance and existing hashes remain intact. Section 2.7 records only the new screenshot-supported observations, the user's explicit supersession of the older historical Fakeout label, and temporal/provenance limits; it does not create typed replay observations.

### Version lifecycle

The verified baseline is `main` at `9f42fbc8431d6deb6acf528767c603c5106e471c`. The registered version is `nasdaq-0.1.0-draft`; documentation labels it draft, and the definition has no ACTIVE lifecycle marker or promotion mechanism. No ACTIVE version is changed. This is a traceable specification correction to that audited draft, not candidate activation. Any future executable branch must follow the project's versioning/approval rules; this decision does not select its executable version or promote it.

## Decision

### 1. Preserve the standard path

The current standard M5/FVG/M1 contracts and chronology remain valid for their applicable setups. NQ-M5-001 retains its normal M5 formation/confirmation path; no universal intrabar permission is introduced. Continuation FVG remains optional as already corrected in ADR 0025. Selected rejected/conflicting/unavailable evidence retains its existing safe handling.

Wickfill Scenario 1 illustrates the need to complete a still-missing corrective move before an immediate sell is possible. It does not supply a new detector, or establish that every standard setup must be labeled Scenario 1.

### 2. Distinguish Wickfill Scenario 2

For the supported SELL branch:

1. The higher-timeframe bearish Wickfill context is already established before the 08:30 `America/Bogota` opening.
2. The macro upward corrective move into the relevant H4 resistance/supply zone is already complete before opening. This is not automatically the standard NQ-M1-001 event.
3. Asia/London references are identified; the trade-relevant opening London High sweep is still outstanding.
4. The opening movement must perform that relevant sweep. Without it, no entry for this setup.
5. With that prerequisite and the source-reviewed subsequent behavior, this contextual setup can support earlier execution without reconstructing the entire standard post-opening M5/FVG/M1 chain.

Macro context and the required opening sweep are distinct. “The market already spoke” must not be interpreted as pre-satisfying the opening sweep. A prior numerical crossing in a different feed or episode is not automatically this source-selected opening event. No penetration threshold is introduced.

The branch concept is `confirmed`; its applicability is `context_specific`. Human selection of the H4 zone, completion of the corrective move and sufficient post-sweep behavior remain `human_validation_required`. The sweep alone is not an automatic order, fill or fully specified eligibility algorithm.

### 2.1 Opening-sweep reference clarification (2026-10-14, S2)

**Current normative scope.** The required Scenario 2 opening sweep uses a human-reviewed relevant London-session endpoint:

```text
Scenario 2 pre-open context
-> human selection of LondonHigh OR LondonLow for this setup/session
-> required opening sweep of that exact selected endpoint
-> future reviewed post-sweep behavior
```

| Assertion | State | Boundary |
|---|---|---|
| Required opening-sweep reference belongs to `{LondonHigh, LondonLow}` | `confirmed` | Current closed admissible set; this is not an arbitrary SessionLevel or Structural reference |
| The exact selected endpoint must be swept | `confirmed` | Existing strict take arithmetic: High requires `ObservedPrice > ReferencePrice`; Low requires `ObservedPrice < ReferencePrice`; equality is insufficient and no penetration threshold/tolerance is added |
| Which London endpoint is relevant for the setup/session | `human_validation_required` | Retain the human's exact selection and provenance; no automatic winner |
| Autonomous endpoint-selection algorithm | `unresolved` | No selection or side-selection detector is defined |
| Expansion to AsiaHigh, AsiaLow or structural/session alternatives | `unresolved` | Not admitted by the current definition; future expansion is candidate research/evidence only |
| BUY-specific branch semantics or universal branch-direction/endpoint mapping | `unresolved` where not independently confirmed | The existing supported branch remains SELL-only; the endpoint set does not establish BUY symmetry |

This clarification supersedes any generic reading of section 2 item 3 as "Scenario 2 always uses LondonHigh", and any reading of "trade-relevant opening sweep" as admitting any canonical High/Low reference. Replay #2 retains its source-selected `LondonHigh`; that historical endpoint does not define the generic set. London commonly breaking Asia liquidity is a human research observation, not authorization for Asia or structural fallback.

The endpoint restriction composes with the existing reusable SessionLevel/NQ-LIQ-003 contracts. A future Scenario 2 composition may accept a canonical take only when its reference is `NasdaqLiquidityTakeReference.SessionLevel` with `Endpoint` equal to `LondonHigh` or `LondonLow`, and all other applicable canonical requirements hold. Membership in that set is necessary, not sufficient for a usable take or an entry. This clarification adds no Scenario 2 directional mapping; existing canonical H4 alignment and strict arithmetic remain unchanged. It does not require NQ-LIQ-002 for this SessionLevel path or modify NQ-LIQ-001/002/003.

The opening-sweep composition audit's reference-set specification blocker is resolved by S2. This note implements no composition, selection algorithm, evaluator, workflow, eligibility, order, Replay #2 binding or trading-window change. Post-sweep behavior and the other unresolved boundaries below remain separate.

### 2.2 Post-sweep bearish turn: human-source reconciliation (2026-10-15, S3)

S3 closes the human-source audit for the currently evidenced SELL Scenario 2 concept. At that decision it did not close the deterministic algorithm or final contextual eligibility contract. S4 in section 2.3 now closes the scoped SELL eligibility boundary only; autonomous reversal geometry remains unresolved. The source-supported S3 sequence is:

```text
Reviewed pre-open Scenario 2 context
-> human-selected London endpoint under section 2.1
-> required canonical strict liquidity sweep
-> human-recognized bearish turn / reversal
-> consideration of a future SELL early-entry path
```

| Video reference reviewed by S3 | Authoritative evidence treatment | Classification / boundary |
|---|---|---|
| [08:18–08:27](https://www.youtube.com/watch?v=QcN8dE8ry6I&t=498s) | The mentor wants price to take/exceed the London highs, then turn bearish, and from there consider a SELL. The reviewer confirms that the historical trade subsequently follows this sequence. | `confirmed` strategy concept; recognition remains `human_validation_required`, deterministic geometry `unresolved` |
| [06:11–06:28](https://www.youtube.com/watch?v=QcN8dE8ry6I&t=371s) | A response to a student's hypothetical trade without first taking liquidity; described as risky. It is not the causal evidence for Replay #2 Scenario 2 progression. | Inferring a required green first session candle from this passage is `rejected_ai_inference`; do not encode it |
| [38:43–39:03](https://www.youtube.com/watch?v=QcN8dE8ry6I&t=2323s) | Later conversation after the historical trade had resolved around video 18:08; not authorized by S3 as causal entry evidence for Replay #2. | Inferring a required structural break, entry trigger or deterministic geometry from this passage is `rejected_ai_inference`; general strategy interpretation requires a separate future audit |

Video offsets, including the approximate trade-resolution reference at 18:08, are source locations, not exact UTC market-event or historical assertion-availability timestamps. They cannot backfill replay. S3's treatment takes precedence over conflicting interpretations in the AI-assisted report and over using the later S1 context-discussion citation to define Replay #2's entry condition; the earlier attributions are retained as historical reasoning only within those limits.

**Confirmed concept:** after the required London sweep, the currently evidenced SELL branch requires a bearish turn/reversal before considering entry. This is not an authorized mapping to CHoCH, a structural break, NQ-M5-001, M1 realignment, an exact pivot break or exact candle geometry. Earlier wording such as "rejection/break" must not be read as supplying any of those deterministic conditions. The sweep or the reviewed turn alone does not establish executable eligibility, an order or a fill.

**Human validation:** recognition that the bearish turn/reversal has occurred remains `human_validation_required`. Autonomous recognition is `unresolved`: no swing algorithm, wick ratio, candle-body rule, close rule, momentum threshold, return-through-level or return-distance threshold, candle count, timer or penetration buffer is defined. Universal candle-close requirements and non-requirements remain `unresolved` unless independently specified; historical execution before a close does not establish a universal no-close rule.

**Historical execution only:** M30 use and the pending SELL STOP in Replay #2 remain `confirmed`, `context_specific` facts. Neither is a universal Scenario 2 requirement. Universal M30 use, universal Sell Stop use and exact Sell Stop geometry/mechanics remain `unresolved`. No BUY-side equivalent is confirmed.

Canonical NQ-LIQ-003 strict-take semantics and section 2.1's reference set remain unchanged, with no additional penetration threshold. Standard-path NQ-M5-001 retains closed-M5 confirmation and does not exactly represent this human-reviewed bearish turn; do not reuse or modify it for this purpose. This clarification changes documentation only: no new observation, rule, evaluator, metadata, workflow, lifecycle, order logic, TIME rule, Forex behavior or Replay #2 binding is introduced.

### 2.3 Scoped SELL entry-eligibility boundary (2026-10-15, S4)

**Current normative scope: `confirmed`, currently evidenced Wickfill Scenario 2 SELL only.** For the same canonical setup explicitly classified `WickfillScenario2`, valid pre-open Scenario 2 context, the valid sweep of the selected relevant London endpoint, and a confirmed human-reviewed bearish reversal complete all currently confirmed pre-entry strategic conditions for scoped eligibility. S4 confirms that no additional strategic confirmation is currently evidenced between that reversal and eligibility:

```text
Valid reviewed pre-open Scenario 2 SELL context
-> valid selected London opening sweep
-> confirmed human-reviewed bearish reversal
-> scoped strategy entry eligibility
```

Thus a future dedicated Scenario 2 eligibility rule/fact may derive from the exact canonical `NQ-WF2-001 Passed` reversal fact, retaining its setup-specific applicability and initial-take ancestry and only while the setup survives existing terminal protections. It must not re-detect reversal or reuse NQ-M1-003 semantically. This closes the eligibility-sufficiency question left open by S3 and supersedes earlier `unresolved` statements about that specific SELL boundary; it neither closes autonomous reversal geometry nor generalizes BUY or endpoint-direction mappings.

**Meaning and time.** Eligibility states only that the currently confirmed Scenario 2 SELL pre-entry strategy conditions are satisfied. It does not establish order type, a required Sell Stop, submission, fill, an opened trade, entry price, SL, TP, sizing, risk approval or a necessarily Ready StrategyVerdict. Because no additional market event is required, `EligibilityEffectiveAtUtc = ReversalEffectiveAtUtc`. A later canonical transport/emission frame retains that original effective time; all relied-upon evidence and the owning canonical fact must actually be observable before consumption. No synthetic later event, new boundary, same-frame hidden transport or historical backfill is authorized.

**Execution/source limits remain.** Replay #2's pending Sell Stop and M30 chart are historical/context-specific facts. No universal Sell Stop, exact order level, tick/pip offset or "zone lows" geometry is added. Mandatory M30, M30/M5/M1 close and a universal no-close rule are not introduced; universal close mechanics, executable price and BUY Scenario 2 eligibility remain unresolved. The future derivation consumes the already canonical human reversal fact, without automating recognition.

**Confirmed semantics; integration not implemented.** This clarification creates no new RuleId, evaluator, fact, metadata/capability, workflow or runtime behavior. The existing Required Standard rules NQ-M5-001 and NQ-M1-001/002 do not yet consume Scenario 2 applicability. NQ-M1-003 also remains Standard-specific: its metadata is not globally Required, but it is the existing workflow/lifecycle completion dependency. NQ-SL-001 consumes the Standard `NasdaqPreEntryEligibilityRuleFact`; downstream canonical progression and lifecycle still recognize that Standard ancestry. Closing the SELL semantic boundary does not make Scenario 2 globally runnable or waive these dependencies. Branch integration remains unresolved/unimplemented and needs its own contract; no Standard rule may be fake-Passed, supplied fabricated M5/M1 facts or silently skipped. Existing Standard M1 eligibility, NQ-LIQ-003, NQ-WF2-001, TIME, Forex and Replay #2 binding remain unchanged. Independently unresolved SL/TP/order mechanics remain separate.

### 2.4 Replay #2 initial Stop Loss and later management (2026-10-20, S5)

**Historical interpretation: `context_specific`, supported by human-reviewed visual evidence.** During the historical Wickfill Scenario 2 SELL operation, the mentor visually positioned the initial Stop Loss above the upper wick/high of the candle identified by the human reviewer while operating on the M30 chart. The reviewer describes it as the "second candle" in the displayed local sequence; this is a screenshot description, not an ordinal-selection rule. The reported display shows MT5, USTEC, M30 selected, an existing SELL position, a red dotted SL line above the indicated wick and a visible SL value in the trade table. No historical numeric price is adopted as a strategy constant, stop buffer or native MT5 US100 replay coordinate.

Keep two historical phases separate:

- **Initial protection:** visual selection of the indicated upper wick/high and initial SL above it. In the historical narrative this belongs after scoped eligibility; it does not redefine NQ-WF2-002 or establish runtime availability.
- **Post-entry management:** after allowing the trade to develop, the mentor later moved the SL to secure a 1:1 result, according to the reviewer. This does not equate initial SL, Break-Even, protected +1R, realized profit, TP or first-trade session termination. Universal movement trigger, precise R calculation, adjustment time/price and autonomous management remain unresolved.

S5 does **not** confirm that the mentor explicitly selected an M5 structural LH for this initial SL. It also does not establish that no corresponding M5 LH existed. Preserve the general audited Nasdaq 5M HL/LH concept and the existing Standard implementation within their confirmed scope; neither is deleted or automatically extended to this historical M30 reference. The existing M5 protection anchor does not accept an M30 source by reinterpretation.

Universal Scenario 2 SELL SL selection remains `unresolved`. No always-M30, second-candle, sweep-candle, highest-opening-high, most-recent-high, one-tick/other offset or closed-M30 requirement is established. Exact protective candle identity/time, stop offset and execution mechanics are not resolved by pixels, AI commentary or the reviewer-relative candle description. No exact Bogota timestamp or video offset is assigned.

**Candidate for future audit only:** a human-assisted Scenario 2 input might retain exact WF2 eligibility ancestry, the human-selected protective reference and timeframe, exact sources, separate effective/observed times, independently documented StopPrice and terminal protections. This is not an approved architecture or implemented observation. Branch-specific SL contract, safe extension of NQ-SL-001, downstream SL/risk/TP integration, execution and Scenario 2 BUY remain unresolved. Eligibility and pre-entry lifecycle completion still imply neither SL/risk approval, execution nor global Ready.

**Separate technical concern:** the preceding read-only audit found by inspection that `NasdaqLiquidityTakeRuleFact.Blocks` does not explicitly include NQ-SL-001 in same-frame liquidity invalidation checks. This is not a reproduced test failure and is not resolved by S5. Runtime investigation remains separate; no protection, evaluator, workflow, lifecycle, metadata, TIME or replay binder changes are made here.

### 2.5 Scenario 2 SELL sweep-peak initial SL concept (2026-10-20, S6)

**Current normative scope: `confirmed` strategy concept, Wickfill Scenario 2 SELL only.** The initial SL protects the maximum price reached during the liquidity sweep that initiated this exact setup. Its placement relationship is **`StopPrice > protected sweep peak`**. Subsequent price exceeding the protected reference invalidates the bearish post-sweep reversal thesis as a strategic concept. This confirms the protective reference; it does not define a complete deterministic selection, breach-detection or execution algorithm.

S6 identifies S5's visually selected historical wick as that initiating sweep peak. S5 remains `context_specific`: MT5 USTEC M30 display, reviewer-relative "second candle", initial SL above the indicated wick and later movement to secure 1:1. Those historical details do not become universal rules. S6 does not require M30, a second candle, a completed M30 candle High, an arbitrary latest/highest session candle, a fixed candle count, swing rule, threshold or buffer. Standard Nasdaq's existing 5M HL/LH SL contract, Scenario 2 BUY, Forex, unrelated takes and other setups are outside this decision.

The following remain `unresolved` and require a separate scoped audit before runtime implementation:

- Exact beginning/end and membership of the initiating sweep movement.
- When its peak becomes final and causally usable for initial SL selection.
- Whether the referenced candle is closed or in progress at the decision time.
- Whether available historical data can reconstruct the required maximum.
- Exact StopPrice offset above that peak.
- Whether NQ-LIQ-003's existing fact contains sufficient price/time information, and the correct causal transport into an SL consumer.
- Whether a new typed input or an extension of an existing Nasdaq primitive is needed; branch-specific ancestry, consumer/progression integration and execution mechanics remain unimplemented/unresolved.

A future SL consumer must retain exact strategy/version/provider/instrument/session, canonical Start and active instance, initiating liquidity take, explicit WickfillScenario2 SELL applicability, NQ-WF2-001 reversal and NQ-WF2-002 eligibility ancestry. Neither the entire session's maximum nor another setup's latest sweep substitutes for this owner. Session, direction, ordinal or timestamp alone is insufficient.

At StrategyReplayContext.AsOfUtc, no future High, final OHLC from a still-open candle, later peak or future human assertion may be used to backfill earlier frames. This decision supplies no new event/availability timestamp, wall-clock rule or replay boundary. Market-effective time, evidence availability and canonical emission remain distinct; it does not assert when S5's historical peak was observable.

Strategic invalidation does not authorize an order, immediate exit, intrabar stop processing, fill price, broker behavior, new NQ-LIQ-003 terminal condition or additional cancellation rule. Later 1:1 protection is separate post-entry management; BE, protected +1R, trailing and order-modification algorithms remain outside scope. Scoped WF2 eligibility and lifecycle completion still imply neither SL/risk approval, execution nor Ready.

This supersedes earlier unresolved statements **only for the general Scenario 2 SELL protective-reference concept and placement relationship**. Original S5 evidence/history is preserved; sweep segmentation/finality, causal data, offset and deterministic implementation remain unresolved. NQ-SL-001 and its Standard observation/evaluator/M5 anchor, WF2-001/002, liquidity invalidation, workflow/lifecycle, replay, risk/TP, metadata and version activation are unchanged. Counts remain 34 RuleDefinitions / 13 Required / 18 evaluators / zero Required evaluator gaps. The possible same-frame SL liquidity-invalidation guard gap remains a separate inspection finding requiring focused regression validation before Scenario 2 SL runtime integration, not a reproduced failure or a new strategy rule.

### 2.6 Approved initiating sweep measurement interval (2026-10-21, S7)

**Current normative scope: `confirmed` strategy specification, Wickfill Scenario 2 SELL only.** The strategy owner approves the protective sweep peak as the maximum observable market price belonging to the interval that begins at the first valid London High exceedance initiating this exact setup and ends at the effective time of its confirmed bearish reversal. This is an approved operational definition, not a quoted mentor algorithm or a completed automatic SL implementation.

- **Start:** the first valid London High exceedance establishing this setup's authoritative NQ-LIQ-003 initiating take identity. It is not an arbitrary price rise, another earlier London event, the latest take or another setup's sweep. This definition does not discover an earlier intrabar crossing or change existing take timestamps.
- **End:** the effective time of the exact confirmed NQ-WF2-001 bearish reversal belonging to that initiating take. Human import time, wall clock, a later replay/emission frame, execution time and subsequent SL movement are not market-event endpoints.
- **Peak:** the maximum observable market price in that owned measurement interval, subject to the unresolved boundary/data handling below. The swept London High level is not necessarily this protective peak. No session-wide maximum, other setup's peak, post-reversal price, arbitrary M30 High, future High or swing/pivot algorithm is substituted.

Preserve exact strategy/version/provider/instrument/session, canonical Start and active lifecycle instance, initiating take, explicit WickfillScenario2 SELL applicability, exact WF2-001 reversal and WF2-002 eligibility ancestry. Session, direction, ordinal or timestamp alone cannot select the owner; Standard M1 facts are not fabricated. This interval specifically uses London High and introduces no new LondonLow-to-SELL mapping or endpoint-selection algorithm.

The market-event endpoint and canonical availability remain distinct. An earlier reversal effective time does not imply that its confirmation was already observable then. A future peak consumer cannot become eligible before its necessary canonical reversal evidence is genuinely available under existing causal transport/progression. At StrategyReplayContext.AsOfUtc, retain closed-candle and timestamped-price availability and exact source lineage. Neither a still-open candle's final High nor an M1 candle's eventual High is available before its close; later evidence or peaks cannot rewrite earlier frames. No new replay boundary, timestamp or observation rule is introduced.

**Still unresolved for implementation:** exact-boundary observation availability; inclusion/exclusion of boundary timestamps and equal-time ordering where canonical contracts do not establish them; complete M1/M5 coverage; need for timestamped prices in uncovered partial candles; treatment of observations overlapping a boundary without reliable intrabar order; sufficiency of NQ-LIQ-003 beyond ObservedPrice; typed peak source/provenance/availability transport; and missing/conflicting required price evidence. No inclusive/exclusive interval notation, invented intrabar price or complete coverage assertion is selected by this decision.

S7 closes the previously unresolved **operational start/end definition** only; earlier S6 wording about those boundaries remains historical. S5 retains its context-specific visual M30/"second candle" and later 1:1-management meaning, while S6 retains the general sweep-peak protection, `StopPrice > InitiatingSweepPeak` and conceptual bearish-thesis invalidation. Exact StopPrice offset remains unresolved: no tick/point/spread/ATR/percentage/epsilon/rounding policy follows. Calculating the reference and calculating an executable stop remain separate; no order, execution or management algorithm is authorized.

Standard Nasdaq, Scenario 2 BUY, Forex, the general 5M HL/LH concept and unrelated setups are unchanged. M30 is not mandatory. NQ-SL-001's definition/evaluator/observations/selectors/facts, LIQ-003, WF2-001/002, workflow/lifecycle, replay, risk/TP and activation remain unchanged. Counts remain 34 RuleDefinitions / 13 Required / 18 evaluators / zero Required gaps; automatic Scenario 2 SL is not Implemented or fully defined. The possible same-frame SL liquidity-invalidation gap remains a separate technical inspection concern, not a reproduced production failure, requiring validation before downstream SL integration.

### 2.7 Human-selected pre-open screenshot evidence (2026-10-30, S8)

**Scope:** Replay #2, 2026-09-08, SELL. S8 supports the already approved S1 Wickfill Scenario 2 interpretation; it supplies new pre-open source-chart observations, not a new strategy rule. The following visible contents and times are `confirmed` as the user's attributed screenshot report; their visual support is `visual_only`, not an autonomous classifier. Video offsets locate the selected images and are not UTC market timestamps.

| User-selected screenshot | Reported source chart and visible clock | UTC equivalent of that chart-clock reading | Supported observation and limit |
|---|---|---|---|
| 1 — video `02:21` | TradingView US Nas 100 / OANDA, **H4** selected; `08:21:54 UTC-5` | `2026-09-08T13:21:54Z` | Crosshair identifies the September 8 `04:00` candle, previously closed at `08:00`. Displayed OHLC: O `29471.4`, H `29618.6`, L `29448.8`, C `29618.6`. Supports identifying the reviewed H4 candle, not dating every component of the complete Wickfill interpretation. |
| 2 — video `06:58` | TradingView **H4** selected; `08:26:31 UTC-5` | `2026-09-08T13:26:31Z` | Upper resistance region and illustrated retest context remain visible. This image is not an M30 chart or proof of a completed future retest. |
| 3 — video `08:08` | TradingView **H4** selected; `08:27:42 UTC-5` | `2026-09-08T13:27:42Z` | Yellow scenario illustration and upper H4 reference support S1's SELL/Wickfill interpretation. The drawing is illustrative, not an observed future price path or a future market confirmation. |

**Source reconciliation:**

- The AI-reported approximate `29482–29500` region concerns the lower chart area. Promoting it to the approved upper-resistance context is `rejected_ai_inference`. Preserve the existing approximate upper H4 zone `29618.6–29633.1`; no new bounds, midpoint, scalar or tolerance are adopted.
- All three selected pre-open screenshots display H4. Labeling these particular screenshots M30 is `rejected_ai_inference`. S1/S5's separate historical M30 execution/SL observations retain their context-specific scope.
- The third screenshot's visible clock is `08:27:42 UTC-5`. An AI report separately claims complete verbalization by `08:27:46`; that stronger claim remains `human_validation_required`. The four-second difference is not rounded away or used to date complete verbalization at either instant. No transcript or independently verified verbal-completion timestamp is supplied by S8.
- TradingView screenshot values retain their OANDA chart provenance. They are not substituted into independently validated native MT5 US100 candles: the existing native H4 QA records High `29618.7` and Low `29448.9`, distinct from screenshot 1. The context zone remains an attributed cross-feed annotation, not exact feed equivalence.
- S8 explicitly confirms that, for this historical context, S1's later human-approved Wickfill interpretation supersedes the older adapter's Fakeout label. This is a session-specific source reconciliation, not permission to combine the two labels in a typed WF2 context or infer a general classification-conversion rule. The external adapter is not edited by this documentation update.

**Temporal fields remain separate:**

| Field / meaning | Evidence state after S8 |
|---|---|
| Reviewed H4 candle interval | `confirmed`: September 8 `04:00–08:00 America/Bogota`, or `09:00–13:00Z`, under the already retained September source mapping. The candle close is `13:00Z`. |
| Source chart observation | The three chart-clock readings and their UTC equivalents above are retained exactly as reported. They locate the displayed context; they are not automatically runtime `ObservedAtUtc` values for the full strategy assertion. |
| Complete mentor strategy interpretation | S1 already confirms SELL/Wickfill pre-open context and the completed macro move. S8 adds supporting illustrations; exact completion of the full interpretation/verbalization at `13:27:42Z` or `13:27:46Z` remains `human_validation_required`. |
| Full WF2 context `EffectiveAtUtc` | `human_validation_required`: do not assign the H4 close `13:00Z` to every semantic component merely because the candle was closed. No complete-context effective timestamp is selected here. |
| Full WF2 context `ObservedAtUtc` | `human_validation_required`: neither a screenshot's clock, the older preparation available-by boundary `13:27Z`, nor the current audit date automatically establishes when the complete assertion became historically source-observable. |
| Subsequent human audit | S8 was supplied on `2026-10-30`; that audit date is not a historical market event or availability timestamp. Screenshot bytes/hashes remain external and unverified here. |

ADR 0025's effective/observed-time distinction still applies. No new typed Scenario 2 pre-open context is fully authorized solely by these screenshots. The next human-validation step is to confirm the effective time and historically source-observable availability of the complete pre-open assertion against contemporaneous source material, retaining distinct times where applicable. Reversal event/availability, canonical Start binding and all other existing typed-input requirements remain separate. No runtime, evaluator, metadata, adapter, strategy version, SL/TP/risk or execution behavior changes.

### 3. Replay #2 interpretation

| Session fact from S1 | Classification | Boundary |
|---|---|---|
| 2026-09-08, `America/Bogota`, SELL | `confirmed` | Historical session identity |
| H4 04:00–08:00 completed the upward corrective move into the relevant H4 zone before opening | `confirmed` | Human-reviewed historical context; no universal zone coordinates |
| Asia/London references and relevant London High identified before opening | `confirmed` | Marking is distinct from the subsequent trade-relevant sweep |
| Required opening London High sweep not satisfied before 08:30 | `confirmed` | Does not assert that no earlier numerical price crossing ever occurred |
| Opening sweep approximately 08:30–08:31, then stall/rejection and Sell Stop execution approximately 08:32 | `confirmed`, `context_specific` | Approximate source times; no second-level availability, offset or fill derivation |
| M30 operational/execution chart | `confirmed`, `context_specific` | Not a universal M30 gate or candle pattern |
| M1 not used for this execution | `confirmed`, `context_specific` | Standard M1 rules remain intact |
| No pre-entry FVG identified/used | `confirmed`, `context_specific` | Does not prove all FVGs absent or prohibit future FVG support |
| No explicit pre-entry M5 StructuralChange/IFVG used; later M5 close was confluence for an open trade | `confirmed`, `context_specific` | Does not weaken the standard M5-close contract |
| Replay #2 is Wickfill Scenario 2 / pre-established higher-timeframe context | `context_specific` | Future branch-validation case; not an oracle for standard-chain entry at 08:32 |

Earlier Replay #2 notes and the local binder retain an initial `FAKEOUT` label and a standard closed-M5 observation. Those records are not edited here. S1 establishes the new Wickfill Scenario 2 interpretation; the old binder does not represent this branch. No automatic conversion between an initial H4 classification and a contextual branch, or new Breakout/Wickfill/Fakeout precedence rule, is inferred. Earlier M1 observations must not be used retrospectively to explain this mentor's 08:32 execution.

### 4. Rule applicability and runtime boundary

The universal claim that **every** Nasdaq entry must follow opening take -> M5 -> optional FVG -> M1 -> eligibility is superseded. The sequence remains the **standard path**. Future runtime support must explicitly identify branch applicability and evaluate the prerequisites belonging to that branch.

Do not mark standard Required rules Passed to manufacture progression, delete their prerequisites, reuse NQ-M1-003 without its realignment ancestry, or select Scenario 2 merely because a standard gate is missing. This task supplies no rule-status mapping, alternate RuleIds, branch selector or contextual entry-eligibility fact. Missing/ambiguous critical branch evidence must remain safe; it cannot authorize an automatic trade. Current capability registration describes the existing standard implementation, not coverage of this new branch.

Existing time-window, future-data, terminal-state and risk/execution protections are not relaxed. A later video explanation can establish source understanding, but cannot itself backdate historical `ObservedAtUtc`. Historical event time, semantic source availability and current review date remain distinct. Native closed candles may anchor evidence; they do not automatically classify a zone, retracement, sweep relevance or branch.

### 5. Remaining boundaries

| Boundary | State |
|---|---|
| Reviewed pre-open zone/context membership and completed macro corrective movement | `human_validation_required` |
| Deterministic “already at H4 zone” geometry, tolerance and minimum Wickfill depth | `unresolved` |
| Exact macro liquidity algorithm and automatic branch selection | `unresolved` |
| Required post-sweep bearish turn/reversal before considering SELL | `confirmed` concept under S3; recognition `human_validation_required` |
| Scoped SELL eligibility after valid context, London sweep and confirmed bearish reversal | `confirmed` by S4 section 2.3; no additional strategic confirmation currently evidenced, no execution implied |
| Autonomous post-sweep reversal geometry and exact structural reference | `unresolved`; no CHoCH/M5/M1 or pivot-break mapping is authorized |
| Scenario 2 eligibility runtime and downstream Standard/workflow/lifecycle integration | Isolated scoped SELL derivation implemented by NQ-WF2-002; full downstream integration remains `unresolved` / not implemented |
| Universal post-sweep candle-close requirements/non-requirements | `unresolved` unless independently specified; execution timing alone does not define them |
| Sell Stop offset, distance from a candle/open/consolidation and executable order mechanics | `unresolved` |
| Autonomous M30 pattern detector and universal M30 candle requirements | `unresolved`; historical M30 use does not establish such requirements |
| Universal order type and BUY-side symmetry | `unresolved`; neither is generalized from this SELL example |
| Exact historical source-availability boundaries for a future typed branch input | `human_validation_required` |

## Alternatives considered

- **Make NQ-M5-001 universally intrabar:** rejected; the previous human statement was withdrawn, and S1 establishes a distinct branch instead.
- **Treat early execution as skipping gates because of mentor experience:** rejected as the specification explanation; S1 identifies pre-established contextual prerequisites.
- **Force the historical trade through standard M1/FVG evidence:** rejected; those stages were not used for this execution according to S1.
- **Immediately implement M30/order geometry or bypass Required rules:** deferred/rejected in this task; the necessary deterministic predicates are not specified.
- **Record branch-specific source understanding and retain the standard runtime:** selected.

## Consequences

The [specification](../strategies/nasdaq/strategy-specification.md#97-wickfill-scenario-2-pre-established-context) now distinguishes applicability. Standard observations, workflow, metadata and local replay outputs remain unchanged. Historical standard-chain results for Replay #2 do not prove or disprove an unimplemented Scenario 2 trade.

This ADR supersedes universal strategy-scope readings of standard pre-entry descriptions in ADR 0025 and the Nasdaq workflow documents; it does not rewrite their historical decisions or generic replay infrastructure.

The next recommended primitive is a small immutable **human-reviewed Wickfill Scenario 2 pre-open context observation and Missing/Unique/Conflict selector**. `NasdaqHumanWickfillScenario2ContextObservation` is a proposed identifier, not an implemented API. It should retain session/version/provider identity, SELL applicability, reviewed H4/zone and completed pre-open movement evidence, authentic effective/observed times and provenance. It must not detect geometry, synthesize opening-sweep evidence, select a branch automatically, emit entry eligibility or create an order. Its detailed contract requires its own audit before implementation; no implementation is authorized by this document alone.


## Limited human-assisted reversal rule implementation (2026-10-15)

The human-authorized implementation task adds `NQ-WF2-001` to the registered `nasdaq-0.1.0-draft`, sequence 90, stage `Wickfill Scenario 2`, `IsRequired = true`, definition status `HumanValidationRequired`. `WF2` identifies the separate Scenario 2 family; `001` is its first rule, placed after liquidity and before any future branch entry stage. This is a bounded human evidence evaluator in the audited draft, not an ACTIVE strategy promotion or activation of a full alternate execution workflow.

Applicability belongs to the exact established initial-take setup and canonical lifecycle Start, not the preparation session or replay-local ordinal alone. Both paths require explicit human selection. Unique Standard returns `not_applicable` before requesting Scenario 2 context/sweep/reversal. Missing/conflicting applicability requires human validation after a setup exists; without an established predecessor the evaluator waits. Scenario 2 SELL requires the existing pre-open context, exact canonical London opening-sweep composition and compatible Unique human reversal. Named missing data yields `data_unavailable`; reviewed conflicts or absent human evidence yield `human_validation_required`. Only existing terminal protections produce `failed`; no negative reversal predicate is introduced.

Required aggregation already accepts Passed and NotApplicable as non-blocking results. NotApplicable establishes no workflow progression. Existing M5/M1 prerequisites remain unchanged, and no later rule consumes this new fact. Registration alone supplies neither entry eligibility nor full Scenario 2 progression. Existing Standard evaluators retain their contracts; setups lacking positive applicability now have an additional required human-review dependency.

Preserve `ContextEffective < SweepEffective < ReversalEffective <= ReversalObserved <= AsOfUtc`. Applicability binding/availability are the maxima of authentic review times and canonical Start availability; there is no global ordering of applicability before pre-open context. The fact retains Unique applicability and reversal selections, deriving its effective time from the original reversal event even when canonical emission is later. No new replay boundaries are created. NQ-LIQ-003 arithmetic and ownership are reused; its terminal gate additionally covers this exact RuleId. Existing canonical TIME/lifecycle guards are reused without new cutoffs.

Capability is derived as Implemented from exact evaluator registration and means human-assisted coverage only. Autonomous geometry, BUY symmetry, later eligibility, M30/order mechanics and Replay #2 binding remain outside this implementation.

### Limited scoped SELL eligibility implementation update (2026-10-15)

NQ-WF2-002 now derives `NasdaqWickfillScenario2PreEntryEligibilityRuleFact` from the exact latest prior canonical NQ-WF2-001 Passed reversal with established progression, matching surviving initial-take setup and SELL Scenario 2 ancestry. The next family ordinal `002` uses the available sequence 100 after reversal sequence 90 and before existing Standard sequence 110; no existing sequence is changed. Metadata is Required + Confirmed: the derivation is deterministic, while human recognition remains upstream. Explicit canonical Standard NotApplicable propagates as NotApplicable without a fact. A missing setup/predecessor, non-established or incompatible reversal, or upstream human-review dependency waits; upstream DataUnavailable propagates. Only existing terminal/window guards produce Failed. No negative eligibility predicate is added.

The immutable fact retains the exact reversal by reference and derives session, direction and `EligibilityEffectiveAtUtc = ReversalEffectiveAtUtc`. Consumption uses strictly prior canonical history; current-frame positive transport is unavailable. A later emission preserves market effective time and cannot rewrite earlier observations. No new human assertion, candle, replay boundary or execution parameter is introduced. Current setup identity and latest owning-rule result prevent reuse across initial takes or fallback after a newer conflict.

No workflow edge is required for this isolated producer: its evaluator enforces the exact prior reversal's Passed/established progression itself, and generic progression can record its eligible pass. There is no downstream consumer or new prerequisite declaration. Existing Standard workflow and lifecycle remain unchanged. The liquidity terminal gate explicitly includes NQ-WF2-002, including same-frame invalidation; the evaluator reuses TIME and surviving-setup guards for direct calls.

Canonical registration now has 34 Nasdaq rules, 13 Required, 18 registered evaluators and zero Required evaluator gaps. Implemented capability covers this deterministic scoped derivation only. Standard M5/M1 rules still block Scenario 2 globally, NQ-SL-001 and historical consumers retain Standard ancestry, risk follows SL, workflow has no alternative convergence, and lifecycle completion still recognizes NQ-M1-003. Full downstream integration, SL extension, BUY, execution, M30/close mechanics and Replay #2 binding remain unresolved or unimplemented. This update supersedes earlier statements that scoped eligibility runtime itself was absent; historical implementation scopes remain intact.

### Standard setup applicability enforcement (2026-10-20)

NQ-M5-001 and NQ-M1-001/002/003 now resolve the surviving canonical lifecycle Start and its exact initiating-take ancestry through the existing human applicability selector before demanding Standard evidence. Unique Standard continues the existing Standard evaluator. Unique WickfillScenario2 SELL returns NotApplicable without a Standard fact. Missing/conflicting classification for an established setup requires human validation; an absent valid owner waits or preserves the existing unavailable-prerequisite result. Future/foreign reviews cannot select a path, and the latest take or local ordinal alone cannot identify the setup.

Existing protection owners retain precedence. Canonical same-frame/prior terminal gates still dominate. M1 retains its existing TIME/lifecycle precheck; M5 retains its different out-of-window missing-selection mapping and cannot return NotApplicable outside the TIME-owned window. No cutoff, market event, hidden same-frame positive transport or historical rewrite is introduced.

No RuleDefinition, Required flag, registration, workflow prerequisite or lifecycle completion changes. NotApplicable does not establish Standard progression. Optional FVG, NQ-WF2-001/002, SL, TP, risk, historical consumers and Replay #2 binding are unchanged. Scenario 2 SL specification, downstream convergence and alternative lifecycle completion remain pending. Registration remains 34 rules / 13 Required / 18 evaluators / zero Required gaps: evaluator coverage only. This update supersedes earlier statements that these four Standard evaluators were not branch-aware; historical implementation scopes remain above.

### Scoped pre-entry lifecycle completion (2026-10-20)

The Nasdaq lifecycle now recognizes prior canonical NQ-WF2-002 Passed/established eligibility as an alternative to its unchanged Standard NQ-M1-003 completion check. This recognizes scoped pre-entry completion only, never execution or global Ready. Cancellation from authoritative liquidity invalidation retains precedence; incomplete setups still expire under the existing TIME-owned cutoff. A genuinely completed setup receives the same cutoff treatment as completed Standard setups.

The Nasdaq policy produces a typed companion through ADR 0005's existing lifecycle evidence interface. The canonical runner supplies bounded prior observations. When a caller producer is present, both snapshots are independently validated before compatible payloads are combined; no caller can suppress the policy's evidence. No generic eligibility abstraction or workflow edge is introduced.

The companion retains the exact canonical Start owner and initiating take, the prior eligibility owner/fact, and its exact prior NQ-WF2-001 ancestry. Recognition requires matching strategy/version/provider/instrument/session, active instance, explicit Scenario 2 SELL applicability, existing SameFact/setup semantics and established prior progression. Current incompatible human reviews or owning results invalidate the proof. Reference binding to the prior active instance's Start prevents reuse of a foreign proof with matching timestamps or local ordinals. Owner references remain in memory but are excluded from JSON to avoid recursively serializing historical lifecycle graphs; effective and evaluation times remain traceable.

Only strictly prior canonical completion is transported. EligibilityEffectiveAtUtc remains ReversalEffectiveAtUtc; recognition can occur at a later existing replay boundary. Current-frame Passed, raw RuleIds, late/future reviews and terminal setups cannot complete or revive the setup. Post-cutoff TIME rejections do not erase a genuine in-window completion; source compatibility and authoritative invalidation remain enforced. No cutoff, replay boundary or historical frame is changed.

This supersedes the earlier pending alternative-lifecycle-completion statements only. Standard M1 facts/timing, workflow, metadata and registration remain unchanged (34 rules / 13 Required / 18 evaluators / zero Required gaps). Scenario 2 downstream convergence, SL/TP/risk integration, BUY and entry execution remain unresolved/unimplemented; NQ-SL-001 still requires Standard eligibility/M1 ancestry. No orders, trades, Replay #2 binding or new market confirmation is added.
