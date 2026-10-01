# ADR 0018: Define Nasdaq invalidated-origin late-evidence catch-up

## Status

Accepted. This records the owner's architecture and causal replay decisions that close the waiting and backlog questions identified in [ADR 0017](0017-clarify-nasdaq-invalidated-origin-evidence-replay-lifecycle.md). ADR 0017 remains unchanged as the audit of the earlier boundary. This ADR defines no trading rule, evaluator status or runtime implementation.

## Date

2026-10-01.

## Context

After a verified H4 `StructureInvalidated` event, `NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin` holds the exact invalidation and keeps `MarketCursor` on its invalidating candle. [ADRs 0007](0007-define-nasdaq-human-origin-vertex-replay-input.md) and [0008](0008-define-nasdaq-human-origin-vertex-conflict-policy.md) define the origin membership input and visible `Missing | Unique | Conflict` selection. The member resolver, deterministic origin geometry calculator and opposite-impulse initializer support the `Unique` handoff. Focused snapshot reducers consume one later closed H4 candle at a time. ADR 0017 left open what happens when origin evidence arrives after additional H4 candles have closed.

The canonical replay frame may advance `AsOfUtc` and expose more closed H4 candles while the structural snapshot still points to the invalidating candle. Without an explicit policy, a later `Unique` origin could skip those candles, pretend the review was visible in earlier frames, or leave the current structural state incomplete. The owner chooses ordered catch-up at the **current** replay frame, with immutable earlier results.

## Decision

### Waiting on origin evidence

At each later `StrategyReplayContext`, select only matching origin observations visible at that context's `AsOfUtc`. There is no independent timer, wall-clock poll or background retry. `ObservedAtUtc` controls visibility, never priority or market time. `AsOfUtc` is the current canonical replay/evidence-visibility time; `MarketCursor` is the last H4 candle structurally consumed by this episode. While waiting, `AsOfUtc` may be later than `MarketCursor.CloseTimeUtc`. The gap represents observable market candles that this unresolved lifecycle has not consumed.

| Visible origin selection | Snapshot and cursor | Market-candle processing | Evidence consequence |
|---|---|---|---|
| `Missing` | Preserve the **same** `InvalidatedAwaitingOrigin` snapshot, its exact invalidation state and invalidating-candle `MarketCursor` | Consume no later H4 candle; retry selection only on a later replay context | No timeout, candle-count or session expiry, rejection or fallback origin |
| `Conflict` | Preserve the **same** snapshot and cursor | Consume no later H4 candle; do not select a membership or advance dependent structure | No automatic winner by arrival order, time, source, vote or member-set operation |
| `Unique` | Resolve the original invalidation episode and initialize `OppositeImpulse` with the invalidating-candle cursor | The evidence handoff consumes no later H4 candle; ordered catch-up is a separate orchestration step | Retain all compatible human provenance, resolved members and deterministic origin geometry |

While `Missing` or `Conflict`, no hidden `OppositeImpulse`, `Correction`, `Candidate` or later structural progression occurs. Later H4 candles remain unconsumed history, even though canonical replay can observe them. The snapshot may remain pending until replay ends if usable evidence never appears. Incompatible visible observations remain `Conflict` under the current selector: a later assertion neither corrects nor supersedes earlier evidence. An explicit correction/revocation contract is separate future work. ADR 0008's `human_validation_required` meaning applies to a future **origin evidence evaluator/orchestrator**; this ADR does not emit an `NQ-H4-001` `ReplayRuleEvaluationDecision.Status` or `StrategyVerdict`.

### Immediate and late `Unique`

If `Unique` is visible before a later H4 candle needs processing, the focused evidence handoff resolves the selected origin members against the original episode, derives body/wick origin geometry using existing calculators, and initializes the existing `OppositeImpulse` state. Its `MarketCursor` remains the invalidating candle. No additional market candle is consumed by that handoff.

If H4 candles closed while origin evidence was unusable, the **same** handoff occurs when the current selection first becomes `Unique` at replay frame `Tk`. Under today's selector this can follow `Missing`; an existing `Conflict` cannot turn `Unique` merely through another incompatible assertion, because no correction/supersession contract exists. The handoff does not substitute `Tk` for the invalidating candle or jump the impulse cursor to the latest H4 candle. After it, the higher-level H4 lifecycle orchestrator catches up eligible, unconsumed H4 candles in chronological order, oldest first, to derive the **current** structural state at `Tk`.

The catch-up set contains only closed candles of the same provider, symbol and H4 series that are observable at the current `StrategyReplayContext.AsOfUtc` and strictly later than the snapshot's structural `MarketCursor` under the existing non-overlap/chronology rules. A candle at or before the cursor is not processed again. No candle closing after `AsOfUtc` participates; no missing candle is synthesized, no aggregate candle replaces individual candles, and no exact `+4h` spacing is added beyond existing market-data contracts. If the current context cannot supply the required closed H4 history, the orchestrator cannot fabricate a completed catch-up state; the handling of unavailable market data remains with the applicable replay-data contract.

