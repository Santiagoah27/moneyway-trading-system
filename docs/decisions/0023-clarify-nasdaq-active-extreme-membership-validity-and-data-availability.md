# ADR 0023: Clarify Nasdaq active-extreme membership validity and data availability

## Status

Accepted. Clarifies the already-defined causal horizon and separates invalid membership from unavailable historical market data. The scoped member resolver is implementation-ready. No strategy or runtime changes are introduced.

## Date

2026-10-01.

## Context

[ADR 0022](0022-define-nasdaq-post-completion-active-extreme-evidence-lifecycle.md) requires exact selected members to close no later than the verified correction-start turn. The observation primitive (`a270fb2`) retains that turn and requires UTC evidence time no earlier than its close. The selector (`2861163`) consumes causally visible observations and returns `Missing | Unique | Conflict` for one exact membership event.

The subsequent resolver request incorrectly required a visible observation referencing an unclosed future member to return `DataUnavailable` and become successful merely by advancing replay time. That contradicts ADR 0022's fixed turn horizon. Work correctly stopped without changes. This ADR clarifies the resolver/test contract; accepted ADR 0022 remains immutable. No new mentor/video evidence is needed.

## Decision

### Canonical causal invariant

For every selected member of a valid `NasdaqHumanPostCompletionActiveExtremeObservation`, when its evidence is visible in a replay frame:

```text
member.CloseTimeUtc
    <= MembershipEvent.CorrectionStartCandle.CloseTimeUtc
    <= Observation.ObservedAtUtc
    <= StrategyReplayContext.AsOfUtc
```

The correction-start event closes the impulse horizon for this initial vertex. No selected member may originate after that boundary. Actual resolved source close times determine validity; a member open timestamp alone does not authorize a fabricated OHLC interval. This contract does not require that the correction-start candle be selected, or that it be the first/last selected member.

### Distinct resolution domains

| Domain | Meaning | Future resolver consequence |
|---|---|---|
| Success | Exact selected historical candles resolve in the correct H4 series and satisfy member invariants, horizon and contiguity | Return all selected members chronologically with the complete Unique provenance |
| DataUnavailable | No established membership-contract violation, but required exact historical H4 data is absent/unavailable and full resolution cannot be completed | Dedicated domain unavailable result; no partial successful cluster, inference or substitution |
| Invalid membership / contract violation | A member closes after the verified turn; an available intervening candle is omitted; or another member-reference invariant is violated | Reject invalid input under existing validation/invariant exception conventions; do not label it DataUnavailable |

The successful membership must satisfy the full invariant. DataUnavailable never means that an invalid reference is waiting to become valid. When required source data is absent, some exact source checks cannot yet be completed; an unavailable result is not certification of validity. Check all violations that can already be established from the event/references and available source data, rather than disguising a known violation as missing data. Complete remaining validation when historical data is recovered.

ADR 0022 explicitly allows a dedicated primitive-level `DataUnavailable` outcome and rejects invalid caller identity/assertions through existing argument/invariant conventions. Existing human-member resolvers reject inconsistent resolution inputs rather than inventing a global invalid-evidence status. Implementation must follow those conventions; none of these domains is an `NQ-H4-001` rule result or evaluator mapping.

### Post-turn references never become valid

If `member.CloseTimeUtc > CorrectionStartCandle.CloseTimeUtc`, the observation violates this event's membership contract. It is not selector Missing, human Conflict, unavailable history or future evidence waiting for closure. The member remains outside the original vertex at every later `AsOfUtc`. Later visibility cannot extend the horizon, relabel this event or repair the evidence. Explicit human correction/revocation/supersession remains separate future work.

### Historical data availability and recovery

For a turn closing at T5, exact reviewed members T3/T4/T5 can satisfy the horizon. If selected historical candle T4 is absent from the supplied exact H4 context, return DataUnavailable without removing, approximating or synthesizing it. When an otherwise equivalent context supplies that same historical T4 candle, the same evidence may resolve successfully after all checks pass.

This is **historical data recovery**, not waiting for T4 to close: a valid T4 was already required to close no later than the turn. Evidence meaning, event identity, selected members and observation time remain unchanged. A new resolution does not mutate earlier unavailable results or retrospectively overwrite recorded replay frames. Data availability recovery does not guarantee success if recovered data reveals an actual contract violation.

