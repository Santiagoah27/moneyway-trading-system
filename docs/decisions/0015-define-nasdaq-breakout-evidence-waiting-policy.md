# ADR 0015: Define Nasdaq H4 breakout evidence waiting policy

## Status

Accepted. This extends [ADR 0014](0014-clarify-nasdaq-breakout-human-evidence-lifecycle.md) with owner-confirmed architecture/runtime decisions. ADR 0014 remains unchanged as the record of what was known before these decisions. This ADR does not define an `NQ-H4-001` evaluator status or implement a reducer.

## Date

2026-09-23.

## Context

The ordinary rebuilt breakout (009, [ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md)) needs human-reviewed member identities. The contrary-body or exact-doji collision (008, [ADR 0010](0010-define-nasdaq-collision-structural-price-replay-input.md)) needs a human `StructuralPrice`. Their selectors return `Missing`, `Unique` or `Conflict`. ADR 0014 identified the implemented positive completion paths and the unresolved behavior of a `BreakoutAwaitingCompletion` snapshot when evidence is Missing or Conflict. [ADRs 0011](0011-define-nasdaq-h4-reconstruction-replay-lifecycle.md) and [0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) establish causal replay and the persistent frozen breakout variant. The project owner has now decided how these evidence outcomes affect that structural lifecycle.

## Decision

### Missing, Conflict and Unique

For both 009 and 008, `Missing` is a pending evidence condition. It cannot complete the breakout. Preserve the same `BreakoutAwaitingCompletion` snapshot and retry evidence selection in later replay frames using only observations visible at each frame's `AsOfUtc`. No automatic timeout, candle-count expiry, session-end expiry, rejection or fallback evidence applies. The snapshot may remain pending indefinitely if usable evidence never becomes visible.

For both routes, `Conflict` blocks completion. Preserve the same `BreakoutAwaitingCompletion` snapshot; do not select an observation by arrival time, source, order or other priority. Resolving incompatible human assertions requires human validation at the **evidence lifecycle** boundary. This is not an automatic `ReplayRuleEvaluationDecision.Status = human_validation_required` or `StrategyVerdict` mapping. With the current immutable observation and selector contracts, review alone has no defined operation that removes or supersedes an incompatible visible assertion; the selector remains `Conflict` while such assertions remain visible.

When the applicable selector returns `Unique`, the existing positive domain path may complete the **original** frozen breakout after its ordinary input checks. For 009 this is exact member resolution, deterministic rebuilt geometry and ordinary completion. For 008 this is selection of the exact `StructuralPrice`, deterministic wick-based `EffectiveProtectionAnchor` and human-price completion. Neither path consumes a new H4 candle or decides `NQ-H4-001` status. A `Unique` answer is not a bypass for episode identity, observable closed-candle checks or calculator validation.

| Route | Visible selection | Structural completion | Snapshot and market cursor | Evidence lifecycle | Rule status |
|---|---|---|---|---|---|
| 009 | `Missing` | No | Same `BreakoutAwaitingCompletion`; original validating-candle cursor | Pending; retry at later `AsOfUtc`; no expiry | Not decided here |
| 009 | `Unique` | Member resolution, geometry, ordinary completion if checks pass | `Completed.Ordinary`; original validating-candle cursor | Deterministically completable | Not decided here |
| 009 | `Conflict` | No | Same `BreakoutAwaitingCompletion`; original validating-candle cursor | Human validation required; no automatic winner | Not decided here |
| 008 | `Missing` | No | Same `BreakoutAwaitingCompletion`; original validating-candle cursor | Pending; retry at later `AsOfUtc`; no expiry | Not decided here |
| 008 | `Unique` | Selected price and human-price completion if checks pass | `Completed.HumanStructuralPrice`; original validating-candle cursor | Deterministically completable | Not decided here |
| 008 | `Conflict` | No | Same `BreakoutAwaitingCompletion`; original validating-candle cursor | Human validation required; no automatic winner | Not decided here |

### Frozen structural progression and causal replay

