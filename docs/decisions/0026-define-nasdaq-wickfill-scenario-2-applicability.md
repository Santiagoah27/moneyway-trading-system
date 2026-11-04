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

Source S9 is the user's new authoritative human confirmation **"MoneyWay — Register S9 Human-Confirmed Replay #2 WF2 Temporal Context"**, dated 2026-10-30. Retained decision-text attachment identity: `49ff660c-601f-49ce-bf92-643afba282e3`; verified decision-text SHA256: `2ce84337df7ce97c3d6125e7d6622d013b855a8299c71d4c93ee0557f38639af`. The user personally confirms that the complete September 8 H4 SELL/Wickfill context, including the completed corrective movement toward the relevant zone, was established at 08:00 America/Bogota and explicitly recognized by the mentor no later than 08:27:42. This is direct human confirmation independent of prior AI analysis. The hash verifies this decision text only, not screenshot/video bytes; S8's screenshots remain external / not repository-resolved. S1–S8 meanings and provenance remain unchanged. Section 2.8 resolves only the historical pre-open context temporal pair previously left open by S8.

Source S10 is the user's direct human-reviewed decision **"MoneyWay — Document S10 Replay #2 Human-Confirmed Scenario 2 Applicability Decision"**, registered 2026-11-03. Retained decision-text attachment identity: `d24d3263-6c55-4cd9-a1b5-7a42b62fbb7c`; verified decision-text SHA256: `b7532f711efee9d4a0080529705823aec4296fad8ad25d51e3a76e22bef2cd13`. The reviewer personally checked the original mentor video and supplies operational-selection confirmation and attributed MT5 screenshot observations. The screenshot remains external / not repository-resolved: its actual bytes are unavailable for local verification here; no image path, hash or immutable image attachment identity is asserted. The text hash identifies this decision only. This is not an independent assistant audiovisual inspection. S1–S9 source records remain intact; section 2.9 adds the later operational decision and clarifies the tentative scope of video 08:18–08:27.

Source S11 is the user's explicit approval **"MoneyWay - Replay #2 Human-Approved S10 Applicability Binding"**, registered 2026-11-03. Retained decision-text attachment identity: `1e2a10a8-642f-4457-86f7-0f16365753e2`; verified decision-text SHA256: `45531472f99082928d831cdf24183a6c6bcd31c44549bb4b26522c744324081d`. This source approves the conservative meaning of the S10 review fields for September 8, 2026 only. The hash identifies the supplied approval text, not screenshot/video bytes. No exact message-receipt time or new image identity is asserted. S1-S10 records and their original meanings remain intact; section 2.10 supplies the new representation authority.

Source S12 is the user's direct human clarification **"MoneyWay - Nasdaq Scenario 2 SELL Conditional Order vs Reversal Specification Reconciliation"**, registered 2026-11-03. Retained decision-text attachment identity: `f4e47ff3-f2ed-4218-a818-4f4deb1958c5`; verified decision-text SHA256: `6d10172fe3c216a05b283c07e5f7ed518244bb7020e201b3484cfc94aaf0ce08`. The reviewer personally reviewed the original September 8 mentor recording and distinguishes potential setup selection, conditional SELL STOP preparation, subsequent bearish movement and historical activation. This is attributed human evidence, not a new assistant audiovisual inspection or a verbatim transcript. The text hash identifies this clarification only; screenshot/video bytes, image path/hash and an exact message timestamp are not asserted. S1-S11 source records remain intact. Section 2.11 reconciles the scope of S3/S4 with this clarification; the preceding AI audit supplies context, not independent strategy authority.

Source S13 is the user's explicit human approval **"MoneyWay - Register S13 Human-Approved Replay #2 Bearish Reversal Recognition"**, registered 2026-11-04. Retained approval-text attachment identity: `8fddfee6-00d6-4675-bc79-6e7fcd4eafc8`; verified approval-text SHA256: `d9a90be436fe59b345a8b170c967c3c69d55c87e04ba44841d3eb1080844860a`. The reviewer confirms that the relevant bearish reversal was already demonstrated and visible in the original September 8 recording at 08:33:10 AM America/Bogota, explicitly a historical clock time rather than video playback minute 08:33. This directly approves the representation in section 2.12; it is not an AI-inferred market timestamp or a new assistant audiovisual inspection. The hash verifies only the approval text, not screenshot/video bytes. The previously supplied screenshot remains external / not locally byte-verified; no image path, hash, asset identity or exact message-receipt timestamp is invented. S1-S12 source records and original meanings remain intact.

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

### 2.8 Human-confirmed pre-open context temporal pair (2026-10-30, S9)

**Scope: `confirmed`, Replay #2 on 2026-09-08 only.** S9 explicitly confirms two separate historical meanings for the complete H4 SELL/Wickfill context, including the completed H4 corrective movement toward the relevant zone:

| Field | Approved historical value | Meaning and source |
|---|---|---|
| `EffectiveAtUtc` | `2026-09-08T13:00:00Z` | At `08:00 America/Bogota`, when the reviewed H4 candle closed, the complete context conditions had already been fulfilled. S9 directly confirms this semantic fact; it is not inferred merely from candle closure. |
| `ObservedAtUtc` | `2026-09-08T13:27:42Z` | The mentor had explicitly recognized the complete interpretation no later than `08:27:42 America/Bogota`. S9 approves this available-by boundary for the historical record, not a claim that first recognition or an exact utterance began at that second. |
| Subsequent human audit | `2026-10-30` | The date of the user's confirmation, distinct from both historical fields. It is not substituted for historical observation time. |

America/Bogota is UTC-5 for these source times; no DST or America/New_York conversion is applied. The approved pair satisfies `EffectiveAtUtc <= ObservedAtUtc`. Existing ADR 0025 visibility remains mandatory: at `StrategyReplayContext.AsOfUtc < 2026-09-08T13:27:42Z`, this complete observation must not be supplied or consumed, despite its earlier effective time. At or after the approved availability, consumption remains subject to the existing source, identity, workflow and lifecycle contracts. Auxiliary observations create no new replay boundary; a later existing canonical frame can consume them without shifting their effective/observed times or backfilling earlier frames.

**Relationship to S8:** its screenshot clocks, H4 identification, TradingView OHLC, illustrative drawing and original temporal uncertainty remain recorded as they were. S9 supplies the new explicit human confirmation resolving that specific full-context temporal question; it does not retrospectively turn the screenshots or the AI's `08:27:46` verbalization claim into independent proof. Preserve the existing approximate upper zone `29618.6–29633.1`, exact member identities and source provenance. The lower `29482–29500` region is not a substitute, and no TradingView/OANDA-to-native-MT5 price equivalence is asserted. S1's Wickfill interpretation supersedes the older adapter's Fakeout label for this historical context; the external adapter remains untouched here.

**Adapter readiness and limits:** the historical pre-open context temporal semantics are now sufficiently specified to prepare the existing typed observation in a subsequent adapter-only feature, retaining the approved zone, exact closed H4 sources and S1/S8/S9 references. The relied-upon H4 observation must represent the same approved SELL/Wickfill context with compatible effective/observed times; copying the older adapter's Fakeout label or later H4 effective time would not satisfy the current constructor ancestry checks. This decision does not itself construct an observation, change that adapter or claim a successful replay.

S9 does not establish branch-applicability review timestamps, a reversal `EventId` or its effective/observed times, the first intraminute liquidity crossing, or canonical Start/active-setup references. Those independent inputs still require authentic historical evidence and binding to the actual run. No universal Wickfill timing or H4 selection/inference rule, initial SL/offset, execution/fill, TP, risk or PnL follows. Runtime, evaluators, metadata, strategy versions and unrelated unresolved questions are unchanged.

### 2.9 Human-confirmed operational selection and pending-order evidence (2026-11-03, S10)

**Scope: `confirmed`, `context_specific`, Replay #2 on 2026-09-08 only.** S10 clarifies that video 08:18–08:27 discussed a possible Scenario 2 entry, not definitive operational selection at that point. Later the mentor recognized the relevant London High liquidity as taken, selected Wickfill Scenario 2 SELL for this opportunity, and placed a pending SELL STOP as part of that decision. This newer clarification does not erase S3/S4's historical records or alter the approved strategic eligibility boundary. It does not supply an exact video offset for the later decision, a deterministic selection algorithm or a universal SELL STOP requirement.

The following are **human-provided visual observations**, not locally verified image bytes:

| Screenshot item | Attributed observation | Boundary |
|---|---|---|
| Platform / instrument / chart | MetaTrader 5 / `USTEC` / M30 | Operational source identity; distinct from native CSV `US100`. No source-feed or price equivalence is inferred. |
| Pending order | SELL STOP at `29605.75`, status `placed` | Pending placement only, not proof of fill, executed entry price or historical trade outcome. |
| Order creation | `2026.09.08 16:30:37` server | Exact displayed placement timestamp; approved September-pack mapping gives `2026-09-08T13:30:37Z`. |
| Market Watch | `16:30:42` server | Visible observation clock maps to `2026-09-08T13:30:42Z`; not proof of earliest source observation. |
| Windows clock | 08:30 a.m., 2026-09-08 | Minute precision only; does not establish seconds or replace either MT5 timestamp. |

For this audited September source only, UTC = server time minus three hours; the mapped times correspond to 08:30:37 and 08:30:42 America/Bogota (UTC-5). This does not establish a general MT5/provider timezone rule. The registration date `2026-11-03` is a subsequent human evidence record, not historical event time.

