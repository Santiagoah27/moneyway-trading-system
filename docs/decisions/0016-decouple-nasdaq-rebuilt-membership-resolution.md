# ADR 0016: Decouple Nasdaq rebuilt membership resolution from lifecycle state

## Status

Accepted. This specifies the runtime input seam needed to compose the existing ADR 0009 `Unique` path after an ordinary breakout. It extends [ADR 0014](0014-clarify-nasdaq-breakout-human-evidence-lifecycle.md) and [ADR 0015](0015-define-nasdaq-breakout-evidence-waiting-policy.md) without changing their accepted text or implementing the seam.

## Date

2026-09-23.

## Context

[ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md) makes exact human-selected H4 member identities the input to deterministic rebuilt geometry. The existing selector, member resolver, geometry calculator and ordinary completion calculator implement the individual positive steps. `NasdaqHumanRebuiltCandidateVertexMemberResolver.Evaluate` currently takes `NasdaqPostInvalidationCandidateRebuildPendingState`, a *lifecycle phase*, alongside a `Unique` selection and `StrategyReplayContext`. A frozen `NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion` instead owns `NasdaqPostInvalidationCandidateRebuildBreakoutState`; its `Origin.Rebuild` retains the effective prior migration candle, not the obsolete pending object. Recreating pending or tracking history to call the resolver would misstate provenance.

## Decision

### Current resolver dependency

The resolver reads only the following facts from its pending-state argument. The selector's `Unique` result and the replay context's visible closed H4 frame remain separate inputs.

| Resolver dependency | Why it is needed | Current pending-state source | Frozen ordinary Rebuild-origin breakout |
|---|---|---|---|
| Reconstruction identity: strategy/version, provider, symbol, H4 and invalidating candle open | Build and verify the exact ADR 0009 episode and select matching observations | Strategy/version/provider/symbol from `StrategyReplayContext`; invalidating open from `pending.InvalidatingCandle` | `breakout.Episode` already retains all of these. Match the replay context to this frozen identity; do not use its strategy/version to replace it. |
| `CandidateSide` | Distinguish rebuilt HL from LH and scope the episode | `pending.CandidateSide` | `breakout.CandidateSide` is retained. |
| Effective migration candle, including provider, symbol, H4, open and close | Bind the rebuilt episode and require the selected members to include the exact migration candle | `pending.MigrationCandle` | `breakout.Origin.Rebuild.PriorMigrationCandle` is retained for both pending-origin and tracking-origin ordinary breakouts. |
| Known migrated protection anchor | Reject a selected set whose deterministic wick extreme disagrees with the verified migration | `pending.KnownProtectionAnchor` | `breakout.PreviousProtectionAnchor` is copied from the pending/tracking known anchor. For ordinary `CollisionKind.None`, the validating candle has no strict migration, so `breakout.EffectiveProtectionAnchor` has the same value. |
| Observable closed H4 member candles | Resolve each exact selected open time without inventing candles | `StrategyReplayContext` H4 `AvailableCandles`, not a pending-state field | Still supplied by the causal replay context, not by the breakout or the new resolution context. |

The resolver does **not** read pending `ImpulseTerminalSide`, `OriginGeometry`, `FrozenImpulseTerminal` or `LastProcessedCandle`. It needs no earlier pending/tracking object, first-turn candle, validating candle, later market candle or complete lifecycle history to resolve membership. The ordinary completion calculator separately checks that selected members precede the breakout's validating candle, that geometry matches the frozen effective anchor, and that the validating close strictly breaks the frozen terminal.

### Required semantic inputs and resolution-context contract

Introduce a small immutable `NasdaqHumanRebuiltCandidateVertexResolutionContext` (or repository-equivalent name) containing only:

1. `NasdaqHumanRebuiltCandidateVertexEpisode Episode`: the existing exact ADR 0009 identity, composed from the frozen reconstruction `Episode`, effective migration candle open and `CandidateSide`.
2. `Candle MigrationCandle`: the exact effective migration candle retained in typed Rebuild provenance, including its series and close identity.
3. `decimal KnownProtectionAnchor`: the verified migrated protection anchor before the validating breakout candle.

No separate invalidating candle, first-turn candle, frozen terminal, validating candle, human-selected members, price or geometry belongs in this value. The `Unique` selection supplies the member set and supporting human provenance; the `StrategyReplayContext` supplies only currently visible input observations and closed H4 candles. The context must verify that the migration candle matches its episode/series and that the known anchor is the side-appropriate wick of that effective migration. Member resolution must continue to verify exact observable member identities, migration-candle inclusion and derived protection equality. Neither this context nor the human observation can assert a definitive `StructuralPrice` or breakout validity.

### Frozen-breakout provenance audit

