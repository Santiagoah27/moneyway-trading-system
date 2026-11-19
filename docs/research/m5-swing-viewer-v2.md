# Strategy Lab V2: historical OHLC discrepancy viewer

Implemented under the explicit user request of 2026-11-19, external attachment
`2ef0ffe9-072e-425e-a7c6-0a11a84adf40`. This is an isolated research viewer under
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md),
not an operational Nasdaq rule or candidate promotion. The
[four V1 hypotheses and shared policy](m5-swing-comparison-v1.md) are unchanged.

## Product and architecture

The existing React application has a dedicated `#strategy-lab` destination,
linked from its home page. Opening this destination directly does not mount the
documentary workflow or request the ASP.NET API. No MT5, Docker, PostgreSQL,
credentials or other service is needed.

An SVG candlestick chart uses actual supplied OHLC and proportional UTC time/price
scales. There was no existing OHLC component or chart library. SVG is sufficient
for these bounded episodes and adds no dependency or license/maintenance burden.
Hover, click, focus or keyboard inspection shows the original candle open/close
timestamps and prices. Previous/next buttons support inspection; narrow screens
can scroll the chart horizontally. A labelled OHLC table is also available.

The four independent overlay toggles only change visibility. Starts, all tied
reference candles, grouped equal-price levels, confirmation candles, the explicit
anchor and cutoff come from the report. Result cards remain visible when their
overlay is hidden. Pairwise full agreement and confirmed-reference agreement
remain distinct; the frontend does not calculate either.

Two fixed, versioned JSON resources in
`src/frontend/moneyway-web/src/research/reports/` are byte-for-byte outputs of the
existing Worker `m5-swing-lab` command. Dynamic imports select an allowlisted
resource; no upload, caller-supplied path, arbitrary filesystem endpoint or new
API/persistence mechanism is introduced. Resource failures display an error and
never silently fall back to another episode. The runtime reader validates schema,
source identity, OHLC, cutoff, supported hypothesis versions, marker membership,
pair identities and explicit research/no-signal boundaries. This is presentation
validation, not detector calculation or authentication. JSON numbers are used for
SVG geometry; exact decimal detector calculations remain in C#. These registered
episodes use at most two price decimals, independently checked against the source.

Generated resources are LF-pinned and excluded from Prettier rewriting so backend
tests can compare exact Worker output bytes. Original native TSV rows are retained
with documentation and bundled for secondary provenance inspection; tests compare
those copies exactly.
The Worker's JSON output now normalizes physical line endings to LF, so the same
report bytes reproduce across Windows/Linux and Git checkouts. This changes only
report serialization whitespace, not V1 calculations, input hashes or decisions.

## Synthetic regression

Input: [V1 synthetic fixture](../data/strategy-lab/m5-swing-v1/synthetic-disagreement.json).
All prices are controlled synthetic values, not market observations or mentor labels.

| Hypothesis | Start open UTC | Reference | Reference open UTC | Confirmed at UTC |
| --- | --- | --- | --- | --- |
| A | 2026-11-19 13:05 | 90 | 13:10 | 13:30 |
| B | 2026-11-19 13:10 | 90 | 13:10 | 13:30 |
| C | 2026-11-19 13:20 | 93 | 13:20 | 13:30 |
| D | 2026-11-19 13:05 | 90 | 13:10 | 13:30 |

A/D fully agree. A/B have the same reference and confirmation but different
pullback starts. C selects a different reference. No winner is inferred.

## Actual historical episode and provenance

The [registered June package](../data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json)
is documentary-only. Its actual authorized local sources existed and were read
without modification:

| Source | SHA256 |
| --- | --- |
| `C:/MoneyWay/session/raw/2026-06-29/US100_M5_202606220100_202606302355.csv` | `4f468275b5644d4298ba0c085802f04574c765f7dfa662df1ee99456eb9fcf46` |
| `C:/MoneyWay/session/normalized/2026-06-29/5M.csv` | `0133f473592a004535bac89437c96b1c369a753205792459c4e4e12c1835c21a` |