| Temporal meaning | S10 evidence and remaining limit |
|---|---|
| Operational selection already established | **No later than `2026-09-08T13:30:37Z`**, an upper bound supported by placement. |
| First effective selection instant | `human_validation_required`: exact earliest decision time is not independently established. Do not automatically assign `ReviewEffectiveAtUtc = 2026-09-08T13:30:37Z`. |
| Visible source clock | Exact displayed `2026-09-08T13:30:42Z`; earliest historical observation is unknown. Do not automatically assign `ReviewObservedAtUtc = 2026-09-08T13:30:42Z`. |
| First intraminute London High crossing | Unknown. The human recognition/order record does not create an earlier native price event. |
| Existing native closed-M1 take | Retain `2026-09-08T13:31:00Z`; this availability is distinct from the unknown first intraminute crossing. |
| Canonical lifecycle Start | Must be the actual replay run's initiating-take owner, with its real identity and availability. Neither screenshot nor pending order substitutes for it. |
| S9 complete pre-open context | Preserve `EffectiveAtUtc = 2026-09-08T13:00:00Z` and `ObservedAtUtc = 2026-09-08T13:27:42Z`; context availability and operational selection remain separate. |

**Existing contract compatibility and blocker.** [NasdaqHumanBranchApplicabilityObservation](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqHumanBranchApplicabilityObservation.cs) accepts a review preceding the initial take. It requires concrete UTC `ReviewEffectiveAtUtc` and `ReviewObservedAtUtc`, with observed >= effective, and binds `EffectiveAtUtc = max(ReviewEffectiveAtUtc, InitialTakeObservation.AsOfUtc)` and `ObservedAtUtc = max(ReviewObservedAtUtc, InitialTakeObservation.AsOfUtc)`. An earlier decision can therefore bind to a later canonical initiating setup once its actual owner is available, subject to existing identity, causality and active-setup validation. No contract change is needed for that ordering.

S10's bounds alone do **not** authorize a concrete review timestamp pair for this constructor. Clamping to Start does not justify inventing input scalars or claiming first decision/observation times. Real adapter applicability remains unimplemented by this documentation feature. The smallest remaining action is explicit human confirmation of a source-backed review pair or approval of these conservative boundaries as the replay review fields, with their available-by meanings preserved; then a separate adapter feature can bind to the actual canonical owner. No Start/owner ID, canonical observation or artificial price event is fabricated here.

Reversal event identity and effective/observed evidence, initial SL realization, TP, risk and execution/fill remain independent requirements. S10 resolves none of them and establishes no full Replay #2 completion. No runtime, evaluator, metadata, external adapter, CSV, order logic or strategy algorithm changes follow.

### 2.10 Human-approved conservative applicability review pair (2026-11-03, S11)

**Scope: `confirmed`, `context_specific`, Replay #2 on 2026-09-08 only.** S11 explicitly approves the following fields for the existing `NasdaqHumanBranchApplicabilityObservation`, supplementing S10 rather than rewriting its earlier uncertainty:

| Raw review field | Approved UTC value | Approved meaning |
|---|---|---|
| `ReviewEffectiveAtUtc` | `2026-09-08T13:30:37Z` | Operational Wickfill Scenario 2 SELL selection already existed no later than this time; not its exact first decision instant. |
| `ReviewObservedAtUtc` | `2026-09-08T13:30:42Z` | Evidence of that selection was visible no later than this time; not its first possible observation instant. |

These are exact approved input values representing conservative latest-known bounds. S11 supplies the explicit authority missing from section 2.9; it does not establish the mentor's unknowable mental decision second. Preserve S9 context effective/observed values `13:00:00Z` / `13:27:42Z`, S10's pending-order placement versus fill distinction, external screenshot provenance and USTEC versus native US100 identity. The first intraminute London High crossing remains unknown.

The existing constructor retains both raw review fields and derives `EffectiveAtUtc = max(ReviewEffectiveAtUtc, InitialTakeObservation.AsOfUtc)` and `ObservedAtUtc = max(ReviewObservedAtUtc, InitialTakeObservation.AsOfUtc)`. Use the current run's exact canonical initiating-take/active Start owner. Historical record availability must equal the derived observation availability, not the raw review availability. If Start occurs at `13:31Z`, both derived fields are `13:31Z`; the binder requires prior owner history and can first bind at the next existing frame, expected `13:32Z` for this dataset. Verify those values in the real run; no generic minute delay or hardcoded owner/ordinal is authorized.

The external adapter feature may now bind this approved classification using the existing contracts, with source references to S10/S11 and the original decision attachments/hashes. A different canonical emission time must not be silently substituted or backdated to make registration succeed. Preserve identity, active ownership, prior progression, future-data and conflict guards. The screenshot/order never replaces a native market event or canonical Start. No positive same-frame propagation or rewriting of earlier frames is authorized.

