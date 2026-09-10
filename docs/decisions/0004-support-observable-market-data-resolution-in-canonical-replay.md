# ADR 0004: Support observable market-data resolution in canonical replay

## Status

Accepted.

This decision defines the future replay-data boundary and its observability guarantees. It does not implement high-resolution data, target detection, lifecycle behavior or strategy-result mapping.

## Date

2026-09-10.

## Context

MoneyWay's canonical historical replay currently consumes immutable, homogeneous `CandleSeries`. `MultiTimeframeCandleReplayCursor` advances at the union of candle close times, makes candles with the same close time observable atomically and produces `MultiTimeframeReplayFrame`. `ReplayFrame.AsOfUtc` equals `CurrentCandle.CloseTimeUtc`; every exposed candle closes at or before that boundary. `StrategyReplayContext` preserves the synchronized provider, symbol, timeframes, step and `AsOfUtc` for stateless `IReplayRuleEvaluator` implementations. Workflow and lifecycle progression then fold those observations chronologically into safe outcomes, diagnostics and the canonical backtest.

The repository has no historical quote, tick, trade-print or generic price-event contract. The only market-data adapter is a local CSV importer for `CandleSeries`, and no historical candle persistence model is present.

Audited MoneyWay Nasdaq semantics now define a target as an absolute Asia/London price level and its interaction as a timeframe-independent price-quote touch. Closed OHLC can establish that the level was reached during a candle interval. It cannot establish the exact timestamp or path of that touch. In particular, when one minimum-resolution candle both touches a target through its range and closes to complete `NQ-M1-003`, OHLC does not establish which event occurred first.

This is a market-data observability limitation, not an unresolved definition of target touch. Strategy semantics must not be weakened to match the available data.

## Problem

Canonical replay needs to support rules whose causal order may require evidence finer than candles while preserving:

- deterministic and reproducible historical replay;
- strict future-data isolation;
- explicit uncertainty when the source cannot establish order;
- current candle-only workflows and multi-timeframe synchronization;
- strategy and evaluator independence from vendors and brokers;
- auditability of data source, resolution and causal sufficiency;
- local-first operation without requiring large event datasets for every rule.

The architecture must keep five independent axes: rule-definition status, evaluator implementation capability, workflow eligibility, lifecycle state and market-data observability. A fully specified, implemented and eligible rule can still be unobservable for one historical case.

## Decision

MoneyWay will use **dual-resolution canonical replay with an optional chronological market-price observation stream**.

`CandleSeries` remain first-class canonical inputs and retain their current behavior. Future canonical replay may additionally receive immutable, normalized, strategy-neutral high-resolution observations for the same provider and symbol. The optional stream will enrich the existing canonical pipeline; it will not create a Nasdaq-specific or parallel tick backtest stack.

Conceptually, the future pipeline is:

```text
multiple CandleSeries
+ optional chronological market-price observations
→ generic causal synchronization at observable source boundaries
→ immutable canonical replay snapshot
→ StrategyReplayContext
→ IReplayRuleEvaluator
→ StrategyReplayContextObservation
→ workflow eligibility/progression
→ lifecycle progression
→ safe outcomes
→ canonical diagnostics
→ canonical backtest
```

Only rules or strategy-neutral primitives that require finer causal evidence consume the optional observations. Candle-faithful evaluators continue using candle frames without acquiring a high-resolution dependency.

### Observable source boundaries

The future synchronizer will advance over observable boundaries supplied by its configured inputs. With candles only, those boundaries remain the existing candle close times and preserve current behavior. With high-resolution observations, their source timestamps may also create boundaries while the last closed candle for each timeframe remains the only candle visible.

At a canonical boundary `T`, a context may expose only candles closed at or before `T` and high-resolution observations timestamped at or before `T`. No later event in the same candle interval or a future interval may leak into an earlier context.

`AsOfUtc` remains the temporal visibility cutoff. A future replay-position identity may need a stable source sequence in addition to timestamp and global step when ordered observations share a timestamp; that is an implementation contract to be designed under this decision, not a reason to invent ordering.

### Neutral high-resolution observation boundary

The future normalized observation contract must carry enough evidence to establish provenance and, when available, chronology:

- provider/source identity;
- market symbol;
- UTC timestamp;
- observed numeric market price;
- observation kind and source resolution sufficient for audit;
- provider-native stable sequence or equivalent ordering identity when supplied.

The neutral observed price does not decide bid, ask, midpoint, last-traded price, trade print, broker fill or execution semantics. Adapters must preserve the source's actual meaning and provenance rather than relabeling it. Strategy code must not depend on vendor schemas or perform network access.

