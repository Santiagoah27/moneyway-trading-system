# ADR 0014: Clarify Nasdaq H4 breakout human-evidence lifecycle

## Status

Accepted as an inventory of existing contracts and explicit open decisions. This ADR does not choose unresolved Missing, Conflict, replay scheduling, or correction policies.

## Date

2026-09-23.

## Context

`BreakoutAwaitingCompletion` can contain an ordinary rebuilt breakout (`CollisionKind.None`, NQ-Q-H4-009), a directional collision (007), or a contrary-body/exact-doji collision (008). The 007 snapshot reducer is deterministic. Ordinary 009 needs human rebuilt membership under [ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md); 008 needs human `StructuralPrice` under [ADR 0010](0010-define-nasdaq-collision-structural-price-replay-input.md). Their selectors and positive completion calculators exist, but no evidence-aware breakout snapshot reducer exists. [ADR 0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) already defines snapshot persistence.

## Scope

"Confirmed" below means expressly defined in accepted ADRs or implemented in code. "Mechanical" means representable by current immutable types and calculators but not composed into a snapshot reducer. "Unresolved" means an explicit project decision or handoff contract is absent. This ADR adds no trading rule, runtime status, snapshot variant, or evaluator.

## Existing source-backed behavior

`StrategyReplayContext.InputObservations` exposes matching strategy/version, provider and symbol inputs only when `ObservedAtUtc <= AsOfUtc`. Observation time controls causal visibility, never authority or the market cursor. A response visible at T2 cannot change the T1 evaluation. Selectors match an exact episode and preserve every compatible supporting record. [ADR 0008](0008-define-nasdaq-human-origin-vertex-conflict-policy.md) decides origin-vertex conflict only: ADR 0009 expressly adopts its compatible-set safety semantics for rebuilt membership, whereas ADR 0010 separately defines exact-price compatibility for 008. ADR 0008's future-evaluator `human_validation_required` mapping for origin Conflict is not a generic 009/008 mapping.

Per [ADR 0011](0011-define-nasdaq-h4-reconstruction-replay-lifecycle.md) and ADR 0012, an ordinary or 008 `BreakoutAwaitingCompletion` snapshot may persist across replay frames while evidence is unavailable. Its episode, collision kind, side, frozen terminal, effective anchor and validating candle remain known. Missing and Conflict are selector outcomes, not snapshot variants. A later visible `Unique` answer may complete that frozen breakout at T2 without consuming another market candle, advancing its `MarketCursor`, or rewriting T1. How the future fold schedules evidence checks and handles market candles arriving while completion is pending remains unspecified.

### Ordinary rebuilt breakout, 009

The ADR 0009 episode binds exact strategy/version, provider, symbol, H4, invalidating candle open, effective prior migration candle open and candidate side. The immutable observation asserts a nonempty set of distinct H4 member open times including the migration candle, plus `ObservedAtUtc` and `SourceReference`; it stores no derived price. The selector returns `Missing` for zero visible sets, `Unique` for one semantic member set (including identical sets in any input order), and `Conflict` for distinct visible sets. Equal derived geometry from different sets does not make them compatible. Every supporting observation remains available, and time/source does not pick a winner.

With `Unique`, the resolver requires an observable closed H4 frame, resolves exact member candles, includes the migration candle and checks that derived protection equals the known migrated anchor. The geometry calculator derives body price and protection. `NasdaqOrdinaryRebuiltBreakoutCompletionCalculator` requires an ordinary Rebuild-origin breakout, exact episode identity, members before the validating candle, matching geometry/anchor and the stored strict break. Its result retains breakout and member resolution; `Completed.Ordinary.MarketCursor` is the original validating candle. These positive stages are implemented. Their snapshot handoff is not: the current member resolver accepts a `RebuildPending` state, while `BreakoutAwaitingCompletion` holds a breakout with typed Rebuild provenance rather than that pending instance. A future adapter must preserve and verify exact identity without fabricating history. That is a runtime representation task, not authority to select membership.

### Human StructuralPrice collision, 008

The ADR 0010 episode is the exact reconstruction episode plus validating/collision candle open and side; an earlier rebuild migration candle is not part of the key. Its immutable observation asserts one decimal `StructuralPrice`, an auditable `SourceReference`, and `ObservedAtUtc` no earlier than collision close. `Missing` means zero visible prices, `Unique` one exact value including compatible duplicates, and `Conflict` distinct visible values. Supporting records remain available. A later differing price does not supersede the earlier one.

Only `Unique` is accepted by `NasdaqHumanStructuralPriceBreakoutCompletionCalculator`. It checks exact episode, strict migration against `PreviousProtectionAnchor`, strict breakout, contrary or exact-doji body, and deterministic `EffectiveProtectionAnchor` from the validating wick. The human price supplies only the body coordinate. Its result retains human provenance separately from market evidence. `Completed.HumanStructuralPrice.MarketCursor` stays at the validating candle. Selector and calculator exist; the evidence-aware snapshot reducer does not.

## Evidence decision matrix