This approval changes no strategy algorithm, contract, evaluator, metadata, version, cutoff or Standard/Forex behavior. It supplies no reversal event identity/times, WF2 eligibility, SL/TP/risk, fill, snapshot, PnL or Ready. See the [local adapter and validation record](../backtesting/nasdaq-replay2-s11-applicability-adapter.md) for the external artifact checkpoint and focused real-data test scope.

### 2.11 Conditional order preparation versus strategic reversal eligibility (2026-11-03, S12)

**Scope: `confirmed`, `context_specific`, historical Replay #2 SELL on 2026-09-08.** After recognizing London High liquidity as taken in the established H4 SELL/Wickfill context, the mentor interpreted a potential bearish opportunity and selected Scenario 2 SELL. He placed a pending SELL STOP at the lows of the relevant zone to condition entry on subsequent downward movement. The order was later activated and bearish movement was observed. The reviewer explicitly clarifies that the mentor did not wait for the entire bearish move to finish before preparing the pending order. This does not establish the first reversal instant, whether a distinct turn had already been recognized at placement, or an executable zone-low algorithm.

| Meaning | Evidence / strategy boundary | What it does not establish |
|---|---|---|
| Setup selection | Human selection of this potential Scenario 2 SELL opportunity after the recognized London High take; represented by branch applicability under S10/S11. | A completed reversal, NQ-WF2-001 Passed or strategic eligibility. |
| Conditional pending-order preparation | Historical SELL STOP placement at the identified zone's lows; preparation may precede demonstrated completion of the bearish turnaround. | A universal SELL STOP, placement geometry/threshold, MoneyWay order authorization, fill or Passed reversal. |
| Post-sweep bearish turnaround | Genuine bearish reversal remains required for strategic eligibility under S3/S4. Its recognition is human-assisted; autonomous geometry is unresolved. | A completed entire bearish move, an additional M5/M1 signal, mandatory close or separate chart pattern. |
| Strategic pre-entry eligibility | NQ-WF2-001 evaluates the reviewed reversal; NQ-WF2-002 derives scoped eligibility from the prior established canonical reversal. Both concern analysis/evaluation. | Broker submission, pending preparation, activation/fill, opened position, executable entry or global Ready. |
| Historical activation/fill | The reviewer reports later activation and downward movement as separate historical execution evidence. | Automatic proof of NQ-WF2-001, its effective/observed times, a typed execution record or a universal trading rule. |

**Correction of the prior ambiguity:** S3/S4's "consider entry" / "pre-entry eligibility" must not be expanded into an independent mandatory reversal confirmation before preparing a conditional pending order. Current source evidence establishes no such preparation gate. This clarification preserves their genuine post-sweep reversal requirement for strategic eligibility and does not assert that reversal is never required before any kind of entry. Neither a bearish expectation after liquidity take nor order placement confirms the reversal. Activation/fill and later bearish movement do not automatically satisfy it either.

**Still `unresolved` / `human_validation_required`:** the exact relationship between human reversal recognition, conditional triggering and actual trade entry; whether/how a source-supported triggering movement supplies the reviewed reversal for this operation; the reversal's authentic event identity and historical effective/observed times; and deterministic recognition geometry. No extra independent confirmation is inferred, and no trigger/fill-to-reversal equivalence is approved. S12 authorizes documentation reconciliation only, not removal, weakening or reinterpretation of NQ-WF2-001/002 in runtime.

Preserve the following distinct temporal meanings:

| UTC on 2026-09-08 | Existing evidence and limit |
|---|---|
| `13:27:42Z` | S9 complete pre-open context historically observable; context effective time remains `13:00:00Z`. |
| `13:30:37Z` | S10 pending SELL STOP placement; S11 conservative selection-already-established bound, not first decision or reversal time. |
| `13:30:42Z` | S10 visible clock; S11 conservative evidence-already-visible bound, not first observation or reversal time. |
| `13:31:00Z` | Native closed-M1 take and canonical Start; not the first actual intraminute crossing. Derived S11 applicability effective/observed values remain `13:31:00Z`. |
| `13:32:00Z` | First actual S11 applicability binding verified by the real canonical prefix runs; not historical order authorization. |
| `13:33:10Z` | Later screenshot is reported by the human source as showing bearish movement and an open SELL position. Its image bytes are external / not locally verified; it is not a fully typed reversal/fill event, exact activation time or proof of reversal onset. |

The mentor's intraminute discretionary observation and current replay's native closed-M1 evidence are different resolutions. Preserve USTEC operational-chart versus native US100 identities without price-feed equivalence. Order-before-native-take chronology is not silently repaired: the pending order does not move canonical timestamps or demonstrate a strategy violation by itself. Current reversal validation still requires a source-backed effective event strictly after its represented take and availability no earlier than its relied-upon sweep evidence; S11's conservative review representation is not automatically transferred to reversal timestamps. No open M30 candle's eventual High/Low, M1 OHLC ordering, later position screenshot or subsequent result supplies those missing facts.

