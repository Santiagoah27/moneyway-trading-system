# Experimental contextual liquidity reference tracking V1

`EXP-NQ-LIQ-CONTEXT-V1`, version `1.0.0`. Authorized source: user task
`187c3ef3-2fe4-4993-bce3-dee605345b4f`, 2026-11-20. This is a reusable descriptive
research fold under [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md),
not a canonical liquidity rule or a strategy-version change. The existing
[session configuration](../strategies/nasdaq/explicit-session-candidate.md) and
[London–Asia analysis](london-asia-interaction-v1.md) remain separate source contracts.

## Inventory and source contracts

`TrackContextualLiquidityUseCase` takes exact H1 session data, homogeneous exact
M5 observations, operating date, explicit UTC research start/cutoff, origin and
provenance. It reuses `NasdaqSessionLiquidityCalculator.CalculateExplicitSessions`
and `AnalyzeLondonAsiaInteractionUseCase`. The latter's unchanged exact OHLC
comparison was extracted into `ObserveSide` and shared with M5 tracking. The
operational human-take constructor also requires strict price penetration, but
is deliberately not instantiated to turn these research rows into take evidence.
The inclusive price-touch primitive is not substituted for strict penetration.

Initial inventory is completed AH, AL, LH and LL, individually retaining native
price, high/low side, operating date, provider, instrument, exact source timeframe,
all tied source candle identities, formation and availability, complete coverage
and provenance. Stable IDs hash deterministic source/category/price/provenance
identity; they do not depend on evaluation cutoff or machine culture. AH and LH,
or AL and LL, remain distinct when prices coincide. Serialization order conveys
no distance, age, origin, magnitude or profitability priority.

Asia [D-1 17:00,D 02:00) and London [D 02:00,D 08:00) remain America/Bogota windows.
Session formation is the close of the last tied extreme-owning H1; availability
is the completed-window boundary in this **documentary closed-candle experiment**.
Only complete H1 coverage visible at that boundary yields an inventory member.
These nominal times do not prove historical provider publication or authenticate
a later export. Current MT5 admission requirements remain intact.

Optional `ContextualStructuralReference.FromSelection` accepts only a member of
an existing `NasdaqHumanStructuralLiquiditySelection.Unique` resolved against the
supplied replay context. It requires a confirmed validation or an existing typed
validated completion, exact source resolution and H1/H4 contracts. No raw high/low
scan or arbitrary scalar becomes a structural reference. Original strategy/version,
role/model, effective/observed human support and source provenance survive.
Formation uses known member candles; where the source contract supplies no owner,
it remains null. Availability is conservatively the selection's resolving context,
not an earlier invented time. Future structural selections are filtered entirely,
including IDs/counts. Duplicate identities are rejected, not silently merged.
No authentic optional structural reference was supplied for the June 23 run.

## M5 sequence and handoff

For every available high-side reference R, independently record High == R,
High > R, Close > R, Open > R. Low-side predicates use Low == R, Low < R,
Close < R and Open < R. Exact decimal comparisons have no tolerance, threshold,
ATR/body/volume filter or crossing-path assumption. An opening already beyond a
reference remains explicit. Every matching reference survives a multi-level
candle. Opposite-side penetrations in one M5 carry unknown intrabar order; M1
is not used to resolve it.

All eligible closed M5 rows are retained, including non-interactions needed to
inspect later closes on the other side. Each row references its stable inventory
member and exact source candle/availability, carries the four predicates, interval
and availability phases, prior observation/penetration counts, latest prior
penetration ID, pre-open history and a prior later-other-side-close ID where
observed. That close row retains its own prior penetration link, so the observable
sequence can be inspected without inferring intrabar trajectory or a new sweep.
Such a close is later than an earlier observed penetration, not necessarily a
replacement for the latest penetration. References/history are not discarded.

The handoff is the inventory reference plus `ContextualLiquidityObservation`,
its prior-event links and the enclosing explicit cutoff/coverage. The result
contains no selected swing, anchor, trading direction, winner or entry eligibility.
No replay-input/fact/evaluator interface is implemented or registered.

## Phases, coverage and temporal boundaries

