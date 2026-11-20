# Experimental Nasdaq H4 market structure candidate V1

`EXP-NQ-H4-MARKET-STRUCTURE-V1` / `1.0.0` is isolated MoneyWay research under
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md).
It is neither a mentor-confirmed rule nor an approved operational candidate.
`CanonicalAdmissionPermitted=false` and `OperationalSignal=false` for every
observation. No ACTIVE strategy, M5 hypothesis or canonical evaluator is changed.

## Research sources and definition

[StockCharts' Dow Theory explanation](https://chartschool.stockcharts.com/table-of-contents/market-analysis/dow-theory)
describes rising and falling peaks and troughs. This candidate compares confirmed
highs and lows independently; that simplification is **not** a full Dow Theory
implementation. [TradingView's ZigZag documentation](https://www.tradingview.com/support/solutions/43000591664-zigzag-indicator/)
describes pivot confirmation using neighboring bars. Its right-side equality,
alternation, same-direction replacement and price-deviation rules are **not**
adopted here. Strict inequality on both sides, no alternation and no replacement
are explicit MoneyWay research definitions. The
[Directional Change research paper](https://link.springer.com/article/10.1007/s10462-022-10307-0)
is an alternative event-based methodology for a future experiment; no Directional
Change algorithm is implemented here. None of these sources establishes H4 Nasdaq
parameters, independent agreement, profitability or operational authorization.

For each original H4 candle, a High pivot requires its High to exceed **every**
High among the configured `LeftBars` and `RightBars`. A Low pivot requires its Low
to be below **every** corresponding Low. Both parameters are positive integers.
Confirmation occurs at the nominal close of the last required right candle; the
extreme candle's own close is recorded separately. Exact ties produce no strict
pivot. A candle may produce both kinds, retained separately without intrabar
ordering. Candidate IDs include profile, kind and extreme open UTC.

The initial explicit sensitivity profiles are:

| ID | LeftBars | RightBars |
| --- | ---: | ---: |
| `EXP-NQ-H4-STRICT-1-1-V1` | 1 | 1 |
| `EXP-NQ-H4-STRICT-2-2-V1` | 2 | 2 |
| `EXP-NQ-H4-STRICT-3-3-V1` | 3 | 3 |

They are engineering-selected experiments, without an implicit default or
selected winner. Consecutive confirmed Highs independently give `HigherHigh`,
`LowerHigh` or `EqualHigh`; confirmed Lows give `HigherLow`, `LowerLow` or
`EqualLow`. Fewer than two of a kind give `InsufficientEvidence`. HigherHigh plus
HigherLow is `Bullish`; LowerHigh plus LowerLow is `Bearish`; opposite directional
relations are `Mixed`. Equality or simultaneous latest High/Low extrema yield
`Indeterminate`. Missing paired evidence yields `InsufficientEvidence`. Decimal
comparison is exact; no tolerance, swing replacement, prominence, displacement or
maximum-age policy is added.

The pure `EvaluateH4MarketStructureCandidateUseCase` consumes `CandleSeries`,
requested UTC start, `AsOfUtc`, one explicit profile, provenance and optionally
documented closure intervals with evidence references. It uses only original US100
H4 candles nominally closed at or before `AsOfUtc`. An unclassified internal gap
blocks classification as `MissingSourceIntervals`; a missing leading range is
`InsufficientHistory`. An uncovered tail of at least four hours after the last
visible close also blocks classification. A shorter tail may be an unclosed H4
interval. An explicitly documented closure is retained as such and
no pivot neighborhood crosses it. No calendar or DST rule is inferred. Candle
close is only nominal observability, **not** authenticated historical publication.

## One-shot research report

The Worker command reads a bounded canonical H4 CSV from the existing market-data
import contract, verifies its SHA256, and writes `observations.json` in a new
directory. It does not start the Worker host or require API/PostgreSQL. Example
using the already exposed June 29 source and all three explicit profiles:

```powershell
$inputPath = 'C:\MoneyWay\session\normalized\2026-06-29\4H.csv'
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $inputPath).Hash.ToLowerInvariant()
$output = Join-Path $env:TEMP ('moneyway-h4-research-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- h4-structure-research --input $inputPath --sha256 $hash --requested-start-utc 2026-06-28T21:00:00Z --cutoff-utc 2026-06-29T13:00:00Z --provenance nasdaq-standard-2026-06-29-documentary-v1 --output $output --profile EXP-NQ-H4-STRICT-1-1-V1:1:1 --profile EXP-NQ-H4-STRICT-2-2-V1:2:2 --profile EXP-NQ-H4-STRICT-3-3-V1:3:3
```

The command has no implicit profile or trusted strategy output. The pure use case
also accepts documented closures with exact UTC bounds and evidence; this CLI
does not yet accept closure evidence, so its source gaps remain unresolved.
The input hash identifies bytes, not historical availability. For a newly
imported source, pass its `normalized.csv` and its manifest identity as provenance.

## Bounded development observation

At June 29 13:00Z (08:00 Bogotá), the previously exposed June documentary source
has four closed H4 candles in the requested 16-hour interval. A single run of the
three profiles returned:

| Profile | Closed H4 | Confirmed High / Low | Confirmation delay | Classification | Reason |
| --- | ---: | ---: | --- | --- | --- |
| 1/1 | 4 | 0 / 0 | Not available | InsufficientEvidence | InsufficientConfirmedPivots |
| 2/2 | 4 | 0 / 0 | Not available | InsufficientEvidence | InsufficientHistory |
| 3/3 | 4 | 0 / 0 | Not available | InsufficientEvidence | InsufficientHistory |

Across these three profile observations, `InsufficientEvidence` occurred 3/3;
Bullish, Bearish, Mixed and Indeterminate each occurred 0/3. The only observed
profile difference was the reason code. No profile was tuned or selected.
These four candles cannot establish classification availability, distribution,
agreement, false positives, missed pivots or profitability. The synthetic unit
fixtures demonstrate Bullish, Bearish, Mixed and Indeterminate mechanics only.
The frozen June 22 review, June 24/26 protected candidates, June 25 Validation
and June 30 HeldOut were not evaluated for candidate tuning.

Next research needs more original H4 history, independently reviewed structural
references, chronological development/validation separation and an untouched
final set. Human approval and the ADR 0027 promotion requirements remain necessary
before any strategy activation.
