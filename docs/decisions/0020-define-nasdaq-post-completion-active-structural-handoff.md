# ADR 0020: Define Nasdaq post-completion active structural handoff

## Status

Accepted as a human-source clarification and runtime representation audit. The universal post-completion laws below are `confirmed`; automatic multi-candle active-extreme membership remains `unresolved`. No runtime implementation or evaluator status is changed.

## Date

2026-10-01.

## Context

[ADR 0019](0019-clarify-nasdaq-post-completion-h4-lifecycle.md) correctly recorded the evidence then available: the new active HH/LL coordinate and universal confirming-candle role were not established. This ADR incorporates **later human source evidence** and resolves those questions without modifying that accepted ADR. Earlier unresolved statements must be read with this later clarification, rather than treated as contrary source authority.

The human review supplied for this decision identifies **Video 2**, approximately:

- `01:40–01:55`: exact candle-body direction.
- `03:05–05:10`: confirmation and newly active structure, especially approximately `05:00`, where the mentor identifies the newly established pair as the current structural points.
- `05:40–06:15`: continuation of the same causal mechanics.

These are the user's supplied human-review findings, not an independently accessed video or a verbatim transcript. No video URL or additional source identity was supplied. They supersede prior AI/runtime interpretations of the two strategy questions. The [audited specification](../strategies/nasdaq/strategy-specification.md), existing deterministic primitives and [ADR 0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) remain relevant to the separate representation audit.

## Decision

### Confirmed universal structural contract

All of the following are **CONFIRMED** (`confirmed`) human source truth, common to `DirectCandidate`, 007 `Directional`, 008 `HumanStructuralPrice` and 009 `Ordinary` completion. Those branches differ in how the preceding candidate obtains definitive geometry; they do not have different future structural laws. Human-assisted 008/009 evidence remains auditable.

| Structural fact | Bullish completion | Bearish completion |
|---|---|---|
| Strict confirming event | `Close > prior HH.StructuralPrice` validates the preceding HL | `Close < prior LL.StructuralPrice` validates the preceding LH |
| Immediately active pair | New HH + newly validated HL | New LL + newly validated LH |
| New extreme concept | Highest price reached by the breakout impulse/new peak | Lowest price reached by the breakout impulse/new floor |
| New extreme `ProtectionAnchor` | `max(High)` of that impulse/peak; retain its owning candle/member evidence | `min(Low)` of that impulse/floor; retain its owning candle/member evidence |
| New extreme `StructuralPrice` | `max(max(Open, Close))` across the body/member ownership of that new peak | `min(min(Open, Close))` across the body/member ownership of that new floor |
| Next continuation comparison | Strict `Close > active HH.StructuralPrice` | Strict `Close < active LL.StructuralPrice` |
| Next invalidation comparison | Strict `Close < newly validated HL.StructuralPrice` | Strict `Close > newly validated LH.StructuralPrice` |

The confirming candle participates in the new extreme. Other contiguous candles legitimately forming the same peak/floor belong to its geometry and provenance. Body and wick coordinates are distinct; their extrema need not have the same candle owner. These formulas confirm the geometry **given the legitimate members**, not a new automatic member selector. Exact equal coordinates do not require a first/last owner tie-break; preserve their contributing source events. `ProtectionAnchor` does not become an executable Stop Loss.

Intermediate price that has not strictly broken the relevant structural references does not replace the established pair: the mentor's structural meaning is that nothing structural has happened yet while price remains inside the active range. Ongoing impulse extreme development and correction phase bookkeeping remain distinct from a new confirmed pair. No wick-only break, equality break, numeric tolerance or inferred intrabar path is introduced.

### Confirming-candle role and causal cursor

These roles are also **CONFIRMED**, independent of completion branch:

| Completed direction | Exact confirming body | Initial next phase and membership |
|---|---|---|
| Bullish | `Close > Open` | Breakout impulse remains open; no new correction yet. A later qualifying bearish body starts it. |
| Bullish | `Close < Open` | The same candle validates the old HL, participates in the new HH and immediately starts the next bearish correction as its **first member**. |
| Bearish | `Close < Open` | Breakout impulse remains open; no new correction yet. A later qualifying bullish body starts it. |
| Bearish | `Close > Open` | The same candle validates the old LH, participates in the new LL and immediately starts the next bullish correction as its **first member**. |

