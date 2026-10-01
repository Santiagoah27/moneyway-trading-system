# ADR 0019: Clarify Nasdaq post-completion H4 lifecycle

## Status

Accepted as a source and runtime boundary audit. Post-`Completed` structural processing remains **blocked by unresolved strategy evidence and a separate representation contract**. This ADR defines no new trading rule, snapshot, evaluator status or implementation.

## Date

2026-10-01.

## Context

The [audited Nasdaq specification](../strategies/nasdaq/strategy-specification.md) confirms that a second strict H4 body-close breakout can validate the candidate LH/HL and the opposite LL+LH/HH+HL pair. Valid pairs persist across sessions; a new session does not trigger raw-history bootstrap. [ADR 0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) retains `Completed` as the terminal **reconstruction-episode** snapshot until a separately defined active-structure handoff or qualifying later structural transition. [ADR 0018](0018-define-nasdaq-invalidated-origin-late-evidence-catch-up.md) stops ordered catch-up at `Completed`, even if later closed H4 candles are visible. The implemented `NasdaqH4ReconstructionCatchUpOrchestrator` follows that boundary.

The question is which exact active structural state, references and candle membership would permit the **next** market candle after completion. A terminal episode result cannot be treated as a permanent end to the H4 strategy, nor silently repurposed as the input to another reconstruction episode.

## Decision

### Meaning and retained facts

`Completed` means that the current post-invalidation reconstruction episode has a confirmed strict second break and a definitive candidate geometry. It establishes the opposite HH+HL or LL+LH pair **at the validating close**, not at the earlier invalidation or provisional candidate. This is supported by the specification's post-invalidation sequence and the common `StructuralCandidateValidationResult.IsValidated` contract. `CandidateSide.Lower` identifies the validated HL/bullish reconstruction; `CandidateSide.Upper` identifies the validated LH/bearish reconstruction. The exact candidate `StructuralPrice` and `ProtectionAnchor` are available in `ValidatedCandidate.CandidateGeometry`; the validating breakout candle is the market cursor. The protection anchor is not an executable Stop Loss.

The four runtime branches preserve the following facts. “Human provenance” distinguishes records actually embedded in the `Completed` snapshot from evidence retained only by an outer focused-reducer/catch-up result. Every branch also retains the original `NasdaqHumanOriginVertexEpisode`, the prior origin/frozen-terminal chain through its candidate or breakout, and the successful `StructuralCandidateValidationResult`.

| `Completed` branch | Candidate side | Candidate `StructuralPrice` / `ProtectionAnchor` | Validating event and `MarketCursor` | Human provenance in the snapshot | Complete next-cycle inputs? |
|---|---|---|---|---|---|
| `DirectCandidate` | `Result.Candidate.CandidateSide` | Exact prior `CandidateGeometry` via `ValidatedCandidate` | `Result.ValidatingCandle`; same cursor | No new branch-specific assertion; original human origin selection is not embedded | **Unresolved** |
| `Directional` / 007 | `Result.Breakout.CandidateSide` | Validating candle's source-backed directional geometry via `ValidatedCandidate` | `Breakout.ValidatingCandle`; same cursor | No new branch-specific assertion; original human origin selection is not embedded | **Unresolved** |
| `HumanStructuralPrice` / 008 | `Result.Breakout.CandidateSide` | Human-selected body price plus deterministic effective wick anchor via `ValidatedCandidate` | `Breakout.ValidatingCandle`; same cursor | `HumanPriceSelection` and all its supporting observations are embedded | **Unresolved** |
| `Ordinary` / 009 | `Result.Breakout.CandidateSide` | Resolved rebuilt-member geometry via `ValidatedCandidate` | `Breakout.ValidatingCandle`; same cursor | `MemberResolution` retains exact selected candles; the full supporting observation selection is retained by the focused reduction result, **not** the `Completed.Ordinary` snapshot alone | **Unresolved** |

