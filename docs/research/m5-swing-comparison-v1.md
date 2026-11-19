# Strategy Lab: experimental M5 swing comparison V1

Follow-up: [Strategy Lab V2 OHLC viewer](m5-swing-viewer-v2.md) now displays these
unchanged results and an explicitly initialized retrospective documentary episode.
The original V1 execution record below remains historical.

This is isolated MoneyWay hypothesis research authorized on 2026-11-19, not a
mentor-rule correction, operational strategy evaluation or candidate activation.
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)
records the governance decision and future promotion requirements. The published
[strict-close SC candidate](../strategies/nasdaq/m5-strict-close-candidate.md)
and default strategy/evaluator catalogs remain unchanged.

## Four hypotheses

All algorithm versions are `1.0.0`; all classifications are
`ExperimentalMoneyWayHypothesis_NotMentorConfirmed`.

| ID | Ascending impulse: start condition | Descending impulse: start condition |
| --- | --- | --- |
| EXP-M5-SWING-A-V1 | Close < Open | Close > Open |
| EXP-M5-SWING-B-V1 | Low < previous Low | High > previous High |
| EXP-M5-SWING-C-V1 | A AND B on this candle | A AND B on this candle |
| EXP-M5-SWING-D-V1 | A OR B on this candle | A OR B on this candle |

Directional symmetry is an explicit experimental hypothesis. Neither BUY nor
the mirror is represented as independently confirmed original mentor evidence.
Exact doji is neutral for A; exact equality is no violation for B. There is no
body-size, distance, ATR, percentage, volume or tolerance parameter.

## Shared experimental policy

Policy `EXP-M5-BOUNDED-CONTINUATION-V1` isolates one supplied episode:

1. Require an explicit source-bound M5 anchor identity, impulse direction,
   declared availability, provenance and evaluation cutoff. No automatic first
   swing, session start or lookback is selected.
2. Use only supplied candles closed at/before the UTC cutoff. Resolve the anchor
   from that visible source sequence. The fixed threshold is its actual High for
   ascending or Low for descending, never previous Close or a later wick.
3. Start after the anchor, using the first qualifying start for each hypothesis.
   Previous means the preceding supplied closed candle, initially the anchor.
   Source gaps are not filled; data completeness and market schedules are not
   authenticated or inferred.
4. From the start, aggregate minimum Low ascending / maximum High descending
   through every pre-continuation candle. Include the start; retain every candle
   identity tied at the exact aggregate extreme. Do not select a preferred tie.
5. The first strict-close continuation beyond the fixed anchor terminates this
   one episode. Exclude that candle from aggregation. If no start existed, abstain
   rather than skip the break and discover a later cycle. If start and continuation
   coincide before a pullback exists, abstain for unknown intrabar order.
6. Missing continuation leaves a provisional reference, never confirmation. Anchor
   latency uses the actual anchor close; start latency uses start close. With this
   fixed common threshold, confirmed algorithms can share confirmation time while
   disagreeing on start, reference and start-to-confirmation latency. No missing
   latency is converted to zero and no profitability ranking is calculated.
7. Initialization unavailable at cutoff abstains. Availability before anchor close
   is rejected. If post-anchor candles already closed before initialization became
   available, abstain for late initialization instead of retrospectively authorizing
   them or silently inventing a truncated episode. These are explicit research
   safety assumptions, not mentor rules.

Results carry hypothesis/version, series/provider/instrument/timeframe, cutoff,
initialization, declared data origin/provenance, proposed price, all reference
candles, confirmation time, provisional/abstention reasons and pairwise comparison.
Full agreement differs from agreement on confirmed price/time: equal prices can
come from different pullback starts. Identical abstention is not proof of quality.
Output retains visible source OHLC for inspection, including later context rows
that algorithms do not consume as another cycle.

## Architecture and execution

`CompareM5SwingHypothesesUseCase` is pure Application research code. It reuses
`CandleSeries`, `CandleBodyDirectionCalculator`,
`StructuralCandidateExtremeCalculator` and `StructuralBodyCloseBreakCalculator`.
It implements no replay input, rule-fact or evaluator interface and is not wired
into Standard, WF2, `NQ-M5-001` or supervised session actions.

The existing Worker has a one-shot command, with no Worker host, MT5 or API needed:

```powershell
$inputFile=(Resolve-Path 'docs/data/strategy-lab/m5-swing-v1/synthetic-disagreement.json').Path
$reportDir=Join-Path $env:TEMP ('moneyway-m5-lab-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- m5-swing-lab --input $inputFile --output $reportDir
```

It publishes `comparison.json` and `comparison.md` atomically in a **new** output
directory; existing reports are never replaced. The JSON envelope records SHA256
of actual input bytes for reproducibility, not source authentication. This hash
covers the whole file, including any supplied future rows; cutoff-prefix stability
is tested on the pure comparison result, not equality of different source-file
hashes. JSON fixtures are LF-pinned across checkouts.

