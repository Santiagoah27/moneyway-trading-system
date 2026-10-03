# ADR 0024: Clarify Nasdaq post-completion 009 first-turn and rebuilt-tracking causal order

## Status

Accepted. This reconciles existing audited strategy semantics and the canonical reconstruction runtime with the post-completion primitives through `f6fef7b`. It introduces no trading rule, runtime implementation, evaluator result or evidence supersession policy.

## Date

2026-10-02.

## Context

[ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md) establishes first turn and lightweight rebuilt tracking before definitive membership and geometry. [ADR 0013](0013-define-nasdaq-post-invalidation-candidate-transition-contract.md) refines cursor ownership, migration dual role and breakout priority; [ADR 0014](0014-clarify-nasdaq-breakout-human-evidence-lifecycle.md), [ADR 0015](0015-define-nasdaq-breakout-evidence-waiting-policy.md) and [ADR 0016](0016-decouple-nasdaq-rebuilt-membership-resolution.md) place ordinary 009 evidence resolution at a frozen rebuild-origin breakout. They do not reverse ADR 0009's first-turn order. The snapshot variants of [ADR 0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) remain distinct lifecycle boundaries.

The post-completion sequence already implemented is pending materialization (`c444358`), membership selection (`19137a0`), member resolution (`65a3616`) and geometry calculation (`f6fef7b`). These isolated primitives do not implement chronological pending/tracking progression. The proposed next handoff, `NasdaqPostCompletionRebuiltCandidateGeometry -> RebuiltTracking`, would lose the causal first-turn event and reverse the established order. Existing ADRs settle the strategy; this ADR documents the missing integration boundary without reverting valid primitives. No new human strategy evidence or video is required.

## Decision

### First turn, migration and cursor

For a lower/HL rebuild, the first eligible closed candle with `Close > Open` starts the provisional bullish turn. For an upper/LH rebuild, `Close < Open` starts the provisional bearish turn. Exact `Close == Open` starts neither. The candle must belong to the same H4 series, be chronologically eligible after the processed cursor and be observable as closed at the current replay boundary. Breakout takes priority over first-turn initialization.

A strict extension of the protection wick supersedes the previous candidate/rebuilt turn. Its candle becomes the effective `MigrationCandle`, with `KnownProtectionAnchor = Low` for HL or `High` for LH. Without breakout, a matching directional body on that same candle can also be the new `FirstTurnCandle`; tracking starts directly, with migration, first turn and cursor equal. This applies both to a Candidate-origin directional migration and to a later migration/reset during rebuild progression. With a contrary body or doji it remains pending. The particular Candidate-origin candle that **produced `RebuildPending`** cannot subsequently be relabeled as its first turn: that branch already established a nonmatching body, and the processed candle cannot be consumed again. A later candle can be both a new migration and a first turn.

The cursor sequence is the source candidate's `LastProcessedCandle`, then the migration entering pending, then every later consumed pending candle (including no-event candles), then `FirstTurnCandle` when tracking starts, then every later consumed tracking candle, and finally the validating breakout candle. Detecting first turn consumes exactly that incoming candle; materializing tracking consumes zero additional candles. Continuing tracking preserves its original first turn until a strict extension supersedes that turn. Human `ObservedAtUtc`, replay `AsOfUtc`, and member timestamps never replace `MarketCursor`.

### Facts before definitive geometry

The canonical tracking state owns `Episode`, `InvalidatingCandle`, `ImpulseTerminalSide`, `CandidateSide`, `OriginGeometry`, `FrozenImpulseTerminal`, effective `MigrationCandle`, numeric `KnownProtectionAnchor`, `FirstTurnCandle` and `LastProcessedCandle`. Its known wick protection is not a definitive rebuilt vertex. It owns neither final membership nor definitive rebuilt `StructuralPrice`/`CandidateGeometry`; it does not infer a partial member collection.

For post-completion progression, retain the pending state's exact current validated pair, structural episode, superseded source candidate and correction/rebuild provenance together with the same causal migration/first-turn/cursor facts. Do not substitute an invalidation episode or fabricate old reconstruction history to reuse an incompatible type. This is a provenance handoff requirement, not a claim that the old tracking class already contains the new post-completion pair.

### Evidence, geometry and formal validation