All four expose an equivalent *validated candidate* and confirming candle; they do **not** expose a normalized active HH+HL/LL+LH pair or a post-completion phase with every reference required by the old-side lifecycle calculator. In particular, the successful break observation's `ReferenceLevel` is the **frozen provisional terminal that was broken**, not a newly normalized HH/LL structural price for subsequent same-side confirmation. The snapshot retains the confirming candle's OHLC, but choosing a new structural coordinate or turn membership from that candle requires source authority. The [open questions](../strategies/nasdaq/open-questions.md) still distinguish confirmed HL/LH body coordinates from unresolved coordinates for other structural-point classes. Human-assisted 008/009 completion does not by itself authorize a different later structural rule or rereading human evidence to choose future market path.

### Existing structural model and limited reuse

For an already supplied *valid* bullish HH+HL pair, the specification confirms that a formally closed `Close < validated HL` invalidates it; the bearish LL+LH mirror is `Close > validated LH`. Equality and wick-only penetration do not invalidate. The latest confirmed strict body break determines direction, and the active pair persists until qualifying evolution or invalidation. Thus `ValidatedCandidate.StructuralPrice` is the source-backed opposite-point **invalidation comparison** after completion, and `CandidateSide` identifies its direction. This does not establish the other active pair coordinate or all current-turn state.

`StructuralBodyCloseBreakCalculator` can perform a strict current-boundary comparison **if** its caller supplies the proper level and direction. It does not select either. `StructuralCandidateTurnBoundaryCalculator` requires a supplied prior HH/LL structural reference, prior validated opposite point, current correction-origin extreme and active candidate-turn candles; `Completed` supplies neither a normalized new HH/LL structural reference nor a new active correction turn. Reusing that complete boundary calculator immediately after `Completed` would therefore require an unsupported fabricated input. There is no runtime type equivalent to `CompletedStructureAwaitingInvalidation` or a completed-pair handoff. A future state could represent an already-confirmed active pair, but its exact fields and first-candle transition are not merely a naming choice while the source questions below remain open.

### Decision matrix

“Blocks” refers to implementing a **complete** post-`Completed` market-state handoff, not to the existing completed-episode reducer. `confirmed` is a source-backed fact; `unresolved` marks a decision not supplied by the current evidence; `rejected` marks an interpretation contradicted by it.

| Question | Source truth | Status | Gap class / blocks? |
|---|---|---|---|
| Is `Completed` terminal for the current reconstruction episode? | ADR 0012 and the four completion wrappers | `confirmed` | No |
| Does it end all later H4 structural processing permanently? | Valid active pairs persist and can roll or invalidate across sessions | `rejected` | No; runtime stops only because the next handoff is undefined |
| Is later structural evolution, including another invalidation episode, expected? | Nasdaq specification §9 and ADR 0012 | `confirmed` conceptually | No; exact handoff still blocks |
| What is the next opposite-point invalidation reference? | `ValidatedCandidate.StructuralPrice` is the new validated HL/LH | `confirmed` for comparison | No for a narrow supplied-level check |
| What is the normalized next HH/LL structural reference and active-pair geometry? | The second break and candle are retained; other structural-point coordinates and complete pair projection are not defined for all four branches | `unresolved` | **Strategy evidence gap**; yes |
| Which side/direction is monitored for opposite invalidation? | `CandidateSide.Lower` → bullish pair, strict `Close < HL`; `Upper` → bearish pair, strict `Close > LH` | `confirmed` | No |
| What detects that strict comparison? | `StructuralBodyCloseBreakCalculator` accepts caller-supplied level/direction | `confirmed` as a primitive | No for comparison alone; no complete handoff |
| Can the existing full candidate-turn boundary be reused directly? | It requires prior HH/LL, opposite point, correction origin and active turn; no post-completion state supplies all | `rejected` as a direct call | **Architecture/runtime gap**, dependent on the structural-reference strategy gap; yes |
| May the validating candle serve another role in the new active phase? | Some reviewed confirmation/extension cases allow a candle to start the next correction, but no uniform Direct/007/008/009 post-completion rule is established | `unresolved` | **Strategy evidence gap**; yes |
| What is the earliest newly eligible candle after completion? | Current one-candle reducers consume a candle once; same-close handoff membership is not uniformly specified | `unresolved` | **Strategy evidence gap** for same-candle role; **architecture/runtime gap** for cursor/phase handoff; yes |
| Does a later invalidation require a new `Episode` identity? | ADR 0012 creates one episode from each verified invalidating H4 candle | `confirmed` at a later verified invalidation | No; identity of an intervening active-pair state remains an **architecture/runtime gap** |
| Can all branch-specific human provenance be carried into a later active-pair state? | 008 selection is in its snapshot; 009 supporting selection and origin selection can exist only in outer reduction results | `unresolved` as a cross-frame transport contract | **Architecture/runtime gap**; yes for an auditable handoff |
| Can remaining visible backlog continue after `Completed`? | ADR 0018 explicitly stops now; ordered continuation would require the new state and candle boundary first | `unresolved` for future processing | **Architecture/runtime gap** dependent on the above strategy gaps; yes |
| Is another arbitrary-history human bootstrap required? | Valid pairs persist across sessions and ordinary post-anchor evolution is separate from raw bootstrap | `rejected` as an automatic requirement | No |
| Is new human input required for post-completion geometry? | No new anchor is required solely by session or ordinary continuation; other HH/LL coordinates and same-candle roles lack source definition | `unresolved` for the full handoff | **Strategy evidence gap**; yes |

