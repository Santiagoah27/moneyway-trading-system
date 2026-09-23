# ADR 0011: Define Nasdaq H4 reconstruction replay lifecycle

## Status

Accepted. This is an orchestration contract, not an implemented evaluator or a new trading rule.

## Date

2026-09-22.

## Context

The post-invalidation H4 primitives and all three rebuilt-breakout completion branches exist, but no canonical replay component carries their causal structural state into the next frame. `IReplayRuleEvaluator` receives only the current `StrategyReplayContext`; `StrategyReplayContextObservation` holds current raw rule evaluations and workflow/setup lifecycle snapshots, not an H4 reconstruction snapshot. The existing ADR 0005 evidence snapshot is produced **after** raw evaluation for setup lifecycle policy, so it cannot supply prior H4 state to `NQ-H4-001`. Human input observations are causally filtered, but are not the market-state fold. Recomputing from whatever bounded candles happen to be present has no defined bootstrap boundary or guaranteed complete episode history.

## Decision

Canonical multi-timeframe replay will own an **execution-local, immutable, strategy-owned H4 structural snapshot fold before raw rule evaluation**. A generic, typed strategy extension may receive the previous H4 snapshot, the current bounded `StrategyReplayContext`, and only visible replay inputs, then return a new immutable snapshot. Generic orchestration retains and passes that snapshot to a stateless evaluator through an explicit typed context/evidence boundary. It must not hardcode Nasdaq states, use hidden evaluator state or create an alternate replay pipeline. A frame with no newly closed H4 candle may update human-evidence resolution of an already observed episode without advancing its market cursor. The exact generic API, storage schema and result-to-`ReplayRuleEvaluationDecision` mapping are deferred to runtime design.

The existing pipeline remains: multiple `CandleSeries` → synchronized replay frame → bounded `StrategyReplayContext` plus prior immutable H4 snapshot → strategy-owned H4 fold → stateless `IReplayRuleEvaluator` → `StrategyReplayContextObservation` → safe outcomes → canonical outcome run → diagnostics. The H4 snapshot is domain evidence, not `RuleEvaluationResult`, `StrategyVerdict`, `ready`, `no_trade` or an order. The evaluator never emits a verdict or trade. If an implementation stores this snapshot on an observation, that observation must retain the exact causal state separately from raw evaluations; current `StrategyReplayContextObservation` does not do so.

Snapshot identity must bind exact strategy/version, provider, symbol, H4 series, reconstruction episode and market cursor. An immutable pending breakout retains its collision kind, invalidating and validating candle identities, candidate side, frozen terminal, effective protection anchor and source evidence; `ObservedAtUtc` never becomes `LastProcessedCandle`. Any completed result retains its geometry, `StructuralCandidateValidationResult`, evidence and causal breakout identity. Consecutive snapshots cannot substitute a different episode or strategy version. Prior observations remain immutable audit records.

At `AsOfUtc = T`, the fold may use only closed H4 candles observable at T, matching human observations with `ObservedAtUtc <= T`, and causal state already available by T. Appending candles or human observations after T must not change the T result. If breakout occurs at T1 while required human evidence is absent, preserve its market breakout state with unfinished completion. At T2, visible unique evidence may complete that **same** episode; T1 is not rewritten, and T2 does not pretend the breakout happened then. Later conflicting evidence does not automatically supersede an earlier assertion. The fold cannot select an authority where the applicable source has none.

### Audited lifecycle boundary

`StructureInvalidated` is the causal closed-candle event. It is known before the post-invalidation origin vertex is selected. The table separates implemented primitives from a complete transition machine; `LastProcessedCandle` always denotes market processing, never human review time.

