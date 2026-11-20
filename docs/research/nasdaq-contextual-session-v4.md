# Strategy Lab V4: contextual Nasdaq session research

Subsequent configuration: [ADR 0028](../decisions/0028-define-candidate-nasdaq-session-windows.md)
and the [explicit-session candidate](../strategies/nasdaq/explicit-session-candidate.md)
now supply user-defined Asia/London windows and a separately generated documentary
H1 report in this existing route. The original V4 input, unresolved-history
metadata, labels, sources and earlier validation record below remain unchanged.
The new report resolves those boundaries for its named candidate configuration;
it does not approve contextual relevance, causal admission or an operational signal.

Authorized source: user task dated 2026-11-19, attachment
`a2d3dc45-0223-4dad-b255-92a1677d3067`. This extends the existing
[V2 chart](m5-swing-viewer-v2.md) and [V3 research workflow](m5-blind-validation-v3.md)
under [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md).
It is documentary visualization, not a contextual detector, canonical session
evaluation, admission bypass or strategy activation.

## Actual development evidence registered

The supplied `moneyway-m5-research-labels-v1.json` was actually available, read
without modification and independently hashed:

`8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861`.

Its dataset/version/content identity matches the published V3 manifest. It contains
exactly the two original Development records, revision 1,
`SelfDeclaredIndependent` / `IndependentRetrospectiveResearch`. Neither has an
independent human confirmation timestamp. This is unauthenticated browser-local
research evidence, not independently authenticated blindness or mentor truth.
The export contains label/report identities, not full algorithm outputs; those
outputs were independently checked against their original versioned C# reports.

| Original experiment | Human reference / source candle open UTC | Every V1 hypothesis | Algorithm source candle / confirmation UTC | Absolute price difference | Exact reference agreement |
| --- | --- | --- | --- | ---: | --- |
| `m5-20260623-1300-ascending-v1`, cutoff 14:00Z | Low 29381.7 / 13:30 | 29441.2 | 13:25 / 13:35 | 59.5 | No |
| `m5-20260623-1400-descending-v1`, cutoff 15:00Z | High 29811.6 / 14:20 | 29811.6 | 14:20 / 14:40 | 0 | Yes |

All four hypotheses have **one exact agreement in two Development episodes**.
This is a limited descriptive result, not general accuracy, profitability or a
winner. The first human-selected Low belongs to the continuation-confirming
candle, which the shared V1 aggregation deliberately excludes. This is consistent
with the discrepancy and motivates a future separately versioned hypothesis; no
V1 semantics or scores were changed here.

The [public evidence projection](../data/strategy-lab/context-v1/development-review.json)
retains exact original semantic label fields, exposure, revision, cutoffs and
input/report digests. It substitutes `human-reviewer-1` for the local name and omits
device/reveal timestamps and unrelated exported metadata. It declares this
redaction explicitly. Original file bytes remain external and unchanged; the
private raw export is not committed or copied into frontend assets.

## Original market-data coverage

Only the already authorized, registered
[June source package](../data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json)
was used. Actual raw and normalized H4/H1/M5/M1 files existed. Their complete-file
hashes matched the package, and every selected native row matched normalized
timestamp, OHLC and tick volume exactly. No acquisition or aggregation was needed.

Context session: **June 23, 2026**, with explicitly selected preceding history
from **June 21 21:00Z through June 23 16:30Z** (documentary mapping). This is a
43.5-hour requested viewing interval, not a claimed full trading day or an Asia /
London session definition. June 29 was unnecessary; no other session was added.

| Original TF | Candles through maximum cutoff | First available open UTC | Last completed close UTC | Missing source ranges / tail |
| --- | ---: | --- | --- | --- |
| H4 | 10 | June 21 21:00 | June 23 13:00 | No internal row gaps. The following 4H interval is not complete by 16:30 and is not published as a closed candle here. |
| H1 | 41 | June 21 22:00 | June 23 16:00 | No rows June 21 21:00–22:00 or June 22 21:00–22:00. Final half-hour is not a completed H1 candle. |
| M5 | 498 | June 21 22:00 | June 23 16:30 | Same two source ranges without rows. |
| M1 | 2490 | June 21 22:00 | June 23 16:30 | Same two source ranges without rows. |

Provider `oanda-global-markets-mt5-demo`, original instrument `US100`. No TradingView
price or USTEC substitution. Every row retains native timeframe/interval/OHLC,
tick volume and original native server opening identity. `derived=false`; even an
equivalent 240-minute tag is not silently accepted as original H4.

The existing June-only mapping is nominal UTC = server minus three hours;
close = open + native interval. It does not authenticate publication, observation
availability, source-native bar closure or canonical UTC admission. Source holes
are reported as missing rows, not inferred market schedules or corruption.
Continuity in H4 does not establish complete M1 coverage.

