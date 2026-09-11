# ADR 0005: Transport strategy-owned evidence to replay lifecycle policies

## Status

Accepted.

This decision defines an evidence-transport boundary for canonical replay. Runtime implementation and Nasdaq target-consumed cancellation remain deferred.

## Date

2026-09-11.

## Context

Canonical multi-timeframe replay creates one future-safe `StrategyReplayContext` at each observable boundary, produces raw `StrategyReplayContextObservation` rule evaluations, advances workflow progression and then invokes an optional strategy-owned lifecycle policy. Progression and lifecycle folds are local variables of one replay `Execute` invocation.

The generic lifecycle infrastructure already provides exact strategy, version, provider and symbol identity; deterministic replay-local `StrategyReplayProgressionInstanceId` ordinals; immutable workflow and lifecycle snapshots; typed `Start`, `ReplaceActiveState`, `Cancel` and `Expire` transitions; and distinct `Cancelled` and `Expired` terminal states. `StrategyReplayProgressionStateReference` is an opaque identity, not a strategy-data payload.

The current MoneyWay Nasdaq policy starts a pre-entry instance when `NQ-LIQ-003` newly establishes progression and expires an incomplete active instance when `NQ-TIME-002` reaches 11:00 `America/Bogota`. It receives only the raw observation, candidate workflow progression and previous lifecycle snapshot.

That boundary cannot consume the implemented `NasdaqSessionTargetSelection` or reuse `PriceLevelTouchCalculator` because:

- `StrategyReplayLifecyclePolicyContext` receives neither the bounded `StrategyReplayContext` nor companion deterministic evidence;
- `StrategyReplayContextObservation` contains raw evaluations and rule-scoped observability, but no strategy-owned lifecycle evidence;
- progression instances store no immutable selected-target association;
- canonical backtest entry points accept no per-execution strategy evidence;
- a registered policy cannot safely retain mutable per-run or per-instance state.

Encoding targets into string references, recalculating Asia/London inside lifecycle, querying market data from a policy or adding Nasdaq-specific fields to generic lifecycle types would violate current ownership and isolation.

Observability is a separate axis. `PriceLevelTouchResult` preserves provider, symbol, target, direction and a temporal evidence window. `ReplayTemporalEvidenceWindow` can preserve uncertain intervals. The current `ReplayMarketDataObservabilityAssessment` requires a real `RuleId`; target consumption is lifecycle behavior and must not acquire a fabricated rule.

## Decision drivers

- Preserve raw rule evaluations and the canonical pipeline.
- Keep generic lifecycle infrastructure independent of Nasdaq types.
- Carry immutable typed evidence without `object`, `dynamic`, serialized payloads or string-keyed dictionaries.
- Bind setup evidence to the exact replay-local progression instance.
- Preserve strategy, version, provider, symbol, step and `AsOfUtc` provenance.
- Prevent state sharing across replay executions.
- Keep policies without special evidence backward compatible.
- Transport observability facts without selecting lifecycle or verdict mappings.
- Never invent same-observation intrabar order.

## Decision

Canonical replay will provide an **immutable lifecycle evidence snapshot as a companion input to the lifecycle policy context**.

Canonical orchestration produces the snapshot after raw evaluation and candidate workflow progression are available, but before the strategy-owned policy decides the current transition. `StrategyReplayContextObservation` remains the raw rule-observation boundary and does not own lifecycle-specific evidence.

```text
bounded StrategyReplayContext
+ raw StrategyReplayContextObservation
+ candidate workflow progression
+ previous immutable lifecycle snapshot
+ optional per-execution strategy evidence source
→ canonical lifecycle evidence capture
→ immutable lifecycle evidence snapshot
→ StrategyReplayLifecyclePolicyContext
→ strategy-owned lifecycle transition
→ immutable lifecycle snapshot and diagnostics
```

### Evidence ownership and typing

Strategy-specific code owns the meaning and construction of evidence payloads. Generic orchestration owns capture timing, outer identity validation, instance binding and immutable retention.

The generic contract will expose a marker such as `IStrategyReplayLifecycleEvidence`. Concrete payloads are immutable strategy-owned records implementing it. Generic infrastructure stores only marker-constrained values and exposes type-constrained access such as `TryGet<T>() where T : IStrategyReplayLifecycleEvidence`. It does not use untyped payloads, magic string keys or serialization as an in-process type system.