The market input is consumed **once**. An explicit completion-to-active-state handoff derives these multiple causal consequences from that single observation. It must not submit the confirming candle to another ordinary one-candle market reducer. Membership in the new correction does not put the candle back into the preceding candidate turn; branch-specific definitive candidate provenance, such as 007's same-candle vertex, remains intact.

The handoff consumes no additional candle. Initial next-state `MarketCursor` equals `Completed.MarketCursor`, the confirming candle, for both open-impulse and immediate-correction cases. Immediate correction starts with exactly that candle as its first correction member. Later normal market steps require a strictly later closed candle. When completion uses late human evidence, the historical market cursor remains the confirming candle; evidence visibility/observation time must remain separate. This decision does not itself extend the [ADR 0018](0018-define-nasdaq-invalidated-origin-late-evidence-catch-up.md) catch-up orchestrator beyond its current stop.

Exact `Close == Open` reuses existing neutral semantics: it does not start a correction, can contribute applicable body/wick extreme facts, and may belong to an already-active correction when its existing rules permit. A strict structural confirming close remains independent of candle color. No nonzero near-doji threshold is supplied or reopened.

### New structural Episode and required provenance

A **new structural Episode identity** for the next active cycle is **CONFIRMED** by this later human clarification. It starts from the completed known pair at the confirming cursor; the completed reconstruction Episode must not be reused as the new cycle's identity. Retain it as predecessor provenance.

Identity terminology needs an explicit distinction: ADR 0019 confirmed a new **invalidation reconstruction** Episode at a later verified invalidation and left the intervening active-pair identity as an architecture gap. The current `NasdaqHumanOriginVertexEpisode` is keyed by `InvalidatingCandleOpenTimeUtc`. It cannot truthfully identify the just-established active cycle by pretending its confirming candle invalidated the new structure. A later genuine invalidation still creates its own event-bound reconstruction identity. The new active-cycle identity schema and relationship to that later identity are architecture work, not another trading rule.

The future handoff must retain strategy/version, provider, symbol, H4 identity, new-cycle identity and predecessor identity; completed branch and successful break observation; broken prior HH/LL reference and origin/frozen-terminal chain; newly validated HL/LH geometry and its definitive evidence; confirming candle OHLC and cursor; exact breakout/peak/floor member identities and contributions to both active-extreme coordinates; and the initialized phase/correction membership. Preserve human origin selection/resolution, 008 StructuralPrice supporting observations, 009 member resolution and supporting observations, with their source references and observation/visibility times. Do not reduce human provenance to derived prices or backdate late evidence. Prior immutable snapshots remain unchanged.

### Completion payload audit

Audit baseline: `af58960`. The [snapshot variants](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqH4ReconstructionSnapshot.cs) all expose `ValidatedCandidate` and exact confirming `MarketCursor`. `CandidateSide.Lower` means the newly validated HL/bullish pair; `Upper` means LH/bearish pair. `ValidatedCandidate.CandidateGeometry` supplies the **correction-side** `StructuralPrice` and `ProtectionAnchor`, not the new active HH/LL. `BreakObservation.ReferenceLevel` is the frozen preceding terminal already broken, not the next continuation reference.

| Completed branch | Retained definitive candidate and event | Available provenance / active-extreme limitation |
|---|---|---|
| Direct | Original candidate geometry, validating candle, successful validation | Candidate keeps old correction members and `TerminalCandle`, but intervening no-event candles overwrite `LastProcessedCandle`; no complete developing breakout-member sequence. |
| 007 Directional | Validating-candle directional candidate geometry, breakout and validation | That candidate is the HL/LH vertex, not a normalized HH/LL. Typed breakout origin and confirming OHLC do not guarantee the complete incoming breakout peak/floor members. |
| 008 HumanStructuralPrice | Human-selected candidate body price, effective migrated wick anchor, breakout and validation | `HumanPriceSelection` embeds supporting observations. They establish the candidate coordinate, not membership of the new active HH/LL. Complete breakout-member provenance is absent. |
| 009 Ordinary | Resolved rebuilt-member candidate geometry, breakout, validation and `MemberResolution` | Selected members identify the rebuilt HL/LH. Full supporting observation selection is carried by outer focused-reduction/catch-up results, not `Completed.Ordinary` alone. Complete new HH/LL membership is absent. |

