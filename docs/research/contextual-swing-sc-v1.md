# Experimental contextual M5 structural reference and subsequent SC

Authorized source: user task `292ee493-543a-4742-9cf5-4b9b75186392`, 2026-11-20,
under [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)
and the unchanged [ADR 0028 session windows](../decisions/0028-define-candidate-nasdaq-session-windows.md).
This composes [contextual liquidity tracking](contextual-liquidity-tracking-v1.md),
the [inclusive V2 bounded detectors](m5-inclusive-confirmation-v2.md) and the same
strict-close primitive used by the [published SC candidate](../strategies/nasdaq/m5-strict-close-candidate.md).
It does not create trusted facts to invoke the candidate evaluator.

`EXP-NQ-CONTEXT-SWING-SC-V1` / `1.0.0` is isolated experimental research. Low-side
liquidity investigates an upward break, high-side investigates a downward break;
this mapping is explicitly a MoneyWay hypothesis, not operational BUY/SELL.
No strategy, published candidate, workflow, active metadata or verdict is changed.

## Event policies

Each available reference is evaluated independently; none is ranked or discarded.
All policies retain the original source/candle identity, availability, temporal
phase and prior penetration history. Each policy is version 1.0.0:

- `EXP-NQ-CONTEXT-EVENT-E1-V1`: first strict M5 penetration within tracking range
  after reference availability. Missing prior closed slots prevent verification
  of a first event; an observed candidate survives only as unverified evidence.
- `EXP-NQ-CONTEXT-EVENT-E2-V1`: first strict event whose candle **opens** at or
  after 08:30 Bogotá. The 08:25–08:30 candle is excluded from E2. Its prior history
  survives. Verify required post-start search coverage; do not reset at 08:30.
- `EXP-NQ-CONTEXT-EVENT-E3-V1`: first post-start strict event with a distinct prior
  penetration and a distinct later closed candle strictly on the other side
  (Close > low-side level / Close < high-side level). Equality is insufficient.
  Validate three distinct chronological candle identities through existing
  tracking links; never use the current event's own return. Missing search/history
  coverage prevents a verified first qualifying event.

E3 uses an earlier penetration / later return / eligible event chain, not an
assumption that no other penetration occurred between those observations. Its
return candle may itself have penetrated: only the separate closed-candle quote
is used. The report retains the particular supporting chain and intervening rows.
No intrabar path, valid new take, reuse or reentry rule follows from it.
Simultaneous reference interactions and opposite-side ambiguities remain in the
unmodified tracking output. No AH/AL/LH/LL priority is introduced.

## Structural families: six rows per selected event

All families use only eligible closed M5 available **no later than event open**.
Equality of the preceding close and event open is the shared interval boundary;
it supplies no event-candle completed OHLC. Snapshot selection is a retrospective
research boundary, not authentication that a person/system selected it live.

| Family / version | Upward experiment after low-side event | Downward experiment after high-side event |
| --- | --- | --- |
| EXP-NQ-CONTEXT-SWING-PREVIOUS-V1 / 1.0.0 | Immediately preceding exact closed M5 High | Its Low |
| EXP-NQ-CONTEXT-SWING-RANGE-V1 / 1.0.0 | Maximum High over continuous [08:00,event open) | Minimum Low over that interval |
| EXP-NQ-CONTEXT-SWING-CONFIRMED-V1 / 1.0.0 | Descending V2 bounded continuation's confirmed High | Ascending V2 bounded continuation's confirmed Low |

H1 abstains when the immediately preceding interval is missing. It is a baseline,
not a claim that every preceding candle is a valid structural swing. H2 preserves
all tied extreme owners, excludes the event, requires a nonempty continuous
preparation range and uses `StructuralCandidateExtremeCalculator`. Availability
is the full range's end, separately from extreme formation time.

H3 calls the existing `ExecuteInclusive` unchanged for A/B/C/D-V2 / 2.0.0. Its
explicit initial anchor is the exact closed candle beginning 08:00 Bogotá,
available at 08:05. No later substitute anchor is chosen. The episode is bounded
at event open. It keeps each algorithm's start, strict fixed-anchor continuation,
inclusive extreme and abstention semantics. A missing preparation interval is an
additional contextual data-coverage abstention, with actual detector output kept.
Only an already confirmed reference is consumed. No provisional price, extra cycle,
later candidate or penetration-candle extreme is promoted to a structural reference.

## Frozen reference and subsequent strict close

The result preserves source owners, formation, availability and snapshot selection
times. Before SC, scan available eligible closed candles between reference
availability and event open. Any prior strict break makes the frozen reference
stale; preserve that evidence and abstain without replacement. Missing coverage
cannot certify that it was intact.

The event's close is compared separately using `StructuralBodyCloseBreakCalculator`.
If it already closes strictly beyond the selected level, report
`AbstainedSameCandleBreak`; do not relabel later candles remaining beyond as new SC.
This ordering rule is conservative experimental policy, not a mentor prohibition.

Otherwise search from candles opening at or after event close, using strict
Close > selected High / Close < selected Low. Equality and wick-only penetration
do not qualify. Require all necessary closed slots through a claimed first SC;
with missing data retain any observed break as unverified and abstain. Exact M5
duration/alignment and cutoff visibility are enforced, with no interpolation,
tolerance, color/body-size/ATR/volume filter or hidden mutable state.

The reference remains frozen per event-policy/family/variant combination. Matching
reported outcomes may be grouped, but all row/variant IDs and actual evidence
remain in JSON. Combinations and duplicated outcomes are not independent episodes.
`CanonicalAdmissionPermitted=false`, `OperationalSignal=false`; no Ready, order,
candidate activation or canonical observation is produced. The 11:00/11:30 conflict
and authenticated temporal admission restrictions remain unchanged.