One payload type may occur at most once in one evidence scope unless a later decision introduces an explicit typed collection. Null or duplicate payloads are rejected.

Nasdaq may later define its own evidence records containing `NasdaqSessionTargetSelection`, `PriceLevelTouchResult` and a neutral temporal observability assessment. None of those concrete types appear in generic workflow or lifecycle infrastructure.

### Production and lifetime

Evidence production is optional and selected by exact strategy/version. A producer supplied to one canonical replay execution must be immutable or stateless, perform no external I/O and consume only:

- the bounded current `StrategyReplayContext`;
- the raw current observation;
- candidate workflow progression;
- the previous immutable lifecycle snapshot and active-instance evidence;
- immutable strategy-specific inputs explicitly supplied for that execution.

It must not read wall clock, database, filesystem, network, mutable static state or future observations. Generic orchestration retains evidence only in execution-local variables and immutable snapshots. Equivalent executions may produce value-equivalent evidence, but their object graphs and instance ordinals remain isolated. No global replay identifier is introduced.

### Identity and provenance

Every evidence snapshot carries and validates:

- `StrategyId`;
- `StrategyVersion`;
- `MarketDataProviderId`;
- `MarketSymbol`;
- replay step;
- `AsOfUtc`;
- evidence binding scope;
- `StrategyReplayProgressionInstanceId` when bound to an active instance.

Payload-specific provenance remains in the typed payload. For example, `PriceLevelTouchResult` retains its target, direction, evidence kind, temporal window, candle timeframe or authoritative source ordering.

Evidence with a mismatched identity, an evidence window extending beyond `AsOfUtc` or an instance other than the active replay-local instance is rejected before policy evaluation.

### Activation and instance binding

An instance identifier does not exist until `Start` is applied. Evidence capture therefore supports two explicit scopes:

- **activation-candidate evidence**, unbound and valid only for the current possible `Start`;
- **active-instance evidence**, bound to the exact existing `StrategyReplayProgressionInstanceId`.

When a policy returns `Start`, canonical lifecycle orchestration allocates the existing next ordinal and atomically binds captured activation evidence to the new immutable instance. If no instance starts, activation-candidate evidence is not retained as instance state.

On later frames, a strategy evidence producer reads the selected target only from the previous active instance's immutable evidence. It captures current touch evidence against that same instance. It must not recalculate session selection, determine future-validity or reuse another instance's evidence.

`ReplaceActiveState` may preserve or explicitly replace typed instance evidence for the same instance. Generic code does not interpret it. `Cancel` and `Expire` retain final associated evidence in terminal history and cannot rewrite each other's termination kind.

### Lifecycle policy context

`StrategyReplayLifecyclePolicyContext` will gain one required non-null companion evidence snapshot. Its default is an immutable empty snapshot aligned with the current observation.

Construction validates agreement among observation, candidate progression, previous lifecycle and evidence snapshot for strategy, version, provider, symbol, step and `AsOfUtc`. Active-instance evidence must match `PreviousLifecycle.ActiveInstance.InstanceId`.

Policies needing no evidence receive the empty snapshot and keep their current decisions. Missing evidence is not interpreted by generic infrastructure as a lifecycle transition, rule result or verdict.

### Future-data protection

Evidence is captured once per canonical boundary from the same context bounded by `AsOfUtc` that evaluators receive. Only closed candles and normalized price observations visible by that boundary are accessible.

Every temporal evidence window must end at or before the snapshot `AsOfUtc`. Future touches cannot alter earlier snapshots. Source timestamps and authoritative sequences are preserved only when supplied. Collection order, candle direction, OHLC interpolation, synthetic ticks and later-candle reconstruction cannot manufacture chronology.

### Observability transport and mapping

Evidence transport and lifecycle mapping remain independent decisions.

The companion snapshot may carry a strategy-owned typed payload containing a neutral temporal observability fact. That fact may reuse `ReplayTemporalEvidenceWindow` and `ReplayMarketDataObservabilityStatus`, but it cannot require a fabricated `RuleId`. The existing rule-scoped `ReplayMarketDataObservabilityAssessment` remains unchanged; implementation may extract a rule-independent immutable temporal-ordering result rather than overload it.

The transported evidence can represent disjoint windows, exact authoritative ordering or overlapping `ResolutionInsufficient` windows. This ADR does not map insufficiency to `Cancel`, continue, entry wins, target wins, `RuleEvaluationResult`, `ReplayRuleEvaluationDecision` or `StrategyVerdict`.

