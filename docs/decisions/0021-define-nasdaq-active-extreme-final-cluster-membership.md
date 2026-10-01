# ADR 0021: Define Nasdaq active-extreme final-cluster membership

## Status

Accepted as a human-source clarification and architecture audit. Final-cluster membership is a `confirmed` strategy concept; exact automatic first-member detection is `unresolved` and exact membership remains `human_validation_required` until supplied by human review. Geometry from known members is deterministic. No runtime implementation is introduced.

## Date

2026-10-01.

## Context

[ADR 0020](0020-define-nasdaq-post-completion-active-structural-handoff.md) left one focused question: whether the new active HH/LL body members cover the whole breakout impulse or its final contiguous region, and how that region starts. Later human evidence resolves the **structural concept**, without supplying a machine boundary algorithm. ADR 0020 remains immutable; this decision records the later clarification.

The supplied human review identifies **Video 2 approximately `01:40` and `11:10–11:35`**, and **Video 3 approximately `07:25–07:38`**. These are user-supplied reviewed findings, not an independently accessed video or verbatim transcript. No additional video URL or source identity is asserted. The human conclusion explicitly concerns the new post-completion H4 active extreme; this ADR does not independently promote other timeframe examples into automatic H4 rules.

## Decision

### Confirmed membership concept; rejected whole impulse

The new HH/LL consists only of the **final contiguous local turning/deceleration cluster** around the peak/floor, where bodies stall around the structural extreme. This is the mentor's structural point or *freno*, distinct from the directional impulse leading into it. This concept is `confirmed` universally after DirectCandidate, 007, 008 and 009 completion. Their human-assisted completion provenance remains auditable and creates no different future membership law.

The supplied illustration is conceptual, not a fixed candle count or algorithm:

```text
[c1][c2] | [c3][c4_peak]
 impulse | final HH vertex
```

`c1/c2` are earlier directional expansion and are not HH vertex members; `c3/c4_peak` are the final local cluster. Whole-breakout-impulse membership is **`rejected_ai_inference` / rejected interpretation**. A single maximum-wick candle is not an authorized substitute for the human-confirmed multi-candle region either.

### Visual start concept and automation boundary

The source conceptually places the first vertex candle at the transition from clean directional body expansion into the final horizontal region where bodies decelerate/stall and cluster around the extreme. **Strategy concept confirmed does not mean deterministic algorithm defined.**

The current evidence supplies no objective required body-size contraction, overlap, permitted body-coordinate distance, momentum formula/threshold, tick/percentage/ATR threshold, minimum/maximum candle count, fixed lookback or exact horizontal-zone tolerance. It supplies no Fibonacci rule or wick/body tolerance. Exact machine first-member detection is therefore `unresolved`; selecting the exact final-cluster members remains `human_validation_required`. No threshold may be silently inferred from the words “slows,” “stops” or “aligns.” Exact-doji semantics remain those of ADR 0020; no near-doji rule is added.

Until a deterministic detector is independently validated, authoritative exact membership must be **human-supplied/human-reviewed**. The source question “whole impulse versus final cluster” is **RESOLVED**. The remaining automatic first-member detector is **NOT defined** by this evidence. Do not ask for another review of these intervals to recover a numeric rule that the supplied source does not contain.

### Geometry once exact members are known

Use the same selected member set for both coordinates:

| Active extreme | `ProtectionAnchor` | `StructuralPrice` |
|---|---|---|
| Bullish HH | `max(member.High)` | `max(max(member.Open, member.Close))` |
| Bearish LL | `min(member.Low)` | `min(min(member.Open, member.Close))` |

These formulas are `confirmed` and deterministic for known members. `StructuralTurnBodyCoordinateCalculator`, `StructuralTurnProtectionAnchorCalculator` and `StructuralTurnGeometryCalculator` implement the equivalent upper/lower aggregation semantics. They do not select the cluster. Preserve the exact members and coordinate contributions, including distinct body/wick owners and equal-coordinate supporting events. No executable Stop Loss follows from `ProtectionAnchor`.

### Contiguity and confirming-candle relationship