### AsOfUtc and unchanged selector

`ObservedAtUtc <= AsOfUtc` governs human evidence visibility. The market frame also bounds observable closed market candles. Neither time extends membership beyond the original correction-start close.

The existing selector remains unchanged: exact-event visible member sets determine Missing, Unique or Conflict. Unique establishes agreement, not correctness of market references. A Unique selection can fail subsequent resolver validation. The resolver does not rerun compatibility, choose an observation or convert missing market data into human Conflict. All supporting provenance remains available.

### Contiguity and market gaps

Preserve ADR 0022's contiguous positions among the available ordered exact H4 candles. Available `A B C` with selected `A C` is **invalid membership**, because an existing intervening B was skipped. Do not infer B into the set or return DataUnavailable for that violation.

If the available series itself contains a legitimate timestamp gap, adjacent selected positions are not invalid solely because wall-clock time suggests another bar. No exact `+4h` rule, synthetic candle or inferred cluster start is permitted. A selected exact reference absent from history is DataUnavailable; an unselected bar guessed solely from time spacing is not an unavailable requirement. Contiguity among available data does not certify unseen history as complete.

### Correct regression contracts

Future resolver tests must distinguish:

1. **Future-source invariance at T:** source dataset A contains valid required historical members; dataset B has the same required history plus arbitrary candles closing after T. Build both canonical contexts at the same T. Resolution at T must be equivalent. The existing [ReplayFrame](../../src/backend/MoneyWay.Domain/MarketData/Replay/ReplayFrame.cs) rejects future candles in `AvailableCandles`, so add future bars to the upstream source dataset and use canonical replay bounding; do not construct an invalid frame containing them directly.
2. **Historical recovery:** valid selected historical identities with one required exact candle absent produce DataUnavailable. An otherwise equivalent context supplying that already-closed historical candle allows success, subject to all invariants. Earlier results stay immutable.
3. **Invalid post-turn member:** turn closes at T5 and selection references a candle closing at T6. Reject membership. Evaluate again at T10 and still reject it; time advancement never repairs it.
4. **Invalid contiguity:** selected `A C` with available intervening B is rejected, without adding B or misclassifying the failure as unavailable data.

Do not require the contradictory regression “a visible valid observation selects a candle still unclosed at current AsOfUtc and becomes valid later.” The canonical invariant makes that scenario impossible for valid evidence.

### Readiness and independent work

The resolver is implementation-ready with three distinct responsibilities: consume semantic membership already selected as Unique; validate its event/member contract; resolve exact historical market candles and classify unavailable data. No new architecture decision is needed to distinguish these domains. No automatic member detector or geometry is required for this primitive.

Final-cluster concept, human ownership, existing HH/LL geometry, confirming-candle dual roles, exact correction-start mechanics and selector compatibility remain unchanged. Automatic first-member detection remains Human Dependency Reduction — Nasdaq H4. Arbitrary-history bootstrap and broader lifecycle/evaluator work remain separate. **NO HUMAN STRATEGY EVIDENCE REQUIRED. NO VIDEO REVIEW REQUIRED.**

## Alternatives considered

- Treat a post-turn member as unavailable until later closure: rejected; time cannot change the fixed event horizon.
- Treat absent historical data as human Conflict: rejected; agreement and market availability are different domains.
- Claim Unique certifies member validity: rejected; selection precedes market-reference validation.
- Infer intervening candles from four-hour spacing: rejected by available-series contiguity semantics.
- Modify ADR 0022 or existing selector behavior: rejected; this clarification records its existing causal consequences in a new ADR.

## Consequences

The previous resolver test contradiction is resolved. Implementation can proceed without strategy invention, future-member legalization or availability/validity conflation. This task changes only this ADR and the decision index; no C#, tests, observations, selectors, resolvers, geometry, states, orchestrators, evaluators, metadata or capabilities are modified. Nasdaq remains 32 rules, 14 required rules, 4 registered evaluators, 10 required evaluator gaps and `HasFullRequiredEvaluatorRegistration = false`; `NQ-H4-001` remains `BlockedByUnresolvedSpecification`. No status mapping is introduced.

`NEXT_RECOMMENDED_ACTION: IMPLEMENT_DETERMINISTIC_PRIMITIVE`