| Node / domain type | Entry and cursor | Definitive `StructuralPrice`; human gate | Implemented continuation |
|---|---|---|---|
| Origin evidence, `NasdaqHumanOriginVertexObservationSelection` | Exact invalidation episode; no new market cursor from observation | `Missing` or `Conflict`: no origin geometry. `Unique`: member resolver and geometry calculator yield body price and wick anchor | Only `Unique` can initialize the opposite impulse. Invalidation remains known on `Missing`/`Conflict`. |
| `NasdaqPostInvalidationOppositeImpulseState` | `NasdaqPostInvalidationOppositeImpulseInitializer`; cursor = invalidating candle | Origin geometry resolved; provisional impulse terminal is calculable | One later closed candle per opposite-impulse transition; correction start freezes terminal. |
| `NasdaqPostInvalidationCorrectionState` | Correction initializer after `CorrectionStarted`; cursor = first correction candle | Frozen impulse terminal exists; correction geometry is provisional, no validated new pair | One-candle correction transition continues the correction or forms candidate. |
| `NasdaqPostInvalidationCandidateState` | Opposite-body candidate turn; cursor = `TerminalCandle` | `CandidateGeometry` is complete for the provisional turn, not a validated structural pair | Migration **without** breakout enters rebuild pending. No production one-candle transition currently handles this state's other matrix rows, including direct breakout and simultaneous migration/breakout. |
| `NasdaqPostInvalidationCandidateRebuildPendingState` | Strict migration-only reset; cursor = migration candle | New wick extreme known; definitive rebuilt body price absent | One-candle pending transition can remain/reset pending, start tracking, or detect breakout. |
| `NasdaqPostInvalidationRebuiltCandidateTrackingState` | First strict opposite-body turn, possibly migration candle; cursor = turn candle | Turn is provisional; definitive multi-candle rebuilt price absent | One-candle tracking transition advances on every closed candle, resets on stricter wick extreme, or detects breakout. |
| `NasdaqPostInvalidationCandidateRebuildBreakoutState` | Strict frozen-terminal body close; cursor = `ValidatingCandle` | Breakout, collision kind, episode, candidate side and effective wick anchor known; definitive rebuilt body price may still be absent | Completion branches below; no human observation advances cursor. |
| `StructuralCandidateValidationResult` | Successful completion of the same breakout episode | Definitive body price + protection anchor; not an evaluator status | Domain result is available for later mapping; no `NQ-H4-001` evaluator consumes it yet. |

For ordinary `CollisionKind.None` (`NQ-Q-H4-009`), the human rebuilt-member selector resolves `Missing | Unique | Conflict`. `Unique` permits exact-member geometry and `NasdaqOrdinaryRebuiltBreakoutCompletionCalculator`; `Missing` cannot fabricate membership and `Conflict` has no winner. The breakout state remains auditable in either case. For `NQ-Q-H4-007`, a strict same-candle migration and directional body has deterministic `NasdaqDirectionalMigrationBreakoutCompletionCalculator`; no human answer is required **after** breakout. For `NQ-Q-H4-008`, strict same-candle migration with opposite or exact-doji body has deterministic breakout, validating candle, candidate side, episode and `EffectiveProtectionAnchor`. The human StructuralPrice selector resolves `Missing | Unique | Conflict`; only `Unique` permits `NasdaqHumanStructuralPriceBreakoutCompletionCalculator`. Neither missing nor conflict erases those market facts. These completion calculators do not fill the missing transitions out of `NasdaqPostInvalidationCandidateState`.

### Human evidence and unresolved mappings

| Evidence boundary | Known while missing | Progression and status authority |
|---|---|---|
| Initial/raw bootstrap | Closed H4 history exists, but the initial visible anchor and historical scan termination are not selected | No arbitrary raw-history initialization. Missing-input `RuleEvaluationResult` and duplicate/conflict handling are unspecified. Separate from post-invalidation origin. |
| Origin vertex, ADRs 0007–0008 | Invalidation candle, direction and episode | `Unique` allows origin geometry. `Missing` blocks geometry; its runtime result is unspecified. `Conflict` blocks geometry and ADR 0008 explicitly prescribes `human_validation_required` for a future evaluator/orchestrator. |
| Rebuilt membership, ADR 0009 | Breakout, frozen terminal, candidate side, episode, effective anchor, cursor | `Unique` can complete ordinary geometry. `Missing`/`Conflict` stop completion; neither evaluator result is specified. |
| Collision StructuralPrice, ADR 0010 | Breakout, collision candle, candidate side, episode, effective anchor, cursor | `Unique` can complete geometry. `Missing`/`Conflict` stop completion; neither evaluator result is specified. |

Equal observations within an episode are compatible duplicates under their respective ADRs; different visible values conflict. `ObservedAtUtc` controls visibility, not authority. Explicit correction, reviewer priority and supersession of human assertions remain unspecified independently for bootstrap, origin, rebuilt membership and collision price. A later conflicting assertion cannot win by recency. A later **market** migration superseding a prior rebuild episode is distinct from human-evidence correction.

The exact comparisons in implemented transitions use `Close > Open`, `Close < Open`, and exact `Close == Open`. They need no invented near-doji tolerance. `NQ-Q-H4-002` remains open only if small but nonzero bodies must be classified differently for broader H4 interpretation; it does not invalidate these exact-body primitives. It remains one of the broader `NQ-H4-001` specification questions, not a missing numeric parameter that this fold may choose.

