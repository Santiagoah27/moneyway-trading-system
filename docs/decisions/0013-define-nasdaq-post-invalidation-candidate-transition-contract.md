# ADR 0013: Define Nasdaq post-invalidation candidate transition contract

## Status

Accepted. This refines the `Candidate`, `BreakoutAwaitingCompletion` and `Completed` payload contracts of [ADR 0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md), while retaining the eight top-level variants and the replay boundary of [ADR 0011](0011-define-nasdaq-h4-reconstruction-replay-lifecycle.md). It specifies no runtime implementation or evaluator result.

## Date

2026-09-23.

## Context

The [Nasdaq H4 specification](../strategies/nasdaq/strategy-specification.md) classifies the closed-candle events after a provisional candidate, but the production `NasdaqPostInvalidationCandidateRebuildTransitionCalculator` handles only strict migration without breakout. Its `CandidateState.LastProcessedCandle` and the current snapshot's `Candidate.MarketCursor` always equal `TerminalCandle`. Returning that state after a no-event candle would claim that the new candle was not consumed. The state also has no direct-completion wrapper, and the existing rebuilt-breakout state assumes a historical `PriorMigrationCandle`. Passing the validating candle as that prior candle would falsify the strict migration checks in the 007/008 completion calculators.

The existing provisional `CandidateGeometry` has a correction-body `StructuralPrice` and protection that may include the terminal candle's wick while excluding its body. Recomputing geometry from `CorrectionTurnCandles` alone would lose that protection contribution. `StructuralCandidateValidationResult` can carry the precomputed geometry and the confirming break, but by itself has neither the reconstruction episode nor the direct candidate's provenance. The current `StructuralCandidateValidationCalculator.EvaluateCurrentBoundary` also takes a replay context and recalculates geometry; that API is not itself the one-candle, state-plus-candle transition contract.

## Decision

### Candidate identity and causal cursor

`TerminalCandle` remains the fixed structural boundary: the correction terminal and first opposite-impulse candle. It is not overwritten by later observations. `FrozenImpulseTerminal.StructuralPrice` is the strict breakout reference. `CandidateGeometry.ProtectionAnchor` is the strict migration reference until migration. A candidate's separate `LastProcessedCandle` starts at `TerminalCandle` and advances to **every** later closed H4 candle consumed while the candidate remains active, including a no-event candle. The `Candidate` snapshot's `MarketCursor` is this processed candle, refining the initial-entry equality in ADR 0012. A no-event transition returns a new immutable candidate value with unchanged `Episode`, `TerminalCandle`, `FrozenImpulseTerminal`, `CorrectionTurnCandles` and `CandidateGeometry`, and with the incoming candle as cursor. It neither validates a pair nor creates human evidence. Repeating that candle is invalid because it is no longer later than the cursor; no hidden reducer state is needed.

The next candle must have the same provider, symbol and H4 timeframe, open strictly after the cursor's open, open at or after its close, and close strictly after its close. It must be closed at the observation boundary. No exact four-hour spacing or contiguity rule is added. `ObservedAtUtc` and replay `AsOfUtc` never replace the market cursor.

### Direct completion and migration-only continuation

If there is no migration but `Close` strictly breaks the frozen terminal, the existing `CandidateGeometry` becomes definitive **unchanged**, including any terminal-wick protection. The incoming candle is the confirming event and market cursor; its body color does not add a condition. Compose the existing `StructuralCandidateValidationResult` semantics from that exact geometry and the incoming strict closed-body break. Do not recalculate geometry from `CorrectionTurnCandles` alone or invoke ADR 0009/0010 evidence. A minimal immutable **direct-candidate completion result** must retain the original candidate or equivalent complete candidate provenance, exact `Episode`, `CandidateGeometry`, validating candle, confirmed `StructuralCandidateValidationResult` and validating-candle cursor. It is distinct from ordinary *rebuilt* completion under `NQ-Q-H4-009`. No existing completion wrapper carries those facts truthfully.

With strict migration and no breakout, the original candidate geometry and failed-turn membership cease to be authoritative. The new known protection is the incoming `Low` for HL or `High` for LH. A migration candle without a directionally matching first-turn body enters `RebuildPending`. A matching bullish HL or bearish LH body may enter `RebuiltTracking` on that **same** candle through the source-backed pending/initializer semantics: `MigrationCandle = FirstTurnCandle = LastProcessedCandle`. This is one causal transition, not a requirement to persist a pending snapshot for a frame. Neither branch creates definitive rebuilt `StructuralPrice`; exact final multi-candle membership remains the ADR 0009 human boundary.

### Same-candle migration and breakout from either origin

For a `Candidate` origin, compare the incoming wick to the **pre-candle** `CandidateGeometry.ProtectionAnchor`. For a `RebuildPending` or `RebuiltTracking` origin, compare it to the **pre-candle** effective `KnownProtectionAnchor`. This numeric prior anchor is the semantic strict-migration reference. The existing `PriorMigrationCandle` is specifically earlier rebuild history and can evidence a rebuild-origin anchor; it is neither the general meaning of prior anchor nor a required fact for Candidate-origin breakout. The validating candle is never compared to itself. The outgoing effective anchor is the incoming wick if strict migration occurred, otherwise the prior anchor.