Every branch retains the original invalidation Episode and origin/frozen-terminal geometry through its candidate/breakout. Full human origin selection is not embedded in these completed snapshots; the origin-evidence reduction/catch-up result is a separate audit boundary. No branch guarantees all facts needed for auditable multi-candle active-extreme derivation. Even after a member boundary is clarified, preserving already observable breakout candles and their owners is an **ARCHITECTURE/RUNTIME REPRESENTATION GAP**, not missing human strategy evidence. A one-candle confirming example cannot justify dropping preceding legitimate members in general.

### Runtime representation and primitive reuse

| Existing type/primitive | Audit conclusion |
|---|---|
| `NasdaqH4ReconstructionSnapshot.Completed` | Terminal validated reconstruction result; no normalized active pair, next phase or new active-cycle identity. Retain as predecessor, not as complete next state. |
| `NasdaqPostInvalidationOppositeImpulseState` / initializer | Provisional opposite impulse after verified invalidation, with unvalidated origin/terminal and an invalidation-bound Episode. It cannot truthfully represent an established HH+HL/LL+LH pair. |
| `NasdaqPostInvalidationCorrectionState` | Correction of that provisional post-invalidation vector before pair validation; its invalidation Episode, origin and frozen-terminal semantics are not the new valid-side correction contract. Do not force reuse. |
| `NasdaqPostInvalidationCandidateState` | Provisional candidate before opposite-pair validation; it is not the just-established active pair. Its continuation preserves a cursor, not complete breakout members. |
| `StructuralTurnGeometryResult` and geometry calculators | Reusable dual-coordinate value/calculation for **supplied members**; geometry result does not carry full member ownership or Episode/phase. |
| `CorrectionBoundaryObservationCalculator`, `CorrectionStartTransitionCalculator` | Reusable exact extreme/body observations and opposite-body start classification once correct valid-side references are supplied; they do not select peak members or construct an active pair. |
| `CorrectionTurnLifecycleResult` | Valid-side awaiting/active correction fragment; useful semantics, but no complete pair, prices or Episode. `CreateAwaitingCorrectionStart()` also lacks the consumed confirming cursor. |
| `StructuralCandidateTurnBoundaryCalculator` | Requires supplied prior structural reference, validated opposite point, correction-origin extreme and preceding active-turn members. Not an immediate open-impulse handoff; its existing no-extension confirmation path waits for correction. The universal new dual-role contract cannot be assumed already wired by calling it again on the same candle. |
| `StructuralBodyCloseBreakCalculator` | Strict comparison is directly reusable at a canonical updated H4 boundary with the table's supplied levels/directions. It selects no references or phase. Its current-boundary API cannot be assumed to process an arbitrary historical backlog candle without a truthful boundary/context adapter. No new break algorithm is needed. |

No existing pair/anchor type found in the audited path carries the whole required established-pair state. A future explicit active-structure representation is required: both confirmed point geometries and provenance; new identity with predecessor link; ongoing-impulse versus active-correction phase; consumed cursor; and phase members/extreme evidence. For trend-directional or neutral confirming bodies it must retain the valid pair while awaiting continued extreme development, opposite-body correction start or strict protected-point invalidation. For an opposite body it must initialize the valid-side correction at the same confirming cursor and include that candle once. Exact schema, construction invariants, provenance transport across frames and ordered backlog continuation are **architecture/runtime gaps**. This ADR does not implement or finalize that schema.

### Membership audit and the one remaining source question