`OBSERVABILITY_TO_LIFECYCLE_MAPPING_BLOCKER` therefore remains open. Until another accepted decision resolves it, lifecycle behavior may use only chronology established without guessing; same-observation ambiguous cases remain undecided.

### Diagnostics

Canonical diagnostics may project the evidence snapshot, instance binding, typed payload identity, temporal windows and observability status. They consume the canonical snapshot and do not rerun producers, target selection or market-data scans.

Evidence is not a rule evaluation, prerequisite, transition or verdict. This contract creates no `RuleId`.

## Alternatives considered

| Alternative | Benefits | Risks and impact | Decision |
|---|---|---|---|
| A. Companion evidence snapshot produced by canonical orchestration and supplied through policy context | Preserves raw observations, supports instance binding and keeps payloads typed | Adds small generic contracts, orchestration wiring and immutable instance evidence | Selected |
| B. Add evidence to `StrategyReplayContextObservation` | Travels with the current observation | Conflates raw rule facts with lifecycle inputs and cannot naturally bind pre-`Start` evidence | Rejected |
| C. Put target data in `StrategyReplayProgressionStateReference` | Avoids a snapshot type | Turns an opaque identity into a serialized payload and requires fragile parsing | Rejected |
| D. Store state inside the registered policy | Minimal signatures | Shares mutable state across executions and identities | Rejected |
| E. Add Nasdaq fields to generic lifecycle types | Direct immediate access | Couples generic infrastructure to one strategy | Rejected |
| F. Recalculate selection or touch inside lifecycle | Fewer transport types | Duplicates primitive ownership, lacks current context and risks future-validity inference | Rejected |
| G. Create an evaluator and `RuleId` for target consumption | Reuses evaluation transport | Misclassifies lifecycle behavior and changes capability/workflow concerns | Rejected |

## Consequences

### Positive

- Strategy-owned evidence reaches lifecycle policies without generic Nasdaq dependencies.
- Selection and touch primitives retain single ownership.
- Raw evaluations remain unchanged after termination.
- Instance association, provenance and future isolation become testable.
- Candle-safe and high-resolution chronology share one transport path.
- Existing policies remain valid through an empty snapshot.

### Negative

- Generic lifecycle gains marker, snapshot, validation and instance-evidence concepts.
- Orchestration must bind activation evidence while applying `Start`.
- Strategy evidence producers require explicit per-execution composition.
- Diagnostics gain another evidence axis.
- Temporal ambiguity still cannot authorize a transition.

## Backward compatibility

- Existing replay overloads may delegate with an empty evidence source.
- Existing observations, evaluations, workflow definitions, evaluators and capability reports remain unchanged.
- Existing policies receive an aligned empty snapshot and retain their decisions.
- Existing progression ordinals, transitions and terminal semantics remain unchanged.
- Strategies without lifecycle evidence gain no strategy-specific dependency.

## Follow-up implementation increment

The next feature will implement only this generic infrastructure:

1. Add the marker and immutable identity-validated evidence snapshot with empty, activation-candidate and active-instance scopes.
2. Add optional per-execution evidence production to canonical replay without shared mutable state.
3. Extend `StrategyReplayLifecyclePolicyContext` with the aligned companion snapshot.
4. Bind activation evidence atomically to the existing newly allocated instance and preserve it in terminal history.
5. Add synthetic-strategy tests for identity mismatch, `AsOfUtc`, instance binding, execution isolation, immutability, determinism and backward-compatible empty evidence.

It will not yet add Nasdaq payloads, target cancellation, evaluator/capability/workflow changes or an ambiguity mapping. A later Nasdaq feature may transport selected-target and touch evidence and cancel only when chronology is established.

## Non-goals

- No runtime implementation in this ADR.
- No Nasdaq target cancellation or future-validity decision.
- No change to lifecycle runtime types in this documentation feature.
- No structural fallback, Break-Even or Take Profit behavior.
- No evaluator, capability, metadata, workflow or verdict mapping.
- No provider, persistence, API, Worker, frontend or execution model.
- No invented intrabar chronology.

## Review conditions

Review this ADR if evidence must persist beyond one replay execution, multiple same-type payloads are required, progression identity changes, evidence crosses a process boundary, an ambiguity mapping is accepted or canonical replay stops owning lifecycle orchestration.