Ordinary 009 membership becomes required at `BreakoutAwaitingCompletion` for a rebuild origin **without same-candle strict migration**. Ordinarily this follows tracking; the canonical pending transition also permits a strict frozen-terminal breakout before any first turn, regardless of candle body. That route goes directly from pending to the same ordinary breakout evidence boundary. Membership is not a prerequisite for pending progression or tracking initialization. An assertion can already be visible earlier, but visibility does not identify a first turn or validate a candidate.

At the frozen breakout, select currently visible evidence for the exact episode, effective migration identity and candidate side. Resolve the exact asserted members against observable market data, verify the known anchor, calculate definitive geometry, and apply the ordinary completion checks against the **stored** strict terminal break. Members must precede the validating candle; neither guessed membership nor the validating candle can be substituted. ADR 0016 supplies the resolution context independently of obsolete pending/tracking states. No fixed member count, synthesized `+4h` interval or new contiguity requirement is added.

`Missing` or `Conflict` retains the frozen validating-candle cursor under ADR 0015, consuming no later market candle. Usable unique evidence and observable member data allow evidence-only resolution/completion at that same cursor. Geometry alone does not create a formal HL/LH. Formal validation requires the strict structural breakout plus matching definitive geometry, provenance and completion guards. The ordinary path completes there; it does not go from definitive geometry back to earlier tracking or create another provisional Candidate stage.

### Placement of the current post-completion primitives

| Existing primitive | Correct causal role | Current integration limitation |
|---|---|---|
| `c444358` pending materializer | Enter pending at the nonmatching migration candle | Initial entry consumes no additional candle; this does not freeze subsequent pending progression while membership is absent. |
| `19137a0` evidence selector | Select ordinary 009 membership at the frozen breakout boundary | Its input still binds to the supplied pending state; it does not establish first turn, later resets or breakout provenance. |
| `65a3616` member resolver | Resolve selected membership after selection at that boundary | Its result preserves the supplied pending lineage/cursor, rather than an integrated later breakout event. |
| `f6fef7b` geometry calculator | Calculate definitive geometry after successful member resolution, before ordinary completion | Its result inherits that pending cursor and contains no first-turn event or structural validation. It cannot initialize tracking. |

These remain valid isolated selection, resolution and mathematical primitives. The audit found no incorrect geometry formula requiring reversal. Their current pending-bound APIs are **not** a truthful drop-in lifecycle handoff after market progression. Integration needs typed provenance for the effective rebuilt episode/reset and frozen breakout, preserving first turn when present and the actual processed cursor. Do not forge a pending state, fabricate history, rewrite existing results' cursors, or interpret geometry readiness as candidate validation. Defining and implementing that later evidence/completion handoff is separate from the next one-candle primitive.

### Reconciled causal state machine

```text
Candidate
  strict migration, no breakout, nonmatching body/doji -> RebuildPending
  strict migration, no breakout, matching body         -> RebuiltTracking (dual role)

RebuildPending + next observable closed H4 candle
  no breakout, no turn, no migration -> PendingContinues (incoming cursor)
  no breakout, migration, no turn    -> PendingReset (new migration/anchor/cursor)
  no breakout, matching first turn  -> TrackingStarted (reset first if migrating)
  strict terminal breakout          -> BreakoutDetected (priority over turn)

RebuiltTracking + each next observable closed H4 candle
  no migration/breakout -> continue tracking (incoming cursor)
  migration, no breakout -> supersede turn; pending or dual-role tracking
  strict terminal breakout -> BreakoutAwaitingCompletion

Rebuild-origin breakout without same-candle migration (ordinary 009)
  -> visible membership selection
  -> exact member resolution
  -> definitive rebuilt geometry
  -> ordinary completion against stored breakout -> Completed
  Missing/Conflict -> retain frozen breakout

Same-candle migration + breakout
  -> existing directional 007 or human-StructuralPrice 008 branch, not ordinary 009
```

Direct Candidate completion without migration remains the separate existing path. The diagram does not require a first turn for every pending-origin breakout or route 007/008 through 009 membership.

### Late evidence and future-data protection

As `AsOfUtc` advances, pending may retain its state if no eligible candle is available; observable unconsumed candles otherwise progress through the one-candle contract without awaiting 009 membership. Missing membership does not freeze the original pending migration indefinitely. Each incoming candle must close by the current `AsOfUtc`; future candles cannot determine an earlier first turn. Human observations after `AsOfUtc` are invisible, and unavailable asserted members cannot be synthesized.