The selected region is **contiguous and local** according to the human source. At baseline `ec65c38`, `CandleSeries` verifies unique ascending open timestamps and non-overlap; it does not require exact four-hour spacing or certify that every historical candle is available. The rebuilt-member observation sorts distinct member identities, and its resolver orders resolved candles, but neither validates a contiguous selected region. ADR 0009 explicitly did not impose contiguity on that different membership fact.

Simple chronological validation is **architecture-ready**: resolve exact closed same-series candles, order by their source positions and check that a selected region has no omitted candle between its first and last members in the supplied ordered H4 history. This checks a proposed set; it cannot infer its first member, prove visual stall or authorize an incomplete history as complete. Missing source history must not be concealed by synthesizing candles or requiring exact `+4h` spacing across market closures. No cluster-size or elapsed-time threshold is introduced.

Preserve ADR 0020's single-observation dual-role handoff. A confirming candle may belong to the new extreme cluster **when it is part of the human-confirmed final vertex**; it is not automatically its first or last member. Opposite-body confirmation still starts the next correction with that same candle as first correction member. Trend-directional or exact-neutral confirmation does not start correction. Vertex membership and correction membership are distinct facts: one causal candle may contribute to both without being consumed twice by ordinary market reducers.

This later membership clarification qualifies any unconditional vertex-inclusion reading of ADR 0020: a confirming event participates in establishing the structure, but its body membership follows the reviewed final cluster. Do not force it into or out of that set based solely on its confirming role. Neither membership order nor a later-selected last member replaces the confirming `MarketCursor`. A closed member becoming observable after that cursor cannot retroactively enter an earlier frame; its treatment in an evolving active extreme requires the future phase/cursor contract.

### Human-membership architecture audit

[ADR 0009](0009-define-nasdaq-rebuilt-candidate-vertex-replay-input.md) and the current [rebuilt observation](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqHumanRebuiltCandidateVertexObservation.cs), selector, resolver and geometry path provide a reusable **architecture pattern**, not a directly reusable domain observation.

| Audited concern | Current behavior and reuse boundary |
|---|---|
| Observation envelope | `IStrategyReplayInputObservation` carries strategy/version, provider/symbol, UTC `ObservedAtUtc` and `SourceReference`. This causal/provenance envelope is appropriate for the future distinct membership fact. |
| Episode identity | `NasdaqHumanRebuiltCandidateVertexEpisode` binds parent invalidation, migration candle and `CandidateSide`. The new HH/LL instead belongs to the new active structural Episode and its predecessor completion. Do not invent a migration/invalidation to fit that key. |
| Structural side | Rebuilt `CandidateSide.Lower` means HL and `Upper` means LH. After completion the active extreme is respectively HH/upper or LL/lower. Existing candidate-side identity cannot be relabeled as active-extreme side. |
| Member representation | Nonempty distinct immutable H4 `OpenTimeUtc` identities are useful. The rebuilt DTO additionally requires the migration candle and is bound to a different vertex. Its membership constraints do not transfer directly. |
| Missing / Unique / Conflict | Existing selector filters exact episode matches in the causally bounded context, groups identical member sets and preserves all supporting observations. The safety pattern is reusable, but the selector and its typed results accept only rebuilt-candidate observations. |
| Member resolution | The current resolver requires a rebuilt resolution context, migration membership and equality to its known migrated protection anchor. Exact candle lookup/identity validation is reusable in principle; this resolver is not a post-completion resolver. |
| Geometry | Upper/lower generic geometry aggregation is reusable unchanged given correct members; the specialized rebuilt geometry adapter retains rebuilt-episode semantics. |
| Provenance | Selection retains all compatible supporting records; member resolution alone does not replace the complete selection. New active state/handoff must transport exact members, selection, source references and visibility times, plus 008/009 and predecessor origin evidence. |

A **new semantic observation type** for `PostCompletedActiveExtremeMembership` (or repository-equivalent name) is required. No such contract currently exists. It must distinguish the new active Episode, predecessor confirming completion, active HH/LL side, exact reviewed region and causal source envelope. Exact identity/schema, lifecycle scope for an evolving extreme, resolver guards and transport are still architecture specification work; this ADR does not prescribe a constructor or implement a DTO. Do not reuse `NasdaqHumanOriginVertexEpisode` as a new active identity by falsely labeling confirmation as invalidation.

