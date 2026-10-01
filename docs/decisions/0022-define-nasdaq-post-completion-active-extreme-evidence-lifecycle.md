# ADR 0022: Define Nasdaq post-completion active-extreme evidence lifecycle

## Status

Accepted. The human-assisted observation, identity, selection, resolution and initial post-completion phase contracts below are specification-complete for incremental implementation. Automatic cluster-start detection remains `human_validation_required` / `unresolved` for automation. No runtime types, market reducers or evaluator mappings are implemented.

## Date

2026-10-01.

## Context

[ADR 0019](0019-clarify-nasdaq-post-completion-h4-lifecycle.md) audited the former boundary; [ADR 0020](0020-define-nasdaq-post-completion-active-structural-handoff.md) confirmed universal structural handoff and dual-role mechanics; [ADR 0021](0021-define-nasdaq-active-extreme-final-cluster-membership.md) confirmed that the active HH/LL is the final contiguous turning cluster, not the whole breakout impulse. Exact members remain human-reviewed, while their body/wick geometry is deterministic. These accepted ADRs remain unchanged.

Audit baseline: `a591f31`. This decision closes the **architecture** questions left by those ADRs. The [audited Nasdaq source](../strategies/nasdaq/strategy-specification.md) supplies exact opposite-body correction start, update-before-start semantics and terminal freeze at that causal close. Architecture choices below govern representation and replay scheduling; they do not infer a numerical cluster detector or change a trading rule.

## Decision

### Runtime audit and reuse boundaries

The current [reconstruction snapshot](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqH4ReconstructionSnapshot.cs) exposes four terminal `Completed` payloads:

| Branch | Normalizable facts | Provenance requiring preservation |
|---|---|---|
| DirectCandidate | Candidate side, validated original candidate geometry, successful break and confirming candle | Candidate/correction/origin/frozen-terminal chain and original invalidation Episode |
| Directional / 007 | Candidate side, definitive directional-collision geometry, successful break and confirming candle | Typed breakout origin, collision and original reconstruction chain |
| HumanStructuralPrice / 008 | Candidate side, human candidate body price, deterministic wick anchor, successful break and confirming candle | Embedded `HumanPriceSelection` with all supporting observations and breakout chain |
| Ordinary / 009 | Candidate side, resolved rebuilt geometry, successful break and confirming candle | Embedded `MemberResolution`; complete supporting selection is in the outer focused completion result, not this snapshot alone |

All retain the old `NasdaqHumanOriginVertexEpisode`. `Completed.MarketCursor` is exactly the confirming candle. `CandidateSide.Lower` supplies validated HL and establishes bullish direction; `Upper` supplies validated LH and establishes bearish direction. Neither candidate geometry nor `BreakObservation.ReferenceLevel` is the new active HH/LL coordinate. Full original human-origin selection also lives in the outer origin reduction/catch-up result. A completion snapshot alone is therefore not a complete audit envelope.

`OppositeImpulse`, `Correction` and `Candidate` are post-invalidation **provisional** states; their constructors, origin/frozen-terminal invariants and invalidation Episode do not represent an established valid pair. The existing closed snapshot union has no truthful post-completion variants. Preserve these types and their reducers.

[ADR 0008](0008-define-nasdaq-human-origin-vertex-conflict-policy.md), [ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md) and [ADR 0010](0010-define-nasdaq-collision-structural-price-replay-input.md) provide causal envelopes, exact semantic compatibility and provenance patterns. Their actual selectors are typed to origin membership, migration-bound rebuilt membership or collision-specific decimal price respectively. Their DTOs, Episode keys, selector results and resolvers cannot be reused as this new domain fact. ADR 0009's resolver requires a migration member and known migrated anchor; ADR 0010 supplies a candidate price rather than members. Generic body/protection geometry calculators, exact body-direction classification and the replay envelope are reusable without those semantic substitutions.

### Deterministic identity ownership

Introduce conceptually distinct immutable value identities; names below express contracts, not required C# declarations:

1. `PostCompletionEpisode` = **(PreviousCompletedEpisode, ConfirmingCandleOpenTimeUtc)**. The predecessor is the complete typed `NasdaqHumanOriginVertexEpisode`, already owning strategy ID/exact version, provider, symbol, H4 and original invalidating-candle identity. The additional confirming open timestamp distinguishes the completion event in that series. This is a new identity type, not reuse of the predecessor identity and not a random GUID or session key.
2. `ActiveExtremeMembershipEvent` = **(PostCompletionEpisode, CorrectionStartCandleOpenTimeUtc)**. The turn timestamp binds the exact closed impulse/first correction boundary, including the immediate case where it equals the confirming timestamp. A different turn is a different event even within the same session. This contract concerns the initial extreme after this completion; later structural cycles retain their own causal identities rather than reusing this event.