Provider: `oanda-global-markets-mt5-demo`. Original symbol: `US100`, not USTEC or
TradingView NAS100USD. Native source domain: MT5 server clock. The existing June
mapping is server minus three hours, with nominal close = open + five minutes;
it does not authenticate UTC publication, bar closure or historical availability.

The researcher explicitly selected the fixed interval **June 29, 13:00–15:00Z**
(nominal documentary UTC), with 24 contiguous M5 candles. The original native
opens are **16:00–17:55 server**. Selection used available data, a bounded round-hour
interval and a directly identifiable first candle, before executing the detector;
it was not selected for a desired reference, agreement, profit or trading outcome.
No claim is made that this arbitrary research interval is a strategy/session
boundary or that its first candle is the correct structural impulse.

Explicit initialization: ascending research hypothesis; first candle open
13:00Z; fixed High **29507.9**, nominal anchor close/declared research availability
13:05Z; cutoff 15:00Z. This is a researcher-supplied fixture, **not a mentor label**,
autonomous anchor-selection rule or authenticated causal initialization.

The [research input](../data/strategy-lab/m5-swing-v2/june29-documentary.json) and
[original native rows](../data/strategy-lab/m5-swing-v2/june29-native-source.tsv)
preserve the source values. All 24 normalized rows were independently matched
exactly to raw native rows for OHLC/tick volume and timestamps under the existing
documentary mapping, with no tolerance, interpolation or gap filling. The original
full-file hashes match the registered package.

**Actual result:** all four hypotheses return
`AbstainedNoPullbackBeforeContinuation`. The first continuation candle opens
13:10Z and closes 13:15Z, without an established pullback in this bounded episode.
There is no selected swing, reference-confirmation time, trade or economic outcome.
The viewer honestly shows agreement on abstention, not four successful detections.
The anchor was not moved to obtain a more attractive result. This demonstrates an
initialization/episode limitation rather than validating market-structure quality.

## Temporal and operational limitations

Reference circles are retrospectively plotted over their formation candles.
Separate markers and result fields show when the later continuation close made
that reference experimentally known. Provisional references are explicitly labelled.
The displayed cutoff is fixed for each report. Candle inspection and overlay toggles
do not change it; there is no simulated candle-by-candle replay retaining future
conclusions. Any future cutoff interaction must request actual C# evaluation at
that cutoff.

`CanonicalAdmissionPermitted=false`, `OperationalSignal=false`. No canonical fact,
strategy verdict, order, ACTIVE mutation, candidate activation, human market label,
economic outcome or temporal admission is produced. Original V1 calculation files,
Standard/WF2 evaluators and `nasdaq-0.2.0-sc-candidate` are unchanged.

## Startup and reproducibility

From the repository root:

```powershell
Set-Location src/frontend/moneyway-web
npm ci # only if dependencies have not been installed
npm run dev -- --host 127.0.0.1 --port 5178 --strictPort
```

Open `http://127.0.0.1:5178/#strategy-lab`. The API is not needed for this route.
To regenerate a report with the unchanged backend command, from repository root:

```powershell
dotnet build MoneyWay.sln
$inputFile=(Resolve-Path 'docs/data/strategy-lab/m5-swing-v2/june29-documentary.json').Path
$newReport=Join-Path $env:TEMP ('moneyway-lab-v2-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- m5-swing-lab --input $inputFile --output $newReport
```

Use `docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json` for the other
registered episode. Do not overwrite reports. The integration tests regenerate
both resources and compare exact bytes and source input hashes.

## Acceptance and next step

Human follow-up, 2026-11-19: the user explicitly reports eight passed browser
checks: dedicated navigation, OHLC rendering, candle inspection, structural
reference visualization, discrepancy comparison, formation/confirmation distinction,
overlay toggles and authentic documentary episode selection/rendering. The user
approved V2 as the basis for continued development. This is user-reported manual
acceptance, not automated browser verification, algorithm accuracy or authenticated
market-data admission. Screenshot bytes were not supplied with this acceptance.
See [V3 multi-episode review](m5-blind-validation-v3.md) for the subsequent feature.

