# ADR 0003: Define canonical replay prerequisite gating

## Status

Accepted.

This decision defines the architectural boundary for prerequisite eligibility. Runtime implementation and Nasdaq setup-lifecycle rules remain deferred.

## Date

2026-09-09.

## Context

MoneyWay Nasdaq now documents a strict chronological workflow: 4H context, liquidity references, liquidity take, 5M Structural Change `OR` IFVG, separate 5M FVG confirmation, and 1M pullback plus realignment before entry eligibility. A downstream market pattern is not strategy-valid when a required predecessor has not occurred.

The canonical replay pipeline is already:

```text
multiple CandleSeries
→ synchronized MultiTimeframeReplayFrame
→ StrategyReplayContext
→ IReplayRuleEvaluator
→ StrategyReplayContextObservation
→ safe context outcome
→ canonical outcome run
→ canonical diagnostics
→ GenerateCanonicalMultiTimeframeBacktestUseCase
```

Current contracts do not enforce prerequisite relationships:

- `StrategyRuleDefinition.Sequence` provides unique ordering metadata. It determines definition/evaluator ordering and which required blocker is reported first; it does not declare dependency.
- `StrategyRuleDefinition.IsRequired` controls required evaluator coverage and final blocking participation; it does not mean prerequisite for every downstream rule.
- `IReplayRuleEvaluator` receives only the current `StrategyReplayContext`. It cannot see another evaluator decision, the current observation, or prior observations.
- `EvaluateStrategyReplayContextUseCase` invokes every registered matching evaluator once per context in definition order and builds raw `RuleEvaluation` records.
- `StrategyReplayContextObservation` validates and preserves current-frame evaluations but does not calculate eligibility or verdict.
- `EvaluateStrategyReplayContextOutcomeUseCase` checks required coverage and delegates complete current-frame observations to `SequentialStrategyEvaluator`.
- `SequentialStrategyEvaluator` selects the first blocking required evaluation by `Sequence` and maps its result to `StrategyVerdict`; it does not implement workflow progression.
- `GenerateMultiTimeframeStrategyBacktestRunUseCase` accumulates observations in increasing `AsOfUtc` order, but no accumulated history is supplied to an evaluator or prerequisite processor.

Therefore neither ordering, required coverage, observation construction nor current verdict aggregation is equivalent to strategy prerequisite gating.

## Decision drivers

- Preserve the canonical multi-timeframe pipeline.
- Keep evaluators deterministic, stateless and future-data safe.
- Avoid evaluator-to-evaluator calls and duplicated prerequisite logic.
- Keep strategy-specific relationships outside generic orchestration code.
- Distinguish raw market-pattern evidence from strategy eligibility.
- Preserve complete diagnostics and traceability.
- Support event-like prerequisites whose occurrence may remain relevant after the triggering condition is no longer currently true.
- Avoid assigning unaudited `RuleEvaluationResult` or `StrategyVerdict` mappings.
- Make the smallest reusable architectural addition.

## Decision

Sequential prerequisite eligibility will be owned by canonical Application orchestration, between raw rule evaluation and safe outcome generation.

The future implementation will add a strategy-neutral prerequisite/progression component to the existing canonical pipeline. It will consume:

- the exact versioned `StrategyDefinition`;
- separate immutable workflow dependency metadata owned by that exact strategy version;
- raw current-context evaluations;
- the replay-scoped progression derived only from observations at or before the current `AsOfUtc`.

Generic orchestration must not branch on `StrategyId` or hardcode Nasdaq `RuleId` values. Nasdaq supplies its relationships as strategy-owned data. Strategies with no workflow dependency metadata retain current behavior.

### Dependency representation

Dependencies will be represented in a separate immutable, versioned workflow definition keyed by exact `StrategyId` and `StrategyVersion`, with downstream and direct-prerequisite `RuleId` references.

It will not overload `Sequence`, `IsRequired`, `DefinitionStatus` or replay capability status. Keeping workflow metadata separate from `StrategyRuleDefinition` avoids changing the existing public strategy-definition API merely to introduce internal replay orchestration. If workflow metadata is exposed through an API later, that will require an explicit contract change.

The workflow definition must reject:

- unknown strategy versions or `RuleId` values;
- duplicate dependency declarations;
- self-dependencies;
- dependency cycles.

Dependency validity is established from explicit edges, not inferred from numeric sequence. `Sequence` may remain useful for presentation and deterministic processing order without becoming dependency semantics.