The originating verified `Completed` event owns direction/active-extreme side: validated Lower candidate → bullish/HH/Upper geometry; validated Upper candidate → bearish/LL/Lower geometry. The event's typed resolution context must retain this side and the exact completion. Side is **derived**, not another independently human-controlled key field. It is sufficient to distinguish HH/LL because one exact completion establishes one direction; a consumer must verify this mapping. Conflicting completion payloads for the same causal key are invalid state, not rival human member assertions. Observation envelope identity fields may expose inherited values for `IStrategyReplayInputObservation`, but they must equal the embedded identity, never select another strategy version.

Retain a typed immutable predecessor provenance envelope containing the exact branch-specific `Completed` result, applicable full outer origin/completion evidence selections and resolutions, and confirming candle. The new identity links to it; identity alone does not replace audit payload. Do not label confirmation as a new invalidation to manufacture `NasdaqHumanOriginVertexEpisode`.

### Dedicated observation contract

`NasdaqHumanPostCompletionActiveExtremeObservation` conceptually implements the existing replay-input envelope and asserts **only** the exact closed H4 final-cluster member set for one `ActiveExtremeMembershipEvent`.

Its minimum payload is the typed event key; nonempty distinct immutable `SelectedMemberOpenTimesUtc`; authoritative UTC `ObservedAtUtc`; and nonempty trimmed `SourceReference`. Strategy/version/provider/symbol/H4 are inherited from the key and exposed consistently through the envelope. Normalize members by ascending timestamp as current membership conventions do; input order has no authority. Do not add a redundant session, migration, direction, price or random observation ID. Exact record identity for audit comprises event key, member set, observation time and source reference; retain any external source record identifier through provenance without making it a structural authority key.

Constructor validation covers non-null identities, Nasdaq ownership, UTC timestamps, nonempty/distinct members and source reference. The event's turn cannot precede confirmation. `ObservedAtUtc` cannot precede the actual closed turn or any selected member; validate exact close times against resolved source candles. The observation cannot create a completion or correction-start event. A typed consumer must verify both against supplied causal state.

The human supplies no `StructuralPrice`, `ProtectionAnchor`, continuation/invalidation outcome, `RuleStatus` or `StrategyVerdict`. Human authority is the visual region's exact membership, not competing derived geometry.

### Eligibility: open impulse versus closed turning boundary

The final cluster is **not frozen merely by completion**. ADR 0020's trend-direction confirming body leaves the impulse open; exact-neutral body starts no correction either. Consume subsequent closed H4 candles chronologically under existing exact body rules. The first qualifying opposite body starts correction: bearish for bullish structure; bullish for bearish structure. Existing update-before-start semantics permit that same candle's applicable extreme contribution before closing the impulse, without requiring its inclusion in the human vertex.

The specification's correction-start/terminal-freeze mechanics and ADRs 0020–0021 support using this first corrective close as the boundary for **this initial extreme event**. This is not importing a whole-segment body aggregation: phase closure is deterministic; cluster membership remains human-owned. Later correction resets or later completed cycles may establish other extremes but do not retroactively alter this frozen event.

If confirmation itself has the opposite body, the boundary exists immediately at `Completed.MarketCursor`; create pending state from its already-consumed consequences. If it has trend or neutral body, no membership selection/resolution is eligible until the first correction-start boundary exists. An observation for an anticipated turn cannot advance the open impulse. Every selected member must close **at or before this verified turn's close**. Members may precede confirmation when they belong to the reviewed final region; confirmation is not an artificial start, compulsory member or compulsory end. The turn candle is likewise not required to belong to the vertex, although it is the first correction member. Later candles cannot be members of this closed initial vertex.

Existing protected-point invalidation remains an independent strict comparison. An open-impulse transition must not ignore a qualifying invalidation in favor of starting same-side correction; established invalidation precedence governs any same-observation collision. It ends that valid-side pursuit and routes a verified invalidation to the separate reconstruction contract. This ADR specifies the membership path, not a replacement continuation/invalidation reducer.

### Minimum truthful phase model

Use a separate immutable post-completion state family with a common Episode, direction, protected HL/LH geometry, typed predecessor provenance and exact `MarketCursor`:

