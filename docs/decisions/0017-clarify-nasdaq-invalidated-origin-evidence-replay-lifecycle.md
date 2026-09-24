# ADR 0017: Clarify Nasdaq invalidated-origin evidence replay lifecycle

## Status

Accepted as an audit of the existing causal boundary and an explicit record of unresolved runtime decisions. This ADR does not choose a late-evidence catch-up policy, add a trading rule, or implement a reducer. [ADRs 0008](0008-define-nasdaq-human-origin-vertex-conflict-policy.md), [0011](0011-define-nasdaq-h4-reconstruction-replay-lifecycle.md), and [0012](0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) remain unchanged.

## Date

2026-09-24.

## Context

`NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin` retains a verified `StructureInvalidated` boundary and its exact `NasdaqHumanOriginVertexEpisode`. Its `MarketCursor` is the invalidating H4 candle. The origin observation and selector implement the membership and conflict contracts in [ADRs 0007](0007-define-nasdaq-human-origin-vertex-replay-input.md) and 0008. The origin member resolver, geometry calculator, and opposite-impulse initializer implement the positive path, while the later one-candle snapshot reducers start from `OppositeImpulse`.

An origin review may become visible several replay frames after invalidation. Unlike completion of an already frozen breakout, origin resolution starts an active market-candle lifecycle. [ADR 0015](0015-define-nasdaq-breakout-evidence-waiting-policy.md) freezes a different, terminal breakout episode; its no-expiry and completion policy is not an origin policy. This ADR distinguishes the reusable causal constraints from decisions that the current source does not make.

## Decision

### Evidence and the positive handoff

`StrategyReplayContext.InputObservations` contains only matching strategy/version, provider and symbol inputs with `ObservedAtUtc <= AsOfUtc`. The origin selector additionally matches the exact H4 invalidation episode. It returns `Missing` for no visible matching record, `Unique` for one semantic set of selected member candle open times (including compatible duplicate records), or `Conflict` for two or more different sets. It retains supporting records; observation time orders visibility, not authority. A future record cannot change the selection previously produced at an earlier `AsOfUtc`.

The human assertion owns the exact origin member identities and the visual claim that they contain the defining turning extreme. It does not supply `StructuralPrice`, `ProtectionAnchor`, later impulse classification or market-candle chronology. For a usable `Unique` selection, the existing `NasdaqHumanOriginVertexMemberResolver` currently accepts **one visible observation**, resolves its exact selected H4 candles and the invalidating candle from the bounded frame, and rejects members that do not close before invalidation begins. `NasdaqHumanOriginVertexGeometryCalculator` checks the verified boundary and calculates the origin body's `StructuralPrice` and wick `ProtectionAnchor` on the side implied by the invalidated candidate. `NasdaqPostInvalidationOppositeImpulseInitializer` checks the same event and origin side, calculates its initial terminal from **only** the invalidating candle, and returns `NasdaqPostInvalidationOppositeImpulseState`. Wrapping that state in `Snapshot.OppositeImpulse` retains the episode and sets `MarketCursor = InvalidatingCandle`. Evidence resolution and initialization consume no later H4 candle. The opposite-impulse one-candle transition, and not this handoff, processes a subsequent H4 candle.

This is a composition of existing primitives, not a standalone implemented handoff reducer. The selector exposes a semantic membership and all compatible records, whereas the current resolver accepts one observation. With exactly one supporting record the positive path has a direct input. For compatible duplicates the same member set is usable by ADR 0008, but a future adapter must carry that semantic selection and all provenance without silently making one reviewer authoritative. This is a runtime representation detail, not permission to change membership or the selector's compatibility rule.

### Evidence lifecycle matrix

In this table, “persists” describes the source-backed snapshot shape and structural gate; it does not assign an evaluator result. `AsOfUtc` is replay/evidence visibility time, while `MarketCursor` names the last market candle structurally consumed by the snapshot.

| Visible origin selection | Origin geometry resolvable? | Next lifecycle state | MarketCursor | May `AsOfUtc` advance? | Later H4 candles consumed by this state? | Late-evidence behavior | Evaluator status |
|---|---|---|---|---|---|---|---|
| `Missing` | No | `InvalidatedAwaitingOrigin` persists; exact invalidation remains known | Invalidating candle | Yes, replay may expose later frames | No opposite-impulse processing is established; ADR 0012 says the snapshot does not pretend to have processed them | Later visibility can change the selection; scheduling, backlog and expiry beyond the ADR 0007 no-`D+1` boundary remain unresolved | Missing mapping unresolved |
| `Unique`, visible before any later H4 candle needs processing | Yes, after member/boundary checks | `OppositeImpulse` can be initialized from the existing positive primitives | Still the invalidating candle | Yes | No candle consumed by this evidence-only handoff; later candles belong to subsequent transitions | Immediate positive handoff is source-backed; compatible-duplicate adapter remains a runtime detail | No automatic mapping |
| `Unique`, first visible after later H4 closes | Yes for the origin, after checks | Initial `OppositeImpulse` state is constructible, but progression through already closed candles is **unresolved** | Initial handoff cursor remains the invalidating candle; any later cursor requires explicit candle transitions | Yes | Whether/how the backlog is processed is unresolved | Blocking catch-up and frame-attribution decision below | No automatic mapping |
| `Conflict` | No | `InvalidatedAwaitingOrigin` retains the verified invalidation; structural progression is blocked | Invalidating candle | Yes, replay may expose later frames | No origin-dependent progression may choose a member set | A later differing assertion does not win or supersede; explicit correction/revocation remains unresolved | ADR 0008 prescribes `human_validation_required` for a **future** origin evaluator/orchestrator; no runtime `ReplayRuleEvaluationDecision` or `StrategyVerdict` is emitted here |

