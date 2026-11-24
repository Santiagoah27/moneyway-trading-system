# Nasdaq M5 to M1 optional continuation FVG research V2

`EXP-NQ-M5-TO-M1-OPTIONAL-FVG-PATH-V2` / `2.0.0` compares two experimental
anchors in the same replay. The observations are conditional on the existing
reversal hypotheses. They carry no canonical H4 authorization, temporal
admission, or operational signal. [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)

## Audit of the gate

| Question | Audited evidence and implementation |
| --- | --- |
| A–B. First trigger and timestamp | Standard requires the first eligible SC or IFVG after a valid aligned Step 3. V1 builds SC from the verified structural close and IFVG from the inversion close, with the source candle attached. `EvaluateEpisode` collects all eligible confirmations at the earliest timestamp in `FirstEligibleIds` and `FirstEligibleUtc`. [Specification, Steps 3–6](../strategies/nasdaq/strategy-specification.md) · [M5 code](../../src/backend/MoneyWay.Application/Research/Nasdaq/M5ContextualSequence.cs) |
| C. Experimental eligibility | A selected sweep must have a verified source and strict penetration. Confirmation requires compatible direction, verified evidence, the same Bogotá operating date, continuity from the sweep, and a timestamp strictly after the sweep. `PostSweepObservation` reports chronology without filtering the two closes by the trading window. `WindowEventOnly` requires both closes within the window. `WindowConfirmationWithPriorSweep` permits a same-day prior take and requires the trigger within the window. Equal sweep and trigger closes stay in the evidence. `SameCloseDiagnostic` additionally records the unresolved order. [M5 sequence V1](nasdaq-m5-contextual-sequence-v1.md) |
| D. Why M1 uses `CompletedAtUtc` | V1 sets it from the earliest eligible directional FVG after the first trigger. The stricter formation policy also requires C1 to open on or after the trigger. The V1 M1 use case then anchors at that FVG's C3 close and requires a binding with its exact non-null `FvgId` and trigger ID. [M1 code](../../src/backend/MoneyWay.Application/Research/Nasdaq/M1RealignmentCandidate.cs) · [M1 V1](nasdaq-m1-realignment-candidate-v1.md) |
| E–F. SC or IFVG without a subsequent FVG | V1 keeps the trigger evidence and leaves `CompletedAtUtc` null. M1 returns `WaitingForM5Sequence` with zero scans for that association. The prior zone used by IFVG stays attached to its own trigger and cannot count as a continuation FVG. [M5 code](../../src/backend/MoneyWay.Application/Research/Nasdaq/M5ContextualSequence.cs) · [FVG/IFVG V1](nasdaq-m5-fvg-ifvg-candidate-v1.md) |
| G. Authority | The updated audited Standard correction and NQ-FVG-001/002 and NQ-M1-001 treat clean absence as optional support. Rejection, conflict, unusable selected evidence, and unavailable sources have distinct canonical handling. Standard can retain distinct support at the trigger close with reviewed evidence. Both research routes reuse the existing strictly later experimental FVG eligibility and add no canonical quality selection. [Specification, optional continuation correction](../strategies/nasdaq/strategy-specification.md) · [Rule catalog](../strategies/nasdaq/rule-catalog.md) · [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md) |
| H. WF2 | Scope remains explicit Wickfill Scenario 2 SELL. Its canonical opening sweep, reviewed bearish reversal and scoped eligibility retain their own ancestry under NQ-WF2-001/002. V2 describes experimental M5/M1 geometry independently of that branch. [ADR 0026](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md) |
| I. Order and invalidation | V1 retains equal-close events and alternative hypotheses. The earliest eligible trigger remains the reference even if its IFVG later invalidates. A directional FVG can later invert. V1 preserves the historical scan and its timelines, and chain projection checks the bound evidence at realignment. SC has no experimental invalidation rule. [M5 code](../../src/backend/MoneyWay.Application/Research/Nasdaq/M5ContextualSequence.cs) · [V1 chain projection](../../src/backend/MoneyWay.Application/Research/Nasdaq/NasdaqResearchChainProjection.cs) |

## Comparison