- **`ActiveImpulse`**: valid protected HL/LH and confirmed directional lineage, currently developing HH/LL context, no final resolved extreme geometry and zero correction members. Starts at confirmation. Cursor advances only through later consumed closed H4 candles until a valid turn or independent invalidation. Do not claim a complete normalized pair with final coordinates before resolution.
- **`ExtremeMembershipPending`**: verified turn candle/event, original completion provenance, frozen vertex eligibility horizon, correction start and its one first member. Cursor equals the turn candle. Final extreme geometry is absent; retain current selection outcome/provenance in a typed evidence result alongside the immutable pending state.
- **`ActiveCorrection`**: same new Episode, resolved HH/LL plus protected validated HL/LH, turn event and correction members initialized to the turn candle, complete membership selection/resolution/geometry evidence and predecessor envelope. Initial cursor remains the turn candle. Later market transitions are a distinct component.

These phases are required by truthful semantics, not naming convenience. In the immediate opposite-body case, pending initialization preserves confirmation as turn, first correction member and cursor without another market call. In the trend/neutral case, handoff initializes open impulse at the same confirming cursor. Evidence-only resolution never advances a market cursor or duplicates the turn in the correction list.

Use the **smallest source-backed state**: preserve completion/turn/cursor candles, causal references and a stable immutable replay-history binding, rather than embedding every consumed candle into every phase. The replay owner must retain the exact source history needed for later selected-member lookup, including available pre-confirmation breakout history and intervening open-impulse candles. Current completion payloads are not that history; cursor overwriting must not evict required source data. Resolution uses the current context's bounded H4 history. If retention cannot supply it, return the data-unavailable boundary below rather than approximate members.

Whole-impulse maximum body coordinates are **not required or authoritative**: they could import excluded expansion candles. Open-phase wick extrema may be tracked as diagnostic development facts with source owners, but are not final human vertex geometry or continuation coordinates. Neither such diagnostics nor a complete list of impulse candles is a member detector. Phase/source identities and retained source history are sufficient for the assisted path.

### Selection and no supersession

The dedicated selector takes the verified membership event and current matching `StrategyReplayContext`. Select only matching observations with `ObservedAtUtc <= AsOfUtc`; no strategy/version fallback. Output dedicated `Missing | Unique | Conflict` semantic results:

- `Missing`: no causally visible matching assertion.
- `Unique`: exactly one distinct member **set**, supported by one or more compatible observations. Preserve every supporting record even with different source/time. Chronological normalization is not priority.
- `Conflict`: more than one exact visible member set for the same event, even if derived coordinates coincide. Preserve all sets and observations; calculate no authoritative final geometry.

No first/last/latest winner, source priority, majority, confidence, union, intersection or price equality fallback. Later incompatible assertions remain conflict; no generic correction/revocation/supersession contract exists. Recheck evidence for the retained resolved event on subsequent contexts before using its dependent state. If a new incompatible assertion appears after successful resolution, quarantine further dependent processing at the **current consumed cursor** and retain the resolved historical state/geometry as audit, not usable current authority. Do not rewind to the turn or replay already-consumed candles. This guard is an architecture policy; explicit causal evidence correction is separate future work.

### Exact member resolution and contiguity

`Unique selection + verified pending event + current context -> Resolved members | DataUnavailable` is the new primitive-level resolution contract. `DataUnavailable` is a dedicated domain outcome, **not** an `NQ-H4-001` evaluation/status. Existing rebuilt resolver throws `InvalidOperationException` for unavailable H4/source candles; this new explicit outcome makes the same unavailable-data boundary reviewable without treating valid human evidence as malformed. Invalid caller identity or malformed/inconsistent assertions remain rejected validation inputs under existing argument/invariant conventions; never repair them silently.

Resolve each selected `OpenTimeUtc` to the exact existing Candle in the matching provider/symbol/H4 series. Verify full event/strategy identity, actual close visibility, the turn's exact source OHLC identity and horizon, and the observation's actual close-time bounds. Do not synthesize OHLC, resolve another instrument, use a future candle, choose a nearby timestamp or infer membership. Missing H4 frame or unavailable selected/required boundary candles means `DataUnavailable`: preserve pending state, selection and cursor; consume no later market candle. Future-referencing members beyond the verified turn horizon are invalid assertions, not merely unavailable market data.

Contiguous means **consecutive indices among the available chronological H4 source candles between the selected first and last member**: no omitted available candle inside that interval. Use `CandleSeries` unique ascending/non-overlap semantics; require no exact `+4h` wall-clock adjacency. Unsorted supplied identities normalize to chronology; duplicate identities, wrong series, known omitted intervening members or overlap are invalid. This validates the human set and never infers its first member. It does not certify that unseen market history is complete; externally known missing required source history is a data limitation. The resolver cannot verify visual “stall” by a numerical rule because none is defined.