### Stable ordering and unknown order

Events with distinct timestamps are ordered by timestamp. Events with the same timestamp are ordered only when the source supplies a trustworthy stable sequence. Collection position, hash iteration, candle direction, OHLC interpolation or importer arrival order must not manufacture causality.

If equal-timestamp observations have no authoritative sequence, they form an unordered source group for order-sensitive analysis. Replay remains reproducible, but exact order remains unavailable. Deterministic replay means reproducing the evidence and its uncertainty, not forcing a total order unsupported by the source.

### Market-data observability axis

Replay inputs and diagnostics will eventually expose data-resolution capability independently from strategy metadata and evaluation results. At architecture level, a case can be:

- safely observable from candles because relevant events occur in distinct canonical candle observations;
- exactly orderable from a supplied chronological high-resolution stream;
- resolution-insufficient because available evidence establishes occurrence but not required order.

These are conceptual observability categories, not approved runtime enum names or `StrategyVerdict` mappings. `Sequence`, `IsRequired`, `DefinitionStatus`, replay evaluator capability, workflow eligibility and lifecycle state must not be overloaded with data-resolution meaning. A future replay-data requirement or observability catalog should declare what evidence a primitive needs without coupling it to a provider.

### Fallback policy

High-resolution data is optional and capability-driven. When it is absent:

- candle-faithful rules continue normally;
- rules requiring only ordering across distinct candle observations use the safe candle subset;
- an order-sensitive same-observation case records resolution insufficiency and remains undecided;
- the entire backtest is not rejected solely because another case or rule could use finer data.

This preserves progressive usefulness without claiming facts that OHLC does not contain. The mapping from resolution insufficiency to `data_unavailable`, `human_validation_required` or another rule result/verdict remains a separate decision.

### Primitive and lifecycle ownership

Evaluators and lifecycle policies must not scan arbitrary vendor event histories. Future high-resolution access belongs at the immutable canonical replay context or a strategy-neutral observation view bounded by `AsOfUtc`.

A future deterministic target-touch primitive may consume that normalized view and report whether touch occurred, the earliest orderable occurrence when proven and whether the available data can establish required causal order. The primitive must not create orders or model fills. Nasdaq target-consumed cancellation remains lifecycle policy consuming a canonical detected observation; lifecycle code must not parse raw vendor ticks.

Conceptually:

```text
normalized market data
→ deterministic strategy-neutral observation primitive
→ canonical auditable observation
→ strategy lifecycle policy
```

No production type or schema name is selected by this ADR.

### Provenance and reproducibility

Backtest provenance must eventually record the configured market-data sources and effective resolution, including whether high-resolution observations were present and whether an outcome was exact or resolution-limited. Provider, symbol, timestamps and source ordering capability must remain auditable. Candle-only and event-enhanced runs can differ in evaluability and must not be presented as equivalent inputs.

This ADR does not choose storage tables, file formats or vendors. Local-first implementations must allow small deterministic fixtures and streaming/bounded loading so high-volume datasets do not require loading an entire history into memory.

## Nasdaq target-consumed cancellation

With current candle-only data, MoneyWay can eventually implement the unambiguous subset: if entry progression is established in an earlier canonical observation and target touch is first observed in a later candle, candle chronology proves the order. Candle range can also prove that a target was touched during an interval.

If target touch and final entry confirmation occur in the same minimum-resolution candle, candle-only replay cannot choose cancellation or entry ordering. That case must remain resolution-insufficient.

The selected architecture supports full-fidelity cancellation later when normalized high-resolution observations establish the required order. It does not resolve source questions for equal, already-crossed, unavailable or initially-beyond targets, nor whether the first management target is the final Take Profit.

## Future-data guarantees

- Every source is immutable for a replay run.
- A context at `AsOfUtc = T` exposes no candle closing after `T` and no high-resolution observation timestamped after `T`.
- Provider-native sequence is used only within its documented scope.
- Equal-timestamp order without authoritative sequence remains unknown.
- Evaluators receive a bounded snapshot and cannot query external providers, the wall clock or future scheduling state.
- Derived candles, if introduced later, must use only events observable through their own close boundary and must not replace imported candle evidence silently.

## Backward compatibility

- Existing `Candle`, `CandleSeries`, `ReplayFrame`, `MultiTimeframeReplayFrame` and candle-only use cases remain valid first-class infrastructure.
- Existing candle-only runs retain close-time boundaries and deterministic results.
- Strategies with no intrabar chronology requirement do not need high-resolution data.
- Fine-resolution support extends the generic canonical pipeline rather than replacing it.
- No rule is automatically migrated to event data merely because that data exists.

