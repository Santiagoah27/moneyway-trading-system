# ADR 0009: Define the Nasdaq human-reviewed rebuilt-candidate vertex replay input

## Status

Accepted. This decision defines the semantic payload, validation boundary and reconstruction identity contract. Current implementation status and remaining runtime work are recorded under Consequences.

## Date

2026-09-22.

## Context

After an existing H4 `CandidateProvisional` makes a strict new HL `Low` or LH `High` **without** breaking the frozen provisional terminal, the old candidate and its body geometry are superseded. The new wick extreme is known, but the final turning vertex may contain visually selected adjacent candles. Exact multi-candle membership remains `NQ-Q-H4-009 = human_validation_required`; assigning the migration candle's body or retaining the old `StructuralPrice` would invent a rule. A later strict migration in the same reconstruction supersedes this rebuild episode again.

The existing `NasdaqPostInvalidationCandidateState` carries the invalidating candle, candidate side, human origin geometry, frozen terminal, complete candidate geometry, original correction members and `TerminalCandle`, with `LastProcessedCandle = TerminalCandle`. It cannot represent a known migrated wick with no definitive rebuilt body geometry or serve automatically as the post-migration provisional tracker. `IStrategyReplayInputObservation` supplies strategy/version, provider, symbol, `ObservedAtUtc` and `SourceReference`; `StrategyReplayContext.InputObservations` exposes matching records only when `ObservedAtUtc <= AsOfUtc`. H4 candles have unique `OpenTimeUtc` within their provider/symbol/timeframe series. [ADR 0007](0007-define-nasdaq-human-origin-vertex-replay-input.md) selects the **earlier origin vertex**; [ADR 0008](0008-define-nasdaq-human-origin-vertex-conflict-policy.md) resolves duplicate/conflicting observations for that different domain fact.

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

The causal lifecycle is `CandidateProvisional` → verified migration-only reset → rebuild pending with known wick and no definitive body price → first strict opposite-body closed H4 turn → lightweight provisional rebuilt-candidate tracking → chronological processing of every later closed H4 candle → strict frozen-terminal breakout → retrospective definitive vertex classification. A rebuilt HL turns provisionally on `Close > Open`; a rebuilt LH on `Close < Open`. Exact doji starts neither. The migration candle may itself be that first opposite-body turn, so no second candle is forced. Its provisional tracker starts with `LastProcessedCandle` equal to that processed turn candle; each subsequent processed H4 candle advances this market cursor, regardless of whether the candle belongs to the final vertex. A stricter HL `Low` or LH `High` before breakout supersedes the provisional turn and returns tracking to the new strict extreme. The cursor is never derived from selected-member order, `ObservedAtUtc`, `AsOfUtc` or the latest candle in a dataset.

The first turn establishes provisional existence, not definitive multi-candle `StructuralPrice`. At the strict later `Close > frozen provisional H` for HL or `Close < frozen provisional L` for LH, the mentor classifies the final turning region retrospectively. Only then does this ADR's visible, nonconflicting human membership supply exact members for deterministic definitive geometry; the result must match the known migrated protection anchor. The existing member resolver and geometry calculator perform that deterministic calculation without making the earlier turn definitive. A breakout observed before human evidence becomes visible cannot acquire that evidence in its earlier replay frame; the missing-evidence evaluator status remains open. The existing complete `NasdaqPostInvalidationCandidateState` retains its original formation contract and must not be constructed from rebuilt geometry alone while its correction-member and terminal-candle invariants do not describe this route. Neither provisional tracking nor this input makes `NQ-H4-001` evaluator-ready.

### Reconstruction identity propagation

The exact strategy identity that owns a reconstruction is causal episode data, not ambient evaluator configuration. Once the origin-vertex episode is resolved, its `StrategyId` and immutable `StrategyVersion` must remain unchanged through opposite impulse, correction, candidate, rebuild pending, rebuilt tracking and breakout. A consumer must not substitute a Nasdaq constant, the latest/current version, a default or nullable value, or the identity of whichever `StrategyReplayContext` later happens to invoke completion. Evidence visibility (`ObservedAtUtc <= AsOfUtc`) and exact episode equality are independent requirements; satisfying either one does not satisfy the other.

The existing `NasdaqHumanOriginVertexEpisode` is the smallest source-backed owner for the reconstruction's stable identity: strategy, version, provider, symbol, H4 and invalidating-candle identity. It must be retained by `NasdaqHumanOriginVertexMemberResolution` and propagated unchanged into every long-lived post-invalidation state. Rebuild-specific identity remains the stable reconstruction identity plus the effective migration candle and `CandidateSide`, as already represented by `NasdaqHumanRebuiltCandidateVertexEpisode`. A stricter migration may create a new rebuild episode, but it does not change the parent strategy/version binding.

