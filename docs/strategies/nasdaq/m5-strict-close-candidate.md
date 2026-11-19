# Nasdaq Standard M5 strict-close candidate

Version: `nasdaq-0.2.0-sc-candidate`. Status: candidate, not activated, excluded
from default strategy/evaluator catalogs. Baseline `nasdaq-0.1.0-draft` remains
unchanged. Candidate promotion requires independent backtesting, human approval
and separate activation; this feature performs none of those activation actions.

## Source and historical distinction

The user submitted the strategy clarification on 2026-11-19 in the task
"MONEYWAY — NASDAQ DEMO V0: DETERMINISTIC M5 STRUCTURAL CHANGE DETECTOR",
attachment ID `5678233d-bd46-421e-80ed-7da19af43277` (external conversation source;
no repository media bytes or fabricated source hash).
It is user-supplied secondary review of mentor material, not independently
verified original audio/video. The review reports wick rejection around 03:21;
around 22:24–22:35, on XAUUSD H1, "Hizo un break chiquito, pero lo hizo. Entonces
este es mi nuevo punto estructural." The user explicitly confirms the strict-close
criterion for Nasdaq M5. September 8 approximately 08:48–08:53 distinguishes
Standard from WF2; approximately 15:36 and 16:28–17:08 support the reported
close-based management interpretation. These are retained provenance statements,
not independent verification or permission to change Forex/other timeframes.

The baseline specification's strong/decisive human classification remains in
history. Only this candidate replaces that additional SC classification.

## Exact scope

Given an independently validated relevant M5 swing, Buy requires closed M5
`Close > SwingHigh.Price`; Sell requires `Close < SwingLow.Price`. Exact equality,
wick-only penetration and an unfinished candle do not qualify. A marginal exact
decimal crossing suffices. There is no penetration threshold, body-color or
opening-cross requirement, size/ATR/volume filter, tolerance or inferred pivot.
The existing structural body-close primitive owns the numeric comparison.

The reference must identify its exact decisive liquidity take, selected M5 source
candles, native instrument price, direction, validation effective/available times
and provenance. The candidate consumes only source-resolved references available
at the current closed M5 boundary after canonical prerequisites. It never scans
earlier candles using a later selection. Unvalidated/missing/conflicting reference
selection remains human-validation-required; unresolved source candles remain
data-unavailable. Conflicting selections do not use latest-wins. Reference authority
is scoped to the exact current decisive take: replacement takes cannot borrow it.

Computed SC events retain deterministic derivation provenance explicitly; they are
not represented as a fresh human SC approval. They enter the existing first-trigger
selector alongside existing human IFVG observations. Earlier confirmations remain
first. Their derived availability is the evaluation frame's `AsOfUtc`, after
source resolution; it is not an attestation of native UTC or provider publication.
Unresolved equal-time alternatives conflict. A nonqualifying closed candle
waits, not a failed session. No unrelated FVG/M1 rule is approved, and no order or
Ready verdict is created. Same-candle take/trigger order is not inferred from OHLC;
the automatic path requires the canonical take stage already established before
the evaluated frame. Human same-candle evidence retains its existing contract.
Existing human SC observations remain usable only when their candle also satisfies
the candidate's strict comparison. They are optional compatible evidence, not a
requirement for detection. Equal/wick-only claims cannot override this candidate.
Computed conflict evidence survives later frames, so an SC/IFVG tie cannot resolve
merely because a new candle arrived. Retained evidence is deduplicated rather than
growing copies of earlier frame facts.

Human swing selection, H4 classification, liquidity relevance, IFVG geometry,
optional continuation FVG, M1 geometry, SL/TP and risk stay independent. WF2 is
excluded by existing setup applicability. The 11:00/11:30 Bogota conflict and all
temporal-admission guarantees remain unchanged. Synthetic fixtures demonstrate
software behavior; June 29 remains documentary-only and no authentic candidate
backtest or activation is claimed.

## Explicit canonical research composition

`MoneyWayNasdaqM5StrictCloseCandidate.CreateBacktest()` composes the existing
canonical use case with the candidate definition, exact-version evaluators,
unchanged workflow dependencies and the same lifecycle policy. No single-timeframe
pipeline, alternative verdict calculator or activation service is introduced.
Existing evaluator constructors continue to bind to the baseline; internal
exact-version constructors enable this explicit candidate composition. All other
rule logic is shared. The default definition/evaluator catalogs and API selection
remain unchanged. Repository contracts do not currently implement a persistent
candidate approval/activation service; this composition adds no shortcut to it.