Curated source: `src/frontend/moneyway-web/src/research/context/data/june23-context.json`.
Its source paths/digests are preserved inside the dataset. No validation/held-out
rows or candles beyond the maximum cutoff are included. No synthetic context
stands in for authentic documentary rows.

## Session semantics remain separate

The audited [Nasdaq specification](../strategies/nasdaq/strategy-specification.md)
and existing `NasdaqSessionLiquidityCalculator` were inspected. The latter has a
runtime convention Asia `[D-1 17:00,D 02:00)`, London `[D 02:00,D 07:00)` in
America/Bogota. This is existing code, not a newly approved experimental definition
for this contextual view. The current V4 instruction explicitly leaves exact
session boundaries unresolved; this feature does not silently decide applicability
or plot authoritative Asia/London extrema. Available preceding OHLC remains visible.

Preparation at **08:00 America/Bogota** is a clock reference, not proof that both
preparation steps completed or that NQ-TIME-003 passed. The current request's
**08:30–11:30** and runtime **[08:30,11:00)** are displayed separately as a limitation.
The repository specification also records a later 11:00 correction; V4 explicitly
requests leaving the discrepancy unresolved. Neither source nor ACTIVE metadata
was edited, migrated or used to synthesize eligibility. America/New_York is unused.

No new H4/H1 structural point, liquidity take, SC/IFVG, FVG, M1 pullback/realignment,
SL, target, risk parameter or trade outcome is inferred from generic OHLC extrema.
Setup-specific structural selection remains distinct from a generic detector.
Standard and Wickfill Scenario 2 remain separate, including their existing human
evidence boundaries, session guards and terminal no-reentry behavior.

## Product and temporal behavior

Dedicated route **`#strategy-lab-context`**, linked from V2 and V3. Compact controls
select the one registered session, H4/H1/M5/M1, six shared cutoffs (08:00, 08:30,
09:00, 10:00, 11:00, 11:30 Bogotá) and viewing history (2/6/12/24 hours or all).
The same cutoff applies across timeframes. Hover, focus/arrows and previous/next
inspection expose original OHLC, documentary UTC, Bogotá time and native identity.

The existing SVG chart was extended with labelled time guides and read-only M5
reference lines. It uses native interval spacing for dense M1 data. Markers align
with their actual source M5 candle when on M5; on other timeframes they retain the
M5 label/formation timestamp, not an invented H1/H4 structural identity or intrabar
extreme time. No new chart library, API, database, service or engine was introduced.
The versioned JSON is loaded through a fixed bundled asset URL only on the context
route, outside the JavaScript chunk. No arbitrary local-file endpoint exists.

All displayed candles have `CloseTimeUtc <= cutoff`. A viewport never cuts a
partially observable candle into a fabricated complete one. Counts, source gaps,
scales and pending-tail messages are derived from that closed prefix, not future
OHLC. Unavailable zooms explicitly ask the operator to widen the view.

Each original comparison retains its own initial anchor, input/report hash and
14:00Z or 15:00Z cutoff. It is hidden in full when the shared cutoff is earlier,
even if its algorithm confirmation occurred earlier than its original report
cutoff. The joint 1/2 summary is also hidden until both original cutoffs are reached.
This is not a new evaluation of the wider contextual dataset. Later context cannot
retroactively revise the original human label, score or fixed-anchor result.

Formation, algorithm confirmation, original review cutoff and human confirmation
are distinct. The latter stays null. Human annotation visibility is tied to the
original review cutoff, never backdated to formation or copied from the algorithm.
Reference lines are retrospective research illustrations, not operational signals.

The original export does not overwrite/import into V3 browser storage. A separate
local exposure list marks Development material viewed in context. A future first
review is then PreviouslyExposed. Existing original labels/scores remain intact;
an already committed, unrevealed selection cannot be edited after seeing context
before explicit reveal freezes it. Subsequent corrections retain V3's post-reveal
classification. Browser-local exposure tracking is unauthenticated and can be
cleared; failures ask for manual exposure declaration rather than claiming blindness.

June 25 remains unreviewed by this feature. June 30 remains reserved, with no
held-out detector evaluation or data added to this route. The V3 partition manifest,
original labels, V1 algorithms and SC candidate are unchanged. The physical source
file name ending June 30 is provenance for the registered full export, not a
payload of held-out rows. Existing deliberate access to V3 validation is not a new
contextual session selector or evidence used to tune hypotheses.

`CanonicalAdmissionPermitted=false`, `OperationalSignal=false` throughout.
No canonical observations, Ready verdicts, broker orders or activation are produced.

## Reproduction, startup and manual acceptance

From repository root, with existing dependencies:

```powershell
Set-Location src/frontend/moneyway-web
npm run dev -- --host 127.0.0.1 --port 5178 --strictPort
```

Open `http://127.0.0.1:5178/#strategy-lab-context`. No API, MT5, Docker or PostgreSQL
is needed. If using an already running frontend, open the new hash route there.

To reproduce curation, from repository root:

