# Experimental Nasdaq H4 context candidate V1

`EXP-NQ-H4-CONTEXT-V1` / `1.0.0` combines experimental Breakout, Fakeout and
Wickfill observations under [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md).
It builds on [H4 Market Structure Candidate V1](nasdaq-h4-market-structure-candidate-v1.md).
Every observation carries `CanonicalAdmissionPermitted=false` and
`OperationalSignal=false`. Evidence is nominally observable documentary data.
The audited strategy catalog still requires human validation of H4 context.
[Nasdaq rule catalog](../strategies/nasdaq/rule-catalog.md)

## Research foundations

- Prior highs and lows are examples of support and resistance references.
  [CME Group](https://www.cmegroup.com/education/courses/trading-and-analysis/support-and-resistance)
- A false breakout describes a move beyond support or resistance that returns
  rather than sustaining the move. [IG](https://www.ig.com/en/trading-strategies/what-is-a-false-breakout-and-how-can-you-avoid-it--230130)
- Wick fill articles discuss the area outside the body and price revisiting that
  area. [Angel One](https://www.angelone.in/knowledge-center/online-share-trading/wick-fill-trading-strategy) · [Motilal Oswal](https://www.motilaloswal.com/learning-centre/2023/8/wick-fill-trading-strategy-how-to-apply)

These publications establish conceptual background. Latest-pivot selection,
strict inequalities, confirmation rules, horizons and wick geometry are MoneyWay
research definitions specified for this experiment. Validation of the exact
algorithm and assessment of profitability remain separate research questions.

## Evidence rules

- Each closed candle uses the latest confirmed High and Low as of its open.
  Confirmation at exactly the open is eligible. Each side is evaluated independently.
  Pivot identity, price, extreme open and confirmation time remain attached.
- At an upper level, a prior close and current open at or below the level plus a
  strict high penetration and close above give `BullishBreakout`. A close below
  after the same penetration gives `UpperImmediateRejection`. The lower side
  mirrors the comparisons and yields `BearishBreakout` or
  `LowerImmediateRejection`. An exact close at the level is a boundary.
  Continued closes beyond the reference cannot duplicate a crossing. A fresh
  qualifying recross after a return may start a separate breakout record.
- If prior close is on the body side of the reference or equal and current open
  is strictly beyond, `OpeningGapBeyond` takes precedence over the summary
  label. Independent flags retain the range reaching the level, strict
  penetration, closing side and closing equality. Both level interactions
  can be retained on the same candle. OHLC cannot establish their order.
- A confirmed breakout starts a separate timeline for each explicitly supplied
  horizon of one or two subsequent closed H4 candles. It freezes the originating
  pivot. Subsequent close strictly below a bullish reference or above a bearish
  reference invalidates it exactly at that close, even on the final horizon bar.
  An equal close counts toward the horizon without invalidation. Earlier pending
  records remain in the timeline. A completed horizon records only what was seen
  during that horizon.
- The previous closed H4 candle supplies both wick intervals. Upper base is
  `max(open, close)` and extreme is High. Lower base is `min(open, close)` and
  extreme is Low. Zero length is `NotApplicable`. For an open on the body side
  or base, reach is the relevant candle extreme's distance from the base,
  clamped to zero and wick length. The ratio is reach divided by length.
  The candle range determines no reach, touch, partial reach or full reach.
  An open strictly inside or beyond the wick makes base-origin reach and ratio
  unavailable. The report retains range intersection and boundary coverage under
  `GapAmbiguous`. Full range coverage is geometric and cannot prove that every
  intervening price traded.
- Pivot neighborhoods stay within contiguous source segments. An unexplained gap
  preserves earlier history, ends pending follow-ups as `EvidenceLimited` and
  requires fresh structural references in the new segment. A missing full
  terminal interval limits pending tracks at its nominal closing boundary. A
  shorter internal discontinuity becomes reportable when the next bar closes.
  An explicitly documented market closure preserves known references and the
  pending count, with no synthetic bars. Closure evidence must be available at
  the supplied cutoff. The C# use case accepts it via the existing request.
- The report includes chronological evidence and a separate latest closed candle
  with its age at the cutoff. Parallel age arrays follow the order of their
  corresponding histories. Latest means last in the supplied visible source.
  The research candidate assigns no freshness threshold or context priority.

## Run the preparation snapshot

The isolated Worker command reuses the original normalized CSV input, SHA256
verification and the explicit `AsOfUtc` contract. It writes the existing
`observations.json` and adds `context-observations.json` when a horizon is
supplied. Each pair of structural profile and horizon is independent. Start
the command from the repository root for the previously exposed June 29 package.
The cutoff is 08:00 America/Bogota (13:00 UTC).

```powershell
$inputPath = 'C:\MoneyWay\session\normalized\2026-06-29\4H.csv'
$hash = 'feeabe95702d92ba464a224b40275c34f4935a8d32584835471f20859839d34d'
$output = Join-Path $env:TEMP ('moneyway-h4-context-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- h4-structure-research --input $inputPath --sha256 $hash --requested-start-utc 2026-06-28T21:00:00Z --cutoff-utc 2026-06-29T13:00:00Z --provenance nasdaq-standard-2026-06-29-documentary-v1 --output $output --profile EXP-NQ-H4-STRICT-1-1-V1:1:1 --profile EXP-NQ-H4-STRICT-2-2-V1:2:2 --profile EXP-NQ-H4-STRICT-3-3-V1:3:3 --context-horizon 1 --context-horizon 2
```

The CLI uses unexplained-gap diagnostics when the CSV has discontinuities. It
requires neither a running API nor PostgreSQL. A normalized original H4 export
from a new versioned import can be supplied with its recorded hash and provenance.
The calculation uses the explicit cutoff as in `StrategyReplayContext.AsOfUtc`.
It runs in Application research with `CandleSeries` and the existing structural
use case. The Worker performs CSV ingestion and report publication.

## Development result

All six configurations analyzed the same four closed candles at preparation.
`H1` and `H2` below refer to the number of subsequent H4 closes.

| Structural profile | Horizon | Eligible sides | Breakout | Immediate Fakeout | Delayed Fakeout | Candles reaching both wicks | Structural limitation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1/1 | 1 | 0 | 0 | 0 | 0 | 1 | Insufficient confirmed pivots |
| 1/1 | 2 | 0 | 0 | 0 | 0 | 1 | Insufficient confirmed pivots |
| 2/2 | 1 | 0 | 0 | 0 | 0 | 1 | Insufficient history |
| 2/2 | 2 | 0 | 0 | 0 | 0 | 1 | Insufficient history |
| 3/3 | 1 | 0 | 0 | 0 | 0 | 1 | Insufficient history |
| 3/3 | 2 | 0 | 0 | 0 | 0 | 1 | Insufficient history |

Each configuration reported two full wick reaches, one partial, two not reached,
one gap ambiguous and two insufficient observations on the first candle.
The simultaneous case opened at 05:00 UTC on June 29 and reached the upper wick
fully and the lower wick partially. There was no overlap with a structural event
in this sample. Synthetic fixtures exercise such overlaps. The structural
reason codes were the only difference between configurations. With zero eligible
levels, the sample cannot distinguish horizon behavior or rank configurations.

## Validation scope

The fixtures verify both sides, immediate and delayed rejection, exact
confirmation and close boundaries, horizon independence, overlapping wick and
level evidence, gaps, and future append and mutation invariance. Development
uses the previously exposed June 29 preparation slice. The frozen June 22
review, June 24/26 protected candidates, June 25 Validation and June 30 HeldOut
retain their roles. Further evaluation needs more original H4 observations with
documented UTC normalization and independently reviewed reference labels. Any
strategy promotion requires human approval under ADR 0027.