### AND and the Step-4 OR

Direct prerequisite lists use AND semantics: all declared direct prerequisites must have the required active progression evidence before the downstream rule is eligible.

MoneyWay Nasdaq Step 4 does not require an OR expression between dependency `RuleId` values. `NQ-M5-001` is the canonical aggregate rule for “Structural Change `OR` IFVG, whichever occurs first”; `NQ-M5-002`, `NQ-M5-003` and `NQ-M5-004` describe its branches and unresolved evaluation details. The internal alternative belongs to the future local evaluation of `NQ-M5-001`. The dependency from Step 5 is therefore to the aggregate Step-4 rule, avoiding an invented graph interpretation of its supporting rules.

If a future audited strategy requires OR between distinct prerequisite rules, the workflow metadata must be extended explicitly rather than encoding OR through `Sequence` or optional flags.

### Evaluator isolation

`IReplayRuleEvaluator` remains unchanged in responsibility:

- evaluates one exact strategy rule version;
- consumes only the current future-safe `StrategyReplayContext`;
- returns a raw `ReplayRuleEvaluationDecision`;
- does not invoke or inspect other evaluators;
- does not receive mutable workflow state;
- does not calculate eligibility or `StrategyVerdict`;
- performs no filesystem, network, database, wall-clock, random, trade or order operation.

Where its local inputs are available, a downstream evaluator may still observe a raw market pattern even when workflow eligibility is absent. This preserves the auditable distinction between “pattern observed” and “pattern strategy-valid.” A missing prerequisite must not rewrite or suppress truthful raw evidence.

### Observation and eligibility

Raw evaluator output and prerequisite eligibility are separate facts.

The future canonical observation boundary must preserve raw `RuleEvaluation` data and associate a separate immutable eligibility/progression diagnostic with the relevant rule. It must be possible to explain that a pattern was observed but did not advance the strategy because a prerequisite was absent, unavailable or awaiting human validation.

This ADR does not choose new diagnostic enum names and does not map prerequisite states to `passed`, `failed`, `waiting`, `not_applicable`, `human_validation_required` or `data_unavailable`. It also does not decide whether ineligible rules participate in final verdict aggregation. Those runtime mappings require a separate accepted decision after the strategy lifecycle is sufficiently specified.

`StrategyReplayContextObservation` construction itself remains a validation/snapshot boundary, not the owner of orchestration policy. Eligibility is calculated before the safe outcome boundary and recorded with the canonical observation data.

### Chronology and replay progression state

The canonical replay runner already emits strictly increasing global steps and `AsOfUtc` values. Prerequisite processing will fold those chronological observations into replay-scoped progression. Each produced progression snapshot must be attributable to one global step and use no observation after its `AsOfUtc`.

`REPLAY_PROGRESSION_STATE_REQUIRED` is the selected state model.

An event such as a liquidity take can enable a later step even after price is no longer beyond the selected level. Requiring the prerequisite rule to be currently `Passed` at the same `AsOfUtc` would lose that event history. Re-evaluating or scanning all prior contexts inside each evaluator would duplicate orchestration and violate evaluator isolation.

Progression state is:

- scoped to one exact strategy version, provider, symbol and replay run;
- advanced only by chronological canonical observations;
- captured as immutable per-step snapshots for auditability;
- deterministic and reconstructible by replaying the same inputs;
- never static, global, shared across runs or persisted implicitly;
- not trade, order, position or broker state.

The fold may be implemented with a private mutable accumulator inside the single replay execution, provided every emitted snapshot is immutable and future isolation is tested. External persistence is not required by this decision.

### Human and unresolved prerequisites

Definition status, evaluator capability and workflow eligibility remain independent axes.

A downstream evaluator may be technically implementable even when a prerequisite is human-only or blocked by unresolved specification. The generic gating component must preserve that prerequisite evidence/absence without automatically propagating a capability category to downstream rules.

The full strategy cannot automatically advance through a gate whose required occurrence has not been established. Exactly how human validation is supplied and how this situation maps to runtime results or verdicts remains deferred.

### Verdict and diagnostics boundary

Individual evaluators and prerequisite processing do not calculate `StrategyVerdict`.

Safe outcome generation remains the verdict boundary after required coverage and future gating semantics have been applied. `SequentialStrategyEvaluator` may require a later, separately approved adaptation or an upstream effective-evaluation projection, but this ADR does not change it.