ADRs 0011 and 0012 establish persistence of the invalidation facts and the cursor while origin membership is not uniquely usable. They do not specify an indefinite origin wait, a timeout, a replay scheduling policy or whether a later review can rescue a pending episode after an arbitrary number of H4 closes. ADR 0007 explicitly rejects expiry merely at `D+1`; that narrower rule must not be turned into a universal no-expiry policy. ADR 0008 forbids selecting a winner on conflict. As long as incompatible visible records remain in the current selector input, another record does not automatically turn `Conflict` into `Unique`; no correction, supersession or reviewer-authority contract exists.

### Late evidence and unconsumed candles

Suppose T0 closes the invalidating candle and creates `InvalidatedAwaitingOrigin`. H4 candles close at T1 and T2 while selection is `Missing`. At T3 the first matching origin evidence becomes `Unique`. At T1 and T2 that evidence was not visible, so neither earlier result may be rewritten or represented as if origin geometry were known then. At T3 the existing resolver can access closed candles visible in `ReplayFrame.AvailableCandles`, including the historical origin members and invalidating candle, subject to its checks. The opposite-impulse initializer still starts with the invalidating candle as cursor. `MultiTimeframeCandleReplayCursor` advances chronologically and exposes bounded cumulative frames; it is **not** an origin-specific backlog scheduler and does not define a catch-up fold or per-frame attribution of delayed structural transitions.

The current accepted sources do not choose among leaving T1/T2 unconsumed for later ordered processing, consuming them under a different pending policy, initializing directly at T3, or another explicit mechanism. Nor do they define how many historical H4 transitions may run at the T3 frame, whether a `Snapshot.OppositeImpulse` handoff must be observable before any catch-up step, how a non-H4 evidence frame schedules that work, or how an evaluator distinguishes market-event time from the later time at which evidence made reconstruction possible. One-candle reducers require one later non-overlapping candle per transition; they do not answer this orchestration question. No future implementation may apply T3 evidence to a T1/T2 evaluation or overwrite its audit result.

Consequently, **the immediate, no-backlog positive handoff is implementation-ready for one visible record after the existing checks; the full `InvalidatedAwaitingOrigin` reducer across late evidence is blocked by an architecture/runtime decision**. A focused reducer must not silently skip, collapse, replay as previously known, or automatically advance through the accumulated H4 candles. The full H4 lifecycle fold remains a separate concern. This boundary begins after a verified invalidation of known structure and does not solve arbitrary raw-history bootstrap.

### Required runtime decisions before full reduction

1. Define the `Missing` and `Conflict` scheduling/persistence policy as frames and later H4 candles arrive, including whether any expiry exists beyond the explicit absence of a `D+1` cutoff.
2. Define what happens to each closed but structurally unconsumed H4 candle when origin evidence first becomes `Unique` after T0: ordered catch-up, no catch-up, or another explicit causal mechanism; define how many transitions a frame may perform and the resulting cursor.
3. Define how an evidence-only handoff on a non-H4 replay frame interacts with later or already closed H4 frames, and how market-event time and evidence-availability `AsOfUtc` are recorded without changing earlier evaluations.
4. Define the selector-to-resolver adapter for compatible duplicate `Unique` records while preserving all human provenance and avoiding reviewer priority.
5. Define explicit origin-evidence correction, revocation or supersession if a conflicted episode is ever to become usable under revised human input. This is separate from the existing no-automatic-winner rule.

These are replay and representation decisions. **NO HUMAN STRATEGY EVIDENCE REQUIRED.** No video review is needed to document this boundary. Origin evidence lifecycle outcomes do not automatically map to `NQ-H4-001` evaluator status; ADR 0008's future origin-conflict mapping is the sole explicit exception and is not implemented by this ADR. `NQ-H4-001` remains `BlockedByUnresolvedSpecification`. Metadata and capabilities remain unchanged.

## Alternatives considered

- Copy ADR 0015's frozen-breakout and no-expiry behavior to origin: rejected as unsupported because origin resolution starts active progression with potentially unconsumed candles.
- Treat evidence visible at T3 as if it had been available at T1/T2: rejected by causal `AsOfUtc` and immutable earlier replay results.
- Jump the opposite-impulse cursor directly to the latest candle: rejected as unsupported by the initializer and one-candle transition contracts.
- Select a conflicting observation by time, order, source or majority: rejected by ADR 0008.
- Record the positive path and the open backlog contract separately: selected; it preserves existing primitives without inventing a catch-up algorithm.

## Consequences

The snapshot, selector, member resolver, origin geometry calculator and opposite-impulse initializer already support the immediate positive case. The replay infrastructure exposes causally closed history but supplies no origin backlog policy. Implementation of a full evidence-aware `InvalidatedAwaitingOrigin` reducer must wait for the listed runtime decisions. This ADR changes no C#, tests, evaluator status, metadata, capability or trading rule.