## Diagnostics and auditability

Future canonical diagnostics must show:

- available market-data source and resolution capability;
- whether relevant chronology was candle-safe, event-proven or resolution-insufficient;
- provenance of the evidence used;
- unordered same-timestamp groups when applicable;
- no fabricated event ordering.

Observability diagnostics remain separate from `StrategyVerdict`. This ADR does not decide how a resolution-insufficient rule contributes to a final outcome.

## Alternatives considered

### A. Candle-only replay with explicit ambiguity

Benefits: smallest implementation, low storage cost, broad historical availability and complete compatibility with current replay.

Costs: permanently prevents faithful evaluation of valid same-candle causal cases and limits later target-management automation even when better source data exists.

Decision: rejected as the long-term architecture, while retained as the supported fallback mode within the selected design.

### B. Event-first replay replacing candle replay

Benefits: maximum chronological fidelity when complete event data exists and the ability to derive candles from a single event source.

Costs: makes every replay depend on expensive, high-volume and potentially unavailable data; risks changing established candle values; requires a large migration; weakens local-first operation; and couples candle-faithful strategies to unnecessary resolution.

Decision: rejected.

### C. Dual-resolution replay with optional high-resolution observations

Benefits: preserves current candles, adds fidelity only where required, supports explicit fallback, remains provider-neutral and extends one canonical replay path.

Costs: requires generic synchronization, observability metadata, provenance, diagnostics and careful handling of timestamp collisions. Event-enhanced fixtures and storage can be larger.

Decision: selected.

### D. Synthetic intrabar reconstruction

Benefits: produces an apparent total order from OHLC without acquiring finer data.

Costs: invents evidence through candle-color assumptions, fixed OHLC paths, interpolation, randomness or heuristics. Even a deterministic or conservative guess is not source fidelity and can alter strategy outcomes.

Decision: rejected for canonical deterministic replay. Any future approximation mode would require a separate explicit decision and must never be represented as exact canonical evidence.

### E. Separate companion event service queried by evaluators

Benefits: limits changes to candle frames initially.

Costs: introduces hidden I/O and source state during evaluation, duplicates causal filtering, weakens reproducibility and lets evaluators disagree about event visibility.

Decision: rejected. Optional events belong in the bounded canonical replay input and context.

## Consequences

### Positive

- Strategy semantics remain independent from current data limitations.
- Current candle-only replay remains useful and backward compatible.
- Exact event chronology can be added without a parallel Nasdaq replay stack.
- Missing resolution becomes explicit, reusable and auditable.
- Vendor and broker concerns remain outside the strategy engine.
- Rules pay the storage and processing cost of fine data only when needed.

### Negative

- Canonical synchronization will eventually support heterogeneous source boundaries.
- Replay identity may need a stable position beyond `AsOfUtc` for sequenced timestamp collisions.
- Diagnostics and reproducibility metadata gain a separate observability dimension.
- High-resolution datasets increase local storage and processing costs.
- Full fidelity still depends on source data that may be unavailable or internally unordered.

## Non-goals

- No runtime type, event stream, adapter, storage schema or replay-engine change.
- No bid-versus-ask, spread-side, last-trade or trade-print strategy semantics.
- No broker fill, order, slippage, commission or execution simulation.
- No target detector, evaluator or Nasdaq lifecycle transition.
- No final Take Profit, Break-Even cost-basis or target edge-case resolution.
- No mapping from observability insufficiency to rule result, human validation or `StrategyVerdict`.
- No market-data provider or vendor selection.
- No autonomous order execution or live credential integration.

## Follow-up implementation increments

1. Add a generic, independent replay-data observability model and diagnostics that can represent candle-safe and resolution-insufficient cases without changing verdicts.
2. Define normalized high-resolution historical observation contracts, stable chronological identity and deterministic source validation.
3. Extend canonical synchronization and `StrategyReplayContext` with optional bounded observations while preserving candle-only behavior.
4. Add provider-neutral fixtures and adapters only after a separate data-source decision.
5. Implement a deterministic target-touch primitive for source-resolved cases.
6. Integrate Nasdaq target-consumed cancellation through canonical lifecycle policy only after required source edge cases and result mappings are approved.

## Review conditions

Review this ADR if:

- canonical replay no longer uses immutable chronological snapshots;
- a provider cannot preserve the chronology or provenance required by normalized observations;
- high-resolution data becomes mandatory for most strategies;
- a future execution simulator needs bid/ask or fill semantics at the same boundary;
- observability requires an approved mapping to strategy results or verdicts.