### Deterministic geometry and normalized active pair

After `Unique` and successful resolution, reuse generic `StructuralTurnGeometryCalculator` over **only** resolved final-cluster members:

| Direction | Active extreme | `StructuralPrice` | `ProtectionAnchor` | Protected turn |
|---|---|---|---|---|
| Bullish | HH / Upper | `max(max(Open, Close))` | `max(High)` | Validated HL / Lower from completion |
| Bearish | LL / Lower | `min(min(Open, Close))` | `min(Low)` | Validated LH / Upper from completion |

The normalized pair carries new Episode, direction, both complete geometries, membership event/evidence, source completion envelope, phase and cursor. Verify consistent geometry sides and existing strictly ordered structural-reference invariants; inconsistent supplied geometry is not usable and cannot authorize downstream progression. Preserve distinct body/wick owners and equal-coordinate contributions. The body's coordinate supplies next continuation and the protected turn supplies next invalidation under ADR 0020; no executable Stop Loss or evaluator success is inferred.

### Waiting, late evidence and ordered catch-up

Choose architecture policy **A: freeze dependent market progression at the verified turn** while evidence is Missing, Conflict or resolution is DataUnavailable. There is no timeout, session expiry or fallback geometry. Canonical context/evidence visibility may advance, but later H4 candles remain unconsumed by this structural phase. This conservative schedule changes no trading mechanics and needs no mentor/video review.

Retry only at a later replay context. On usable `Unique`, resolve the **original** pending event, derive geometry and initialize `ActiveCorrection` at its original turn cursor. Evidence-only resolution consumes no additional candle. Then a broader lifecycle owner may fold currently visible closed same-series candles strictly after that cursor, oldest first, through the appropriate future market components. Stop at another unresolved evidence boundary, unavailable required data or a transition outside implemented scope. Never skip individual causal boundaries or claim catch-up is available before those components exist.

At T1 before visibility, selector returns Missing and the original pending state remains. At T2 after compatible evidence becomes visible, resolve/catch up for **T2 only**; incompatible visible sets instead mean Conflict. T1 is immutable. No synthetic historical evaluation, backdated observation or history scan to recreate known completion is permitted. An existing Conflict cannot become Unique by simply adding another differing assertion. `ObservedAtUtc` and `AsOfUtc` never replace `MarketCursor`.

### Decision matrix

`S` denotes confirmed source mechanics; `A` denotes the architecture policies adopted here. “Retain” includes predecessor provenance always and all visible human records when selection is applicable.

| Phase/event | Market state | Membership eligible? | Evidence outcome | Cursor behavior | Later H4 consumed? | Next state | Catch-up? | Provenance retained? |
|---|---|---|---|---|---|---|---|---|
| Completed, trend body (S) | Open impulse | No final turn yet | Not selected (A) | Confirming candle | Yes, ordered in open phase (A) | ActiveImpulse | If visible backlog (A) | Yes |
| Completed, opposite body (S) | Correction starts on consumed confirmation | Yes | Select at current context (A) | Same confirmation/turn | Only after resolved Unique (A) | Pending or ActiveCorrection | After resolution if backlog | Yes |
| Open impulse, no correction (S) | Developing extreme, no final cluster | No | Not selected | Each actual consumed candle | Yes, until turn/invalidation | ActiveImpulse | Ordered fold if backlog | Yes; source history retained |
| First correction start (S) | Closed initial impulse, first correction member | Yes | Select current visible evidence | Advance to consumed turn once | Freeze until resolved (A) | ExtremeMembershipPending | After resolution | Yes |
| Missing (A) | Frozen pending turn | Yes | No matching visible assertion | Preserve turn | No | Same pending state | Deferred | Yes |
| Conflict (A) | Frozen pending turn | Yes | Different visible sets | Preserve turn | No | Same pending state plus conflict result | Deferred | Yes, all conflicting records |
| Unique, source unavailable (A) | Pending, membership known but unresolved | Yes | Unique + DataUnavailable | Preserve turn | No | Same pending state plus unavailable result | Deferred | Yes |
| Unique, resolved (S/A) | Pair normalized, correction initialized | Yes | Exact members + deterministic geometry | Preserve turn during evidence step | Yes, through future market components | ActiveCorrection | If backlog | Yes, complete selection/resolution |
| Late Unique after Missing (A) | Original pending event resolved at current frame | Yes | Unique + successful resolution | Original turn, then actual catch-up steps | Yes, oldest unconsumed first | Current derived active state | Yes if backlog | Yes; earlier frames unchanged |
| Later incompatible evidence after resolution (A) | Dependent state quarantined | Same retained event | Conflict | Freeze current consumed cursor; no rewind | No | Retained state plus blocking conflict | No automatic recovery | Yes, old and new evidence |

