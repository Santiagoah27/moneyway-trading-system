# Experimental Nasdaq M1 countertrend and realignment V1

`EXP-NQ-M1-REALIGNMENT-V1` / `1.0.0` is isolated research under ADR 0027.
It builds on completed M5 research sequences and uses the directional FVG
completion as the M1 anchor. The canonical Step 6 remains a human evidence
workflow after Step 4 where approved FVG support is optional. Both admissibility
and operational signal flags in this candidate are false.
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md) · [Audited Nasdaq specification](../strategies/nasdaq/strategy-specification.md) · [M5 contextual sequence](nasdaq-m5-contextual-sequence-v1.md)

## Research foundations

- Previous highs and lows can supply reference levels and retracements can
  revisit those levels. [CME Group](https://www.cmegroup.com/education/courses/trading-and-analysis/support-and-resistance)
- Candlestick bodies distinguish the relationship between Open and Close.
  Price sequences can describe directional structure.
  [Charles Schwab](https://www.schwab.com/learn/story/how-to-read-stock-charts-and-trading-patterns)
- A pivot may only be observable after the required later candles confirm it.
  That principle motivates recording the actual confirmation close separately.
  This experiment calculates candle bodies and a frozen attempt range.
  [TradingView Zig Zag documentation](https://www.tradingview.com/support/solutions/43000591664-zigzag-indicator/)

The following exact state transitions are MoneyWay hypotheses. Independent
validation, parameter suitability and profitability remain separate questions.

## Evidence and policies

- A completed M5 association retains the episode, policy, tied trigger set and
  each selected directional FVG. The anchor is that FVG's observable close.
  The Application use case matches provider and instrument and shares the M5
  cutoff. Only original, grid-aligned M1 candles opening on or after the anchor
  and closing strictly after it can generate evidence. The count of earlier
  closed M1 candles is contextual. A missing or incomplete M5 prerequisite
  produces `WaitingForM5Sequence`.
- An opposite body starts and extends the attempt. A bullish sequence uses a
  bearish body as opposite and the bearish sequence mirrors this. Exact dojis
  are neutral. Neutral candles during the active pullback are included in its
  high/low range. The first aligned candle closes the interval and freezes
  the extremes from the preceding members. The first opposite candle's close
  supplies the observable pullback timestamp.
- `BodyDirectionTurn` confirms at the first aligned close. For
  `PullbackRangeReclaim` an aligned close must be strictly above the frozen
  high for bullish or strictly below the frozen low for bearish. Equality and
  neutral candles leave the reclaim pending. A new opposite body interrupts
  pending reclaim and starts a new attempt. The reclaim timestamp is the close
  that passes the strict test. One opposite body is sufficient to start a
  research attempt.
- Each attempt retains its identity, first and last interval candles, range,
  counts, first aligned candle and any realignment or interruption evidence.
  Earliest verified in-window realignment is stable as more attempts occur.
  Later attempts are descriptive. The report preserves shared scan and branch
  identities for equally timed M5 evidence.
- In-window means the M1 evidence close falls in `[08:30, 11:30)`
  America/Bogota. Earlier and later realignments, including an exact 11:30
  close, remain diagnostic. A pre-window attempt may lead to an in-window
  confirmation if its evidence is contiguous from the M5 anchor. Expiry
  is visible independently from pending attempts and data limitations.
- A missing expected M1 interval closes the active attempt as incomplete.
  Later prices can start a new diagnostic attempt. A prior gap prevents proof
  that a later realignment was the first eligible event after the anchor.
  Earlier verified events remain visible.
- M5 validity is assessed per trigger/FVG association at each M1 observation
  close. A directional FVG loses its original role upon inversion. The
  originating IFVG loses its inverted role on invalidation. At an equal close,
  the invalidation is already observable at that boundary. A later invalidation
  remains separate from validity at the earlier observation. Evidence
  limitations are retained, and SC provides no invalidation rule.
  M1's first window-eligible observation refers to the window and continuous
  data only. Consumers can inspect zone validity separately, including prior
  invalidation, before defining any next-stage hypothesis.

The implementation reuses `CandleSeries`, `CandleBodyDirectionCalculator`,
the existing Bogotá timezone and the nominal cutoffs used by
`StrategyReplayContext.AsOfUtc`. It composes the M5 sequence as research data
and keeps no external state. Calculations are shared per operating date,
timestamp, direction, window and M1 profile. Associations and separately
indexed zone timelines preserve each original result. The frozen range and
its timestamps can support later structural SL/TP research when independently
validated.

## Run the exposed June 23 context

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-m1-realignment-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-context-realignment --cutoff-utc 2026-06-23T16:30:00Z
```

The command publishes `contextual-m1-realignment.json` alongside the existing
`contextual-sequence.json`. Both share input hash and cutoff. The M1 report
retains original source metadata. The mode is available for exposed
development data and rejects the frozen June 22 context.

## Development findings

At 16:30 UTC (11:30 Bogotá) there were 12 episode/reference-policy combinations.
Six episodes had historically complete M5 evidence, representing 120 of 288
episode/configuration combinations. Those 120 completions share four distinct
M5 timestamps and directions. There are eight M1 scans, four per profile.
The counts below use distinct anchor/profile scans.

| Metric | BodyDirectionTurn | PullbackRangeReclaim |
| --- | ---: | ---: |
| Distinct eligible M5 anchors | 4 | 4 |
| Countertrend attempts | 352 | 352 |
| Observed realignments | 348 | 129 |
| In-window realignments | 174 | 67 |
| Pending at cutoff | 4 | 4 |
| Interrupted attempts | 0 | 219 |
| Insufficient M1 coverage | 0 | 0 |
| Pullback-to-realignment delay min / median / max, minutes | 1 / 2 / 7 | 1 / 2 / 5 |

Anchor-to-first-pullback delay was 1 / 2 / 3 minutes (min / median / max) in
both profiles. Each scan had continuous M1 coverage from its anchor through
16:30 UTC. The first in-window close differed at three of four anchors.

| Anchor UTC | Subsequent closed M1 | First in-window body turn UTC | First in-window range reclaim UTC |
| --- | ---: | --- | --- |
| 07:15 | 555 | 13:32 | 13:30 |
| 08:40 | 470 | 13:32 | 13:30 |
| 13:55 | 155 | 13:58 | 13:58 |
| 14:05 | 145 | 14:07 | 14:17 |

For LL/E1 the 13:55 UTC M5 completion is the anchor under the prior-sweep
window policy. The 13:56–13:57 M1 candle supplied the first countertrend body,
with range 29609.5–29651.6. Both profiles confirmed at 13:58 UTC
(08:58 Bogotá). The directional FVG was still valid in its original role at
that close, with a later inversion at 15:10 UTC. Across the complete M5
configurations, eight associations per profile already had an invalidated
zone in every bound branch at their first M1 realignment. The remaining
112 per profile had no such invalidation at that timestamp. These counts
reflect overlapping configurations and one exposed session.

The run supplies documentary development evidence without authenticated
historical publication or expert labels. Additional independent sessions and
human approval remain necessary for promotion. Running the experiment requires
no manual labels. June 22, 24 and 26, Validation and HeldOut remain protected.
