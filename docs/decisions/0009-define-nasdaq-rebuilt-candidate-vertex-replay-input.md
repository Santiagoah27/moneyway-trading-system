# ADR 0009: Define the Nasdaq human-reviewed rebuilt-candidate vertex replay input

## Status

Accepted. This decision defines the semantic payload and validation boundary; it does not implement an observation type, transport, selector, pending state or evaluator.

## Date

2026-09-22.

## Context

After an existing H4 `CandidateProvisional` makes a strict new HL `Low` or LH `High` **without** breaking the frozen provisional terminal, the old candidate and its body geometry are superseded. The new wick extreme is known, but the final turning vertex may contain visually selected adjacent candles. Exact multi-candle membership remains `NQ-Q-H4-009 = human_validation_required`; assigning the migration candle's body or retaining the old `StructuralPrice` would invent a rule. A later strict migration in the same reconstruction supersedes this rebuild episode again.

The existing `NasdaqPostInvalidationCandidateState` carries the invalidating candle, candidate side, human origin geometry, frozen terminal, candidate geometry and last processed candle. It cannot itself represent a known migrated wick with no definitive rebuilt body geometry. `IStrategyReplayInputObservation` supplies strategy/version, provider, symbol, `ObservedAtUtc` and `SourceReference`; `StrategyReplayContext.InputObservations` exposes matching records only when `ObservedAtUtc <= AsOfUtc`. H4 candles have unique `OpenTimeUtc` within their provider/symbol/timeframe series. [ADR 0007](0007-define-nasdaq-human-origin-vertex-replay-input.md) selects the **earlier origin vertex**; [ADR 0008](0008-define-nasdaq-human-origin-vertex-conflict-policy.md) resolves duplicate/conflicting observations for that different domain fact.

## Decision

A positive immutable **post-migration rebuilt-candidate vertex observation** asserts only that a human selected the exact closed H4 candles of one candidate HL/LH turning vertex after one verified migration-only reset. It does not assert a breakout, validated pair, `NQ-H4-001` passage, calculated `StructuralPrice`/`ProtectionAnchor`, automatic cluster discovery or a numeric near-equal tolerance. It is not a `NasdaqHumanOriginVertexObservation` and cannot substitute for one.

The minimum semantic identity and payload are:

1. The existing replay-input envelope: exact Nasdaq strategy ID/version, provider, symbol, authoritative UTC `ObservedAtUtc` and nonempty auditable `SourceReference`.
2. H4 timeframe and the original invalidating candle's series-unique `OpenTimeUtc`, binding the parent reconstruction across sessions.
3. The series-unique `OpenTimeUtc` of the closed H4 candle that made the strict new candidate wick extreme **without** a frozen-terminal breakout. Together with the parent invalidation and candidate side (`Lower` HL or `Upper` LH), this identifies one rebuild episode; a later strict migration has a different candle identity and a different episode. No arbitrary GUID or session date is needed. The asserted side must match the supplied causal state rather than override it.
4. A nonempty immutable **set** of distinct selected H4 member `OpenTimeUtc` values in the same series. Order conveys no strategy meaning and no contiguity, fixed count or automatic adjacency criterion is inferred. The set must include the migration candle as its known core strict extreme. Earlier failed zig-zag members are not imported automatically; a candle belongs only if this human selection explicitly includes it.

The observation's own validation checks identity fields, UTC timestamps, nonempty/distinct membership and provenance. Transition-aware validation must additionally verify an actual `CandidateProvisional`-to-rebuild-pending event: the migration candle follows the previous processed candle, makes the strict side-appropriate wick extreme, does **not** strictly break the frozen terminal, and belongs to the same invalidation, side and H4 series. The source members must resolve to exact already-closed H4 candles visible at the consuming `AsOfUtc`; none may lie outside the causally available reconstruction or include a later validating breakout/expansion candle as an ordinary member. The selected set must include the migration candle. A later strict migration supersedes this episode for current candidate selection; evidence for the earlier episode remains historical but cannot silently attach to the new one. Neither a reviewer assertion nor an observation constructor can manufacture the migration event.

