# Nasdaq M5 structural Stop Loss reference V1

`EXP-NQ-M5-STRUCTURAL-SL-V1` / `1.0.0` extends the isolated M5 → M1 research
pipeline. The confirmed concept references the lowest wick of the relevant M5
Higher Low for BUY and the highest wick of the relevant M5 Lower High for SELL.
The pivot algorithm, profiles and selection policy below are MoneyWay research
choices. The executable price policy remains unresolved and both canonical
admission and operational signal flags are false.
[Audited Stop Loss specification](../strategies/nasdaq/strategy-specification.md#15-stop-loss) · [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)

## Research foundation

- CME describes a stop level as a logical invalidation point and explains why
  the distance must be known before monetary risk can be computed.
  [CME Group, Proper Position Size](https://www.cmegroup.com/education/courses/trade-and-risk-management/proper-position-size)
- Schwab describes higher and lower swings and gives examples of protective
  placement on the other side of support or resistance.
  [Charles Schwab, The Ins and Outs of a Swing Trade](https://www.schwab.com/learn/story/ins-and-outs-swing-trade)
- IG illustrates stops below swing lows for longs and above swing highs for
  shorts and defines risk distance geometrically in its examples.
  [IG Academy, Developing a Swing Trading Strategy](https://www.ig.com/ae/ig-academy/charting-essentials/developing-a-swing-trading-strategy)

The sources provide general protective geometry and distance concepts. Nasdaq
M5 pivot definition, neighborhood size, selection and executable offset remain
MoneyWay-specific questions.

## Evaluation contract

- The report preserves each completed M5 configuration and M1 profile and uses
  the earliest continuous in-window realignment defined by the upstream M1
  candidate. Tied earliest observations retain their identities. Pending,
  expired or incomplete M1 scans retain upstream reasons. Each FVG/trigger
  binding references the unchanged companion reports, which retain later
  attempts and zone invalidation times. FVG/IFVG validity at the realignment
  is reported per binding, independently of structural geometry.
  [M1 candidate](nasdaq-m1-realignment-candidate-v1.md) · [11:30 research window](nasdaq-trading-window-1130-alignment-v1.md)
- `CandleSeries` provides chronological, homogeneous, non-overlapping OHLC.
  The shared strict pivot kernel also serves H4, whose coverage and classification
  policies are preserved. The M5 evaluator accepts nominal UTC grid-aligned 5M
  candles only through the exact close of the realignment M1 candle, following
  the inclusive replay `AsOfUtc` availability contract. The original dataset
  remains retrospective documentary evidence with unauthenticated publication.
- Initial `LeftBars/RightBars` profiles are 1/1, 2/2 and 3/3. A swing high
  requires its High to exceed all neighboring Highs. A swing low requires its
  Low to be below all neighboring Lows. Each pivot becomes observable at the
  close of its final right candle. A candle can emit both kinds at the same
  time, flagged without assuming an intrabar order. Equal neighbors fail strict
  detection. Equal confirmed pivots remain `EqualHigh` or `EqualLow`.
- Highs and lows are classified independently by confirmation time. A low is an
  HL only when it exceeds its immediate preceding confirmed low. A high is an
  LH only when below its immediate preceding confirmed high. A first pivot has
  insufficient predecessor evidence. Same-side ties preserve the candidates
  and predecessor set as ambiguous. A valid single `CandleSeries` supplies at
  most one pivot per kind and confirmation time for a given profile.
- An unexplained interval breaks the series for detection and predecessor
  classification. The observation reports visible gaps and evaluates only the
  contiguous suffix through the cutoff. Missing expected trailing intervals
  produce `MissingSourceEvidence`. The first supplied M5 open bounds the scan.
  Leading coverage metadata outside the supplied series stays in source
  provenance. No structural age limit is applied within the covered segment.
- `LatestConfirmedStructuralReference` selects the latest confirmed HL for
  bullish sequences and the latest confirmed LH for bearish sequences.
  `M1RealignmentCloseReference` contains the evidence candle and its close.
  BUY distance is close minus HL wick Low. SELL distance is LH wick High
  minus close. The result stores only strictly positive decimal distances, in
  raw price points. An equal or incorrect-side level yields
  `ReferenceNotProtective` and blocks fallback to older pivots.
- Evaluations are shared by M1 close timestamp, direction and pivot profile.
  Associations retain the episode, upstream configurations, M5 trigger,
  directional FVG, M1 realignment and research window identity. The calculation
  is stateless and consumes only evidence observable at each M1 cutoff.

## June 23, 2026 Development

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-structural-sl-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-structural-sl --cutoff-utc 2026-06-23T16:30:00Z
```

The command writes `contextual-m5-structural-sl.json` plus the two companion
reports into a new directory. The input SHA-256 was
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`. The report
preserves context source metadata including the original M5 raw and normalized
hashes. June 22, 24 and 26 review episodes, June 25 Validation and June 30
HeldOut were excluded from the evaluation.

Six research episodes and four M5 anchors contributed 240 M5/M1 combinations
and 720 pivot-profile combinations. These shared five distinct bullish M1
realignment closes and 15 structural evaluations. Forty-eight of the 720
associations had an already invalidated M5 zone in every bound branch at the
M1 cutoff. Their geometric observations remain auditable alongside invalidation.

| M5 profile | Positive HL distances (points), sorted | Selected HL confirmation age for positive distances (minutes) | Nonprotective | Insufficient pivots | Ambiguous |
| --- | --- | --- | ---: | ---: | ---: |
| 1/1 | 173.0, 243.5, 285.5 | 3, 12, 22 | 2 | 0 | 0 |
| 2/2 | 96.5, 167.0, 209.0 | 133, 142, 152 | 2 | 0 | 0 |
| 3/3 | 96.5, 167.0, 209.0 | 128, 137, 147 | 2 | 0 | 0 |

No LH evaluation occurred in the exposed session. Bearish behavior is covered
by mirrored fixtures. The six nonprotective observations were shared across the
13:30 and 13:32 UTC anchors. Profiles 2/2 and 3/3 selected the same positive
levels, with the latter observing confirmation five minutes later. At 13:58 UTC
(08:58 Bogotá), all profiles used only M5 data closed through 13:55 UTC.
All 15 evaluations saw the June 22 21:00–22:00 UTC source gap. All had
sufficient contiguous post-gap evidence, so zero outcomes were blocked by the
gap. Fixtures exercise missing trailing intervals and predecessor reset.

One exposed session supplies development evidence. Independent review of
labels, admissible historical evidence and human approval remain necessary
before promotion. The output can serve as an input to separate structural TP
research at the retained causal cutoff. It supplies neither a validated TP nor
an approved executable SL. Reproducing this run requires no manual labels.