The current runtime flow does not yet satisfy that contract. Its identity inventory is:

| Runtime value | StrategyId | StrategyVersion | Provider / symbol / H4 | Invalidation identity | Migration identity | CandidateSide |
|---|---|---|---|---|---|---|
| `StrategyReplayContext` | Yes | Yes | Yes | No | No | No |
| `NasdaqHumanOriginVertexObservation` / `NasdaqHumanOriginVertexEpisode` | Yes | Yes | Yes | Yes | No | No |
| `NasdaqHumanOriginVertexMemberResolution` | No | No | Via resolved candles | Via `InvalidatingCandle` | No | No |
| `NasdaqPostInvalidationOppositeImpulseState` | No | No | Via candles | Yes | No | No; only `TerminalSide` |
| `NasdaqPostInvalidationCorrectionState` | No | No | Via candles | Yes | No | Yes |
| `NasdaqPostInvalidationCandidateState` | No | No | Via candles | Yes | No | Yes |
| `NasdaqPostInvalidationCandidateRebuildPendingState` | No | No | Via candles | Yes | Yes | Yes |
| `NasdaqPostInvalidationRebuiltCandidateTrackingState` | No | No | Via candles | Yes | Yes | Yes |
| `NasdaqPostInvalidationCandidateRebuildBreakoutState` | No | No | Via candles | Yes | Yes | Yes |
| `NasdaqHumanRebuiltCandidateVertexEpisode` / member resolution | Yes | Yes | Yes | Yes | Yes | Yes |

Identity is first dropped when `NasdaqHumanOriginVertexMemberResolver` materializes `NasdaqHumanOriginVertexMemberResolution`: the source observation and replay context agree on strategy/version, but the resolution retains only candles. `NasdaqPostInvalidationOppositeImpulseInitializer` therefore cannot transfer strategy/version into the structural state, and every later transition preserves only market and structural fields. The rebuilt-member resolver currently constructs a rebuilt episode from its calling context plus pending-state candle identity; that proves which evidence is visible in that context, but it cannot prove that the pending or breakout state itself originated under the same strategy version.

The minimum future runtime change is to retain `NasdaqHumanOriginVertexEpisode` in `NasdaqHumanOriginVertexMemberResolution`, copy it into `NasdaqPostInvalidationOppositeImpulseState`, and propagate the same value through `NasdaqPostInvalidationCorrectionState`, `NasdaqPostInvalidationCandidateState`, `NasdaqPostInvalidationCandidateRebuildPendingState`, `NasdaqPostInvalidationRebuiltCandidateTrackingState` and `NasdaqPostInvalidationCandidateRebuildBreakoutState`. Their existing initializers, constructors and one-candle transition calculators must copy rather than recreate it. `BreakoutState` must expose that stable identity, directly or through the typed episode, so ordinary completion can prove exact equality with `NasdaqHumanRebuiltCandidateVertexEpisode` across strategy, version, provider, symbol, H4, invalidation, effective migration and candidate side. This adds audit identity only; it changes no migration, breakout, body/wick geometry, membership or collision rule.

## Alternatives considered

| Alternative | Assessment | Decision |
|---|---|---|
| Reuse the human origin-vertex observation | It asserts membership for a different vertex before the opposite impulse and is bound to invalidation, not a later migration. | Rejected |
| Store a reviewer-entered `StructuralPrice` or reuse the old candidate body | Creates a competing or obsolete coordinate source and can violate retrospective geometry. | Rejected |
| Bind selection to a session, arbitrary GUID or original invalidation alone | Session is irrelevant; invalidation alone cannot distinguish multiple migration resets. | Rejected |
| Bind exact member identities to the invalidation, strict migration candle and candidate side | Reproducible episode scope, causal evidence and deterministic downstream geometry. | Selected |

## Consequences

The rebuild-pending state, human observation, selector, exact member resolver, deterministic geometry calculator, rebuilt tracker and their one-candle transitions are implemented. Exact strategy/version propagation through that structural state chain remains a runtime prerequisite before a rebuilt membership can complete an ordinary breakout safely. Exact definitive multi-candle membership is still human-reviewed; correction/supersession, missing-evidence runtime result and downstream orchestration remain open. The reviewed directional same-candle migration-and-validation case retains its separate precedence, while contrary/doji-body `NQ-Q-H4-008` remains a distinct human `StructuralPrice` boundary. No evaluator, metadata, capability or live-order permission changes follow from this clarification.