For an ordinary breakout, require `CollisionKind.None`, `HasStrictMigration == false` and `Origin is Rebuild`. The pending and tracking transition calculators both construct that breakout by copying `Episode`, `CandidateSide`, the effective prior `MigrationCandle` and `KnownProtectionAnchor` into `breakout.Episode`, `breakout.CandidateSide`, `Origin.Rebuild.PriorMigrationCandle` and `breakout.PreviousProtectionAnchor`. With no strict migration on the validating candle, `EffectiveProtectionAnchor` equals `PreviousProtectionAnchor`. The full prior migration candle supplies the wick used to verify this anchor. All three resolution-context fields are therefore already retained or unambiguously derivable. **No `BreakoutState` schema change or missing provenance field is required.**

The frozen breakout, rather than the current replay context or a reconstructed earlier phase, is authoritative for these market facts. Runtime derivation must reject the wrong collision/origin and inconsistent episode, migration series or anchor. A pending call site can create the same context from its own `Episode`, `MigrationCandle`, `CandidateSide` and `KnownProtectionAnchor`; both call paths must use one set of resolver semantics.

### Lifecycle-state decoupling

`NasdaqPostInvalidationCandidateRebuildPendingState` remains a valid market lifecycle state while that phase is active. It is not the semantic identity of a rebuilt-member resolution after breakout. Do not store the entire obsolete pending state in `BreakoutState`: an ordinary breakout can arise after `RebuiltTracking`, and retaining a past phase would couple evidence resolution to irrelevant cursor and transition fields. Do not reconstruct a pending or tracking state by replaying transitions, scanning historical candles, inferring migration history or using hidden mutable state. Adapt the resolver to consume the immutable resolution context plus the existing `Unique` selection and replay context; adapt existing pending call sites without changing selection or geometry semantics.

### Causality and future-data protection

The resolution context contains frozen market provenance only. It contains no `AsOfUtc`, human `ObservedAtUtc`, wall-clock value or future candle. `StrategyReplayContext.InputObservations` remains responsible for `ObservedAtUtc <= AsOfUtc`; its H4 frame supplies only observable closed member candles. At later replay `AsOfUtc`, newly visible `Unique` evidence may complete the *same* frozen breakout without consuming another H4 candle. `BreakoutAwaitingCompletion.MarketCursor` and `Completed.Ordinary.MarketCursor` both remain the original `ValidatingCandle`, and earlier frames are not rewritten.

## Implementation consequences

The smallest safe runtime sequence, outside this documentation task, is:

1. Introduce the immutable resolution-context value and its episode/migration/anchor consistency checks.
2. Adapt the existing member resolver to consume it while retaining exact member, H4 visibility and protection checks.
3. Adapt existing pending-state call sites to construct the same context; regression-test their ADR 0009 behavior.
4. Add derivation from a frozen ordinary Rebuild-origin breakout and test both pending-origin and tracking-origin provenance, including rejection of mismatches.
5. Regression-test the selector, member resolution, deterministic geometry and ordinary completion together without changing their outputs.
6. Then implement the focused 009 evidence-aware snapshot reducer with the `Missing | Conflict | Completed` behavior of ADR 0015.

This resolves a software representation seam, not a trading-rule question. **NO HUMAN STRATEGY EVIDENCE REQUIRED.** No video review is needed.

## Non-goals

This ADR changes no human membership, `StructuralPrice`, protection-anchor or breakout rule. It does not implement code, replace the selector or calculators, add evidence priority/supersession, process a market backlog, advance the market cursor, define an `NQ-H4-001` evaluator status or register a capability. `Missing` and `Conflict` continue to preserve the same frozen snapshot under ADR 0015; this ADR addresses only the `Unique` input seam.

## Remaining unresolved items

Human evidence correction/supersession, post-`Completed` handoff, raw arbitrary-history bootstrap, broader near-doji interpretation, the full H4 lifecycle reducer and `NQ-H4-001` rule-status mapping remain separate. The focused 009 reducer is implementable after the resolution-context seam exists; that does not make the evaluator ready.

## Alternatives considered

- Preserve the entire historical pending state in the breakout: rejected because the resolver uses only episode, migration and anchor facts; the breakout may have originated after tracking.
- Recreate pending/tracking state or scan candles after breakout: rejected because that would fabricate lifecycle provenance and risk future-data leakage.
- Derive the ADR 0009 episode from the consuming context's current strategy/version: rejected because the frozen reconstruction owns its stable identity.

## Consequences

The existing frozen breakout has sufficient source-backed provenance for one shared member-resolution contract. A dedicated value separates deterministic evidence resolution from the historical lifecycle phase without changing ADR 0009 semantics or snapshot shape.