[ADR 0018](0018-define-nasdaq-invalidated-origin-late-evidence-catch-up.md) and the canonical reconstruction catch-up orchestrator resolve evidence before dispatching each eligible oldest-first candle through the current snapshot reducer. Late origin evidence may initialize at the original invalidation cursor and fold currently observable backlog across pending and tracking. Intermediate fold values belong to the current frame, not rewritten historical evaluations. Catch-up stops at an unresolved frozen breakout or `Completed`; late 009 evidence completes the stored breakout with no additional candle. ADR 0018 does not authorize a generic post-009-completion backlog policy. Post-completion orchestration is separate; this ADR designs no new catch-up system and permits no retroactive consumption outside the audited replay contract.

### Exactly one next implementation task

Implement the deterministic **one-closed-H4-candle pending transition** for `NasdaqPostCompletionRebuildPendingState`, using the canonical `NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator` as the behavioral reference. Retain its existing closed outcomes `PendingContinues`, `PendingReset`, `TrackingStarted` and `BreakoutDetected`, rather than a first-turn-only binary that drops breakout priority. Preserve exact post-completion provenance, pre-candle anchor comparison, effective reset identity, first-turn event when applicable and incoming market cursor. A future tracking payload must be lightweight and pre-geometry. Do not initialize it from the geometry result or require membership to inspect a market candle.

Build and test that primitive first, then the applicable rule, evaluator/reducer and integration in separate scoped work. This ADR does not implement any of them, register an evaluator, or implement a full lifecycle/catch-up system.

## Authoritative implementation and test evidence

All paths below are relative to the repository root; audited tests were read as evidence, not executed for this documentation change.

- `src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/`: `NasdaqPostInvalidationRebuiltCandidateTrackingState`, `NasdaqPostInvalidationRebuiltCandidateTrackingInitializer`, `NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator`, `NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator`, `NasdaqPostInvalidationCandidateTransitionCalculator`, `NasdaqH4CandidateSnapshotReducer`, `NasdaqH4RebuildPendingSnapshotReducer`, `NasdaqH4RebuiltTrackingSnapshotReducer`, `NasdaqH4OrdinaryRebuiltBreakoutEvidenceSnapshotReducer`, `NasdaqHumanRebuiltCandidateVertexMemberResolver`, `NasdaqHumanRebuiltCandidateVertexGeometryCalculator`, `NasdaqOrdinaryRebuiltBreakoutCompletionCalculator` and `NasdaqH4ReconstructionCatchUpOrchestrator` (`.cs`). The current post-completion selector, resolver, pending materializer and geometry types/calculators were checked against this sequence.
- `tests/unit/MoneyWay.Application.UnitTests/Strategies/Nasdaq/ReplayInputs/`: `NasdaqPostInvalidationRebuiltCandidateTrackingInitializerTests` (first turn, doji rejection, dual role, cursor and absence of geometry), `NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculatorTests` (all four outcomes, reset/turn dual role, breakout regardless of body), `NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculatorTests` (continued cursor and reset), `NasdaqH4OrdinaryRebuiltBreakoutEvidenceSnapshotReducerTests` (pending/tracking origins, future/late evidence, frozen cursor, compatible provenance and conflict), and rebuilt member/geometry calculator tests (`.cs`).

## Alternatives considered

- Initialize tracking from resolved geometry: rejected because first turn and market progression causally precede definitive geometry.
- Infer first turn from human membership or reuse the original pending migration: rejected because neither establishes the required body event and processed-candle chronology.
- Block pending on missing membership: rejected because the ordinary evidence wait belongs to the frozen breakout.
- Replace the next transition with a first-turn-only binary: rejected because canonical breakout and reset outcomes would disappear.

## Consequences

Only this specification and its index change. All existing primitives, runtime, tests, registrations and capability metadata remain unchanged. Nasdaq retains 32 RuleIds, 14 required rules, 4 registered evaluators, `RequiredEvaluatorGapCount = 10` and `HasFullRequiredEvaluatorRegistration = false`; `NQ-H4-001` remains `BlockedByUnresolvedSpecification`. Remaining work is a runtime integration gap, not missing human strategy evidence. No trade is authorized by this clarification.
