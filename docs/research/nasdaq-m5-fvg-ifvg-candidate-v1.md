# Experimental Nasdaq M5 FVG and IFVG V1

`EXP-NQ-M5-FVG-IFVG-V1` / `1.0.0` implements documentary research.
`CanonicalAdmissionPermitted=false` and `OperationalSignal=false`.
The authorized strategy uses human evidence for Step 4 and a separately selected
FVG for Step 5. The current audited Standard specification treats clean absence
of Step 5 FVG evidence as optional support. Selection, quality review and WF2
scope stay with their existing contracts.
[Nasdaq specification](../strategies/nasdaq/strategy-specification.md) · [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md) · [ADR 0026](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md) · [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)

## Research foundations

| Source | Concept examined |
| --- | --- |
| [LuxAlgo FVG](https://www.luxalgo.com/library/concept/fair-value-gap/) | Three-candle imbalance and non-overlap of the first and third candles |
| [LuxAlgo IFVG](https://www.luxalgo.com/library/indicator/inversion-fair-value-gaps-ifvg/) | An opposite-direction close through the gap can reverse its observed role |
| [TrendSpider FVG Basics](https://trendspider.com/blog/fair-value-gap-basics/) | Three-candle geometry using adjacent wicks |
| [LuxAlgo FVG publication on TradingView](https://www.tradingview.com/script/jWY4Uiez-Fair-Value-Gap-LuxAlgo/) | The published criteria give the C1/C3 wick inequalities and a middle close check. The publication also uses a size threshold. |
| [LuxAlgo IFVG publication on TradingView](https://www.tradingview.com/script/8FHucjY6-Inversion-Fair-Value-Gaps-IFVG-LuxAlgo/) | Opposite-direction close as an inversion concept. Its display and retest policies are specific to that indicator. |

The geometry, two explicit profiles and experimental lifecycle below are an
original C# implementation. The cited sources provide conceptual background.
Profitability, Nasdaq parameter suitability and strategy compatibility require
separate validation. Candle geometry alone cannot establish whether trades
executed within the interval.

## Candidate rules

- With three contiguous closed M5 candles in chronological order, a bullish FVG
  requires `C3.Low > C1.High` and spans `[C1.High, C3.Low]`. A bearish FVG
  requires `C3.High < C1.Low` and spans `[C3.High, C1.Low]`. Width is upper
  minus lower. Equality yields no zone. Formation is visible at C3 nominal close.
- `GeometryOnly` records any strict zone and records whether C2 confirmed it.
  `MiddleCloseConfirmation` also requires `C2.Close > C1.High` bullish or
  `C2.Close < C1.Low` bearish. There is no profile default, minimum width,
  price tolerance or volatility, body or volume filter.
- Zone identity includes candidate, provider, instrument, profile, direction
  and C1 open. Exact contiguous M5 duration fixes the C2 and C3 opens from C1.
  The report carries all three source candles, fixed boundaries, opening
  discontinuities, formation time, provenance and snapshot cutoff. Overlapping
  and nested zones retain separate identities.
- The near boundary is upper for bullish FVGs and lower for bearish FVGs.
  With an open on the near side or near boundary, reach is the distance from
  near to the relevant candle extreme, clamped to the zone width. A range that
  misses, touches near only, enters partly or includes both bounds yields
  `NotReached`, `BoundaryTouched`, `PartiallyReached` or `FullyReached`.
  The reach ratio is clamped reach divided by width. If the candle opens inside,
  at far or beyond far, the report uses `GapAmbiguous` and leaves the near-origin
  reach and ratio empty. Geometric range intersection, contact, opening and
  closing positions, and signed change from the prior close remain visible.
  `OpeningGapUncertainty` specifically marks a discontinuous open away from
  the near side. Even a full geometric span cannot reconstruct intrabar order.
- An active bullish FVG inverts bearish on the first subsequent close strictly
  below the lower bound. An active bearish FVG inverts bullish on a subsequent
  close strictly above the upper bound. Equality, wick penetration and full
  reach alone do not invert. An opening discontinuity can accompany a valid
  close-based inversion. It remains separate evidence about the price path.
  A later close strictly beyond the opposite bound invalidates the inversion.
  Invalidation terminates that track. Full reach does not terminate a track,
  and there is no expiry, session reset, retest prerequisite or repeated flip.
- All events appear at their evidence close. A source discontinuity never
  forms a three-candle window. For an unexplained gap, earlier observations
  survive and incomplete tracks end with `InsufficientEvidence`. This avoids
  claiming the first inversion or invalidation when intervening closes are
  missing. A documented closure supplied to the C# API preserves existing
  tracks but still breaks the formation window. The caller supplies evidence
  available at the cutoff. The CLI classifies discontinuities as unexplained.

The existing human FVG event validates candle identity and non-overlap.
Its selector binds human observations to the workflow owner and causal source
frame. The research evaluator reuses `CandleSeries`, decimal OHLC and the
explicit cutoff model of `StrategyReplayContext.AsOfUtc`. The Worker reuses
`CsvMarketDataImporter`. The research DTOs stay in Application Research and
carry timestamped evidence that a later contextual orchestrator can join to
existing reference IDs. An FVG may form before a sweep and invert afterward.
Coincident SC and IFVG closes must remain tied until a separate policy resolves
how to handle them.

## Run the exposed June 23 development slice

Run from the repository root with the original normalized M5 CSV. Report JSON
goes to standard output. The CLI limits report intervals to 1,000 nominal M5
slots because it emits per-zone histories. Evaluation has no expiry or implicit
lookback. The hash is pinned to the registered documentary package.

```powershell
$inputPath = 'C:\MoneyWay\session\normalized\2026-06-29\5M.csv'
$hash = '0133f473592a004535bac89437c96b1c369a753205792459c4e4e12c1835c21a'
$reportPath = Join-Path $env:TEMP ('moneyway-m5-fvg-' + [guid]::NewGuid().ToString('N') + '.json')
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- m5-fvg-research --input $inputPath --sha256 $hash --start-utc 2026-06-23T07:00:00Z --cutoff-utc 2026-06-23T16:30:00Z --provenance nasdaq-standard-2026-06-29-documentary-v1 --profile GeometryOnly --profile MiddleCloseConfirmation > $reportPath
```

## Development results

The registered CSV supplied 114 consecutive closed bars in the exposed
07:00–16:30 UTC slice. An independent Decimal check against the source CSV
matched the C# report for every formation, direction, boundary, middle-close
result, first inversion and first subsequent invalidation in both profiles.

| Metric | GeometryOnly | MiddleCloseConfirmation |
| --- | ---: | ---: |
| Eligible windows | 112 | 112 |
| Bullish / bearish FVG | 10 / 12 | 10 / 12 |
| Bullish / bearish middle closes confirmed | 10 / 12 | 10 / 12 |
| Bullish gap width min / median / max | 0.7 / 6.6 / 50.9 | 0.7 / 6.6 / 50.9 |
| Bearish gap width min / median / max | 0.5 / 6.55 / 24.2 | 0.5 / 6.55 / 24.2 |
| Not reached / boundary touch only / partial / full | 194 / 0 / 11 / 39 | 194 / 0 / 11 / 39 |
| Gap-ambiguous reaches / actual discontinuous opens among them | 274 / 264 | 274 / 264 |
| Bullish / bearish IFVG, by inverted direction | 12 / 7 | 12 / 7 |
| Invalidation of bullish / bearish IFVG | 11 / 4 | 11 / 4 |
| Formation to inversion min / median / max, minutes | 10 / 40 / 365 | 10 / 40 / 365 |
| Positive-width overlapping active pairs / distinct zones in those pairs | 5 / 8 | 5 / 8 |
| Closes confirming multiple inversions / missing intervals | 2 / 0 | 2 / 0 |

Reach totals count per-zone candle interactions, including interactions on a
transition candle. Active overlap ends at invalidation or the cutoff. Open
zones at the cutoff are bounded by the observed slice. Both profiles agreed
because every geometric formation satisfied the middle-close check here.
The equality fixture exercises their difference.

The following timestamp joins use the same observable June 23 source and the
existing contextual reference and event IDs. Each row reports the first FVG
formation and first inversion at or after the selected penetration close,
regardless of direction. These are descriptive joins. [Contextual SC report](contextual-swing-sc-v1.md)

| Existing reference and policy | Penetration close UTC | Existing subsequent SC close | Next FVG formation | Next IFVG close and its origin formation |
| --- | --- | --- | --- | --- |
| AL / E1 | 07:05 | 07:10 | 07:15 bullish | 07:45 bearish, origin 07:15 |
| LL / E1 | 13:10 | 13:35 | 13:55 bullish | 13:25 bullish, origin 12:40 |
| AL and LL / E2 and E3 | 13:35 | Same-candle SC abstention in H1/H2 | 13:55 bullish | Two bullish at 13:50, origins 12:25 and 12:30 |

At 13:10, five earlier formations later inverted by the cutoff. At 13:35,
four did. There were no formation or inversion closes coincident with the
reported E1 SC closes at 07:10 and 13:35. These timings leave any SC/IFVG
selection, ties, and choice of later FVG to future contextual research.

Imported historical publication is unauthenticated. Independent reference labels,
additional M5 data with documented availability and human approval remain necessary
for promotion. June 22, 24 and 26 remain protected, June 25 remains Validation
and June 30 remains HeldOut.
