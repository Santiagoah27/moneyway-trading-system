# ADR 0006: Define the Nasdaq preparation replay input contract

## Status

Accepted. This specifies an input boundary for a future implementation; it does not add a runtime observation, evaluator or workflow prerequisite.

## Date

2026-09-17.

## Context

`NQ-TIME-003` requires Step 1 and Step 2 to have actually completed for one Nasdaq morning session `D` in `[08:00:00, 08:30:00)` `America/Bogota`. The rule definition is required and confirmed, but its replay capability is `NotImplemented`: `StrategyReplayContext` currently exposes closed candles and optional chronological market-price observations, not preparation completion.

`StrategyReplayContextObservation` is the *output* of rule evaluation. `HistoricalMarketPriceObservation` represents a numeric market price, not a human-reviewed workflow fact. The typed `StrategyReplayLifecycleEvidenceSnapshot` is captured after raw evaluation for a possible or active setup instance, so it cannot supply an input to `IReplayRuleEvaluator` before Step 3. None is a reusable pre-evaluation preparation observation contract. ADR 0004 establishes optional chronological market-price input and an `AsOfUtc` visibility boundary; ADR 0005 establishes immutable, replay-local evidence identity. Neither authorizes deriving preparation from market data.

## Decision

The future canonical replay input will admit an immutable, source-backed **positive preparation-completion observation** for the exact Nasdaq strategy/version and morning trading session `D`. Canonical orchestration supplies the visible observation through the bounded `StrategyReplayContext` to `IReplayRuleEvaluator`. This is an input observation, separate from the output `StrategyReplayContextObservation` and from setup-instance lifecycle evidence. The owning abstraction is a canonical, immutable auxiliary replay input attached to context construction; a generic collection may be introduced only if its typed identity, timing and provenance constraints preserve this contract. No evaluator-specific lookup, database query, network call, filesystem read, wall-clock callback or hidden mutable state is permitted.

The semantic payload and enclosing replay identity must establish:

1. **Session identity:** the exact Nasdaq trading day `D` in `America/Bogota`, bound to the exact strategy/version and replay instrument identity. This is a semantic identity, not a storage key or date-string format. It cannot be satisfied by `D-1`, `D+1`, another session, strategy or instrument; there is no “latest preparation wins” lookup across sessions. Preparation expires with the morning session and cannot be reused in the afternoon.
2. **Completion state:** presence of this positive observation asserts that **both** Step 1 and Step 2 were completed for that same `D`. The positive observation kind supplies the `Completed` state; no separate `PreparationFailed`, `NotCompleted` or partial-step state is required. An absent observation never means completed. The assertion records completion of the workflow, not correctness of the human-reviewed 4H analysis or liquidity selection.
3. **One authoritative UTC causal timestamp:** the instant at which the fully completed preparation became source-observable to replay. It must not precede actual completion or the source's ability to attest it. The local `America/Bogota` projection of this instant determines whether it falls in `[08:00:00, 08:30:00)`. At `08:29:59.xxx` it is timely; at `08:30:00` it is late. A later-entered retrospective assertion cannot be backdated to make an earlier replay frame or deadline pass. The existing architecture does not define a separate event-time/ingestion-time pair for this kind of evidence; this contract uses one authoritative causal timestamp and requires the future input source to substantiate it. If a source cannot establish when the completed fact became observable, the observation cannot satisfy the timing rule; no timestamp is synthesized.
4. **Provenance:** a source/evidence reference sufficient to audit the assertion and its timestamp, carried with the immutable input and retained in the rule's evidence trail when evaluated. The producer and storage mechanism are left open. Provenance alone does not validate upstream analysis or override its separate human-review requirements.

For a given exact replay identity and `D`, canonical input accepts **at most one** completion observation. Duplicate or conflicting candidates must be rejected as ambiguous before replay evaluation; collection order, first/last arrival and a later replacement are not precedence rules. The absence of a positive observation is sufficient to establish a missed deadline when the deadline is reached, without inventing a negative event.

At `StrategyReplayContext.AsOfUtc = T`, only an observation whose authoritative causal timestamp is `<= T` can be visible. A completion stamped 08:20 is absent from an 08:10 context and visible at 08:20 or later, subject to the exact session identity. Input supplied later in a dataset cannot rewrite earlier snapshots. Canonical synchronization or equivalent bounded context construction must preserve the observation's causal boundary and the 08:30 deadline; neither the presence of 4H/1H candles, calculated Asia/London extrema, `NQ-TIME-001` passing nor elapsed time creates the completion observation.

Conceptual evaluation for `D`: before 08:30, no yet-visible timely completion means the deadline is still reachable, with no new runtime `RuleEvaluationResult` assigned by this decision. A visible matching completion in the interval satisfies the preparation timing requirement. At or after 08:30, if no timely completion for `D` was observable, the deadline is missed for `D`; a late assertion cannot recover it. The evaluator reports its rule result and evidence, not a `StrategyVerdict`. This contract cannot by itself establish correctness of `NQ-H4-001` or `NQ-LIQ-002`, or advance a downstream gate whose separate prerequisites have not passed.

## Alternatives considered

| Alternative | Assessment | Decision |
|---|---|---|
| Reuse market-price observations or derive completion from candle availability | A price or candle is not a completed human-dependent workflow assertion. | Rejected |
| Put the assertion in `StrategyReplayContextObservation` or lifecycle instance evidence | Both are produced after raw rule evaluation; they cannot be the evaluator's canonical input. | Rejected |
| Add an immutable auxiliary input exposed through `StrategyReplayContext` | Matches the current bounded context/evaluator ownership and preserves source identity and causal visibility. | Selected |
| Accept multiple same-session assertions and choose first or last | No source-backed precedence exists; order of input collection is not causal evidence. | Rejected |

## Consequences

The input contract is deterministically specified, but no runtime type, canonical transport or evaluator currently exists. The next implementation step is the canonical input primitive and its future-safe context integration; an `NQ-TIME-003` evaluator is not ready to consume an existing runtime abstraction. The provider of a timely source-backed assertion remains an integration choice, not a trading-rule decision. A source unable to provide an authentic causal timestamp cannot satisfy the contract.

The current `NQ-LIQ-003` workflow graph lists `NQ-H4-001`, `NQ-LIQ-002` and `NQ-TIME-001`, but omits the confirmed preparation prerequisite `NQ-TIME-003`. Reconcile that graph only after `NQ-TIME-003` can yield a deterministic runtime result. This decision changes no workflow code, evaluator capability or strategy rule metadata.
