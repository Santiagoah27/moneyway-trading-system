# ADR 0008: Define the Nasdaq human origin-vertex conflict policy

## Status

Accepted. This decision adds selection semantics for the observation defined by [ADR 0007](0007-define-nasdaq-human-origin-vertex-replay-input.md). The selector and structural consumer are not implemented by this ADR.

## Date

2026-09-21.

## Context

Canonical replay can expose multiple immutable `NasdaqHumanOriginVertexObservation` records at one `StrategyReplayContext.AsOfUtc`. `NasdaqHumanOriginVertexMemberResolver` materializes the exact H4 candles for **one observation already chosen by its caller**; it does not decide which observation is authoritative. ADR 0007 left duplicate, conflict, correction and missing-evidence policy open. Human review now distinguishes compatible repeated evidence from incompatible member selections.

## Decision

The conflict scope is one post-invalidation reconstruction episode identified by strategy ID, strategy version, market-data provider, symbol, H4 timeframe and the invalidating candle's exact `OpenTimeUtc`. Observations for different episodes do not compete. No additional transition ID or session boundary is inferred.

At replay time `T`, only observations visible through `ObservedAtUtc <= AsOfUtc = T` participate. Within one episode, semantic structural identity is the **set** of `SelectedMemberOpenTimesUtc`. Input order is irrelevant. Equal sets are compatible even when `SourceReference`, `ObservedAtUtc` or other human provenance differs. The future selector may produce one usable semantic membership while preserving every supporting observation as audit evidence; compatibility does not erase or mutate records. More than one compatible record alone does not require human validation.

Two or more distinct visible membership sets for the same episode are a **conflict**, even if later body/wick geometry would produce identical prices. Structural processing for that episode must stop: no vertex geometry, `StructuralPrice`, `ProtectionAnchor`, post-invalidation reconstruction or dependent structural progression may use an arbitrarily chosen member set. A future evaluator/orchestrator maps this conflict to the existing `human_validation_required` rule result. It introduces no new `RuleStatus` value; `no_trade` remains a higher-level strategy verdict, and `failed`, `waiting` and `data_unavailable` are not automatic conflict mappings.

`ObservedAtUtc` controls causal visibility, never priority. If only A is visible at `T1`, a future conflicting B cannot make `T1` ambiguous. Once B becomes visible at `T2`, the episode is conflicted from `T2` unless a future explicit, causal human resolution contract applies. Historical replay results are not rewritten. A later differing membership is not an automatic correction or supersession.

The future centralized selector conceptually receives visible observations for one episode and distinguishes: **missing** (zero records), **unique semantic membership** (one record or multiple records with the same set), and **conflict** (multiple distinct sets). The runtime mapping for missing evidence remains unresolved. On a unique outcome, selection precedes `NasdaqHumanOriginVertexMemberResolver`, which resolves exactly one selected membership against closed H4 candles; only later may structural geometry run. The selector must retain the supporting provenance records for audit. This decision does not select a representative record or specify persistence.

No automatic winner is permitted by latest or earliest observation time, arrival order, source priority, confidence, majority, set size, union, intersection, single-extreme fallback or derived price. Explicit correction/supersession, reviewer authority and revision chains remain unresolved. Any future resolution must preserve immutable historical evidence and causal `AsOfUtc` results.

## Alternatives considered

| Alternative | Assessment | Decision |
|---|---|---|
| Treat record count greater than one as ambiguity | Compatible reviews of the same membership would be blocked. | Rejected |
| Pick by time, source, vote, set operation or price | Invents authority or changes human-selected membership. | Rejected |
| Centralize exact-set compatibility and block disagreement | Preserves human ownership, provenance and causal replay. | Selected |

## Consequences

The duplicate and conflict boundary is specified but no selector, evaluator or structural handoff is implemented. Missing-evidence runtime status and explicit supersession remain open. Initial raw-history H4 bootstrap is a separate human-input domain. `NQ-H4-001` remains not evaluator-ready; no metadata, capability or runtime status taxonomy changes follow from this ADR.