The safe selection policy for that future distinct fact follows the established pattern: no visible matching assertions means `Missing`; one exact semantic member set means `Unique` with every compatible supporting record; different visible sets for the same precisely scoped fact mean `Conflict`. Ordering observations is not reviewer authority. No recency winner, majority, union, intersection or silent correction is authorized. Define the new fact's key before treating different stages of a genuinely evolving extreme as conflicting assertions; assertion correction/supersession remains separate.

### Causal evidence and future implementation path

`StrategyReplayContext` bounds matching input observations by `ObservedAtUtc <= AsOfUtc`. Future exact membership must obey that same visibility rule, and the reviewer assertion cannot predate the actual close of any selected candle. Members must be exact, closed and observable in the matching H4 series. Later evidence never rewrites an earlier replay frame or backdates a known active geometry.

[ADR 0018](0018-define-nasdaq-invalidated-origin-late-evidence-catch-up.md) provides reusable principles: keep evidence time separate from market cursor; resolve usable evidence at the current frame; retain immutable earlier results; process later eligible candles once in chronological order. Its implemented origin initializer and catch-up policy are **not** automatically applicable: the current orchestrator stops at `Completed`, while this new fact concerns a valid structural pair and potentially developing vertex. Post-completion waiting representation, Missing/Conflict behavior, allowed member horizon, phase initialization and any late-evidence backlog resumption require a distinct contract. No automatic jump to the latest member or market candle is permitted.

The safe future path is:

1. Specify and represent exact human-reviewed active-extreme membership with its distinct identity, causal bounds and provenance.
2. Select `Missing | Unique | Conflict` from currently visible matching evidence.
3. With `Unique`, resolve exact member candles and validate the supplied region's order/contiguity against available source history.
4. Derive HH/LL geometry deterministically using existing generic calculators.
5. Initialize an explicit active structural-pair state under ADR 0020's semantic handoff requirements; its exact representation remains to be specified.
6. Preserve confirming-candle dual roles, the causal market cursor and single market consumption, then resume only under an explicitly specified later-candle policy.
7. Research automatic cluster-start detection separately.

Human-assisted implementation is conceptually supported, but not yet fully implementation-ready: the distinct observation/identity and active-pair/phase/provenance contracts are missing. Fully automatic HH/LL construction remains blocked by the unresolved detector. These are separate architecture and automation limitations; no further evidence is needed to decide whole impulse versus final cluster.

### Human Dependency Reduction — Nasdaq H4

The exact first-member detector is a **future research candidate**, not an active rule. Use labeled human examples through the requested lifecycle: human ground truth → candidate algorithm → focused edge-case tests → independent replay/backtest → human approval → candidate strategy version → activation. Activation must retain the project's separate explicit human promotion requirement and immutable versioning; no unapproved rule or automatic activation is permitted. Preserve negative results and avoid future-data leakage. This task defines no thresholds, candidate algorithm, tests or strategy version.

## Alternatives considered

- Aggregate all breakout candles: `rejected_ai_inference`; contradicted by the final-cluster source concept.
- Treat visual deceleration as an already deterministic selector: rejected; no objective boundary is supplied.
- Reuse the rebuilt-candidate DTO, migration key or `CandidateSide` unchanged: rejected; they identify a different structural fact.
- Infer start from sorted/contiguous timestamps: rejected; order validates a selection, not visual membership.
- Reprocess confirmation or backdate late review: rejected by single-consumption and causal replay requirements.

## Consequences

The whole-impulse-versus-final-cluster source question is resolved. **HUMAN VALIDATION REQUIRED FOR EXACT MEMBERSHIP**, satisfied when usable exact members are supplied as human evidence; the deterministic first-member detector remains unresolved for automation. No repeated video request for a nonexistent numeric rule is warranted. The next work is specification of the distinct human-membership and active-state representation, not automatic discovery.

This known post-completion extreme is separate from arbitrary-history bootstrap, which remains unresolved independently. `NQ-H4-001` remains `BlockedByUnresolvedSpecification` pending its complete evaluator contract; this ADR changes no evaluator status, `RuleStatus`, registration, metadata or capability counts. Only this ADR and the index are changed; no C#, tests, snapshots, reducers, orchestrator, API/UI/persistence or Forex changes are made.

`NEXT_RECOMMENDED_ACTION: SPECIFICATION_CLARIFICATION`