London historical context is retained as H1 descriptive analysis. M5 observation
may begin at London start for available Asia references; London references cannot
consume their own pre-availability formation interval. M5 candles must **open at or
after reference availability**, close by cutoff and cover exact five-minute grid
intervals. No partial interval is inferred if a human reference arrives mid-bar.

Preparation is [08:00,08:30) Bogotá; observation starts 08:30. Interval phase and
evidence availability phase are distinct. A candle [08:25,08:30) forms during
preparation but becomes observable exactly at 08:30. It is not counted as observed
strictly before open. Likewise the final London candle can be available at 08:00
while its interval belongs to London context. Nothing resets at 08:30.

Per-reference expected closed slots start no earlier than both the research start
and reference availability. Missing slots/invalid intervals are explicit. Absence
is only `NoObservedStrictPenetrationInCoveredInterval` when closed-slot coverage is
complete; otherwise absence stays `InsufficientHistoricalCoverage`. A seen
penetration can coexist with missing coverage, which is separately retained.
Before the first eligible closed slot, there is no absence claim. Unfinished bars
and future source/structural selections do not enter the fold. No reference
replacement, invalidation or reuse policy is invented.

The June 23 bounded documentary range is [07:00Z,16:30Z] from the actual stored
Development source/cutoff. The endpoint is a research boundary, not approval of
11:00 or 11:30 as an operational cutoff. The runtime conflict remains unchanged.
`CanonicalAdmissionPermitted=false`, `OperationalSignal=false` throughout.

## Actual June 23 Development result

