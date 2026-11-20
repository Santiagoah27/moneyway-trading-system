# Contextual SC chronological human review V1

## Frozen selection policy (before new comparison execution)

Authorized task: `e52279ed-2ae9-456e-9c6f-56f11cd22347`, 2026-11-20.
Use only registered original US100 documentary artifacts from the June package.
Select the earliest source calendar date with all 15 exact H1 intervals across
Asia [D-1 22:00Z,D 07:00Z) and London [D 07:00Z,D 13:00Z), continuous M5 from
13:00Z through 16:30Z, and at least one preceding closed H4 and H1 at 13:00Z.
M1 is optional, original only. No detector results, prices, P&L or human labels
participate in selection. Preceding history starts at D-2 21:00Z where available.
Exclude June 23 and June 29 (exposed), June 25 (Validation) and June 30 (HeldOut).
No other eligible date is evaluated by the contextual hypotheses in this feature.

Registered-source coverage inspection identifies June 22, 24 and 26 as potential
unused dates; June 27/28 lack the required H1 windows. June 22 is selected by the
above policy, not by observed detector behavior. Unreviewed means no recorded
research review/exposure found in the repository, not an authenticated assertion
that a human has never viewed those candles. The reviewer must declare outside
exposure; independent status remains self-declared and browser-local.

The approved research policies and published candidates remain unchanged.
CanonicalAdmissionPermitted=false; OperationalSignal=false.

## Source and coverage

Reuse [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md),
[ADR 0028](../decisions/0028-define-candidate-nasdaq-session-windows.md), the
[context viewer](nasdaq-contextual-session-v4.md), [V3 review](m5-blind-validation-v3.md)
and [unchanged contextual SC hypotheses](contextual-swing-sc-v1.md).
The registered [June documentary package](../data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json)
retains raw/normalized hashes. `tools/research/build_contextual_sc_review.py`
verifies all eight original file hashes, then selected raw/native OHLC and tick
volume against normalized rows exactly. It neither runs hypotheses nor consumes
human labels. New directories only; original sources are never written.

The selected original US100 source is OANDA Global Markets MT5 demo. June-only
nominal UTC = server minus three hours remains documentary, not authenticated
publication, capture or canonical availability. No USTEC/TradingView substitution.

| Original timeframe | Curated count | Closed at preparation 13:00Z | Limitation |
| --- | ---: | ---: | --- |
| H4 | 4 | 4 | Only source June 21 21:00Z through June 22 13:00Z; no complete next H4 by 16:30Z |
| H1 | 18 | 15 | Source starts June 21 22:00Z; both session windows complete; final half-hour not a complete H1 |
| M5 | 222 | 180 | 42 exact preparation/observation intervals [13:00,16:30) |
| M1 | 1110 | 900 | Original optional inspection only, no new intrabar policy |

Requested preceding history is longer than available history. Source gaps retain
the unfilled leading range and do not imply a trading schedule. No candles are
manufactured. June 24/26 are coverage candidates only: no new contextual results
were computed for them. June 23 is exposed, June 25/30 remain reserved. No human
labels, independent agreement rates or winner have been produced by this task.

## Compact chronological review in the existing route

At `#strategy-lab-context`, choose **Revisión contextual cronológica**. Existing
exploration remains exposed June 23 and keeps its reports/labels. The review uses
the frozen June 22 asset, the existing `CandleChart`, original timeframe controls,
closed-prefix filtering, session-reference reader and V3 compare-and-write/read-back
local persistence convention. There is no new route, database, chart framework,
API, evaluator or strategy engine. A separate contextual label schema avoids
overwriting or reinterpreting bounded-continuation labels in V3.

- A: confirm relevant AH/AL/LH/LL (multiple permitted), none, ambiguity, insufficient
  context or skip at exactly 08:00 Bogotá. H4 judgment is optional free-text human
  context, never forced or automated. H1 session extrema and London–Asia interaction
  are descriptive observations; no E-policy or structural recommendation is shown.
- C: choose an original M5 High/Low directly on the chart, optionally attach a
  visible closed confirmation candle, or record no reference/ambiguity/insufficient
  data/skip. Commit before advancing into a future event to support pre-event
  comparison. Formation close, optional human confirmation evidence, review cutoff
  and maximum previously seen cutoff remain separate. Nothing backdates availability.