Manual visual acceptance:

1. Open the direct route with the API stopped. The synthetic chart must render
   six candles, the anchor 110, grouped A/B/D level 90 and C level 93.
2. Inspect 13:10Z: O 100, H 108, L 90, C 106. Confirm A starts 13:05, B 13:10,
   C 13:20, D 13:05; all become known only at 13:30Z.
3. Toggle C off/on: only its chart overlay disappears/reappears; its card and the
   underlying results remain unchanged. Inspect candles with focus, arrows,
   previous/next and hover.
4. Select June 29. Verify 24 candles, provider/symbol/classification, explicit ancla
   29507.9 and all four abstentions. Expand native-source details and verify the
   first native row is 16:00 server and its OHLC matches the 13:00Z candle.
5. Narrow the viewport to 390 px. The chart scrolls horizontally and result cards
   form one column. Inspect the accessible OHLC table and formation/confirmation
   explanation. Refresh; the versioned default episode must load without API calls.

Browser visual acceptance was not executed: automatic approval review rejected
launching headless Chrome with `blocked by policy`. Component/geometry tests do
not substitute for visual browser acceptance. No browser screenshot is claimed.

Executed validation on 2026-11-19:

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application: Nasdaq, StrategyReplay and V1 structural/body primitives | 2222 | 0 / 0 |
| Integration: Nasdaq plus both M5 lab test classes, excluding local environment-gated sessions | 345 | 0 / 0 |
| Six authentic June / Replay #2 and prospective-correction regressions, explicitly enabled against the existing local input | 6 | 0 / 0 |
| Backtesting: Nasdaq, Replay, MultiTimeframe | 35 | 0 / 0 |
| Domain: Replay, StrategyVersion | 26 | 0 / 0 |
| Backend total across these scopes | 2634 | 0 / 0 |
| Complete frontend: nine files, including 21 viewer/reader cases | 91 | 0 / 0 |

New backend coverage has four cases: both generated report byte comparisons,
native/normalized fixture integrity and stability at every historical candle-close
cutoff. Frontend coverage includes OHLC geometry, marker source identity,
timestamps/prices, overlay interaction, grouped levels, agreement differences,
abstention, provisional/tied references, classification, invalid resources and
direct navigation without API calls. Synthetic projection-only UI cases exercise
ties and distinct confirmation timestamps; they are not additional C# detector
outcomes or mentor labels. In the actual V1 fixed-anchor run, confirmed algorithms
share the same first continuation; no different confirmation times were invented
for either registered episode.

Both builds passed; .NET reported zero warnings/errors. ESLint, complete frontend
Prettier verification, scoped C# whitespace verification, nine relative documentation
links and `git diff --check` passed. Vite served HTTP 200 on the dedicated local
port; this proves HTTP availability, not browser rendering. Original June / Replay
#2 report hashes and the registered documentary package hash remained unchanged.

Frontend verification commands, from `src/frontend/moneyway-web`:

```powershell
npm run test:run
npm run build
npm run lint
npm run format:check
```

Targeted report reproduction/integrity tests, from repository root:

```powershell
dotnet test tests/integration/MoneyWay.IntegrationTests --filter 'FullyQualifiedName~M5SwingViewerReportTests|FullyQualifiedName~M5SwingLabCommandTests'
dotnet test tests/unit/MoneyWay.Application.UnitTests --filter 'FullyQualifiedName~Nasdaq|FullyQualifiedName~StrategyReplay|FullyQualifiedName~StructuralBodyCloseBreakCalculatorTests|FullyQualifiedName~StructuralCandidateExtremeCalculatorTests|FullyQualifiedName~CandleBodyDirectionCalculatorTests'
```

The smallest next research feature is a fixed multi-episode dataset manifest with
independent, initially hidden reference labels and chronological development /
validation / untouched-final partitions. Reuse this viewer for blind discrepancy
review after label reveal, without tuning against final evaluation data, defining
numeric acceptance thresholds or promoting any algorithm automatically.
