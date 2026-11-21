# Nasdaq structural Take Profit reference V1

`EXP-NQ-STRUCTURAL-TP-V1` / `1.0.0` completes the experimental price-reference
pipeline. The audited Nasdaq concept refers to important highs for BUY and
important lows for SELL. Determining which highs and lows are important still
requires validation. The selection rules below are MoneyWay hypotheses. The
executable Take Profit policy remains unresolved, and canonical admission and
operational signal are both false.
[Nasdaq specification](../strategies/nasdaq/strategy-specification.md) · [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)

## Research foundation

- Prior highs and lows can provide observable support and resistance
  references. [CME Group, Support and Resistance](https://www.cmegroup.com/education/courses/technical-analysis/support-and-resistance)
- Schwab illustrates resistance as a potential exit reference for a bullish
  swing. [Charles Schwab, 5 Elements of a Smart Trade Plan](https://www.schwab.com/learn/story/5-elements-smart-trade-plan)
- A target and a stop express the potential reward and risk distances before
  an outcome is known. [IG, Take Profit and Stop Loss Orders Explained](https://www.ig.com/uk/learn/trading/take-profit-and-stop-loss-orders-explained)

The nearest-level policy, the pivot neighborhoods and the scope of each
reference pool are specific to this experiment.

## Evaluation contract

- Both policies use the exact `M1RealignmentCloseReference` already used by
  Structural SL, from the first eligible M1 realignment close. The cutoff is
  inclusive for nominal source confirmation. A waiting or incomplete M1 scan
  retains its source reason and has no target evaluation. The association
  preserves episode, SC/IFVG event, FVG, M1 and SL identities, the 11:30
  window configuration and any M5 invalidation already observable at the M1
  close. [Structural SL candidate](nasdaq-m5-structural-stop-loss-candidate-v1.md) · [Research window](nasdaq-trading-window-1130-alignment-v1.md)
- `NearestConfirmedM5Swing` uses the same previously calculated strict M5
  swing evidence for 1/1, 2/2 and 3/3 independently. For BUY it considers
  confirmed Highs above the M1 close. For SELL it considers confirmed Lows
  below. The rightmost confirmation candle must have closed. Earlier
  unconfirmed extremes expose no candidate. The SL observation supplies the
  contiguous M5 suffix and recorded gaps. Missing or insufficient coverage
  remains explicit.
- `NearestAuditedSessionExtreme` calculates the exact original H1 Asia and
  London references for the operating date at the M1 cutoff. It uses the
  versioned ADR 0028 bounds, with Asia `[17:00, 02:00)` and London
  `[02:00, 08:00)` in America/Bogota. BUY considers session Highs and SELL
  session Lows. Each endpoint retains boundaries, nominal availability,
  missing expected opens and all extreme candle identities. Open sessions
  or later snapshots expose no price. Incomplete exact H1 stays unavailable.
  Canonical session bounds, Standard and WF2 retain their existing rules.
  [ADR 0028](../decisions/0028-define-candidate-nasdaq-session-windows.md)
- Each policy selects the smallest strictly positive distance in its own
  pool. Equal prices keep all nearest identities. Wrong-side and exact-equal
  levels retain their diagnostics. BUY reward is the target High minus the
  M1 close. SELL reward is the M1 close minus the target Low. All differences
  use decimal price units.
- Prior interaction scans original closed M5 highs or lows after the pivot
  candle's close or session end, through the last nominal M5 close at or
  before M1. Equality or a crossing contributes observable reach evidence.
  This field does not establish an exact touch time or whether skipped prices
  traded. Unexplained gaps qualify negative evidence, and an explicitly
  documented market closure retains its own evidence reference. June 23
  supplies no documented closure for its recorded M5 gap. The partially
  elapsed M5 interval between the last close and M1 is outside this diagnostic.
  Prior interaction does not filter candidates.
- Evaluation is shared by M1 timestamp, direction, operating date and TP
  profile. Every selected target identity can be paired with every SL profile
  for the same exact M1 evidence. The report references the original SL
  observation, including selected pivot, price and positive distance when
  available. `HypotheticalRewardRisk` is reward distance divided by stop distance
  using .NET decimal division without application-level rounding. A missing
  or nonprotective SL leaves the target intact and the ratio unavailable.
  The report applies no ratio threshold or profile ranking. The measurements
  describe geometry in price units.

## Exposed Development run, June 23, 2026

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-structural-tp-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-structural-tp --cutoff-utc 2026-06-23T16:30:00Z
```

The command writes `contextual-structural-tp.json` plus the unchanged M5, M1 and
SL companions to a new directory. The input SHA-256 was
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`. The
report retains the input sources, timeframe hashes and documentary mapping.
Protected review dates June 22, 24 and 26, June 25 Validation and June 30
HeldOut were excluded.

Six episodes and four M5 anchors produced five distinct bullish M1 closes.
The 240 M5/M1 configurations produced 2,880 associations with TP and SL
profiles, sharing 20 TP evaluations, 66 available source identities and
11 selected identities. All 20 evaluations found a target. Sixty shared
TP/SL pairings produced 36 positive hypothetical ratios. The other 24
retained their TP with a nonprotective SL. In 192 associations all bound M5
branches already had a zone invalidation observable at the M1 close.

| TP profile | Available references, observation rows / unique | Selected / unavailable evaluations | Positive reward distances, sorted points | Positive ratios / pairs | Ratio min / median / max |
| --- | ---: | ---: | --- | ---: | --- |
| M5 1/1 | 191 / 39 | 5 / 0 | 2.3, 9.1, 25.8, 33.6, 35.5 | 9 / 15 | 0.013295 / 0.145791 / 0.212575 |
| M5 2/2 | 75 / 15 | 5 / 0 | 2.3, 42.9, 84.9, 132.3, 149.0 | 9 / 15 | 0.013295 / 0.205263 / 0.508383 |
| M5 3/3 | 50 / 10 | 5 / 0 | 2.3, 42.9, 84.9, 132.3, 149.0 | 9 / 15 | 0.013295 / 0.205263 / 0.508383 |
| Exact H1 sessions | 10 / 2 | 5 / 0 | 42.9, 84.9, 155.4, 317.7, 334.4 | 9 / 15 | 0.150263 / 0.508383 / 1.610363 |

Ratios are rounded here for display. The M5 profiles recorded respectively
41, 12 and 12 wrong-side rows and no exact-price equality in this run.
Asia High 30,463.9 and London High 29,818.6 were available at all five M1
closes. London was nearer each time. Four of the five selected 1/1 targets
and three of five in each wider profile had an observable prior reach.
The covered M5 intervals from London end showed no prior reach for the
selected London reference. The M5 pivot pool retains the June 22
21:00–22:00 UTC gap and uses continuous history after it.

The report is available for end-to-end research composition. SELL, equal
levels, missing coverage and future-data exclusion were exercised with
fixtures. The exposed day is documentary, without authenticated historical
availability, independent labels or development evidence for SELL. Repeating
the command requires no manual labeling. Admissible chronology, independent
reference review and human approval are required before promotion.
