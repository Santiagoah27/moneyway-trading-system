# Nasdaq end-to-end multi-timeframe research V1

`EXP-NQ-END-TO-END-MTF-V1` / `1.1.0` composes the existing experimental
Nasdaq components for one operating day and one explicit historical UTC
cutoff. It keeps `CanonicalAdmissionPermitted=false` and
`OperationalSignal=false`. The published Nasdaq strategy version remains
the identity of the replay context, and its canonical rules and outcomes
remain separate from this report.
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md) · [Audited strategy specification](../strategies/nasdaq/strategy-specification.md)

## Replay and evidence

- Original H4, H1, M5 and M1 series enter `CanonicalMultiTimeframeReplayCursor`.
  Equal-time closes advance together. `CreateStrategyReplayContextUseCase`
  creates contexts from the retained canonical frames. A small adapter
  exposes their already-observable candles as the existing `CandleSeries`
  inputs. A requested cutoff between closes uses the last canonical
  frame and retains the requested clock cutoff separately. An empty
  stream represents missing data. Instrument and provider must agree.
- The preparation snapshot is fixed at 08:00 Bogotá. The H4 evidence
  includes all three existing pivot profiles and both horizons, with
  the original market-structure and overlapping context observations.
  Context available means the structure candidate classified its evidence
  and the context history contains an interaction observation anchored to a
  confirmed reference. This does not select a bias or force a classification
  of the latest candle. Independent validation is recorded separately.
  H4 limitations do not suppress M5/M1 diagnostics.
  [H4 context candidate](nasdaq-h4-context-candidate-v1.md)
- Exact H1 Asia and London observations use the ADR 0028 version and
  retain source owners, coverage and availability. The existing contextual
  liquidity research preserves E1/E2/E3 and its source identities. Its
  observations do not satisfy Standard or WF2 eligibility. M5 reuses all
  existing sweep-order, window and formation policies and FVG profiles.
  Tied SC or IFVG evidence remains visible. M1 scans both profiles from the
  actual M5 directional FVG close.
  [Session references](../decisions/0028-define-candidate-nasdaq-session-windows.md) · [M5 sequence](nasdaq-m5-contextual-sequence-v1.md) · [M1 candidate](nasdaq-m1-realignment-candidate-v1.md)
- Each distinct eligible M1 realignment close has a canonical frame diagnostic.
  SL and TP consume source candles bounded by that exact reference close.
  The component outcomes, target candidates, source-level interactions,
  distances and hypothetical ratios are retained as indexed evidence. The
  research clock is `EXP-NQ-TRADING-WINDOW-1130-V1`, with `[08:30, 11:30)`
  Bogotá. The published `[08:30, 11:00)` clock remains visible for comparison.
  These clocks and observations have no position-management, execution or
  session-lifecycle authority.
  [Research window](nasdaq-trading-window-1130-alignment-v1.md) · [Structural SL](nasdaq-m5-structural-stop-loss-candidate-v1.md) · [Structural TP](nasdaq-structural-take-profit-candidate-v1.md)
- The report uses a dedicated research result over `StrategyReplayContext`.
  `IReplayRuleEvaluator`, `EvaluateStrategyReplayContextUseCase`,
  `StrategyReplayContextObservation` and
  `GenerateCanonicalMultiTimeframeBacktestUseCase` remain reserved for
  their existing strategy and rule contracts. The Worker writes canonical
  frame diagnostics and candidate evidence in a research artifact.

## Identity, state and counts

The logical run ID includes candidate, dataset and requested cutoff. The
artifact envelope records SHA-256 of the supplied context and retains the
raw and normalized source hashes and documentary UTC mapping. The nested
research result represents the observable prefix, so it can remain
identical when the input file hash changes due to future rows.

The chain index uses the existing episode, M5 configuration, M1 profile and
realignment evidence identity. The geometry configuration ID combines M5,
M1, SL and TP profiles. Its context outcome groups list the exact H4
configuration IDs sharing that outcome. Pairing an H4 ID in the group with
the geometry configuration ID identifies the full configuration. Each
configuration and episode can be looked up without duplicating shared source
evidence or choosing among tied observations. Counts for associations include
waiting configurations. Distinct event counts use the component association
keys and observable timestamps with direction for realignment events.

`StructuralReferencesObserved` records completion of downstream SL/TP
geometry. Full completion requires available H4 preparation, completed
geometry and at least one verified branch observed at the M1 realignment
timestamp T. Verification ties the selected M5 confirmation and directional
FVG, including the originating zone for an IFVG trigger, to the exact M1
scan and realignment attempt. Each branch retains its invalidation and
evidence-limit timestamps, both observable states and a
`realignmentBindingVerified` flag.

Only branches that supported completion at T determine the status at a
later cutoff. One observed supporting branch yields
`ResearchChainComplete`. When all supporting branches have since been
invalidated, the result is `ResearchChainCompleteWithInvalidatedContext`.
When the surviving evidence is limited, the result is
`ResearchChainCompleteWithLimitedContext`. All three retain T and count
toward full completion. Only the second counts toward completions with
invalidated context. Geometric completion has its own timestamp and count.
These are experimental evidence statuses and carry no entry eligibility.