The known migrated `ProtectionAnchor` is calculated from the strict migration candle's `Low` for HL or `High` for LH. After exact member resolution, the existing structural calculators own rebuilt geometry: HL `StructuralPrice = min(min(Open, Close))` and `ProtectionAnchor = min(Low)` over selected members; LH uses `max(max(Open, Close))` and `max(High)`. Before accepting the selection, a future consumer must verify the derived protection anchor equals the known current migrated extreme. If it differs, the selection is inconsistent with that rebuild episode and cannot complete geometry. The observation stores member identities, never manually entered derived prices. It supplies no `NQ-Q-H4-008` same-candle contrary/doji-body `StructuralPrice` answer; that separate case needs a different human decision.

`ObservedAtUtc` cannot precede the migration candle's close or the close of any selected member. It records when the human-reviewed assertion became observable and does not backdate an earlier historical turning event. At replay `T1` before observation, the rebuild remains unresolved; at `T2` when the observation becomes visible, member resolution and geometry may proceed if all other conditions hold. `T2` cannot rewrite `T1`. The evidence belongs to the H4 structural episode and does not expire on a day/session boundary. The producer, storage, UI and transport through canonical context construction are outside this decision.

For this **new** observation type, compatible repetitions for the same rebuild episode are those with the same exact member set, regardless of order, observation time or provenance; preserve all supporting records. Different visible sets for the same episode conflict and cannot be resolved by first/last observation, majority, union, intersection, largest set, price or source order. This explicitly adopts the safety semantics of ADR 0008 for the distinct rebuild-episode key; ADR 0008 itself continues to govern only origin vertices. `ObservedAtUtc` controls visibility, not authority: a later conflicting assertion does not retroactively conflict earlier replay frames and is not an automatic correction. Explicit correction/supersession of human assertions remains unresolved. Zero visible observations leave geometry unavailable; the exact evaluator `RuleEvaluationResult` for missing evidence is also unresolved. No new runtime status is defined here.

The future composition is `CandidateProvisional` → verified migration-only reset → immutable rebuild-pending state with known wick and no definitive body price → visible, nonconflicting human membership → exact H4 member resolution → deterministic rebuilt geometry → rebuilt provisional candidate → later causal processing. The rebuild-pending state can be implemented before the replay-input value object: its causal identity, side, origin, frozen terminal, migration candle, known anchor, last processed candle and audit evidence are already identifiable without fabricating `StructuralPrice`. The value object is also semantically implementable independently, while canonical transport, conflict selection, correction and missing-evidence mapping remain later integration work. Neither component alone makes `NQ-H4-001` evaluator-ready.

## Alternatives considered

| Alternative | Assessment | Decision |
|---|---|---|
| Reuse the human origin-vertex observation | It asserts membership for a different vertex before the opposite impulse and is bound to invalidation, not a later migration. | Rejected |
| Store a reviewer-entered `StructuralPrice` or reuse the old candidate body | Creates a competing or obsolete coordinate source and can violate retrospective geometry. | Rejected |
| Bind selection to a session, arbitrary GUID or original invalidation alone | Session is irrelevant; invalidation alone cannot distinguish multiple migration resets. | Rejected |
| Bind exact member identities to the invalidation, strict migration candle and candidate side | Reproducible episode scope, causal evidence and deterministic downstream geometry. | Selected |

## Consequences

The semantic observation contract and rebuild-pending state boundary are concrete enough for focused future value types. Exact multi-candle membership is still human-reviewed; its producer, canonical transport, selector, explicit correction/supersession, missing-evidence runtime result and downstream orchestration are not implemented. `NQ-Q-H4-008` remains separate. No evaluator, metadata, capability or live-order permission changes follow from this ADR.