One breakout domain contract must retain a typed origin and its truthful provenance: original `Candidate` with its geometry and prior anchor, or `RebuildPending`/`RebuiltTracking` with their effective migration identity and prior anchor. An optional rebuild-origin `PriorMigrationCandle` must not be fabricated for Candidate origin. The common breakout facts remain the exact `Episode`, candidate side, frozen terminal, validating candle/cursor, strict-migration flag, effective anchor and collision kind. The existing rebuilt-breakout type may be generalized with this typed origin and numeric pre-candle anchor; it must not silently reinterpret `PriorMigrationCandle`. The 007/008 completion guards must eventually check strict migration against that pre-candle anchor. Their current comparison with `PriorMigrationCandle.Low/High` is a representation-specific rebuild-origin guard, not a strategy rule that a prior rebuild candle must exist. This ADR does not modify the calculators.

For either origin, strict migration plus strict breakout with a matching directional body is `NQ-Q-H4-007`: migration precedes confirmation, and the validating candle supplies the definitive single-candle candidate body and wick geometry. Deterministic 007 completion remains separate from breakout detection. With an opposite body or exact doji, it is `NQ-Q-H4-008`: breakout, new wick protection and episode are known; definitive `StructuralPrice` requires the existing human price assertion before completion. No old candidate body price or guessed candle ownership survives as definitive geometry. [ADR 0010](0010-define-nasdaq-collision-structural-price-replay-input.md) already keys that assertion by base episode, validating candle and candidate side; it does not require a prior migration candle or a new origin discriminator. Its `Missing | Unique | Conflict` selection and provenance rules remain unchanged.

### Closed candidate matrix

Every row consumes exactly one later closed H4 candle and preserves the same `NasdaqHumanOriginVertexEpisode`. `c` denotes that incoming candle. Equality is neither migration nor breakout; `Close == Open` is exact doji, not a first turn. No tolerance is implied.

| Migration | Breakout | Body / first turn | Next result | Geometry source | Effective protection | Human evidence at this transition | Cursor | Snapshot variant |
|---|---|---|---|---|---|---|---|---|
| No | No | Irrelevant | Candidate continues | Unchanged `CandidateGeometry` | Unchanged candidate anchor | None | `c` | `Candidate` |
| No | Yes | Any, including doji | Direct candidate completion | Unchanged `CandidateGeometry` plus strict break evidence | Unchanged candidate anchor | None | `c` | `Completed.DirectCandidate` |
| Yes | No | No matching first turn, including doji | Rebuild pending | No definitive rebuilt body geometry | `c.Low` for HL / `c.High` for LH | None to enter; ADR 0009 may be needed at later ordinary completion | `c` | `RebuildPending` |
| Yes | No | Bullish HL / bearish LH | Rebuilt tracking starts | Provisional turn only; no definitive rebuilt body geometry | `c.Low` for HL / `c.High` for LH | None to enter; ADR 0009 may be needed at later ordinary completion | `c` | `RebuiltTracking` |
| Yes | Yes | Directional body | 007 breakout, then deterministic completion | Validating candle's directional body and wick | `c.Low` for HL / `c.High` for LH | None | `c` | `BreakoutAwaitingCompletion`, then `Completed.Directional` |
| Yes | Yes | Opposite body | 008 breakout awaiting price | Definitive body price unavailable | `c.Low` for HL / `c.High` for LH | ADR 0010 `Unique` required for completion | `c` | `BreakoutAwaitingCompletion`, then `Completed.HumanStructuralPrice` if resolved |
| Yes | Yes | Exact doji | Same 008 breakout awaiting price | Definitive body price unavailable | `c.Low` for HL / `c.High` for LH | ADR 0010 `Unique` required for completion | `c` | `BreakoutAwaitingCompletion`, then `Completed.HumanStructuralPrice` if resolved |

The snapshot keeps its eight top-level variants. `Completed` needs the fourth, direct-candidate payload alongside ordinary rebuilt, 007 and 008 results. `BreakoutAwaitingCompletion` must accept the typed-origin breakout contract, including a Candidate-origin 008 while human price is unavailable. A 007 breakout may be completed in the same causal step; the intermediate breakout remains a legal typed boundary. These are follow-up runtime contract changes, not changes made by this documentation decision.

## Alternatives considered

- Keep `Candidate.MarketCursor = TerminalCandle` after no-event candles: rejected because an already processed H4 candle would be replayed or require hidden state.
- Move `TerminalCandle` to the latest no-event candle: rejected because it changes the structural terminal's identity.
- Treat direct completion as ordinary rebuilt/ADR 0009: rejected because no migration superseded the existing complete candidate geometry.
- Set `PriorMigrationCandle = ValidatingCandle` for Candidate-origin collisions: rejected because it asserts nonexistent prior rebuild history and makes strict migration against itself false.
- Create a second, unrelated breakout lifecycle: rejected because both origins share the same breakout and 007/008 completion semantics; typed origin and pre-candle anchor preserve their distinct provenance in one contract.

## Consequences

The Candidate one-candle matrix is domain-contract ready, subject to the explicit runtime changes above. No production transition, snapshot, test, evaluator, metadata or capability is changed by this ADR. `NQ-H4-001` remains blocked by raw-history bootstrap, human-evidence Missing/Conflict result mappings and supersession, broader H4 classification, lifecycle reduction, evaluator status mapping and canonical completed-state handoff. The 008 human `StructuralPrice` and ordinary rebuilt-member choices remain human-required; this ADR does not supply them or authorize a trade.
