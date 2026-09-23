# ADR 0012: Define Nasdaq H4 reconstruction snapshot variants

## Status

Accepted. This clarifies [ADR 0011](0011-define-nasdaq-h4-reconstruction-replay-lifecycle.md) without changing its canonical replay boundary or implementing it.

## Date

2026-09-22.

## Context

ADR 0011 requires one immutable, replay-local, episode-bound H4 reconstruction snapshot before raw rule evaluation. Its lifecycle map names the states and completion result, but does not say whether the known invalidation before unique origin evidence or the completed result is a snapshot variant. A typed implementation cannot choose its closed variant set without that decision. The existing origin selector has neither an episode nor a market cursor; `StructuralCandidateValidationResult` alone lacks the complete Nasdaq episode and branch provenance. The existing completion wrappers retain both their breakout and their validation result.

## Decision

One snapshot contains **exactly one** active variant for one Nasdaq H4 post-invalidation episode. The closed set is the eight rows below. It starts when a verified `StructureInvalidated` boundary is observed for an already established structural side; it is not raw-history bootstrap. Its `Episode` is created from exact strategy/version, provider, symbol, H4 and the invalidating candle `OpenTimeUtc` at that boundary, before any human origin selection. The `NasdaqHumanOriginVertexEpisode` value contract can represent this identity without a selected member set. The pre-origin variant must also retain the exact `StructuralCandidateTurnBoundaryResult` of kind `StructureInvalidated` and its `CurrentCandle`; the episode and boundary must refer to the same invalidating H4 candle. This is a new **conceptual** typed state, not a newly implemented runtime type.

| Snapshot variant | Existing value or new conceptual type | Entry condition | Episode/identity owner | Market cursor | Human evidence to enter? | Can persist across frames? | Exit event |
|---|---|---|---|---|---|---|---|
| `InvalidatedAwaitingOrigin` | New immutable value: `NasdaqHumanOriginVertexEpisode` + verified invalidation boundary/candle | Verified `StructureInvalidated` | Its episode, constructed from the verified boundary and exact replay strategy/version | Boundary `CurrentCandle` = invalidating candle | No | Yes, while origin is not uniquely usable | Unique visible origin selection, exact member resolution and origin geometry permit opposite-impulse initialization |
| `OppositeImpulse` | `NasdaqPostInvalidationOppositeImpulseState` | Origin resolved and initializer succeeds | State `Episode` | State `LastProcessedCandle`, initially `InvalidatingCandle` | Yes, a `Unique` origin was required for entry | Yes | Qualifying correction start/frozen terminal |
| `Correction` | `NasdaqPostInvalidationCorrectionState` | Correction initializer succeeds | State `Episode` | State `LastProcessedCandle`, initially correction-start candle | No new human answer | Yes | Opposite-body candidate formation |
| `Candidate` | `NasdaqPostInvalidationCandidateState` | Correction transition forms provisional candidate | State `Episode` | State `LastProcessedCandle` = `TerminalCandle` | No new human answer | Yes | Source-backed next-candle transition; only migration without breakout is implemented at this boundary |
| `RebuildPending` | `NasdaqPostInvalidationCandidateRebuildPendingState` | Strict migration without frozen-terminal breakout | State `Episode` | State `LastProcessedCandle`, initially migration candle | No new human answer | Yes | First opposite-body tracking turn, another strict migration, or frozen-terminal breakout |
| `RebuiltTracking` | `NasdaqPostInvalidationRebuiltCandidateTrackingState` | First strict opposite-body turn after migration, possibly on migration candle | State `Episode` | State `LastProcessedCandle`, initially first-turn candle | No new human answer | Yes | Later strict migration resets to pending, or frozen-terminal breakout |
| `BreakoutAwaitingCompletion` | `NasdaqPostInvalidationCandidateRebuildBreakoutState` | Strict frozen-terminal body-close breakout | State `Episode` | State `LastProcessedCandle` = `ValidatingCandle` | No for market breakout; ordinary/008 completion can require later human evidence | Yes for ordinary or 008 while definitive geometry is unavailable; type is also valid for 007 before deterministic completion | Applicable ordinary, 007 or 008 completion succeeds |
| `Completed` | Exactly one existing `NasdaqOrdinaryRebuiltBreakoutCompletionResult`, `NasdaqDirectionalMigrationBreakoutCompletionResult` or `NasdaqHumanStructuralPriceBreakoutCompletionResult` | Applicable completion yields validated candidate with branch provenance | Completion's `Breakout.Episode` | Completion `LastProcessedCandle` = breakout `ValidatingCandle` | Ordinary or 008: visible `Unique`; 007: none after breakout | Yes; terminal reconstruction evidence remains available across later frames | Only a later qualifying structural transition or an explicitly defined canonical active-structure handoff may replace it; neither is implemented by this ADR |

