# ADR 0010: Define the Nasdaq collision StructuralPrice replay input

## Status

Accepted. This decision defines the human assertion for `NQ-Q-H4-008`; no observation, selector, completion or evaluator is implemented by this ADR.

## Date

2026-09-22.

## Context

A closed H4 candle can make a strict new candidate HL `Low` or LH `High` and close strictly beyond the frozen opposite-impulse terminal while its body is contrary to the breakout direction or an exact doji. The existing breakout state classifies this as `NqQH4008HumanStructuralPriceRequired`. The breakout, strict migration, candidate side, new `EffectiveProtectionAnchor`, frozen reference and validating candle are known. Only the definitive candidate `StructuralPrice` lacks source-backed automatic ownership. `NQ-Q-H4-007` already completes the different directional-body collision deterministically; [ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md) concerns member selection for an ordinary post-migration rebuilt candidate.

`IStrategyReplayInputObservation` carries strategy/version, provider, symbol, UTC `ObservedAtUtc` and auditable `SourceReference`. `StrategyReplayContext.InputObservations` admits an observation only at `ObservedAtUtc <= AsOfUtc`. The breakout state retains `NasdaqHumanOriginVertexEpisode`, `ValidatingCandle`, `CandidateSide` and the deterministic anchor. `StructuralTurnGeometryResult` and `StructuralCandidateValidationResult` store numeric coordinates and validation evidence; neither requires an identified market candle as the source of `StructuralPrice`. A later collision-specific completion must nevertheless preserve the human observation as price provenance alongside the breakout's market evidence; assigning the validating candle as body-coordinate owner would invent a rule.

## Decision

A distinct immutable human replay-input observation asserts exactly: **for this one verified `NQ-Q-H4-008` collision, the definitive candidate `StructuralPrice` is the supplied decimal price**. It asserts no breakout, migration, side, protection anchor, candle membership, strategy version, validated pair, `RuleStatus` or strategy verdict. Those facts are established or checked independently against the breakout state. The payload contains only `StructuralPrice` as the repository's `decimal` price value. It contains no OHLC, `ProtectionAnchor`, selected members, tolerance, derived geometry or HH/LL coordinate.

The exact collision key composes the existing `NasdaqHumanOriginVertexEpisode` (strategy ID and **exact** version, provider, symbol, H4 and invalidating candle `OpenTimeUtc`) with `ValidatingCandle.OpenTimeUtc` and `CandidateSide`. The observation envelope exposes the same identity fields as existing replay inputs; the consumer must compare every field exactly to the breakout's episode, candle and side. For this collision the validating candle itself is the strict new wick migration candle: `MigrationCandleOpenTimeUtc` and `ValidatingCandleOpenTimeUtc` denote one event, so the contract has only one additional candle identity. `PriorMigrationCandle` is earlier history and is not this collision key. H4 series identity plus `OpenTimeUtc` identifies that closed candle. No session date, GUID or price-derived identity is added. A reviewer assertion cannot create or reclassify a breakout; only a state with `HasStrictMigration == true` and `CollisionKind == NqQH4008HumanStructuralPriceRequired` is eligible. Inputs for `NQ-Q-H4-007` and ordinary `NQ-Q-H4-009` are not applicable.

`ObservedAtUtc` must be UTC and no earlier than the validating candle's `CloseTimeUtc`. It controls visibility, not candle identity, breakout time, market cursor or authority. If the collision closes at T1 and the response becomes observable at T2, frames before T2 cannot use its price; T2 does not rewrite T1. Exact strategy-version binding has no active-version fallback. `SourceReference` is nonempty, trimmed audit provenance, and every compatible observation's provenance remains available.

Within one exact collision key, visible responses with exactly equal `StructuralPrice` are compatible duplicates, regardless of observation time or source; preserve all supporting records. Distinct decimal values conflict even if close or visually similar. No first/last, majority, source-priority, average, tolerance or automatic correction wins. A later differing response is a conflict from its visibility time, not implicit supersession; explicit correction/supersession remains unresolved. Zero visible responses mean no human price is available. A future selector may distinguish `Missing | Unique | Conflict` at the domain boundary. The evaluator mapping for missing evidence and for this new conflict is not decided here; no new runtime status is introduced.

Only a future `Unique` response may be composed with the deterministic `EffectiveProtectionAnchor` (`ValidatingCandle.Low` for HL or `High` for LH), the existing frozen-terminal strict break and validation primitives. The future result must keep the human price observation and its `SourceReference` distinct from the validating candle's wick and break evidence. Current numeric geometry can represent the two coordinate values without a required market-candle price owner; it does not itself carry provenance, so the collision-specific result must retain that observation. The validating candle remains the confirmation event and `LastProcessedCandle`; neither `ObservedAtUtc` nor a selected human source changes the market cursor. This ADR does not implement the flow or authorize a complete pair from absent/conflicting evidence.

## Alternatives considered

| Alternative | Assessment | Decision |
|---|---|---|
| Reuse ADR 0009 member selection | It answers which candles form an ordinary rebuilt vertex, not the missing price of this collision. | Rejected |
| Derive price from validating or prior candle body | No source-backed ownership for contrary/doji-body `NQ-Q-H4-008`. | Rejected |
| Let the human supply price and protection anchor | Duplicates and can contradict the known strict wick extreme. | Rejected |
| Bind one human price to the exact collision and preserve its provenance | Completes only the missing fact without altering deterministic evidence. | Selected |

## Consequences

The positive observation value-object contract is ready for implementation without new strategy semantics. The selector, provenance-preserving completion and evaluator integration are not implemented. Initial raw-history H4 bootstrap, missing-evidence runtime mapping, human correction/supersession, ordinary rebuilt-candidate human membership, broader near-doji questions and H4 lifecycle orchestration remain independent blockers. `NQ-H4-001` is not evaluator-ready. No metadata, capability, runtime or trading permission changes follow from this ADR.