Canonical diagnostics will project the preserved raw evaluation, eligibility/progression reason, prerequisite identities and `AsOfUtc`. Diagnostics must reuse the canonical outcome run and must not replay market data or reevaluate rules.

## Alternatives considered

| Alternative | Benefits | Risks and impact | Decision |
|---|---|---|---|
| A. Each evaluator enforces prerequisites | Localized initial code | Evaluator coupling, duplicated logic, hidden history/state, evaluator-to-evaluator calls, poor auditability and future-data risk | Rejected |
| B. Canonical orchestration plus explicit strategy-owned workflow metadata | Generic infrastructure, stateless evaluators, auditable raw/effective separation, one chronological state owner | Adds workflow metadata and progression snapshots; requires lifecycle and status-mapping decisions | Selected |
| C. Observation constructor performs gating | Keeps data together | Places policy in a validation model that has no strategy history or workflow metadata; constructor side effects would obscure orchestration | Rejected |
| D. Outcome/verdict processing performs gating | Central final result | Too late to preserve progression semantics cleanly; conflates eligibility with verdict and current required blockers | Rejected |
| E. Infer every lower `Sequence` as prerequisite | No new metadata | Factually incorrect for optional/parallel rules and silently redefines existing contracts | Rejected |
| F. Infer dependencies from `IsRequired` | No new metadata | Conflates coverage with causality and makes every required rule a universal predecessor | Rejected |
| G. Nasdaq-specific replay coordinator | Smaller one-strategy implementation | Creates a parallel path and hardcodes strategy identity into canonical behavior | Rejected |
| H. Reconstruct history independently inside every evaluator | Avoids shared state object | Repeated scans, duplicated lifecycle interpretation, poor consistency and evaluator contract expansion | Rejected |

## Consequences

### Positive

- Existing timing and session-liquidity evaluators remain compatible.
- Raw market evidence remains available even when strategy progression is ineligible.
- Strategy relationships are explicit, versioned and testable.
- Generic code remains strategy-neutral.
- Historical events can participate without requiring their trigger condition to remain currently true.
- One chronological owner makes future-data isolation and diagnostics testable.
- Strategies without dependencies remain backward compatible.

### Negative

- The canonical observation/outcome boundary will eventually need an additional eligibility/progression representation.
- Replay orchestration will maintain run-scoped derived state.
- Workflow definition validation and lifecycle tests add complexity.
- Existing verdict aggregation cannot consume gated eligibility until an explicit runtime mapping is accepted.

## Specification blockers before runtime gating

The architecture is defined, but Nasdaq runtime configuration cannot yet be completed safely. It needs audited answers for:

- when a liquidity-take event stops enabling Step 4;
- whether another liquidity take replaces, coexists with or starts another setup;
- how multiple Step-4 triggers and multiple Step-5 FVG confirmations are associated with a setup;
- what resets progression after a downstream failure or incomplete confirmation;
- whether 11:30 expires an analytical setup or only prevents new entry eligibility;
- how human validation is introduced into replay progression;
- how ineligible prerequisite states map to rule results and final verdict processing.

Evaluator-specific geometry for 4H structure, relevant liquidity selection, 5M structure/IFVG/FVG and 1M realignment remains separately unresolved. Those algorithms are not architectural prerequisite semantics.

## Smallest future implementation boundary

After the minimum setup-lifecycle clarification, one feature may add generic workflow metadata models and validation, a replay-scoped chronological progression fold, immutable eligibility diagnostics, and canonical orchestration tests using a synthetic strategy. It must not add a Nasdaq detector, evaluator, trade model, order model or final verdict mapping unless separately authorized.

## Non-goals

- No prerequisite engine in this documentation feature.
- No evaluator or evaluator-contract modification.
- No Nasdaq-specific orchestration.
- No strategy status or verdict mapping.
- No reset, expiry or multi-event policy inferred from trading knowledge.
- No structural, FVG, IFVG, 1M, Stop Loss, Take Profit or Break-Even algorithm.
- No trade, order, position, broker, persistence or LLM behavior.

## Review conditions

Review this ADR if:

- the canonical replay pipeline changes ownership boundaries;
- an audited strategy requires OR/choice semantics between distinct prerequisite rules;
- progression must cross replay runs or require persistence;
- human validation receives a canonical runtime contract;
- prerequisite eligibility receives accepted rule-result and verdict mappings.