The required `NQ-H4-001` definition asks for the formally closed 08:00 H4 context, HH/HL or LL/LH structure, Breakout/Wickfill/Fakeout classification and permitted direction. A completed post-invalidation candidate alone does not prove that rule `passed`; no complete mapping to `passed`, `failed`, `waiting`, `human_validation_required` or `data_unavailable` is specified. Raw-history bootstrap, classification and direction, evidence policies, candidate transition coverage and replay transport all remain prerequisites. Its `BlockedByUnresolvedSpecification` capability remains accurate; evaluator registration is not authorized by this ADR.

### Readiness matrix

| Concern | State | Evidence/source | Blocks `NQ-H4-001`? |
|---|---|---|---|
| Raw bootstrap | Human boundary unresolved | Nasdaq open questions `NQ-Q-H4-001`, `004` | Yes |
| Post-invalidation deterministic transitions | Partial chain; candidate-state matrix not fully consumed | Production calculators listed above | Yes, for complete lifecycle |
| Origin human vertex | Observation, selector, resolver, geometry implemented | ADRs 0007–0008 | Missing policy and transport block |
| Ordinary rebuilt breakout | `Unique` completion implemented | ADR 0009 and ordinary calculator | Missing/Conflict mapping blocks evaluator |
| `NQ-Q-H4-007` | Deterministic completion implemented | Directional calculator | No additional human gate |
| `NQ-Q-H4-008` | `Unique` human-price completion implemented | ADR 0010 and collision calculator | Missing/Conflict mapping blocks evaluator |
| ADR 0009 Missing/Conflict | Domain selection implemented, result mapping open | ADR 0009 | Yes |
| ADR 0010 Missing/Conflict | Domain selection implemented, result mapping open | ADR 0010 | Yes |
| Human supersession | No explicit authority/revision chain | ADRs 0007–0010 | Yes for corrected/conflicted evidence |
| Near-doji | Exact doji handled; nonzero small body unresolved | `NQ-Q-H4-002` | No for exact-body primitives; possible broader H4 blocker |
| Explicit lifecycle transport | Contract defined here, runtime absent | Replay context/observation APIs | Yes |
| Evaluator status mapping | Only origin Conflict maps explicitly | ADR 0008; rule definition | Yes |
| Canonical integration | Existing pipeline; H4 pre-evaluation fold absent | ADRs 0003, 0005 | Yes |

### Runtime work classification and next feature

| Unit | Classification |
|---|---|
| One-candle H4 lifecycle reducer | `IMPLEMENTABLE NOW` only for source-backed rows; candidate-state nonmigration/direct-breakout rows still need a production transition. A complete reducer is not yet implemented. |
| Immutable H4 lifecycle snapshot and replay transport | `IMPLEMENTABLE NOW` as a narrow infrastructure/domain feature under this ADR; no evaluator result mapping needed. |
| Origin Missing/Conflict mapping | `BLOCKED BY HUMAN-EVIDENCE POLICY` for Missing; Conflict mapping is `ALREADY SPECIFIED` in ADR 0008 but not integrated. |
| ADR 0009 and ADR 0010 Missing/Conflict mappings | `BLOCKED BY HUMAN-EVIDENCE POLICY` for evaluator results; domain selectors are `ALREADY IMPLEMENTED`. |
| Human correction/supersession | `BLOCKED BY HUMAN-EVIDENCE POLICY`. |
| Initial bootstrap input contract | `BLOCKED BY SPECIFICATION` for anchor/scan boundary and evidence policy. |
| `NQ-H4-001` evaluator | `BLOCKED BY SPECIFICATION` and missing lifecycle transport; do not register. |
| Canonical H4 integration | `BLOCKED BY SPECIFICATION` for full evaluator; typed transport itself is implementable now. |

**Smallest safe next runtime feature:** an immutable, episode-bound H4 lifecycle snapshot/domain state with a replay-local, typed pre-evaluation transport seam. Implement only identity, cursor, causal pending-breakout retention and explicit input/output handoff; do not add an evaluator, decide Missing/Conflict results or fill candidate transition rows by inference. This removes the stateless-evaluator transport gap while leaving all rule decisions in their proper layer.

## Alternatives considered

- Hidden mutable state in an evaluator: rejected because replay runs could contaminate each other and the current evaluator contract is stateless.
- Reconstruct from the current bounded candle window on every evaluation: rejected as the canonical contract because the window need not include the human-selected bootstrap and entire episode history.
- Reuse ADR 0005 setup lifecycle evidence: rejected because it is captured after raw rule evaluation and is bound to setup progression, not H4 structural state.
- Put H4 details directly into generic replay types: rejected because generic orchestration must remain strategy-neutral.

## Consequences

This decision defines where a future H4 fold enters canonical replay and which causal facts must survive frames. It does not implement runtime, change metadata/capability, define a missing-evidence verdict or permit live execution.