While 009 or 008 evidence is `Missing` or `Conflict`, the reconstruction episode is frozen at its detected breakout. Retain the exact `BreakoutState`, `ValidatingCandle`, `MarketCursor`, collision kind and structural candidate facts. Do not advance `LastProcessedCandle`, the validating candle, the market cursor or candidate state. Later closed H4 candles are not consumed or interpreted by **that episode** before completion; no parallel reconstruction, hidden candidate update or automatic restart is implied.

Canonical replay may still reach later frames with later `AsOfUtc`. This changes which human observations are visible, not the frozen market cursor. Only matching observations with `ObservedAtUtc <= AsOfUtc` may affect a frame. At T, `Missing` leaves the breakout pending. If evidence first becomes visible and `Unique` at T+N, completion at T+N uses the original breakout and retains its original validating-candle `MarketCursor`. The result at T remains unchanged. Human `ObservedAtUtc` is visibility metadata, never breakout market time.

### No evidence supersession yet

There is no correction, revocation or supersession mechanism for 009 membership or 008 price observations. A later observation does not invalidate an earlier one, and `ObservedAtUtc` does not imply latest-wins. Distinct visible member sets for the same 009 episode or distinct visible prices for the same 008 episode remain `Conflict` under the existing selectors. Compatible duplicate assertions remain compatible. A future correction feature needs a separate explicit contract; this ADR adds no `SupersedesId`, revision order, active flag or source priority.

### Completion boundary and separate concerns

After successful completion, `Completed` remains the terminal snapshot until a future qualified structural event or explicit handoff/replacement contract exists, as in ADR 0012. This ADR does not define how later market candles are caught up, how another reconstruction begins, or any post-completion handoff. Those broader lifecycle questions do not block a focused 009/008 evidence-completion reducer. Ordinary 009 still needs a truthful handoff from the frozen breakout to the existing member resolver's pending-state input, as identified in ADR 0014.

`Missing` pending, `Conflict` requiring evidence-level human validation, and `Unique` permitting deterministic completion are **evidence lifecycle** outcomes. They are not automatic `ReplayRuleEvaluationDecision.Status`, `RuleEvaluationResult`, `NQ-H4-001 passed` or `StrategyVerdict` values. `NQ-H4-001` evaluator mapping, raw arbitrary-history bootstrap and other unaffected H4 blockers remain unresolved. Structural completion alone cannot make the evaluator ready.

## Implementation consequences and remaining boundaries

These decisions unblock focused evidence-aware snapshot reducers for ordinary 009 and human-`StructuralPrice` 008, subject to the existing positive calculator checks and the 009 input adapter. A reducer may reselect evidence at the current replay `AsOfUtc`; on `Missing` or `Conflict` it must return the unchanged frozen snapshot, and on successful `Unique` completion it must retain the validating-candle market cursor. It must not consume a later H4 candle or rewrite an earlier frame. This ADR specifies no runtime code, evaluator mapping, metadata or capability change.

Still unresolved are a future human evidence correction/supersession mechanism, post-`Completed` handoff/new structural reconstruction, `NQ-H4-001` evaluator statuses, raw arbitrary-history bootstrap and other existing H4 specification blockers. **NO HUMAN STRATEGY EVIDENCE REQUIRED** for the architecture/runtime decisions in this ADR; no video review is needed.

## Alternatives considered

- Expire a pending breakout by time, candle count or session: rejected by the owner's no-expiry decision.
- Consume later H4 candles while the breakout waits: rejected by the frozen-episode decision.
- Resolve incompatible assertions by recency, source or implicit correction: rejected; no supersession or priority contract exists.
- Map evidence lifecycle labels directly to `NQ-H4-001` status or a verdict: rejected because structural completion and evaluation remain separate.

## Consequences

The original breakout remains causally auditable through later evidence-only replay frames. Missing and Conflict cannot silently advance structure or select an unsupported answer. A later visible Unique answer can complete the same market event without future-data leakage. The separate correction, evaluator and post-completion contracts remain open.