The validating candle's `MarketCursor` is exact in every branch, including when 008/009 human evidence arrives at a later `AsOfUtc`. A future evidence-only handoff could preserve that cursor, but its state and treatment of any same-candle structural role are **not** specified here. This ADR does not advance the cursor, reuse the candle, skip a later candle or define a new episode prematurely. It does not infer that additional backlog is permanently irrelevant; it records why ADR 0018 currently stops there.

Raw-history bootstrap starts with **no trusted pair or boundary** and still requires human selection of the initial anchor and historical scan termination. Post-`Completed` continuation starts with a validated candidate and causal breakout provenance. These are distinct problems; missing raw bootstrap does not license a fabricated post-completion state, and post-completion clarification would not solve raw bootstrap.

`Completed` is a structural milestone, not an `NQ-H4-001` rule evaluation. That rule also requires the 08:00 context, scenario classification, permitted direction, evidence/status policy and canonical evaluator integration. No `passed`, `ready`, trade authorization, metadata or capability change follows from completion.

### Minimal human strategy evidence needed

1. For **each** Direct, 007, 008 and 009 completion (or an explicitly confirmed common rule), identify the resulting active HH+HL or LL+LH references after the validating close: specifically the HH/LL structural comparison coordinate and its source candle/member geometry, distinct from the validated candidate HL/LH body price and wick anchor. Which exact reference is used by the *next* same-direction structural break?
2. At that same validating close, can the validating candle also initialize the next active impulse or correction turn? If so, state the exact body/extreme conditions and membership for each completion branch, including 007's candidate-plus-break candle and 008's contrary/doji-body case. If it cannot, confirm that the first eligible new market input is strictly after `Completed.MarketCursor`.

Answers must identify causal source examples or the exact reviewed video interval. Until they exist, no uniform post-`Completed` structural transition, same-candle reuse, active-pair state or backlog continuation is authorized. These are **strategy evidence gaps**, separate from the later **architecture/runtime work** of representing the confirmed pair, preserving branch provenance and choosing the next episode at its verified invalidation.

## Alternatives considered

- Treat `Completed` as permanent end of H4 structure: rejected by the documented persistence and rollover of valid pairs.
- Feed the validating candle through another reducer or automatically start a new turn: rejected without a branch-specific same-candle contract; it risks double consumption and false membership.
- Use `BreakObservation.ReferenceLevel` as the next HH/LL level: rejected because it denotes the already broken frozen terminal.
- Rebootstrap arbitrary history on the next session or after completion: rejected; valid active structure does not expire on a calendar boundary.
- Convert `Completed` directly into another `InvalidatedAwaitingOrigin`: rejected because a **new verified invalidation** has not occurred.

## Consequences

The existing catch-up orchestrator correctly retains `Completed` and stops. A narrow strict invalidation comparison can be understood from the confirmed HL/LH coordinate and side, but it is **not** a complete next structural lifecycle. Post-`Completed` implementation remains blocked pending the two focused human source questions and a subsequent active-pair representation/handoff contract. This ADR changes documentation only; it does not alter code, tests, evaluators, metadata, capabilities or Forex behavior.