- `FvgAnchored` reads the V1 associations, binding identities and scans. `TriggerAnchored` starts at the observable first eligible Step-4 close and keeps every tied trigger. The scan provides shared geometry when order is unresolved. A later trigger cannot replace an invalidated first trigger.
- The comparison uses the same synchronized candle prefixes, 24 existing M5 configurations, and both existing M1 profiles. The first opposite body, pullback range, interruptions and first continuous in-window realignment come from the same M1 scanner. An M1 candle must open on or after the anchor and close after it. A close at 11:30 Bogotá is outside the research window. The published Standard window remains [08:30, 11:00) and the research window remains [08:30, 11:30). [V1 replay](nasdaq-end-to-end-multitimeframe-research-v1.md) · [ADR 0028](../decisions/0028-define-candidate-nasdaq-session-windows.md)
- An eligible optional FVG is supporting evidence in the comparison only when it is present before the assessed realignment and its lifecycle is observable there. A later FVG is retained with its later timestamp, and a same-close FVG retains unresolved ordering. An observed invalidation or missing M5 coverage is reported separately from a geometric realignment. SC validity is explicitly undefined, with source gaps reported where applicable.
- The report retains the original episode, selection, liquidity reference and event. Each comparison links to the M5 configuration, original confirmation evidence, M1 scan and attempts. The M5 input evidence retains unselected and ineligible events, lifecycle transitions, ambiguous sweep ordering, and M5 gaps. The replay footer and M1 scans retain source coverage. No additional setup identity is generated by a profile or route.
- Counts marked distinct deduplicate first realignments by direction and exact M1 close within the fixed provider, instrument, and operating day. SC uses its source break candle and direction. IFVG uses its originating formation endpoint and inversion candle with direction across the FVG profiles. A comparison association is one episode × M5 configuration × M1 profile. Associations without an eligible FVG are counted per first trigger identity and M5 configuration before expanding the M1 profiles.

## June 23 Development

US100, OANDA Global Markets MT5 Demo, cutoff 2026-06-23 16:30 UTC. Closed
prefixes contain 10 H4, 41 H1, 498 M5 and 2,490 M1 candles. Observed Step-3
candidates comprise four distinct reference-level takes on three candles.
The report contains two verified distinct SC events and 19 verified distinct
IFVG events. Two SC and 12 IFVG are eligible under at least one policy.
Five distinct events are first eligible triggers. One first trigger had
no eligible later continuation FVG in eight M5 trigger associations.

| First continuous in-window M1 geometry | FvgAnchored | TriggerAnchored |
| --- | ---: | ---: |
| Distinct market events | 5 | 6 |
| Associations | 240 | 256 |
| Associations with a sweep at or after 08:30 Bogotá | 192 | 192 |
| Associations with a sweep before 08:30 Bogotá | 48 | 64 |

Three events are shared across the two routes (13:30, 13:32 and 13:58 UTC).
The TriggerAnchored route alone reports 13:38, 13:41 and 13:56 UTC; the
control alone reports 14:07 and 14:17 UTC. Both routes report the same
event in 68 matched associations. In 188 matched comparisons the trigger
route has a different or sole event. When both anchors exist, the trigger
moves the anchor earlier by 5, 15, 20 or 90 minutes. The three exclusive
market events therefore describe changed first-event timing. The 16 net
extra associations are under `PostSweepObservation` for the London Low
take at 13:10 UTC. The first IFVG confirmed at 13:25 UTC, invalidated at
13:30 UTC and was already invalidated at both M1 closes (13:30 and 13:32
UTC). Those closes were already counted as market events in the control
via other associations. There were also 16 associations in each route
with a directional continuation FVG invalidated by realignment. The
per-configuration and pre-window counts are in `byPolicy`.

The 224 comparisons with tied first evidence comprise 208 with tied
distinct events and 16 with tied hypotheses for the same market event.
There were no unexplained M5 or M1 gaps in these comparisons. Upstream H4
remains provisional and its contextual validity remains unresolved. The
baseline outcome is `H4ContextUnavailable`, with zero complete H4→M1
associations. The 29 additional H4 candles with unresolved UTC mapping
were outside this input. Authentic availability and independent review
remain separate prerequisites.

## Reproduction

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$out = 'C:/MoneyWay/session/june23-m5-to-m1-optional-fvg-v2'
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $out --compare-m5-to-m1-optional-fvg-v2 --cutoff-utc 2026-06-23T16:30:00Z
```

The Worker requires the pinned Development input SHA-256
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c` and a
new output directory. It writes `nasdaq-m5-to-m1-optional-fvg-v2.json`.
The V1 integrated replay at the same cutoff reproduced SHA-256
`d19c506bbce9609a8d3237e9f21b527d4d0150a1ed0339dc3a86f04735b8cb61`.

Prioritize an admissible Development case with source-resolved NQ-H4-001,
an in-window aligned take, and an independently reviewed Step 4 and M1
sequence with clean absence of continuation FVG. That case would exercise
the missing end-to-end demo path through the existing human-assisted
Standard workflow. Preserve the pre-window and invalidated June 23
observations as negative controls.