NQ-WF2-001/002, their contracts/tests, evaluators, metadata, lifecycle, pipeline, external adapter, CSVs/manifests, ACTIVE versions, Standard/Forex, SL/TP/risk and execution logic remain unchanged. There is no fake reversal, autonomous order permission or new close/order-type requirement. The smallest next evidence step is to assess the already-supplied source material for an identifiable human-reviewed bearish turn and its historical timing, keeping it separate from conditional preparation/activation; request only a genuinely missing source item if that material is insufficient. Do not repeat the full video review merely to reconfirm S12.

### 2.12 Human-approved historical bearish reversal recognition (2026-11-04, S13)

**Scope: `confirmed`, `context_specific`, historical Replay #2 Scenario 2 SELL on 2026-09-08 only.** The user explicitly approves documenting the already-visible genuine bearish reversal as a historical recognition/establishment event at **08:33:10 AM America/Bogota**. This is the historical platform/session clock, not playback minute 08:33, the current audit time, or a claim about the first turning tick.

| Approved field | UTC value | Authorized meaning |
|---|---|---|
| `EffectiveAtUtc` | `2026-09-08T13:33:10Z` | Documented historical recognition/establishment of the already-visible bearish reversal. |
| `ObservedAtUtc` | `2026-09-08T13:33:10Z` | Historical availability of that documented evidence. |
| Subsequent approval registration | `2026-11-04` | Human approval record date, separate from both historical fields; no exact message time is asserted. |

Neither field claims the first bearish tick, original turning-point instant, or the first time the mentor recognized the move. The approved recognition is evidence-backed for this setup, not a universal reversal detector, threshold, candle-close rule or new strategy algorithm. This resolves the specific recognition-time representation left open in S12; S12's original uncertainty and all earlier source records remain historical. The exact original onset and the general relationship between reversal, conditional triggering and trade entry are not resolved by this pair.

**Attributed screenshot evidence and conversion.** The reviewer previously supplied an original-video screenshot identifying the movement as "reversal bajista": MT5, USTEC, M30, visible bearish movement, Market Watch `16:33:10` server, and an open SELL position with separate displayed entry time `16:32:00` server. Under the already-audited September-pack mapping only, server minus three hours gives `13:33:10Z` and `13:32:00Z` respectively; America/Bogota is UTC-5 without DST for these source times, giving 08:33:10 and 08:32:00. These are different facts. The reversal assertion rests on explicit human identification/approval, not on the filled position, red candle alone or an open M30 candle's final OHLC. Image bytes remain external / not locally verified; neither an image hash nor a native-US100 price event is supplied. Preserve USTEC versus US100 source identities without feed/price equivalence.

**Existing chronology and causal boundary:** S9 context remains effective `13:00:00Z`, observed `13:27:42Z`; S10 placement remains `13:30:37Z` and visible clock `13:30:42Z`; S11 raw review bounds remain those latter two values, with derived applicability effective/observed `13:31:00Z`; the documented native closed-M1 initiating take and canonical Start remain `13:31:00Z`, with first real applicability binding `13:32:00Z`. S13 recognition `13:33:10Z` is strictly later than that represented take and satisfies effective <= observed. The first actual intraminute London High crossing remains unknown. No source or canonical timestamp is shifted to match the mentor's order.

S12's distinction between conditional preparation, genuine post-sweep reversal, strategic eligibility and historical activation/fill remains mandatory. S13 does not retroactively authorize the historical 08:32 entry from recognition documented at 08:33:10, identify placement/activation with reversal confirmation, or prove a typed fill. NQ-WF2-001's genuine reversal requirement remains intact; NQ-WF2-002 can only derive from its actual prior canonical established fact. No earlier frame may consume the assertion and no hidden same-frame positive transport is authorized. Equal approved event/availability values do not create a second-resolution replay boundary or a successful evaluator result.

**Readiness and remaining implementation work.** The approved temporal semantics are now sufficiently specified to prepare a focused real-adapter `NasdaqHumanWickfillScenario2PostSweepReversalObservation` using existing contracts. A later feature must still define a traceable documentary recognition `EventId` anchored to S13 rather than invent a market-price/tick identifier; validate `SourceReference` against the retained approval text/identity/hash and related human evidence; compose the approved context and exact initiating take from the current run's actual active Start; preserve strategy/version/provider/instrument/session and source availability; and verify selector, binder, workflow/lifecycle and evaluator behavior. The prior S11 run demonstrates applicability ownership only, not a constructed S13 opening-sweep composition or reversal binding. Actual canonical ancestry, binding, NQ-WF2-001/002 results and downstream timing have not been demonstrated for S13.