Exact-neutral confirmation uses the open-impulse row: existing exact-doji behavior starts no correction. Membership becomes eligible only at a later real corrective boundary.

### Orchestration boundary and implementation order

Recommend a **broader H4 lifecycle wrapper**, not widening `NasdaqH4ReconstructionSnapshot` or contaminating provisional post-invalidation reducers. The current `NasdaqH4ReconstructionCatchUpOrchestrator` correctly stops at Completed and returns origin/completion provenance. The wrapper receives that complete envelope, initializes the new post-completion family once, selects/resolves eligible evidence before later market steps and owns phase-aware ordered backlog dispatch. Preserve existing reconstruction behavior and one-candle focused components; the wrapper may route later verified invalidation into a new reconstruction only through its existing verified invalidation contract.

Incremental implementation order: deterministic identities/observation; dedicated selector; exact resolver with unavailable-data and contiguity boundaries; geometry adapter with provenance; post-completion states; completion handoff; scoped market transitions/orchestration. No step creates four branch-specific machines. Primitive regression expectations include cross-event/version isolation, duplicate-set compatibility, exact conflicts, T1/T2 visibility, unavailable candles, contiguity without wall-clock spacing, horizon checks, both sides and single-candle dual-role initialization. These tests are requirements for future implementation, not tests executed by this documentation task.

### Gap classification and readiness

| Concern | Classification / consequence |
|---|---|
| Final contiguous vertex, universal branches, correction-start eligibility and dual roles | Strategy concept `confirmed`; no new mentor question |
| Exact-set selection, identity checks, chronological contiguity validation and geometry over supplied members | Deterministic algorithm/contract defined here; runtime implementation missing |
| Automatic first cluster member | `human_validation_required` / `unresolved` for automation; Human Dependency Reduction — Nasdaq H4, separate candidate research |
| New identity/observation, selection, resolver, phase states and full provenance transport | Architecture/runtime implementation gap; assisted contracts now defined |
| Initial handoff waiting, evidence-only resolution and late-evidence schedule | Architecture contract defined here; no unresolved scheduling choice for this path |
| Later continuation/invalidation market reducers and general wrapper | Separate incremental runtime work; not implemented or represented as working by this ADR |
| Human correction/revocation/supersession, automatic detector research, arbitrary-history bootstrap, broader near-doji and complete NQ-H4-001 evaluator | Separate future work; not prerequisites for introducing the scoped assisted primitives |

The **initial human-assisted post-completion evidence path is specification-complete and ready for incremental primitive implementation**. Exact human memberships are still required at each eligible visual vertex; that is a supported input dependency, not a demand for a new trading rule. **NO ADDITIONAL HUMAN STRATEGY/VIDEO EVIDENCE REQUIRED** for this contract. Automatic cluster detection remains a separate labeled-example/candidate-testing/replay/backtest/human-approval project and must not block the assisted MVP.

## Alternatives considered

- Resolve/freeze a final cluster at trend-body completion: rejected; impulse is still open.
- Reuse old invalidation or rebuilt migration identity: rejected; it identifies a different causal event.
- Continue dependent correction progression without its final reference: rejected in favor of frozen pending state and explicit catch-up.
- Copy every impulse body into geometry or use a numeric stall detector: rejected; human selects the final region.
- Rewrite earlier frames or rerun the confirming market input: rejected by causal visibility and single consumption.
- Extend the existing provisional-state union for convenience: rejected in favor of a separate family and broader lifecycle owner.

## Consequences

The next work can implement the new event identity and human observation without inventing cluster boundaries. Current runtime and orchestrator remain unchanged. This decision concerns known completion, not raw bootstrap, and does not define `ReplayRuleEvaluationDecision`, rule-result/status mappings, evaluator registration or capability counts. `NQ-H4-001` remains `BlockedByUnresolvedSpecification`. Only this ADR and its index are changed; no C#, tests, runtime snapshots, reducers, orchestrators, evaluator, metadata/capabilities, API/frontend/persistence or Forex modifications occur.

`NEXT_RECOMMENDED_ACTION: IMPLEMENT_DETERMINISTIC_PRIMITIVE`