Each eligible candle is passed once to the focused reducer matching the **current** snapshot variant. That reducer performs one immediate lifecycle transition for that one candle and returns an immutable next snapshot. The orchestrator then chooses the appropriate reducer for the next candle. Ordered catch-up may therefore cross `OppositeImpulse`, `Correction`, `Candidate`, `RebuildPending`, `RebuiltTracking` and `BreakoutAwaitingCompletion` as existing transitions allow; it may not skip a lifecycle boundary or change a focused reducer to accept a collection. Internal intermediate states are fold values for `Tk`, not historical snapshots newly asserted for T1 or T2.

### Evidence boundaries and stopping

The orchestrator resolves an available evidence-only transition before selecting the next market candle. If ordered catch-up reaches `BreakoutAwaitingCompletion`, it applies the existing branch-specific completion contract. A deterministic 007 completion may proceed without human evidence; 008 and ordinary 009 require their own currently visible evidence. If required evidence is `Missing` or `Conflict`, stop with that frozen breakout and consume no later H4 candle for the episode; [ADR 0015](0015-define-nasdaq-breakout-evidence-waiting-policy.md) governs its wait. If evidence is `Unique`, the evidence-only completion may occur without another market candle, subject to existing checks. On `Completed`, stop catch-up even if more H4 candles are observable. Post-`Completed` replacement, a new episode, and processing any remaining backlog are outside this decision.

If no eligible candle remains, retain the current snapshot. Catch-up does not run past an unresolved evidence boundary, does not create another episode and does not clear a terminal `Completed` snapshot because a new frame or session begins. The orchestration contract is bounded by the current context and remains replay-local; it introduces no mutable singleton lifecycle state, hidden database state or wall-clock input.

### Causal audit and provenance

At T0 the invalidating candle may create `InvalidatedAwaitingOrigin`. At T1 and T2, `Missing` or `Conflict` leaves that snapshot and its cursor unchanged. If evidence first becomes `Unique` at Tk, its `ObservedAtUtc <= Tk` makes it usable **at Tk**. The ordered fold over previously unconsumed candles constructs a current state for Tk only. It does not assert that the origin was known at T1/T2, rerun their evaluator with Tk evidence, emit synthetic historical evaluations, or overwrite their stored `StrategyReplayContextObservation` results. A T1 result remains the T1 result permanently. Information after Tk cannot affect Tk.

For compatible `Unique` origin observations, the focused handoff must retain the selector's complete supporting provenance, exact resolved members and resulting deterministic geometry alongside the initialized `OppositeImpulse` state. The current member resolver accepts one observation, while the selector can return several compatible observations; a future typed adapter/result must preserve their shared semantic membership and all supporting records without treating one reviewer as authoritative. Human evidence does not own future market path, impulse classification or later geometry.

### Component boundary and implementation order

The focused `InvalidatedAwaitingOrigin` evidence reducer is now implementation-ready with the shape `InvalidatedAwaitingOrigin + StrategyReplayContext -> Missing | Conflict | Resolved(OppositeImpulse)`. It has **no market-candle parameter** and performs no backlog iteration. `Missing` and `Conflict` return the original frozen snapshot; `Resolved` retains the evidence selection, member resolution, geometry and initialized impulse state with the invalidating-candle cursor.

The future scoped H4 lifecycle orchestrator owns selection of currently eligible unconsumed H4 candles and sequential dispatch through the existing single-candle reducers. The safe implementation order is: focused origin evidence reducer with immediate `Missing`, `Conflict` and `Unique` tests and complete `Unique` provenance; then a scoped ordered catch-up orchestrator with evidence-boundary and `Completed` stops. Neither step requires implementing post-`Completed` handoff. This contract fits the canonical path: synchronized `CandleSeries` replay -> bounded `StrategyReplayContext` -> replay-local pre-evaluation H4 snapshot -> stateless evaluator.

This policy starts only after verified invalidation of **known** H4 structure. Arbitrary raw-history bootstrap, origin-evidence correction/supersession, post-`Completed` handoff, `NQ-H4-001` evaluator mapping and broader near-doji interpretation remain separate or unresolved. **NO HUMAN STRATEGY EVIDENCE REQUIRED.** No mentor/video review is needed. Metadata and capabilities are unchanged; `NQ-H4-001` remains `BlockedByUnresolvedSpecification`.

## Alternatives considered

- Consume H4 candles while origin membership is `Missing` or `Conflict`: rejected because origin-dependent structural progression has no usable origin.
- Jump directly to the latest candle when evidence appears: rejected because it drops individual causal transitions and their boundaries.
- Recreate T1/T2 evaluations with Tk evidence: rejected because the evidence was not visible at those earlier `AsOfUtc` values.
- Put backlog iteration inside a focused one-candle reducer: rejected because it breaks the established single-step contract.
- Expire the pending origin after elapsed time, candles or a session: rejected by the owner's no-expiry policy.
- Resolve incompatible human assertions by recency, source or priority: rejected; no supersession contract exists.

## Consequences

The origin evidence handoff and late-evidence catch-up contract can now be implemented in separate focused components without inventing replay scheduling. Earlier replay results stay immutable while the current frame may incorporate evidence first visible there. Catch-up stops at unresolved evidence or terminal completion. This ADR changes no C#, tests, selectors, evaluator, metadata, capability or trading rule and does not implement the general H4 lifecycle reducer.