This feature creates no typed observation or EventId, changes no contract/evaluator/metadata/lifecycle/replay/adapter/CSV, and marks neither WF2 rule Passed. SL/TP/risk, broker execution, historical fills, Standard/Forex and ACTIVE versions remain unchanged. The smallest next implementation is to add only this approved documentary recognition to the real adapter, with a unique verified source identity and the current run's exact opening-sweep ancestry, then test first causal consumption and safe outcomes. No additional review of the first turning tick is required by this approval.

### 2.13 Candidate human SL evidence contract for Scenario 2 SELL (2026-11-04)

**Specification-first architecture: `candidate`, not implemented.** This section answers the task "MoneyWay - Nasdaq Scenario 2 SELL Human SL Evidence Contract Specification" (retained request attachment `128b48ec-3d16-4584-b8d9-c5e554905641`). That task authorizes specification work, not new historical testimony. No S14 human evidence is registered. S1-S13, including the original S5-S7 provenance and uncertainties, remain intact. This section narrows the earlier candidate contract discussion; it does not approve an automatic SL algorithm or change the registered strategy/version.

#### Evidence and implementation checkpoint

| Finding | Definition classification | Source and limit |
|---|---|---|
| Initial protection belongs above the initiating sweep peak for Scenario 2 SELL | `confirmed` | S6, section 2.5: `StopPrice > InitiatingSweepPeak`; a protective concept, not a complete deterministic algorithm |
| Owned measurement interval starts at the initiating valid London High exceedance and ends at its confirmed reversal effective time | `confirmed` | S7, section 2.6; boundary membership, coverage and observation ordering remain unresolved |
| Replay #2 selected an upper wick/high while displaying USTEC M30, and later moved SL to secure 1:1 | `context_specific`, with `visual_only` support | S5, section 2.4; S6 identifies that wick as the initiating sweep peak. This does not supply a universal M30 selector or management algorithm |
| Human-selected reference, exact numerical protective price, native-source mapping and authentic selection/parameter availability for Replay #2 | `human_validation_required` / `unresolved` | S5's "second candle" is not a uniquely resolved candle. Screenshot bytes remain external / not repository-resolved |
| A documented numerical historical StopPrice | `unresolved` | Original S5 says a value is visible in the trade table but does not transcribe it; original S6/S7 supply no numerical stop. No usable human-approved scalar is registered by those sources |
| Automatic M30/ordinal/highest-session selection, fixed buffer or inference from later OHLC | `rejected_ai_inference` | Not authorized by S5-S7; neither visual proximity nor a future candle can fill missing fields |

The [real canonical diagnostic](../backtesting/nasdaq-replay2-canonical-preentry-diagnostic.md) at Git `6f0890d5850a7c6fc3b0758a0c083bf8bdaa13c2` documents Start 13:31Z / step 7591 / local instance 1, native initiating event `native-M1-close-2026-09-08T13:31Z`, WF2-001 Passed at 13:34Z and WF2-002 Passed at 13:35Z. At 13:36Z the verdict is Wait, NQ-SL-001 is Waiting and risk remains Waiting; Required coverage is complete (13/13). This is a causal path contract gap, not proof that M30 market data is missing. No Scenario 2 SL fact or executed trade is established.

The existing [SL observation](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqHumanStructuralStopLossObservation.cs) requires `NasdaqPreEntryEligibilityRuleFact`, `NasdaqHumanM5ProtectionAnchor` and a distinct StopPrice. Its effective time follows its closed 5M sources, and availability follows Standard realignment evidence. The [SL evaluator](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayEvaluators/MoneyWayNasdaqHumanStructuralStopLossEvaluator.cs) and [workflow](../../src/backend/MoneyWay.Application/StrategyReplay/Workflow/MoneyWayReplayWorkflowDefinitions.cs) require prior established NQ-M1-003 Passed. Scenario 2 instead makes that Standard gate NotApplicable and supplies `NasdaqWickfillScenario2PreEntryEligibilityRuleFact`. Neither passing a price alone nor relabeling an M30 wick as M5 LH satisfies the existing contract.

Existing [SL tests](../../tests/unit/MoneyWay.Application.UnitTests/Strategies/Nasdaq/ReplayInputs/NasdaqHumanStructuralStopLossTests.cs) enforce exact Standard ancestry, closed M5 sources, strict protective side, missing/conflicting/unavailable evidence, future visibility and terminal/cutoff handling. [WF2 eligibility tests](../../tests/unit/MoneyWay.Application.UnitTests/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2PreEntryEligibilityTests.cs) enforce exact surviving owner, prior canonical reversal, no same-frame positive transport and no fallback after conflict. [Scoped completion tests](../../tests/unit/MoneyWay.Application.UnitTests/Strategies/Nasdaq/ReplayLifecycle/NasdaqScenario2LifecycleCompletionTests.cs) establish lifecycle completion without downstream readiness. These tests are inspected evidence, not new Scenario 2 SL coverage.

#### Alternatives and recommendation

