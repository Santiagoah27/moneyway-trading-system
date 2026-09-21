# ADR 0007: Define the Nasdaq human-reviewed origin-vertex replay input

## Status

Accepted. This decision specifies an observation's semantic payload and validation boundary; no observation type, canonical transport or evaluator is implemented by this ADR.

## Date

2026-09-21.

## Context

After an already-valid H4 pair is invalidated, the opposite provisional impulse has a final turning origin vertex. Its defining extreme belongs to the vertex; the invalidating candle belongs to the new opposite impulse instead. Human review of nearby, non-equal candle bodies is required to decide the origin's backward and multi-candle membership. A single wick-extreme candle or invented numeric zone width cannot replace that decision. Once members are supplied, `StructuralTurnGeometryCalculator` can derive the separate body `StructuralPrice` and wick `ProtectionAnchor`.

`IStrategyReplayInputObservation` already carries strategy ID/version, provider, symbol, `ObservedAtUtc` and `SourceReference`; `StrategyReplayContext.InputObservations` filters matching immutable inputs at `AsOfUtc`. The current `CreateStrategyReplayContextUseCase` transports only `NasdaqPreparationCompletionObservationSeries`; it does not yet accept this vertex input. `CandleSeries` has unique chronological `OpenTimeUtc` within one provider/symbol/timeframe series, and `StructuralCandidateTurnBoundaryResult.CurrentCandle` identifies the invalidating candle in a supplied active-turn transition. The initial raw-history human bootstrap anchor and session-scoped `NQ-TIME-003` preparation assertion prove different facts.

## Decision

A positive, immutable Nasdaq **post-invalidation origin-vertex membership observation** asserts only that a human reviewer selected the exact source candles of the final High-side or Low-side turning vertex for one identified H4 invalidation episode. It does not assert `NQ-H4-001` passage, a validated opposite pair, manually calculated coordinates, algorithmic discovery or a near-equal tolerance.

The minimum semantic payload is:

1. The existing replay-input envelope: exact Nasdaq strategy/version, market-data provider, symbol, authoritative `ObservedAtUtc` and auditable `SourceReference`.
2. H4 timeframe identity and the invalidating candle's `OpenTimeUtc` within that same series. Together with the envelope, this binds the observation to one structural transition, not to a trading day or a floating global vertex. The transition must actually be a `StructureInvalidated` event derived from the already-valid prior structure; the observation cannot create that event.
3. A nonempty set of distinct selected H4 candle `OpenTimeUtc` values in the same provider/symbol/timeframe series. These are source identities, not prices, arbitrary new candle IDs or a contiguous range. A canonical consumer resolves them to the exact closed `Candle` objects and may present them in chronological order to calculators; extrema geometry does not depend on input ordering. No contiguity or automatic adjacency detector is inferred.

The invalidation transition's already-valid prior side determines whether the reviewed origin is High-side or Low-side. The reviewer observation must be checked against that side; it cannot infer a side from later candles or override the transition. A separately asserted side is not required in the minimum membership payload because the transition context already supplies it.

The source members must exist in the matching H4 replay series, have closed before the invalidating candle begins, and include the reviewer-selected defining extreme. The invalidating candle itself must be rejected as an origin member; it is the first member of the opposite impulse. Observation-level validation owns nonempty/distinct identities, UTC/provenance and same-series references; transition-aware structural validation owns the actual invalidation match, side, closed-candle chronology and displacement exclusion. The human assertion owns the visual claim that the selected set contains the defining turning extreme; mechanical checks cannot independently rediscover that visual choice. Both boundaries must be satisfied before coordinates or lifecycle progression consume the selection. No future candle may be selected relative to the claimed event.

`ObservedAtUtc` records when the human-reviewed assertion became source-observable, separately from the historical candle times. It cannot precede the invalidating candle's close or be backdated to change an earlier replay result. At `AsOfUtc = T`, evidence with `ObservedAtUtc > T` is invisible; a later annotation cannot rewrite prior frames. H4 structure and this episode-specific observation can cross daily sessions without expiring at `D+1`. The producer, storage and human review interface are outside this decision.

After validation, the selected candles go to the existing deterministic body/wick calculators: High-side `StructuralPrice = max(max(Open, Close))`, `ProtectionAnchor = max(High)`; Low-side `StructuralPrice = min(min(Open, Close))`, `ProtectionAnchor = min(Low)`. The observation stores membership, not manually entered derived prices. Exact equal body coordinates still form one structural price level; reviewed inclusion of near-equal bodies does not establish a numeric tolerance. Downstream opposite-impulse, terminal-freeze, correction and second-break processing remains conditional on this valid selection and its other established inputs.

The generic `InputObservations` boundary can expose this type, but current canonical construction has no vertex-series transport. Nor does the generic collection guarantee one authoritative selection per episode. Conflicting selections, later corrections/supersession and the rule-result mapping for missing human vertex evidence remain unresolved. Collection order, newest/oldest annotation, highest wick and a later session are not precedence rules. A single immutable observation value can be implemented without resolving those orchestration policies; an ambiguous episode must not be consumed as though one selection were authoritative. The initial raw-history bootstrap anchor remains a separate domain observation and may have a different lifecycle contract.

## Alternatives considered

| Alternative | Assessment | Decision |
|---|---|
| Infer membership from a numeric near-equal threshold or one extreme candle | Changes the audited visual multi-candle decision and can change the body coordinate. | Rejected as canonical behavior |
| Carry only a manually entered structural/protection price | Loses exact member identity and creates a competing source for deterministic geometry. | Rejected |
| Reuse the session-scoped preparation assertion or initial bootstrap anchor as the same observation | Their asserted facts and lifecycle scopes differ. | Rejected |
| Carry exact member identities through the existing causal auxiliary-input boundary | Preserves human selection, episode identity, auditability and deterministic downstream calculations. | Selected |

## Consequences

The observation's purpose and minimum payload are specified well enough for a focused immutable value type. Canonical transport, episode conflict/supersession policy, missing-evidence runtime status and the full post-invalidation consumer remain separate work. This decision does not make `NQ-H4-001` evaluator-ready, alter strategy metadata or capability, or authorize a live order.
