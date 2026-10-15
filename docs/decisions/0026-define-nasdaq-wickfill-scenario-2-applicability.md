# ADR 0026: Define Nasdaq Wickfill Scenario 2 applicability

## Status

Accepted for specification of a `confirmed`, `context_specific` SELL branch. Runtime support is not implemented or activated. This decision does not modify rule metadata, capabilities, workflow, evidence binders, execution or the registered strategy version.

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

S3 closes the human-source audit for the currently evidenced SELL Scenario 2 concept. It does not close the deterministic algorithm or final contextual eligibility contract. The source-supported sequence is:

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
| Autonomous post-sweep reversal geometry, exact structural reference and final contextual eligibility predicate | `unresolved`; no CHoCH/M5/M1 or pivot-break mapping is authorized |
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