| Option | Assessment | Classification |
|---|---|---|
| A: reuse NQ-SL-001 with a dedicated Scenario 2 SELL input and WF2-002 dependency | Smallest proposed convergence: preserve the existing SL responsibility, RuleId and Required flag; add explicit branch-specific ancestry without pretending the current Standard input/fact is compatible. A later feature must address workflow gating and downstream fact consumers explicitly | `candidate`, recommended; not currently supported |
| B: separate Scenario 2 SL rule and observation | A dedicated observation is necessary under A as well. An additional RuleId would duplicate the Required SL responsibility and require an independently approved workflow/lifecycle decision; no evidence currently requires that duplication | `candidate`, not selected |
| C: leave the capability human-only until source evidence is available | Correct present replay behavior remains blocked. Missing historical evidence does not prevent specifying a safe input contract, but does prevent populating a valid real-source input or claiming Passed | `human_validation_required` / `unresolved` for the missing evidence; not a replacement architecture |

Recommendation A is safe only as a future explicit extension, not reuse of the existing constructor, M5 anchor or Standard-only SL fact. Keep Standard behavior isolated. Risk, TP and historical snapshot consumers currently depend on the Standard-shaped SL/eligibility facts; choosing the same RuleId does not make those types branch-neutral. No redundant rule, metadata change, ACTIVE promotion or automatic downstream compatibility follows from this specification.

#### Minimum proposed path-specific evidence

Field labels below are conceptual, not new C# declarations. Contract design is `candidate`; inherited source constraints remain confirmed within S5-S7 scope.

| Evidence group | Minimum content and ownership requirement |
|---|---|
| Session/setup identity | Strategy, exact version, provider, native symbol, Nasdaq trading day/timezone, SELL and explicit WickfillScenario2 applicability. For Replay #2: `moneyway-nasdaq`, `nasdaq-0.1.0-draft`, `oanda-global-markets-mt5-demo`, US100, 2026-09-08, America/Bogota |
| Canonical owner | Exact actual Start observation and its active instance plus the initiating NQ-LIQ-003 fact/event and selected London High reference. Session/direction/time/step/local ordinal alone cannot substitute for owner identity; extensions or another take cannot replace the initiating event |
| Eligibility ancestry | Exact prior canonical owner and `NasdaqWickfillScenario2PreEntryEligibilityRuleFact` from NQ-WF2-002 Passed with established progression, retaining its own WF2-001 reversal, applicability, opening sweep and Start/take references. Must belong to this same surviving setup; no synthetic Standard M1 facts |
| Protective reference | Human-selected initiating sweep peak with independently documented protective price, semantic role, authentic source identity/location and source-observable time. Preserve the S7 interval ancestry; do not claim an automatically reconstructed maximum without complete approved coverage/boundary semantics |
| Visual/candle identity | For a candle-based claim retain source feed/symbol, displayed timeframe, uniquely identified candle open/close and available price evidence under ADR 0025. Alternatively an exact retained external level/event reference and price can identify a non-candle reference. A source-located visual selection can be retained as testimony, but "second candle" or an unexplained scalar cannot qualify as a resolved protective reference. M30 is historical display context, not a mandatory general input timeframe |
| Stop parameter | Separately documented, human-reviewed initial `StopPrice`, its exact source and assertion time; establish `StopPrice >` the selected protective peak in the same validated price domain. No default, derived buffer or OHLC maximum is a substitute. A later managed stop is a different event |
| Provenance and feed mapping | Retain reviewer/decision identity, original source location, all compatible supporting assertions, and only actually verified asset identities/hashes. USTEC visual evidence is not native US100 price evidence. A documented mapping/validation must substantiate any native use; normalization validation alone establishes no cross-feed price equivalence |
| Time/availability | Preserve authentic SL selection/parameter `EffectiveAtUtc` and `ObservedAtUtc` in UTC, with effective <= observed, plus source-event/candle availability. Keep historical placement/activation, later management, subsequent review/import and canonical binding/evaluation times separate |

#### Temporal and causal consumption contract