### Invalidation audit

Version `1.0.0` accepted an invalidated branch when assigning a completion
timestamp and separately classified the result as invalidated when any
branch was invalidated at the report cutoff. A synthetic H4-available fixture
with a liquidity take, SC or IFVG, directional FVG, M1 realignment at
14:00 UTC and positive SL/TP geometry reproduced the discrepancy in 12 of
21 tests. The same fixture keeps the geometry timestamp in all rows below.

| Branch evidence | Version 1.0.0 | Version 1.1.0 |
| --- | --- | --- |
| All invalidated before T | Complete with invalidated context, T, 1 complete / 1 invalidated | `ContextInvalidatedAtRealignment`, no timestamp, 0 / 0 |
| Directional FVG or origin IFVG invalidated exactly at T, with no other supporting branch | Complete with invalidated context, T, 1 / 1 | `Ambiguous`, no timestamp, 0 / 0 |
| Sole supporting branch invalidated after T | Complete with invalidated context, T, 1 / 1 | Complete with invalidated context, T, 1 / 1 |
| Invalidated branch plus verified branch still observed | Complete with invalidated context, T, 1 / 1 | `ResearchChainComplete`, T, 1 / 0 |
| Limited branch plus verified branch still observed | `InsufficientEvidence`, T, 1 / 0 | `ResearchChainComplete`, T, 1 / 0 |
| Evidence becomes limited after T for the sole supporting branch | `InsufficientEvidence`, T, 1 / 0 | `ResearchChainCompleteWithLimitedContext`, T, 1 / 0 |

Equal close timestamps leave intrabar order unresolved. Unverified branch
bindings leave the result ambiguous when no verified branch supports
completion. Limited or absent evidence yields `InsufficientEvidence` when
it leaves no support or ordering ambiguity. Missing H4 and incomplete SL/TP
retain their existing statuses.

The report schema stays at `1`. New run IDs include the integrated
projection version between the candidate ID and dataset ID. The supervised
viewer accepts both ID formats and checks completion against supporting
branch states on `1.1.0` reports. Frozen `1.0.0` artifacts retain their
original content and meaning.

## June 23 Development run

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-mtf-research-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-end-to-end --cutoff-utc 2026-06-23T16:30:00Z
```

This writes one `nasdaq-end-to-end-research.json`. Existing component
modes retain their files and formats. The Development source SHA-256 is
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`.
June 22, 24 and 26 review episodes, June 25 Validation and June 30
HeldOut were excluded.

At 13:00 UTC the four timeframes advanced together. H4 had ten closed
candles and no recorded gap within its supplied interval. The 1/1, 2/2
and 3/3 profiles yielded 3, 1 and 1 confirmed pivots, respectively, with
insufficient confirmed structure for all six preparation configurations.
The overall outcome was `H4ContextUnavailable`. H1 Asia and London were
complete. Asia High/Low were 30,463.9 / 29,715.2 and London High/Low were
29,818.6 / 29,472.7.

| Quantity | Count |
| --- | ---: |
| Contextual episode IDs / episodes with observed liquidity take | 12 / 6 |
| Distinct first-selected confirmation events / component observation IDs | 8 / 9 |
| Distinct M1 anchors / realignment events / realignment observation IDs | 4 / 5 / 8 |
| M5/M1 episode/configuration associations / associations with realignment | 576 / 240 |
| TP evaluations / SL evaluations | 20 / 15 |
| Evaluated TP/SL associations / downstream geometry completions | 2,880 / 2,688 |
| Full research configuration associations including waiting and H4 profiles | 41,472 |
| Full H4-to-M1 completions / completions with invalidated context | 0 / 0 |

There were nine protective HL observations, six nonprotective SL observations
and 20 available targets. Of 60 shared SL/TP pairings, 36 yielded a hypothetical
ratio (minimum 0.013295, median 0.205263, maximum 1.610363). The source day
contained bullish M1 realignments. The counts use overlapping research
profiles and one Development session.

LL/E1 preserved the 13:10 UTC take, 13:35 SC, 13:55 directional FVG and
13:58 M1 realignment. M5 evidence at the M1 frame ended at 13:55. The FVG
inversion at 15:10 was observable later and did not erase the earlier
completion of downstream geometry. Of the 240 M5/M1 associations with
realignment, 16 had all bound branches invalidated at M1 and 224 observed
a branch invalidation later by 16:30 UTC.

The run uses documentary timestamps, with no independent labels or
authenticated historical availability. Reproducing the report needs no
manual action. Independent backtesting requires reviewed reference labels,
admitted data and held-out chronological evaluation. Supervised Demo v0
depends on the existing human evidence contract, resolved execution policy
and explicit approval for any promoted strategy version.
[Demo evidence contract](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md)
