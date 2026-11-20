# Nasdaq explicit-session candidate

Version `nasdaq-0.3.0-session-candidate`, derived from published inactive
[`nasdaq-0.2.0-sc-candidate`](m5-strict-close-candidate.md). Candidate only, opt-in,
not registered in default catalogs and not activated. The [ADR 0028](../../decisions/0028-define-candidate-nasdaq-session-windows.md)
records direct user configuration/provenance and the immutable historical boundary.

## Exact session contract

America/Bogota operating date D owns both windows. Asia starts on the preceding
calendar date, even across month/year/leap boundaries. Intervals are half-open:

| Session | Bogotá start | Bogotá end | UTC example for D=2026-06-23 | Last H1 open | Last M30 open |
| --- | --- | --- | --- | --- | --- |
| Asia | D-1 17:00:00 | D 02:00:00 | [2026-06-22 22:00Z, 2026-06-23 07:00Z) | D 01:00 | D 01:30 |
| London | D 02:00:00 | D 08:00:00 | [2026-06-23 07:00Z, 2026-06-23 13:00Z) | D 07:00 | D 07:30 |

For each eligible source candle: Open >= start, Open < end, Close <= end and
Close <= evaluation cutoff. Exact duration and complete continuous coverage are
required. No boundary crossing or shared-boundary double count is allowed.
Expected complete Asia/London counts: H1 9/6, M30 18/12, M5 108/72, M1 540/360.
Counts alone do not authenticate availability. No synthetic candles or market
schedule explanation is inferred for missing source rows.

AH/AL are max High/min Low of Asia; LH/LL likewise for London. Exact decimal
comparisons retain all tied source candles. Incomplete or unfinished windows
cannot produce completed extrema. Source identity, strategy version, operating
date, interval, coverage, actual candles and evaluation availability accompany
the result. A finalized research value is documentary, not causally admitted.

## Existing source and strategy contracts

The audited [baseline specification](strategy-specification.md) explicitly uses
completed 1H candles for session extrema. Canonical candidate evaluation keeps
exact 1H and never falls back to M30/M5/M1. An explicit documentary comparison on
another supported timeframe keeps its lineage and cannot authorize NQ-LIQ-001.

The candidate inherits strict-close SC unchanged. NQ-LIQ-001 requiredness, order,
status and all prerequisites remain unchanged. NQ-LIQ-002 relevance/structural
selection and NQ-LIQ-003 take evidence remain independent human boundaries.
No swing research variant is promoted. Standard/WF2, H4, optional FVG, M1, SL/TP,
risk and execution mechanics are unchanged.

Preparation 08:00 and operational start 08:30 are preserved. Human-confirmed
11:30 versus runtime [08:30,11:00) remains an explicit unresolved discrepancy.
No ACTIVE/published candidate is edited to adopt the new session windows.

## Implementation and canonical demonstration

`MoneyWayNasdaqSessionCandidate` explicitly composes the existing synchronized
multi-timeframe use case, definition, exact-version evaluator registry, workflow,
lifecycle and safe diagnostics. It is absent from default catalogs. The parent
SC definition and its registrations are unchanged. Required evaluator gap remains
zero. `NasdaqSessionLiquidityCalculator` reuses its existing timezone conversion,
session-date convention and extreme-calculation primitive; it dispatches only
the new candidate to the explicit-window path. Legacy London still ends 07:00.

`CalculateExplicitSessions` exposes source-bound immutable `NasdaqSessionReference`
records with interval, coverage, missing openings, exact values and every tied
source candle. Canonical use requires exact H1 from `StrategyReplayContext`;
other source timeframes remain explicit non-authoritative documentary comparisons.
The existing NQ-LIQ-001 evaluator preserves Passed/DataUnavailable and includes
candidate coverage/source evidence. The existing `SessionLevel` upstream binding
retains the exact calculation/context identity and all 15 candidate source candles,
without borrowing the legacy 14-candle interval or recalculating prices.

Actual synthetic canonical tests establish DataUnavailable at 12:00Z (07:00
Bogotá), Passed at 13:00Z (08:00), unchanged strict SC BUY/SELL crossings and
equality rejection, WF2 SELL exclusion, rejection of unsupported WF2 BUY, and zero
Ready results. Appending later data leaves earlier canonical observations equal.
These tests demonstrate software behavior, not authentic causal admission.

## Actual June 23 documentary evidence