Apply [ADR 0025 identity and availability](0025-define-nasdaq-demo-human-evidence-contract.md#identity-and-causal-availability). EffectiveAtUtc denotes the documented SL selection/parameter event or reviewed context, not automatically the candle close, take, reversal or eligibility time. ObservedAtUtc denotes authentic availability of that semantic assertion; a visible post-entry SL line alone does not prove that this exact pre-entry parameter was contemporaneously available. Unknown times remain unresolved. Retrospective testimony may be retained for comparison without becoming a historical driving assertion.

Historical placement and canonical strategy eligibility are different events. If authentic SL evidence precedes WF2-002, retain its original times as source evidence without authorizing progression. A future bound observation must retain that raw pair and separately identify the actual later canonical binding/evaluation boundary and exact eligibility owner. Do not rewrite the raw pair with `max(...)`, borrow S11's separately approved bounds, or invent a new historical assertion at the binding time. Transport into the mentor binder requires a subsequent explicit integration design; existing registration-time requirements are not silently relaxed here.

For candidate positive SL consumption at frame T:

- The exact WF2-002 owner must be an actual prior canonical observation (`owner.AsOfUtc < T`), Passed with established progression, for the same surviving Start/take. A same-frame WF2-002 pass cannot supply positive ancestry. Keep eligibility market-effective time distinct from its emission/availability; for this run those are 13:33:10Z and 13:35Z respectively, so 13:35Z cannot already consume its own newly emitted owner.
- Raw SL semantic availability and all necessary supporting price/source evidence must be <= T and genuinely resolved in the bounded snapshot, with effective <= observed. Availability does not itself grant eligibility or create replay boundaries.
- Current conflicting/incompatible applicability, reversal, eligibility or SL assertions block consumption; do not fall back to an older favorable fact. Preserve all compatible provenance and conflicting alternatives, with exact semantic equality independent of input order.
- Existing applicable terminal/session/window protections dominate. Scoped lifecycle completion is not trade readiness or permission to revive an invalidated setup. Validate the separately recorded possible same-frame SL guard gap before runtime integration; it remains an inspection concern, not a reproduced failure or a new cancellation rule.

S12 permits historical conditional-order preparation to be documented separately from reversal eligibility. S13's 13:33:10Z recognition is not the first turning tick or proof of the earlier order's eligibility. Neither earlier SL testimony nor later binding retroactively authorizes the reported 13:32Z entry. S7's market interval still uses the approved take and reversal effective-time ancestry; S13 does not resolve intrabar boundary membership, full price coverage or the protective peak's numerical value.

At T, no final High of an open M30/M5/M1 candle, overlapping candle with unresolved boundary ordering, future price, later management value or later human assertion can fill the interval or StopPrice. Closed source candles must actually be available; partial intervals need independently supported observable price evidence, not final OHLC backfilled before close. No universal closed-M30 requirement is introduced.

#### Expected safe results and implementation boundary

These are proposed runtime expectations under ADR 0025, not modifications to the current evaluator. Rule-definition classifications, selector outcomes, evaluator capability, runtime results and global verdict remain distinct.

| Condition for a future Scenario 2 SL adapter | Expected handling |
|---|---|
| No established usable prior WF2-002 ancestry | Waiting; propagate DataUnavailable where the exact prerequisite is unavailable; no SL fact |
| Eligible stage but human protective selection/basis or authentic availability remains unresolved | HumanValidationRequired; no guessed reference/time or SL fact |
| Required numerical StopPrice/protective price or referenced source/feed resolution is unavailable | DataUnavailable; distinguish absent numerical data from an unresolved human selection; no fabricated scalar |
| Conflicting valid driving assertions | HumanValidationRequired; preserve alternatives, no source ranking or latest-wins |
| Malformed identity, timestamp, wrong branch/owner or nonprotective stop | Reject validation; never reinterpret as positive evidence |
| Unique, fully resolved, contemporaneously available evidence with exact valid prior ancestry and protections | May be evaluated for Passed under a later implementation; uniqueness alone is not Passed, Ready, risk approval or execution |

The current real replay remains NQ-SL-001 Waiting because it first requires Standard NQ-M1-003. Its capability stays Implemented and definition HumanValidationRequired; no status is changed to match this candidate table. Risk remains downstream; TP/risk algorithms, trade snapshots, orders, fills, exits, PnL and later 1:1 management are outside this feature.

**Missing evidence, not a new strategy decision:** S6/S7 already define the protective concept and interval. A human-assisted representation can be specified without inventing a general selector. Real Replay #2 still needs a uniquely source-resolved protective reference/price, separately transcribed and human-approved initial StopPrice, genuine selection/availability times and validated native-price applicability. S5's untranscribed visible table value cannot satisfy that requirement. A complete deterministic selection/offset algorithm remains unresolved and is not required to preserve honest human testimony.

**Single smallest next implementation step (`candidate`):** add only an immutable dedicated Scenario 2 SELL human-SL evidence primitive and Missing/Unique/Conflict selector through the existing bounded StrategyReplayContext, with exact prior WF2-002/Start/take validation and lossless provenance. Keep original source times separate from consumption/binding; do not implement an evaluator, workflow convergence, binder change, numeric selector, metadata or downstream consumer yet. Synthetic contract tests must cover foreign owners/feeds/versions, pre-eligibility evidence without progression, same-frame owner rejection, future observations/sources, duplicate provenance, conflicting references/prices, missing source resolution, strict protective side and unchanged historical prefixes. Real-source population remains blocked on the evidence above; this primitive alone will not resolve the actual evaluator's Standard-only dependency. Subsequent runtime integration needs a separate scoped task and regression validation of Standard and terminal behavior.

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