| Route and visible evidence | Selector result | Deterministic completion possible? | Snapshot behavior defined? | Evaluator/runtime status defined? | Later evidence behavior defined? | Correction/supersession defined? |
|---|---|---|---|---|---|---|
| 009, zero member sets | `Missing` — confirmed | No — confirmed | Frozen breakout may persist — confirmed; scheduling unresolved | Unresolved | Later visible `Unique` may complete without rewriting T1/cursor — confirmed; scheduling unresolved | Unresolved |
| 009, one exact set, possibly repeated | `Unique` — confirmed | Yes after member/geometry checks — confirmed | `Completed.Ordinary` representable — confirmed; handoff unimplemented | Unresolved for `NQ-H4-001` | Later conflict cannot retroactively change T1 or win by recency — confirmed; post-completion handling unresolved | Unresolved |
| 009, distinct sets | `Conflict` — confirmed | No selected winner — confirmed | Frozen breakout auditable/persistable — confirmed; fold handling unresolved | Unresolved | No automatic priority; resolution procedure unresolved | Unresolved |
| 008, zero prices | `Missing` — confirmed | No — confirmed | Frozen breakout may persist — confirmed; scheduling unresolved | Unresolved | Later visible `Unique` may complete without rewriting T1/cursor — confirmed; scheduling unresolved | Unresolved |
| 008, one exact price, possibly repeated | `Unique` — confirmed | Yes with stored deterministic anchor — confirmed | `Completed.HumanStructuralPrice` representable — confirmed; reducer unimplemented | Unresolved for `NQ-H4-001` | Later conflict cannot retroactively change T1 or win by recency — confirmed; post-completion handling unresolved | Unresolved |
| 008, distinct prices | `Conflict` — confirmed | No selected winner — confirmed | Frozen breakout auditable/persistable — confirmed; fold handling unresolved | Unresolved | No automatic priority; resolution procedure unresolved | Unresolved |

## Causal and AsOf requirements

`BreakoutAwaitingCompletion.MarketCursor`, `Completed.Ordinary.MarketCursor` and `Completed.HumanStructuralPrice.MarketCursor` all derive from the same `ValidatingCandle`. Evidence arrival changes what may be completed at the current replay AsOf, not when the breakout happened. This cursor behavior is confirmed by the snapshot and completion result contracts. ADR 0012 allows a frame without a new H4 candle to expose newly visible human input; the general fold performing this handoff is unimplemented. Neither selector may inspect input with `ObservedAtUtc > AsOfUtc`. Human correction/supersession remains unresolved. Under current selectors, two differing visible 009 sets or 008 prices remain `Conflict`: the input models have no active/revoked or supersedes identity.

## Confirmed deterministic paths

009 `Unique` permits exact member resolution, geometry and ordinary completion after their input checks. 008 `Unique` permits human-price completion with deterministic wick protection. Neither positive path establishes `NQ-H4-001 passed` or a strategy verdict. Structural completion and rule evaluation are separate layers.

## Explicit unresolved decisions

The project owner still needs to decide:

1. For 009 and 008 `Missing`, when and how does the fold retry the frozen breakout, what evaluator result applies if evaluated, and is any expiry permitted? No timeout or session expiry is defined.
2. For 009 and 008 `Conflict`, what does the fold do while the breakout persists and what evaluator result applies? The origin-only mapping in ADR 0008 does not decide these routes.
3. How may a reviewer correct or supersede immutable 009 membership or 008 price evidence, including authority, effective visibility time and audit retention? Without this, a later differing visible assertion remains Conflict.
4. How does the fold handle later closed market candles while a frozen breakout awaits evidence, then hand off after completion? ADR 0012 fixes the breakout cursor and allows later `Unique` completion, but does not define backfill/scheduling or post-completion handoff.

NO HUMAN STRATEGY EVIDENCE REQUIRED for these architecture/runtime choices. The 009 member identities and 008 price remain human-reviewed inputs under their accepted ADRs.

## Non-goals

This ADR does not map `Missing` or `Conflict` to `waiting`, `human_validation_required`, `data_unavailable` or another result; choose evidence priority; define expiry; rewrite prior frames; implement a selector, reducer, fold or evaluator; or make structural completion imply `NQ-H4-001 passed`. The latter remains `BlockedByUnresolvedSpecification` under its separate structural/status contract.

## Implementation consequences and blockers

`Unique` evidence already has deterministic domain completion paths. An evidence-aware snapshot reducer can reuse them once input/handoff contracts are settled; ordinary 009 additionally needs a truthful adapter from frozen breakout identity to the resolver's pending-state input. `Missing`, `Conflict`, correction/supersession and pending-frame orchestration remain policy blockers. No runtime, metadata or capability registration changes follow from this ADR.

## Alternatives considered

- Treat Missing or Conflict as selected membership/price: rejected by ADRs 0009 and 0010.
- Apply ADR 0008's origin-specific evaluator mapping to every evidence domain: unsupported by its accepted scope.
- Treat later evidence as automatic correction or observation time as priority: rejected by the causal conflict contracts.

## Consequences

Confirmed completion facts and unresolved runtime choices are recorded separately. The project owner must resolve the four questions above before implementing general evidence-aware `BreakoutAwaitingCompletion` orchestration. This clarification requires no new human strategy/video review.