Input JSON declares `seriesId`, `providerId`, `symbol`, `timeframe: "5m"`,
`direction`, `cutoffUtc`, `dataOrigin` (`Synthetic` or `RetrospectiveDocumentary`),
`provenance`, `initialization` and `candles`. Initialization contains `referenceId`,
`anchorOpenTimeUtc`, `availableAtUtc` and `sourceReference`; it can be null to
inspect abstention. Each candle has UTC open/close times and exact decimal OHLC,
optionally volume. Identity/UTC/OHLC are validated. JSON overrides, duplicates,
network paths and linked files/directories are rejected. The IO boundary limits
input to 8 MiB/10000 candles for resource safety, not a trading lookback or minimum
candle requirement. IO is outside deterministic calculations. Failed publication
can leave an unreferenced pending directory, never a partially published report.

## Data and future promotion

The committed dataset is controlled synthetic OHLC, not independently labelled
mentor behavior or authentic market activity. June's actual M5 exports exist, but
this task supplies no independently chosen real impulse anchor/episode for these
hypotheses. Selecting one automatically would violate the experiment boundary;
actual-market comparison is pending that explicit initialization and episode.
No historical prices, labels or timestamps were invented to fill this gap.

Retrospective documentary research remains unauthenticated and is not an admission
path. `CanonicalAdmissionPermitted=false` and `OperationalSignal=false` remain
explicit even for an experimentally confirmed reference. Current MT5 admission,
June 29's documentary-only package, the 11:00/11:30 discrepancy, Standard/WF2 and
terminal no-reentry behavior are untouched.

Future promotion requires independent reference labels, chronological development
and validation separation, an untouched final dataset, discrepancy/false-positive/
missed-detection/coverage/latency analysis, regime robustness, independently
admitted backtesting where required, human approval and an approved candidate
lifecycle. No numerical pass threshold is invented. Do not optimize repeatedly
against the final evaluation set. Agreement does not establish profitability.

The next focused feature is an OHLC discrepancy viewer over an explicitly supplied
real historical episode, displaying all four start/reference/confirmation markers
and retained ties with these research limitations, without operational promotion.

## Actual synthetic comparison

The one-shot command was executed against the committed synthetic input. The fixed
anchor is the candle opened `2026-11-19T13:00:00Z`, High 110, available at its
13:05 close. The explicit cutoff is 13:30Z. Every row confirms experimentally at
13:30Z; the confirmation candle's Low 80 is excluded.

| Algorithm | Start candle open UTC | Proposed reference | Reference candle open UTC | Anchor-close latency | Pullback-close latency |
| --- | --- | --- | --- | --- | --- |
| A | 13:05 | 90 | 13:10 | 25 minutes | 20 minutes |
| B | 13:10 | 90 | 13:10 | 25 minutes | 15 minutes |
| C | 13:20 | 93 | 13:20 | 25 minutes | 5 minutes |
| D | 13:05 | 90 | 13:10 | 25 minutes | 20 minutes |

A/D agree fully. A/B agree on confirmed reference and confirmation time, but
disagree on start and start-to-confirmation latency. C's stricter start produces
a different reference. Its shorter start latency does not mean an earlier
confirmation or a better algorithm. Mirrored synthetic tests produce A/B/D = 110
and C = 107 with the same confirmation time; this verifies the programmed mirror,
not independent mentor evidence. No algorithm was selected as a winner.

The actual machine-readable and human-readable outputs are retained locally at:

```text
%TEMP%/moneyway-m5-lab-v1-e45d31954f684fdfa75a8da3452afc74/comparison.json
%TEMP%/moneyway-m5-lab-v1-e45d31954f684fdfa75a8da3452afc74/comparison.md
```

The command's integration tests also ran against this exact fixture, independently
checked its input SHA256, compared repeated report bytes and confirmed that
existing output directories cannot be overwritten. Synthetic negative inputs
test retrospective declarations without granting authentication, not actual market
capture. See the command above to generate a fresh report directory.

Input SHA256 for that execution:
`ea3893e15ff17d4965b0359b70bbe3f51e35b387111f9a3f757d3c6f84835019`.

## Executed validation

On 2026-11-19, the relevant suites passed with no failures or skips:

| Suite scope | Passed |
| --- | ---: |
| Application: Nasdaq, StrategyReplay and reused structural/body primitives | 2222 |
| Integration: Nasdaq and M5 lab command, excluding environment-gated local sessions | 341 |
| Six authentic June/Replay #2 report and prospective-correction regressions, explicitly enabled with the existing local input | 6 |
| Backtesting: Nasdaq, Replay and MultiTimeframe | 35 |
| Domain: Replay and StrategyVersion | 26 |
| Total across these scopes | 2630 |

The totals include 22 new hypothesis cases and eight command cases. The eight
command cases were also repeated successfully after the final Markdown escaping
change; repeated executions are not added to the total. Unit coverage includes
strict equality/marginal breaks, wick-only attempts, all tied references,
ascending/descending symmetry, ambiguity, missing/late/invalid initialization,
repeated calculation and identical earlier-cutoff results when future candles
are appended. Integration checks retain the original candidate/catalog and reject
untrusted admission overrides.

The full .NET solution build passed with zero warnings/errors. Scoped C# whitespace
verification passed. No frontend files changed. Existing June and Replay #2
report hashes and the June documentary package hash remained unchanged; research
did not feed any result into their evaluators. Documentation links and staged
`git diff --check` were checked before publication.