`Completed` is a **terminal snapshot variant**, not an active correction/candidate. The wrapper, rather than a bare `StructuralCandidateValidationResult`, retains the exact episode, frozen terminal, validating candle, candidate geometry and branch-specific provenance. It does not assert that `NQ-H4-001` passed. The source-backed active HH+HL or LL+LH pair persists across sessions until a qualifying structural transition replaces or invalidates it. Accordingly, completion must not be cleared merely because another replay frame or session begins. A future active-structure reducer/handoff must define and implement the qualifying replacement; this ADR does not invent one or treat the terminal wrapper as a complete general H4 evaluator.

`BreakoutAwaitingCompletion` preserves the market event at T1 if ordinary membership or 008 StructuralPrice is not yet available. Its collision kind, episode, candidate side, frozen terminal, effective protection anchor and validating candle remain known. At T2, a newly visible unique human answer may complete the same episode without advancing the breakout market cursor or rewriting T1. The 007 breakout has no post-breakout human gate; a future reducer can complete it in the same causal step. The breakout value remains a legal typed boundary if supplied before that deterministic completion, but 007 does not require a multi-frame waiting policy. Missing/Conflict selector outcomes do not become separate snapshot variants.

Origin `Missing | Unique | Conflict` are selection outcomes from visible `InputObservations`, not snapshot identity or variants. `InvalidatedAwaitingOrigin` holds the invalidation facts even while selection is Missing or Conflict; its cursor remains the invalidating candle. If later H4 candles close before a unique answer, the snapshot does not pretend to have processed them as an opposite impulse. The current `ReplayFrame.AvailableCandles` exposes causally closed history for a future reducer to process after resolution; replay/backfill behavior is outside this snapshot-shape decision. Human `ObservedAtUtc` and `SourceReference` do not replace episode identity or market cursor. Completion wrappers may retain human evidence as provenance once actually resolved; this does not make a selector result the active variant.

At frame T, canonical replay supplies or produces immutable `S(T)` before `IReplayRuleEvaluator` executes. A future reducer may derive `S(T+1)` from the prior value, the next observable closed H4 candles and human inputs visible at T+1. Neither value mutates the other. A frame without new H4 candles may resolve human evidence while preserving the market cursor. The typed context transport remains separate from `InputObservations`, `StrategyReplayContextObservation`, `ReplayRuleEvaluationDecision` and `StrategyVerdict`. No evaluator retains prior-frame state or queries external storage.

All eight variants have an exact episode and source-backed candle cursor. The closed discriminated immutable representation is therefore implementable without deciding a new trading rule. This is **shape readiness**, not reducer readiness: `Candidate` still lacks production transitions for other matrix rows; raw bootstrap, human-evidence status mapping/supersession, broader near-doji interpretation, full lifecycle reduction, `NQ-H4-001` status mapping and canonical integration remain separate blockers.

## Alternatives considered

- Start only after `Unique` origin: rejected because a verified invalidation at T1 would have no explicit causal state while human evidence is unavailable until T2.
- Store `Missing`, `Unique` and `Conflict` as market-state variants: rejected because they are time-bounded human-evidence selection outcomes and do not themselves own the episode/cursor.
- End the snapshot at completion and use only `StructuralCandidateValidationResult`: rejected because the current canonical observation has no structural-result transport and the bare result loses episode and branch provenance. This ADR retains a terminal wrapper until a future explicit handoff or structural transition.
- Clear completion on the next frame or session: rejected because the validated active pair does not expire on that boundary.

## Consequences

ADR 0011 remains unchanged. The next implementation may add only the immutable closed variant representation and typed pre-evaluation context transport. It must not produce snapshots from candles, implement transitions, change metadata/capability, infer status mappings or allow live execution.