Original context SHA256 remains
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`.
The prior H1 result remains AsiaLowOnlyPenetrated with lower magnitude 242.5.
The generated [JSON](../data/strategy-lab/liquidity-context-v1/june23.json) preserves
source digests, documentary clock mapping and all 312 M5 reference-observation
rows. The [Markdown summary](../data/strategy-lab/liquidity-context-v1/june23.md)
includes the entire post-availability London Low sequence.

| Reference | Price | Source H1 open UTC | Formation close UTC | Documentary availability UTC | M5 rows | First strict interval UTC |
| --- | ---: | --- | --- | --- | ---: | --- |
| AH | 30463.9 | June 22 22:00 | June 22 23:00 | June 23 07:00 | 114 | None observed in covered range |
| AL | 29715.2 | June 23 06:00 | June 23 07:00 | June 23 07:00 | 114 | 07:00–07:05 |
| LH | 29818.6 | June 23 07:00 | June 23 08:00 | June 23 13:00 | 42 | None observed in covered range |
| LL | 29472.7 | June 23 12:00 | June 23 13:00 | June 23 13:00 | 42 | 13:05–13:10 |

All four observation coverages are CompleteThroughLastClosedInterval. These are
four references in **one exposed Development episode**, not four validation samples.
AL has 70 strict rows available before 13:30Z and 35 at/after it. Many rows open
already below AL; counts are exceedance observations, not new sweep counts.
AH/LH have no observed strict penetration in their covered intervals.

LL is already exceeded before 08:30 Bogotá. Its seven strict-penetration rows are:

| M5 open / close UTC | Open | High | Low | Close | Availability relation to 13:30Z |
| --- | ---: | ---: | ---: | ---: | --- |
| 13:05 / 13:10 | 29483.9 | 29494.4 | 29467.1 | 29477.4 | Before |
| 13:10 / 13:15 | 29477.8 | 29510.0 | 29457.4 | 29499.7 | Before |
| 13:15 / 13:20 | 29500.1 | 29501.3 | 29463.2 | 29495.7 | Before |
| 13:25 / 13:30 | 29503.9 | 29503.9 | 29441.2 | 29484.2 | Exactly at boundary |
| 13:30 / 13:35 | 29484.1 | 29536.9 | 29381.7 | 29532.7 | After |
| 13:35 / 13:40 | 29531.9 | 29561.5 | 29436.2 | 29544.7 | After |
| 15:40 / 15:45 | 29488.4 | 29520.4 | 29470.8 | 29508.9 | After |

All seven close above LL and open above LL. They demonstrate strict lower extrema,
not closes/openings beyond LL or authorized takes. Non-penetrating intervening
rows and their closes are retained too. The 13:30–13:35 row already carries four
prior observed penetrations and pre-open history. No operational conclusion is
drawn from that history or from the repeated observations.

## Reproduce

From repository root after `dotnet build MoneyWay.sln`:

```powershell
$source=(Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination=Join-Path $env:TEMP ('moneyway-context-tracking-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --track-context
```

This extends the existing one-shot Worker. It publishes new `liquidity-context.json`
and `.md` in a new directory, without overwriting source or earlier reports. The
default and `--analyze-interaction` modes retain byte-identical old outputs.
No frontend changes, new route, dashboard, forms or browser automation are needed.

## Next explicit experimental decisions

Compare separately identified hypotheses on exposed Development only, with their
assumptions declared before implementation:

- First post-availability strict-penetration candle as an experimental anchor
  candidate: assumes the first observable exceedance identifies a useful context
  boundary; missing earlier coverage makes this hypothesis inconclusive.
- First at/after-08:30 exceedance with retained pre-open history: assumes that an
  observation-phase boundary is useful, without asserting a new valid take or
  discarding earlier penetrations. Treat a candle ending at 08:30 separately.
- A later exceedance supported by an intervening closed M5 on the other side:
  assumes that an observable closed-candle sequence supplies a useful context
  boundary, without inferring an exact crossing or operational reuse rule.

These are proposed future anchor-selection experiments, not implemented winners
or automatically correct swing anchors. Compare exact source/anchor identities,
coverage, availability and downstream detector disagreements under identical
cutoffs. Existing two Development labels constrain retrospective comparison but
are not fresh independent validation or profitability evidence. Do not tune on
June 25 Validation or June 30 HeldOut, and do not promote V2 from 2/2 Development
agreement. Initial anchor hypotheses need explicit scope and versioning before
implementation; operational use still requires the approved promotion lifecycle.

## Executed validation, 2026-11-20

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application Nasdaq/replay/structural/body scopes | 2317 | 0 / 0 |
| Integration Nasdaq and M5 command/viewer/dataset scopes | 387 | 0 / 0 |
| Explicit authentic June/Replay #2 and prospective-correction regressions | 6 | 0 / 0 |
| Backtesting Nasdaq/Replay/MultiTimeframe | 35 | 0 / 0 |
| Domain Replay/StrategyVersion | 26 | 0 / 0 |
| Relevant backend total | 2771 | 0 / 0 |

New coverage comprises 20 deterministic unit cases and three Worker integration
cases. It checks complete/missing inventory, exact predicates and decimal
precision, coincident identities, opposite-side same-bar ambiguity, pre-open and
additional observations, intervening closes, no 08:30 reset, open-boundary
availability, unfinished/invalid/missing M5 intervals, wrong source identities,
all source ties, explicit validated H1/H4 inclusion, unavailable structural
selection filtering and immutable earlier prefixes even after later source changes.
Culture tests reproduce reference IDs and Markdown under es-CO without rounding.
No canonical interface/approval is added. Old session and London–Asia reports
reproduce exactly, alongside the new JSON and Markdown.

An initial focused run had 18 passed/1 failed: a test expected a preceding close
strictly before the next M5 open, although adjacent intervals share a boundary.
Its assertion was corrected to allow that exact equality; no trading or runtime
rule changed. Final focused tracking cases: 20 passed; command cases: 13 passed.
These overlap the broad totals and are not counted twice.

Full .NET build passed with zero warnings/errors. Scoped C# whitespace, research
documentation links and `git diff --check` passed. Frontend source/assets and
dependencies are unchanged, so frontend tests/build/ESLint were not rerun; the
previous 183-test baseline is not represented as a new execution. No new visual
acceptance or browser automation is needed for this Worker/report feature.
Raw/curated inputs, original human labels, published strategies/candidates,
supervised guards, original report hashes and Validation/HeldOut remain intact.

Reproduce focused validation from repository root:

```powershell
dotnet test tests/unit/MoneyWay.Application.UnitTests --filter FullyQualifiedName~ContextualLiquidityTrackingTests
dotnet test tests/integration/MoneyWay.IntegrationTests --filter FullyQualifiedName~NasdaqSessionLiquidityResearchCommandTests
```