Synthetic comparison tests execute baseline and candidate on identical OHLC and
semantically matching version-bound inputs. Baseline still needs an SC review;
the candidate needs only the selected validated swing. Neither result establishes
real source admission or strategy effectiveness. The required-evaluator gap stays
zero: this feature removes a classification dependency, not a registration gap.

## Executed synthetic acceptance

The real `GenerateCanonicalMultiTimeframeBacktestUseCase` produced these outcomes
on identical baseline/candidate OHLC, with a selected validated swing price 100.
All prices are synthetic. BUY fixtures deliberately need no bullish candle color
or opening cross; SELL fixtures likewise need no bearish candle color.

| Direction | Closed M5 price | Baseline NQ-M5-001, no SC review | Candidate NQ-M5-001 | Candidate verdict at that frame |
| --- | --- | --- | --- | --- |
| BUY | 99 | HumanValidationRequired | Waiting | Wait |
| BUY | 100 | HumanValidationRequired | Waiting | Wait |
| BUY | 100.01 | HumanValidationRequired | Passed | Wait |
| BUY | 105 | HumanValidationRequired | Passed | Wait |
| SELL | 101 | HumanValidationRequired | Waiting | Wait |
| SELL | 100 | HumanValidationRequired | Waiting | Wait |
| SELL | 99.99 | HumanValidationRequired | Passed | Wait |
| SELL | 95 | HumanValidationRequired | Passed | Wait |

The synthetic evaluation boundary is `2026-10-03T14:10:00Z`, distinct from the
November 19 development/review date. Unit cases also verify exact crossings of
`100.00000000000000000000000001` and `99.99999999999999999999999999`, explicit
wick-only/equality examples, unavailable/wrong/unvalidated references, no active
M5 consumption, compatible support, IFVG ordering/ties retained through later
frames, WF2 exclusion and immutable prefixes. Passing SC establishes the owning
canonical fact and lets M1 become eligible at the next supported frame; it does
not manufacture a pullback, realignment, entry, SL/TP or risk approval.

Final nonoverlapping validation: 2,180 Application Nasdaq/replay/strict-break
tests, 333 Nasdaq integration tests, 35 backtesting/replay tests, 26 Domain
replay/version tests and six opt-in authentic-report preservation tests passed.
Total 2,580 passed, zero failed/skipped. The new feature contributes 29 unit cases
and eight synthetic canonical comparison cases. Earlier focused runs overlap.
The first broad run had 2,174 passed and six failed because an internal helper's
reflection signature had changed. Its original two-argument contract was restored,
with a separately named exact-version method; the final full run passed all 2,180
without changing existing assertions. C# formatting/EOL issues were corrected.
Full .NET build passed with zero warnings/errors. Frontend was unchanged and its
tests were not rerun. No candidate activation or authentic backtest was performed.

Detailed current comparison JSON (actual rule evidence, facts, timestamps,
workflow and verdict) and TRX are retained outside the repository in:

```text
%TEMP%/moneyway-m5-candidate-20261119-de9a16cf99004440909c3546214b8a5e/final-evidence/nasdaq-m5-candidate-comparison.json
%TEMP%/moneyway-m5-candidate-20261119-de9a16cf99004440909c3546214b8a5e/final-evidence/canonical-comparison.trx
```

The post-fix 333-case integration TRX is in the adjacent
`after-compatibility-repair/nasdaq-m5-candidate-regressions.trx`. Reproduce with:

```powershell
dotnet test tests/integration/MoneyWay.IntegrationTests --filter FullyQualifiedName~NasdaqM5CandidateBacktestTests --logger 'trx;LogFileName=canonical-comparison.trx' --results-directory "$env:TEMP/moneyway-m5-new-comparison"
```

June's original report SHA256 remains
`6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8`;
Replay #2 remains
`48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db`;
the documentary manifest remains
`12684556c3a095ead00d1138b41902b9aba1844cf315383ec5ed87c966665155`.
These were independently rechecked, not regenerated as newly admitted data.

The next minimum human specification question is which exact closed-M5 sequence
selects and validates the relevant swing to be broken, including its causal
selection/replacement boundary. No nearest-pivot, lookback or fractal algorithm
is inferred by this feature.