The original [V4 context](../../../src/frontend/moneyway-web/src/research/context/data/june23-context.json)
remains byte-identical, SHA256
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`.
It contains 10 H4 / 41 H1 / 498 M5 / 2490 M1 source candles. Known absent source
ranges [2026-06-21 21:00Z,22:00Z) and [2026-06-22 21:00Z,22:00Z) are outside
the two D=2026-06-23 windows; they are not filled or interpreted as a market schedule.

| Reference | Exact price | Source H1 open UTC | Source H1 close UTC | Completed window |
| --- | ---: | --- | --- | --- |
| Asia High | 30463.9 | 2026-06-22 22:00 | 2026-06-22 23:00 | 9/9 H1, ends 2026-06-23 07:00Z |
| Asia Low | 29715.2 | 2026-06-23 06:00 | 2026-06-23 07:00 | 9/9 H1 |
| London High | 29818.6 | 2026-06-23 07:00 | 2026-06-23 08:00 | 6/6 H1, ends 2026-06-23 13:00Z |
| London Low | 29472.7 | 2026-06-23 12:00 | 2026-06-23 13:00 | 6/6 H1 |

All four extrema have one source candle in this H1 dataset. Independently
calculated original M5 and M1 sources give the same prices, with complete
108/72 and 540/360 coverage respectively; they do not replace H1 authority.
There is no authentic M30 series in V4; M30 boundaries/counts and compatible
complete sources are covered by clearly synthetic tests only. Original native
price/provider/instrument identity is retained; no USTEC prices are substituted.

The generated [session report](../../../src/frontend/moneyway-web/src/research/context/data/june23-session-liquidity.json)
records input digest, original H1 raw/normalized provenance, candidate/parent,
configuration source, coverage, all tied identities and explicit evaluated prefixes.
Availability is the supplied evaluated prefix, not a claim of the earliest
provider publication. Future final prices are null before window completion.
Original June mapping (server minus nominal 3h) stays documentary.
`CanonicalAdmissionPermitted=false`, `OperationalSignal=false`.

The prior contextual-research boundary gap is resolved **only for this named
candidate configuration**. Earlier unresolved records remain historical facts.
H4 interpretation, structural/relevant liquidity selection, a valid take, M5
relevant swing selection, IFVG, M1 and other human boundaries remain independent.

## Reproduce and inspect

From repository root after `dotnet build MoneyWay.sln`:

```powershell
$source=(Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination=Join-Path $env:TEMP ('moneyway-session-liquidity-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination
```

The existing Worker publishes `session-liquidity.json` only in a new directory.
It never writes raw inputs, invokes canonical admission or starts a background
service. Tests reproduce the committed report byte-for-byte, preserve the source
hash and reject mismatched provider/instrument, duplicate candles, invalid OHLC,
authority flags and overwrite attempts. No Validation/HeldOut data is evaluated.

Frontend: from `src/frontend/moneyway-web`, run `npm run dev`, then open
`http://localhost:5173/#strategy-lab-context` (use the actual Vite port if different).
No API, MT5 or Docker is required. The existing route displays candidate intervals,
H1 extrema, reference formation and evaluated availability, coverage and no-admission
limitations; chart overlays use backend-produced prices. TypeScript does not
calculate session extrema. The loader checks actual fetched context bytes against
the report's input hash and fails closed if they differ.

Manual visual acceptance: select cutoff 01:59 Bogotá (06:59Z), then 02:00,
07:59 and 08:00. Expect neither final reference, then Asia only, Asia only,
then both respectively. Move backward and verify later London prices/overlays
disappear. Switch H4/H1/M5/M1 without changing the cutoff. Original human M5
labels remain hidden at 08:00 and return only at their original later cutoffs.
The 11:00/11:30 discrepancy must remain visible. This feature has HTTP/component
verification; no new browser visual acceptance is claimed.

Subsequent human-reported acceptance, supplied with task
`67059360-6f02-4d85-ba08-62b73a7a7d3b` on 2026-11-19: all four session-reference
checks passed (01:59 neither final session visible; 02:00 Asia visible; 07:59 Asia
visible with final London hidden; 08:00 AH/AL/LH/LL visible). This is documentary
visual acceptance reported by the user, not browser automation or authenticated
causal admission. The separate [London–Asia research analysis](../../research/london-asia-interaction-v1.md)
adds no operational rule or change to this published candidate.

Next smallest feature: deterministic documentary analysis of London's interaction
with these Asia levels, tied to explicit observation intervals and subsequent
setup context. It requires a bounded, versioned interaction definition before
assigning relevance or calling contact a valid take; no heuristic is inferred here.

## Executed validation, 2026-11-19

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application Nasdaq/replay/structural/body scopes | 2272 | 0 / 0 |
| Integration Nasdaq and M5 command/viewer/dataset scopes | 380 | 0 / 0 |
| Explicit authentic June/Replay #2 and prospective-correction regressions | 6 | 0 / 0 |
| Backtesting Nasdaq/Replay/MultiTimeframe | 35 | 0 / 0 |
| Domain Replay/StrategyVersion | 26 | 0 / 0 |
| Relevant backend total | 2719 | 0 / 0 |
| Complete frontend, 13 files | 166 | 0 / 0 |

This feature adds 23 unit cases, 12 integration cases and 14 frontend cases.
Focused runs overlap these totals and are not counted twice. The six opt-in
authentic regressions use the existing local `MONEYWAY_MENTOR_INPUT_SOURCE`.
Initial test setup errors (missing integration imports and an unsupported BUY WF2
fixture) were corrected without widening the existing SELL-only WF2 contract.
Final runs above are green.

Full .NET build: zero warnings/errors. Frontend build, ESLint, Prettier,
scoped C# whitespace, relative documentation links and `git diff --check` passed.
Local Vite on owned port 5182 returned HTTP 200 for page, context module and real
session report; that temporary process was stopped. This is not browser visual
acceptance. Original source/review/swing reports, published definitions and
external June/Replay #2 report hashes remain unchanged.

Reproduce focused tests from repository root:

```powershell
dotnet test tests/unit/MoneyWay.Application.UnitTests --filter FullyQualifiedName~NasdaqExplicitSessionTests
dotnet test tests/integration/MoneyWay.IntegrationTests --filter 'FullyQualifiedName~NasdaqExplicitSessionCandidateTests|FullyQualifiedName~NasdaqSessionLiquidityResearchCommandTests'
```

From the frontend directory: `npm run test:run`, `npm run build`, `npm run lint`
and `npm run format:check`.