The [existing source](../strategies/nasdaq/strategy-specification.md) defines first opposite-body correction start, pre-start waiting exclusion, active-turn continuity and terminal membership. The post-invalidation impulse transition aggregates body/wick extrema from its known invalidating start through the first corrective candle before freezing. These facts define phase boundaries for their supplied contexts. `CorrectionStartTransitionCalculator` explicitly does not select historical turn boundaries; `StructuralTurnGeometryCalculator` aggregates only caller-selected members. Neither establishes the exact first member of a **new post-completion peak/floor body region**.

The specification also distinguishes a selected final turning vertex from the entire incoming leg and records visual adjacent-member limits. That origin/rebuilt-vertex discussion cannot silently be promoted into a new HH/LL selector. Conversely, the provisional post-invalidation terminal's whole-segment aggregation cannot silently be promoted into the new established peak's body ownership. The later evidence confirms the new peak/floor formula, confirming-candle participation and legitimate contiguous membership, but does not specify whether the body member set is the full breakout segment or a final contiguous subset, or the exact backward boundary of such a subset. Phase end alone does not select that set.

This is one **GENUINE REMAINING STRATEGY EVIDENCE GAP** (`unresolved` for automatic membership), distinct from runtime's discarded member data. No max-wick-only fallback, whole-leg fallback, first/last tie-break, fixed count, clustering tolerance or near-doji threshold is authorized.

**HUMAN STRATEGY EVIDENCE REQUIRED** — minimum question:

> For the new post-confirmation HH/LL body coordinate, which exact closed H4 candles form the peak/floor member set: the entire causal breakout impulse segment, or only its final contiguous turning region; if the latter, what source-backed condition selects its first member?

One reviewed multi-candle H4 example with included/excluded members and the applicable boundary rule would resolve this question. It does not reopen the confirmed formula, the four-branch convergence, confirming-candle role, body-direction start, cursor, identity or protected-point comparisons. No additional branch-specific strategy evidence is requested.

### Implementation readiness and independent boundaries

Readiness is **C: still blocked by one narrowly defined human strategy question**, with separate architecture/runtime representation work also required. It is not B: existing types cannot represent the new valid pair and phase truthfully. It cannot yet be called A for a fully automatic multi-candle handoff, because a selected-members geometry calculator is not a deterministic member selector. Source-backed computation from an already supplied legitimate set remains understood; this decision creates no new human-input envelope.

Post-completion continuation from the established known pair is **CONFIRMED distinct from raw-history bootstrap**. No historical scan should recreate a structure completion already established. The focused membership question is not the arbitrary-history initial anchor/scan-termination problem; raw bootstrap remains separately unresolved. Broader nonzero near-doji remains separately unresolved and is not the focused handoff blocker.

This H4 lifecycle evidence does not establish `Completed => NQ-H4-001 passed`, `ready`, trading authorization or canonical evaluator integration. Metadata, capabilities and all existing rule statuses remain unchanged.

## Alternatives considered

- Separate Direct/007/008/009 future laws: rejected by the universal human-source contract.
- Reuse provisional post-invalidation impulse/correction states for a validated pair: rejected because their invariants and identity describe a different phase.
- Feed the confirming candle into a second ordinary reducer: rejected; explicit same-observation initialization preserves one market consumption.
- Derive the new extreme solely from the confirming candle or blindly aggregate the whole incoming leg: rejected as universal fallbacks without the exact member boundary.
- Treat discarded breakout members or outer-only human evidence as new strategy gaps: rejected; these are runtime provenance gaps.
- Rebootstrap raw history or map completion to evaluator success: rejected as separate unsupported transitions.

## Consequences

ADR 0019's two broad questions are resolved by later human evidence: the active-pair geometry concept and universal same-close phase roles are now confirmed. The remaining source request concerns only exact multi-candle peak/floor membership. A subsequent architecture contract must represent the new active Episode, normalized pair, explicit single-observation handoff, phase/cursor and full provenance before extending runtime catch-up. Existing `Completed` stop behavior remains unchanged. This task modifies only this ADR and its index; no C#, tests, snapshots, reducers, orchestrator, evaluator, metadata/capabilities, API/UI/persistence or Forex changes.

`NEXT_RECOMMENDED_ACTION: SPECIFICATION_CLARIFICATION`