## Actual June 23 Development comparison

Source context is unchanged, SHA256
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`.
The exact previous tracking output is embedded without reinterpretation. Session
prices remain AH 30463.9, AL 29715.2, LH 29818.6, LL 29472.7; H1 context remains
AsiaLowOnlyPenetrated with lower magnitude 242.5. Range/cutoff remains documentary
07:00Z–16:30Z. New hypotheses execute only on this exposed Development episode.

| Reference | E1 event open / close UTC | E2 event | E3 event |
| --- | --- | --- | --- |
| AH | None observed | None observed | None observed |
| AL | 07:00 / 07:05 | 13:30 / 13:35 | 13:30 / 13:35 |
| LH | None observed | None observed | None observed |
| LL | 13:05 / 13:10 | 13:30 / 13:35 | 13:30 / 13:35 |

Actual E3 support: AL earlier penetration 07:40–07:45 (Low 29704.6), later return
07:45–07:50 (Close 29717.9 > AL); LL earlier penetration 13:15–13:20 (Low 29463.2),
later return 13:25–13:30 (Close 29484.2 > LL). The latter closes exactly at the
selected event's opening boundary. Both chains use distinct candles. They do not
establish a new valid sweep. AL's London return remains part of its history.

All 72 combinations, exact OHLC, history, coverage and identities are in the
[JSON](../data/strategy-lab/contextual-sc-v1/june23.json) and
[grouped Markdown](../data/strategy-lab/contextual-sc-v1/june23.md).

| Reference / policy / family | Structural price | Source M5 open UTC | Available UTC | Subsequent SC open / close UTC | SC Close |
| --- | ---: | --- | --- | --- | ---: |
| AL / E1 / H1 | 29770.9 | 06:55 | 07:00 | 07:05 / 07:10 | 29808.1 |
| LL / E1 / H1 and H2 | 29510.2 | 13:00 | 13:05 | 13:30 / 13:35 | 29532.7 |

For AL and LL E2/E3, H1 selects 29503.9 from 13:25–13:30; H2 selects 29519.6
from 13:20–13:25, known for its full range at 13:30. Their selected event closes
29532.7 at 13:35, already beyond both levels. All eight combinations abstain
as same-candle cases. A later continuation is not reported as new SC.

H3 produces no confirmed pre-event structural reference in the six selected
event cases: four detector rows lack the preparation anchor, four await pullback
start and sixteen await continuation. Their actual V2 outputs are preserved.
There are no stale selected references in this real session; synthetic tests
exercise confirmed references that have already broken and preserve their prices.

| Outcome | Combinations |
| --- | ---: |
| ExperimentalScObserved | 3 (two physical SC candle intervals, one Development episode) |
| AbstainedSameCandleBreak | 8 |
| AbstainedConfirmedStructure | 24 |
| AbstainedPreparationRangeUnavailable | 1 |
| NoQualifyingEventObserved | 36 |

H1/H2 agree for LL E1 and disagree on E2/E3 selected price while sharing the
same-candle abstention. No family wins. The original ascending continuation label
Low 29381.7 is on the 13:30 penetration candle; it is not the structural High
29503.9/29519.6 selected before that event. Original descending High 29811.6 is
also a separate bounded continuation label, not contextual SC ground truth. The
old V2 2/2 agreement does not define contextual accuracy or independent validation.

## Executed validation

| Scope | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Application Nasdaq/replay and reused candle/structural primitives | 2337 | 0 | 0 |
| Integration Nasdaq and existing swing lab/viewer/blind dataset regressions | 389 | 0 | 0 |
| Explicit opt-in authentic June/Replay #2 and retrospective outcome/closure protection | 6 | 0 | 0 |
| Backtesting Nasdaq/replay/multi-timeframe | 35 | 0 | 0 |
| Domain replay/strategy version | 26 | 0 | 0 |
| Total applicable backend tests | 2793 | 0 | 0 |

The total includes 20 new synthetic contextual unit cases and two new Worker
integration cases. The focused Worker class also passed all 15 cases, reproducing
old and new report bytes. These focused runs overlap the total, not extra tests.
The full `dotnet build MoneyWay.sln` completed with zero warnings/errors. Scoped
C# whitespace verification, documentation links and `git diff --check` passed.
Frontend tests were not run because no frontend behavior or files changed.

Coverage includes mirrored upward/downward SC, exact decimal equality and tiny
strict differences, wick rejection, frozen/stale confirmed references, coverage
abstentions, same-candle rejection, distinct E3 identities, repeated results,
earlier-prefix equality with future extensions, and culture-independent report
bytes. Existing protected datasets, candidates, evaluators and Replay #2 outputs
remain unchanged. Authentic report/input hash checks do not establish new temporal
admission or independent validation of this experiment.

## Reproduce and next evidence step

From repository root after `dotnet build MoneyWay.sln`:

```powershell
$source=(Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination=Join-Path $env:TEMP ('moneyway-context-sc-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-context-sc
```

The existing Worker publishes `contextual-swing-sc.json` / `.md` in a new directory,
never overwriting old reports. Existing modes and hypotheses are unchanged. No
frontend code changes or browser workflow are necessary; the existing viewer has
no contextual-SC projection contract and is not silently fed this different schema.

The minimum next step is a separate independent contextual label protocol: before
seeing algorithm results, identify relevant liquidity/event, the structural High
or Low that was already known, its source owners and confirmation availability,
then the later SC evidence or an explicit no-SC/ambiguous outcome. Preserve source
clock/admission limits. These already exposed June 23 findings can support
retrospective qualitative review only, not fresh blind labels or winner selection.
Do not execute/tune these hypotheses on June 25 Validation or June 30 HeldOut in
this feature. Do not activate a candidate or declare Nasdaq Demo v0 complete.