- B: advance by exact M5 closes, choose a visible interaction candle and one liquidity
  reference, and separately judge relevance. UI shows objective strict penetration
  only against an H1 reference available at that candle's opening. Earlier candles
  remain inspectable. Repeated penetration does not prove a valid new sweep.
- D: independently choose a later closed SC candle, no SC, ambiguity, same-candle
  ambiguity, insufficient evidence or skip. Event-candle SC is never forced into a
  subsequent confirmation; the existing conservative policy is unchanged.
- E: after A/C/B/D are committed and the cutoff reaches the complete report cutoff,
  explicitly reveal the existing pipeline output. Its actual bytes, source hash and
  experiment ID/version are verified; failure cannot substitute another report.

The chart's inspector and scales receive only candles closed at cutoff. No original
human labels, algorithm markers or summary recommendations enter the review chart.
Going backwards preserves the greatest viewed cutoff and hides the full comparison
until its own cutoff is visible again. Invalid/disabled actions explain prerequisites.

## Original labels, exposure and comparison denominators

The first observation of each stage is retained. Later edits append revisions;
they do not silently replace the sequence scored at reveal. Post-reveal corrections
are explicitly classified and remain outside the original comparison. Persisted
records bind dataset/version, provider/instrument/source candles, input hash, report
hash, reviewer, cutoff, maximum viewed cutoff, original prices, optional confirmation
evidence and device review time. Exact native identities survive with source rows.

A comparable pre-event C requires C committed before B, C's maximum seen cutoff
no later than B's event open, a chronological classification and no declared prior
exposure. A C selected after exposure remains retrospective/ineligible even if the
reviewer returns to an earlier cutoff. A returned-cutoff edit is classified as
retrospective; subsequent corrections cannot overwrite the original label.

The comparator reports dimensions separately: liquidity membership, event candle,
structural price, exact candle identity, optional availability delta, SC candle and
confirmation-close delta. It also retains same-candle cases, missing/false SC relative
to eligible labels and algorithm abstentions. Availability is not comparable without
supplied human confirmation evidence. SC binary comparisons additionally require a
matching event and structural price/source, binary human SC evidence at the complete
report cutoff, and a non-abstaining algorithm result. A different structural question
is not silently scored as the same SC task. Null is not counted as false.

Each dimension exposes its denominator. One source day remains **one session**,
regardless of combinations or observations. Ambiguous/insufficient/skipped labels,
retrospective references, outside exposure and inapplicable cutoffs are excluded
where they cannot support binary comparison. No aggregate accuracy, profitability
or winner selection. Original continuation labels are never contextual ground truth.

Persistence key `moneyway.research.contextual-sc.v1` follows V3 local concurrency
checks, read-back verification, strict source binding and append-only revisions.
Export writes `moneyway-contextual-sc-labels-v1.json`, retaining manifest and history.
Reload recovers the maximum viewed cutoff and original/revised labels. Storage errors
fail closed and keep the raw record available for export. Browser storage/device
times are unauthenticated, editable and erasable. Bundled source/report bytes and
DevTools can defeat concealment; outside exposure must be declared. This is not
authenticated blindness, causal backtesting or an immutable evidence system.

## Reproduction and manual acceptance

No API, MT5 or database is needed:

```powershell
Set-Location src/frontend/moneyway-web
npm run dev -- --host 127.0.0.1 --port 5178 --strictPort
```

Open `http://127.0.0.1:5178/#strategy-lab-context` and choose chronological review.
Use a fresh local record only if the material has not actually been seen; clearing
storage cannot restore independence. Declare outside exposure where applicable.

1. At 08:00 inspect H4/H1/M5, session references and descriptive interaction. Enter
   reviewer and confirm A or an explicit alternative. No hypothesis output is shown.
2. On M5 select High/Low for C and commit before exposing the intended event candle.
   Advance chronologically; selecting C after seeing the event is retrospective.
3. Select B's candle/reference and relevance. Select a later SC candle or D's no-SC,
   ambiguous/same-candle/insufficient/skip option. Confirm independently.
4. At episode end reveal the comparison. Check per-dimension denominators, statuses
   and absent availability where no confirmation evidence was supplied. This action
   does not approve a strategy or trade.
5. Return to an earlier cutoff: future comparison disappears. Reload and reopen
   review: maximum exposure and original labels survive. Append a correction and
   export; confirm the original label/reveal sequence remains retained.

Browser automation previously prohibited was not retried. DOM tests and HTTP checks
are software verification, not visual human acceptance.

Rebuild only the frozen curated source/selection manifest, from repository root:

```powershell
$newSource=Join-Path $env:TEMP ('moneyway-contextual-review-'+[guid]::NewGuid().ToString('N'))
python -B tools/research/build_contextual_sc_review.py --session-root C:\MoneyWay\session --output $newSource
```

Run the unchanged existing Worker on that context using new output directories with
`nasdaq-session-liquidity --input <absolute-context.json> --output <new-directory>`
and respectively no flag, `--analyze-interaction`, `--compare-context-sc`. The asset
names are sessions.json, interaction.json and comparison.json; their actual SHA256
values are sealed in manifest.reportHashes. Backend tests regenerate all three byte
for byte. The source-only builder intentionally never executes a hypothesis or
reveals a result as part of dataset selection.

## Remaining research decisions

No new algorithm rule is required to start independent review of these frozen
hypotheses. Human observations must still be supplied; no one has completed this
new review as part of implementation. The minimum next evidence is a pre-event
structural selection/confirmation and later SC judgment from a genuinely unexposed
reviewer. If that reviewer has seen June 22, this session is exposed for that reviewer;
do not clear or relabel it independent. June 24/26 would require a separately frozen
selection rather than silently swapping sessions.

Candidate promotion requires independent evidence across sessions, comparison of
reference relevance/availability, explicit version selection and human approval
under ADR 0027. Same-candle sequencing and possible M1 refinement remain research
questions, not resolved prerequisites for testing the separate-candle hypotheses.
No ACTIVE/published candidate, event/swing/SC policy, Standard/WF2 behavior,
no-reentry guard, temporal admission, Forex or 11:00/11:30 runtime behavior changes.

## Executed software validation

| Scope | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Application Nasdaq/replay and structural/candle primitives | 2337 | 0 | 0 |
| Integration Nasdaq and existing swing lab/viewer/dataset regressions | 391 | 0 | 0 |
| Explicit opt-in authentic June/Replay #2 and later outcome/closure protection | 6 | 0 | 0 |
| Backtesting Nasdaq/replay/multi-timeframe | 35 | 0 | 0 |
| Domain replay/strategy-version | 26 | 0 | 0 |
| Backend applicable total | 2795 | 0 | 0 |
| Complete frontend, 15 files, one worker | 210 | 0 | 0 |

Backend includes two new frozen-source/Worker byte-reproduction integration tests.
Frontend includes 27 new cases covering source selection/coverage, reserved dates,
cutoff filtering and future-independent chart geometry, exact source selection,
pre-event commitment versus retrospective selection, same-candle ambiguity,
no-reference/insufficient/skip labels, local reload/export/concurrency, original and
post-reveal revisions, separate metric denominators and missing/false detections.
The DOM workflow selects chart candles, confirms stages, reveals actual bound
outputs and hides them again when returning to an earlier cutoff. Test labels are
explicitly synthetic; none is registered as an authentic human review.

The full frontend run initially hit two existing five-second test timeouts while
backend scopes were running concurrently; a complete one-worker run passed all
210. The final focused run also passed all 27 new cases after the last UI changes.
A test-only unsupported matcher was corrected without changing production semantics.

Full .NET build: zero warnings/errors. Frontend production build, ESLint, Prettier,
scoped C# whitespace and `git diff --check` passed. Vite reports a size warning for
the lazily loaded comparison chunk (approximately 1.69 MB / 74 KB gzip); it is not
requested before reveal. Generated JSON remains exact Worker/source bytes and is
excluded from Prettier under the existing convention; SHA256 and byte-equality
tests verify it instead. No dependency or framework changed.

Original labels, June/Replay #2 external report hashes and protected strategy/
dataset paths were checked unchanged. The source/selection manifest reproduces
exactly; all three report assets reproduce through the existing Worker and match
their sealed SHA256 values. Relative documentation links were checked. An owned
temporary Vite on 127.0.0.1:5193 returned HTTP 200 for the page and exact hashed
review source, then was stopped without touching existing processes. No browser
automation or visual acceptance is claimed.

Re-run frontend validation from `src/frontend/moneyway-web`:

```powershell
npm run test:run -- --maxWorkers=1
npm run build
npm run lint
npm run format:check
```

From repository root:

```powershell
dotnet build MoneyWay.sln
dotnet test tests/integration/MoneyWay.IntegrationTests --filter FullyQualifiedName~NasdaqSessionLiquidityResearchCommandTests
git diff --check
```