```powershell
$newContext=Join-Path $env:TEMP ('moneyway-context-'+[guid]::NewGuid().ToString('N'))
python tools/research/build_nasdaq_context.py --session-root C:\MoneyWay\session --review-export '<actual-path-to-moneyway-m5-research-labels-v1.json>' --output $newContext
```

The actual original export is required; absent or hash-mismatched evidence fails
closed. Existing output directories are never replaced. All four raw/normalized
source hashes and source rows are checked before publishing the two curated JSON
files. Original source/export bytes are never written. Reviewer/device details are
not accidentally copied from the raw export.

Manual acceptance:

1. Open the context route; select each original timeframe and inspect OHLC/native
   timestamps. The cutoff must remain unchanged when switching.
2. Select all history and maximum 11:30 Bogotá cutoff. Verify H4/H1/M5/M1 counts
   10/41/498/2490 and the stated source holes. H4 must stop at 08:00 Bogotá; no
   incomplete following H4 OHLC is displayed.
3. At 10:00 Bogotá, inspect both original experiments, switching the comparison
   selector. Preserve human 29381.7 versus algorithms 29441.2, and human/algorithms
   29811.6. Human confirmation remains unavailable. Toggle the read-only overlays.
4. Move back to 08:00 Bogotá. Both comparisons and their reference/confirmation
   overlays must disappear. At 09:00, only the first original report is eligible;
   the second report and joint summary remain hidden.
5. Verify unresolved Asia/London status, separate 11:00/11:30 clock references and
   preparation clock-only meaning. No automatic liquidity/structure/trade verdict
   should appear. Expand provenance only when needed.
6. Return to V3: future first reviews of exposed development data must say
   PreviouslyExposed; existing original records/revisions must not be overwritten.

Browser automation prohibited in the previous task was not retried. These steps
remain manual visual acceptance; DOM/unit tests and HTTP availability do not
constitute a browser screenshot or visual certification.

Human-reported follow-up, supplied with the inclusive-policy task on 2026-11-19:
the user completed manual acceptance of H4/H1/M5/M1 rendering, a shared evaluation
cutoff, future candle/reference concealment at 08:00 America/Bogota and recovery
of later comparison context. This records the user's visual acceptance; it is not
automated browser acceptance or authenticated causal market-data validation.
The separate [inclusive confirmation experiment](m5-inclusive-confirmation-v2.md)
preserves these original V1 reports and labels; it does not change this viewer.

## Executed validation

Final relevant scopes on 2026-11-19:

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application: Nasdaq, StrategyReplay and existing structural/body primitives | 2222 | 0 / 0 |
| Integration: Nasdaq plus existing M5 lab/viewer/dataset cases | 357 | 0 / 0 |
| Explicit authentic June / Replay #2 and prospective-correction regressions | 6 | 0 / 0 |
| Backtesting: Nasdaq, Replay and MultiTimeframe | 35 | 0 / 0 |
| Domain: Replay and StrategyVersion | 26 | 0 / 0 |
| Backend total across these scopes | 2646 | 0 / 0 |
| Complete frontend: 12 files | 152 | 0 / 0 |

Four new backend cases check the actual native/source/series contracts, coverage
holes, privacy projection, held-out exclusion, original report digests and exact
one-of-two human-reference agreement for all four V1 hypotheses. Twenty-four new
frontend cases check each timeframe separately, static-asset failure paths,
original-versus-derived identity, cutoff filtering, future-independent chart
geometry, original M5 marker placement, unavailable human confirmation, joint
summary gating, source limitations and exposure handling without original-label
mutation. Original V2/V3 and operational regressions remain included.

Complete .NET build passed with zero warnings/errors. Frontend production build,
ESLint, Prettier, scoped C# whitespace verification, 11 relative documentation links
and `git diff --check` passed. Curated JSON and public label projection reproduced
byte-for-byte from the actual local sources/export. The original export SHA256 was
checked again after curation and remained unchanged. Existing June/Replay #2 report
and registered package hashes also remained unchanged.

Temporary Vite on port 5181 served both the page and actual context JSON with HTTP
200 without an API; the owned test process was stopped afterward. Production assets
were checked for held-out IDs/timestamp payloads and contained none. No source or
algorithm data was fetched from MT5 or an external service.

Commands, from repository root:

```powershell
dotnet test tests/integration/MoneyWay.IntegrationTests --filter FullyQualifiedName~NasdaqContextResearchTests
dotnet build MoneyWay.sln
```

From `src/frontend/moneyway-web`:

```powershell
npm run test:run
npm run build
npm run lint
npm run format:check
```

The smallest next algorithm experiment is a **separate versioned hypothesis**
including the confirming candle's extreme, compared with unchanged V1 on already
reviewed Development context. It must not be called mentor-confirmed, activated,
or tuned on Validation/HeldOut. Autonomous contextual reference selection still
requires explicit setup-specific evidence and applicable session-boundary decisions.
