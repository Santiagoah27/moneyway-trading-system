# MoneyWay Nasdaq strategy specification

## 1. Metadata

| Field | Value |
|---|---|
| Strategy | MoneyWay Nasdaq |
| Specification version | `nasdaq-0.1.0-draft` |
| Status | draft |
| Source material | Complete audited analysis of the 58:24 mentorship video, including direct manual source-video re-verification |
| Real-money trading | Prohibited |
| Autonomous execution | Not approved |

## 2. Purpose and scope

Esta especificación conserva exclusivamente la evidencia auditada para MoneyWay Nasdaq. Describe análisis, backtesting y paper trading supervisado; no contiene reglas de MoneyWay Forex ni autoriza ejecución autónoma.

## 3. Evidence policy

Cada regla usa `confirmed`, `candidate`, `context_specific`, `visual_only`, `human_validation_required`, `unresolved` o `rejected_ai_inference`. Una regla `confirmed` puede ser no automatizable si depende de juicio subjetivo. Los valores no definidos permanecen `null`, `unresolved` o `human_validation_required`.

Los resultados permitidos son `passed`, `failed`, `waiting`, `not_applicable`, `human_validation_required` y `data_unavailable`. El veredicto general es `ready`, `wait`, `no_trade`, `human_validation_required` o `data_unavailable`.

## 4. Source coverage

La documentación representa la evidencia actualmente auditada del video de 58:24, consolidada desde cinco intervalos y una re-verificación humana directa del video fuente. Esta re-verificación confirmó la secuencia de seis etapas, 08:00/08:30/11:30 en hora Colombia, las sesiones Asia/London y sus extremos 1H, la guía de Stop Loss 5M HL/LH y los targets en important highs/lows. No se inventan timestamps, URL ni líneas de transcript para esta corrección. Salvo `50:15` para riesgo máximo por operación, los timestamps de reglas individuales siguen siendo `null` cuando no fueron proporcionados.

## 5. Supported operating modes

| Mode | Readiness |
|---|---|
| Assisted analysis | Sufficient |
| Manual backtesting | Sufficient |
| Semi-automatic backtesting | Partially sufficient; human validation required |
| Supervised paper trading | Sufficient only with human validations |
| Fully automatic backtesting | Not sufficient |
| Supervised demo execution | Not enabled until critical timestamps and blockers are resolved |
| Autonomous execution | Not sufficient and prohibited |
| Real-money trading | Prohibited |

## 6. High-level workflow

1. At 08:00 `America/Bogota`, prepare the market on 4H: determine HH/LL trend and identify Breakout, Wickfill or Fakeout context.
2. Mark Asia High/Low and London High/Low, and review coincident structural points on 1H/4H.
3. Wait for price to exceed an identified liquidity high or low.
4. Move to 5M and identify inversion through structural change `OR` IFVG, whichever occurs first.
5. Remain on 5M and require FVG confirmation/strength for the new direction.
6. Move to 1M, wait for a counter-direction pullback, then require realignment with the sought direction before entry confirmation.

Trading starts at 08:30 and ends at 11:30, always in `America/Bogota`. Entry order mechanics remain unresolved. For buys, the conceptual Stop Loss reference is the structural 5M HL and the target reference is an important high. For sells, the conceptual Stop Loss reference is the structural 5M LH and the target reference is an important low. Structural detection and target-importance algorithms remain unresolved and require human validation.

The workflow is sequential. A later mandatory stage cannot be `passed` while an earlier stage is `failed`, `waiting`, `data_unavailable` or `human_validation_required`.

## 7. Buy workflow

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation requirement | Open variables |
|---:|---|---|---|---|---|---|
| 1. 4H HH/LL context | `confirmed` + `human_validation_required` | 4H candles | Trend and context reviewed | Blocks all lower stages | Yes | HH/LL structural swing algorithm |
| 2. Breakout/Wickfill/Fakeout | `confirmed` conceptually | 4H context and relevant level | One classification recorded | Unclassified context blocks | Yes | Exact geometries and tolerances |
| 3. Relevant liquidity | `confirmed` + `human_validation_required` | Session levels and 1H/4H context | Asia/London extrema and structural coincidences recorded | Missing levels block sweep evaluation | Yes | Structural-point algorithm, coincidence tolerance and priority |
| 4. Asia High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the Asia interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 5. London High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the London interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 6. Preparation | `confirmed` | Clock, context and levels | Preparation begins at 08:00 `America/Bogota` | Incomplete preparation blocks entry | Yes for analysis content | Calendar eligibility |
| 7. Trading-window start | `confirmed` | Clock in `America/Bogota` | `waiting` before 08:30 | Entry prohibited before start | No for timezone/DST | None for timezone/DST |
| 8. Liquidity sweep | `confirmed` conceptually | Marked high/low and price | Price exceeds the level; remaining details human-approved | Without an exceedance, remain `waiting` | Yes | Minimum penetration, rejection, pre-08:30 validity |
| 9. Move to 5M | `confirmed` | Approved sweep, 5M data | 5M review enabled | Blocks inversion without sweep | No | Data alignment |
| 10. 5M inversion OR | `confirmed` | 5M structure/FVG evidence | First occurring traditional change `OR` IFVG validated | Without either, `waiting` | Yes | Swing algorithm and IFVG geometry |
| 11. 5M close | `confirmed` | 5M candle close | Close beyond required level validated | Wick alone is `failed`; open candle is `waiting` | Yes for marginal close | Minimum distance |
| 12. Continuation FVG | `confirmed` + `human_validation_required` | Three 5M candles after inversion | Directional, clear FVG validated | Missing/weak FVG means `no_trade` | Yes | Minimum size and quality threshold |
| 13. Move to 1M | `confirmed` | Approved FVG, 1M data | 1M review enabled | Blocks entry if data absent | No | Data alignment |
| 14. Corrective retracement | `confirmed` + `human_validation_required` | 1M candles against new 5M move | Retracement identified | No retracement means `waiting` | Yes | Correction boundaries |
| 15. 1M realignment | `confirmed` + `human_validation_required` | Corrective microstructure | Candidate realignment identified | No realignment means no entry | Yes | Corrective swing algorithm |
| 16. Corrective swing break | `confirmed` | Entry swing and closed 1M candle | Body closes beyond corrective swing | Wick/open candle does not pass | Yes for swing selection | Marginal close distance |
| 17. Entry | `confirmed` + `unresolved` | All earlier stages approved | `human_validation_required` | No automatic order creation | Yes | Order type, timing, slippage, attempts |
| 18. Stop Loss | `confirmed` conceptually + `human_validation_required` | Structural 5M HL and entry | Human validates SL where the buy idea loses structural meaning | Missing/ambiguous structural SL means `no_trade` | Yes | HL algorithm, invalidation geometry, buffer and costs |
| 19. Break Even | `confirmed` mechanism + `candidate` universality | Post-entry 1M swing and closed candle | SL moved to entry after break and close | Not evaluated before entry | Yes for post-entry swing | Costs, applicability, later management |
| 20. Take Profit | `confirmed` conceptually + `human_validation_required` | Candidate highs and position state | Important high selected | No automatic target selection | Yes | Importance algorithm, priority, ratio, partials |
| 21. Trading-window end | `confirmed` | Clock in `America/Bogota` | `failed` for new entry after 11:30 | New entries prohibited | No for timezone/DST | Open-position management |
| 22. Risk controls | `confirmed` max trade risk + open controls | Entry, Stop Loss, sizing inputs | At most 1% risk after human validation | Missing sizing/control data blocks execution | Yes | Position size, daily limit, kill switch |

## 8. Sell workflow

Solo se incluyen relaciones direccionales expresamente documentadas. No se completan objetivos, Stop Loss ni gestión por simetría automática.

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation requirement | Open variables |
|---:|---|---|---|---|---|---|
| 1. 4H HH/LL context | `confirmed` + `human_validation_required` | 4H candles | Trend and context reviewed | Blocks all lower stages | Yes | HH/LL structural swing algorithm |
| 2. Breakout/Wickfill/Fakeout | `confirmed` conceptually | 4H context and relevant level | One classification recorded | Unclassified context blocks | Yes | Exact geometries and tolerances |
| 3. Relevant liquidity | `confirmed` + `human_validation_required` | Session levels and 1H/4H context | Asia/London extrema and structural coincidences recorded | Missing levels block sweep evaluation | Yes | Structural-point algorithm, coincidence tolerance and priority |
| 4. Asia High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the Asia interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 5. London High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the London interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 6. Preparation | `confirmed` | Clock, context and levels | Preparation begins at 08:00 `America/Bogota` | Incomplete preparation blocks entry | Yes for analysis content | Calendar eligibility |
| 7. Trading-window start | `confirmed` | Clock in `America/Bogota` | `waiting` before 08:30 | Entry prohibited before start | No for timezone/DST | None for timezone/DST |
| 8. Liquidity sweep | `confirmed` conceptually | Marked high/low and price | Price exceeds the level; remaining details human-approved | Without an exceedance, remain `waiting` | Yes | Minimum penetration, rejection, pre-08:30 validity |
| 9. Move to 5M | `confirmed` | Approved sweep, 5M data | 5M review enabled | Blocks inversion without sweep | No | Data alignment |
| 10. 5M inversion OR | `confirmed` | 5M structure/FVG evidence | First occurring traditional change `OR` IFVG validated | Without either, `waiting` | Yes | Swing algorithm and IFVG geometry |
| 11. 5M close | `confirmed` | Bullish-structure swing and 5M close | Candle body closes beyond relevant swing | Wick alone is `failed`; open candle is `waiting` | Yes for swing selection/marginal close | Pivot and minimum distance |
| 12. Continuation FVG | `confirmed` + `human_validation_required` | Three 5M candles after inversion | FVG favors new sell-side move and is clear | Missing/weak FVG means `no_trade` | Yes | Minimum size and quality threshold |
| 13. Move to 1M | `confirmed` | Approved FVG, 1M data | 1M review enabled | Blocks entry if data absent | No | Data alignment |
| 14. Corrective retracement | `confirmed` + `human_validation_required` | 1M candles against new 5M move | Retracement identified | No retracement means `waiting` | Yes | Correction boundaries |
| 15. 1M realignment | `confirmed` + `human_validation_required` | Corrective microstructure | Candidate sell realignment identified | No realignment means no entry | Yes | Corrective swing algorithm |
| 16. Corrective swing break | `confirmed` | Entry swing and closed 1M candle | Body closes beyond corrective swing | Wick/open candle does not pass | Yes for swing selection | Marginal close distance |
| 17. Entry | `confirmed` + `unresolved` | All earlier stages approved | `human_validation_required` | No automatic order creation | Yes | Order type, timing, slippage, attempts |
| 18. Stop Loss | `confirmed` conceptually + `human_validation_required` | Structural 5M LH and entry | Human validates SL where the sell idea loses structural meaning | Missing/ambiguous structural SL means `no_trade` | Yes | LH algorithm, invalidation geometry, buffer and costs |
| 19. Break Even | `confirmed` mechanism + `candidate` universality | Post-entry 1M swing and closed candle | SL moved to entry after break and close | Not evaluated before entry | Yes for post-entry swing | Costs, applicability, later management |
| 20. Take Profit | `confirmed` conceptually + `human_validation_required` | Candidate lows and position state | Important low selected | No automatic target selection | Yes | Importance algorithm, priority, ratio, partials |
| 21. Trading-window end | `confirmed` | Clock in `America/Bogota` | `failed` for new entry after 11:30 | New entries prohibited | No for timezone/DST | Open-position management |
| 22. Risk controls | `confirmed` max trade risk + open controls | Entry, Stop Loss, sizing inputs | At most 1% risk after human validation | Missing sizing/control data blocks execution | Yes | Position size, daily limit, kill switch |

## 9. 4H context

Preparation begins at 08:00 `America/Bogota`. Analysis starts on 4H, identifies trend by reviewing whether the market is constructing HH or LL, and then classifies the context as Breakout, Wickfill or Fakeout.

- `Breakout` — `confirmed` conceptually: prior audited evidence that a Break requires a candle-body close beyond the relevant structural level remains applicable. Swing selection, structural algorithm, candle count, tolerances and marginal closes are unresolved.
- `Wickfill` — conceptually `confirmed`, operationally `human_validation_required`: it is a distinct 4H context. Wickfill geometry, fill amount, effect, invalidation and distinction from a liquidity sweep remain open.
- `Fakeout` — conceptually `confirmed`, operationally `human_validation_required`: it is a distinct 4H context. Range, return, timeframe/close, distance, allowed candles, sweep distinction and bias effect remain open.

HH/LL and these three contexts are confirmed strategy concepts, not completed deterministic algorithms. They must not be collapsed into one body-close rule.

## 10. Liquidity

Mark Asia High, Asia Low, London High and London Low. Also review structural points on 1H/4H, especially when they coincide with those session extrema. A liquidity level must be taken before searching for 5M inversion; the sweep alone is never an entry.

For each trading/preparation day `D`, session membership is defined in local `America/Bogota` time by the 1H candle `OpenTime`:

| Session | Interval | Included nominal 1H opens | Nominal count sanity check |
|---|---|---|---:|
| Asia | `[D-1 17:00, D 02:00)` | 17:00 through 23:00 on `D-1`, then 00:00 and 01:00 on `D` | 9 |
| London | `[D 02:00, D 07:00)` | 02:00 through 06:00 on `D` | 5 |

Intervals are start-inclusive and end-exclusive. Asia crosses midnight; London does not within this definition. Therefore, the 02:00 candle belongs to London, not Asia, and the 07:00 candle does not belong to London. The nominal intervals have neither overlap nor gap. The candle counts are sanity checks for complete, hourly-aligned data; they do not authorize synthesis of missing candles or assumptions about weekends, holidays or early closes.

`NQ-LIQ-001` uses only completed, closed 1H candles whose local `OpenTime` belongs to the corresponding interval:

- `AsiaHigh(D) = maximum Candle.High` among the relevant completed Asia candles.
- `AsiaLow(D) = minimum Candle.Low` among the relevant completed Asia candles.
- `LondonHigh(D) = maximum Candle.High` among the relevant completed London candles.
- `LondonLow(D) = minimum Candle.Low` among the relevant completed London candles.

These extrema do not use `Open`, `Close`, candle bodies, averages, pivots or future candles. No resampling, interpolation, forward fill or synthetic candle generation is permitted. Missing required session data must produce `data_unavailable`; a future implementation must define and test provider-specific data completeness without inventing calendar behavior. At the normal 08:00 `America/Bogota` preparation time, Asia has ended at 02:00 and London at 07:00, so all four references come from completed prior sessions; this observation does not add a new execution rule.

These definitions make `NQ-LIQ-001` sufficiently specified for a future deterministic implementation, but no evaluator is implemented by this documentation change. Structural-point detection, coincidence tolerance, liquidity priority, reuse after sweep, multiple sweeps, internal/external liquidity, equal levels, prior-day levels and pre-08:30 sweeps remain unresolved or require human validation. The confirmed high-level sweep condition is only that price exceeds an identified high or low; minimum penetration, close-back, rejection, displacement, timing and invalidation remain unresolved, and neither wick-only nor candle-close confirmation is inferred.

## 11. Operating schedule

```yaml
preparation_start_time: "08:00"
entry_start_time: "08:30"
trading_window_end_time: "11:30"
timezone: "America/Bogota"
daylight_saving_adjustment: false
```

Preparation/analysis starts at 08:00. The trading window starts at 08:30 and ends at 11:30. All three are local Colombia times governed normatively by `America/Bogota`, which does not apply seasonal DST adjustments to these limits.

The strategy operates the New York market, but that market reference does not change its clock to `America/New_York`, EST or EDT. The same confirmed timezone governs the Asia and London definitions in section 10. Allowed days, holidays, early closes, low-liquidity sessions and management of positions after 11:30 remain unresolved.

## 12. 5M inversion

Two alternatives are valid with `OR`: traditional structural change or IFVG, whichever occurs first. Both are not required and neither has a fixed priority.

- Traditional change — for buys, break bearish structure; for sells, break bullish structure. A relevant 5M swing and candle-body close beyond it are required. A wick does not confirm. Pivot selection and marginal-close thresholds require human validation.
- IFVG — conceptually `confirmed`: invalidation of a prior FVG may replace traditional structural change. Geometry, direction, partial/full close, mitigation, confirming candle, expiry, direct-entry capability and displacement relation are unresolved. No external IFVG definition applies.

## 13. 5M continuation FVG

```yaml
required: true
candle_count: 3
minimum_size_points: null
quality_validation: human_validation_required
```

It must occur after 5M inversion and favor the new move. The space is evaluated between wicks of the first and third candles. Without a continuation FVG there is `no_trade`. One or two points were described as insufficient evidence of strength, but this qualitative observation does not establish a three-point minimum. Clear/evident size, volatility relation, fills, invalidation, lifespan and multiple-FVG selection remain open.

## 14. 1M entry

After the 5M inversion and FVG, move to 1M. Wait for a retracement against the new 5M move and corrective microstructure. Touching the 5M FVG alone does not authorize entry. The last corrective swing must break with a candle-body close; if 1M never realigns, no entry occurs.

### Entry swing

The last swing of the corrective 1M structure whose break and close enables entry.

### Break Even swing

A different swing formed after entry and used for position management.

Order type, close-versus-next-open timing, slippage, maximum chase distance, attempts, reentries, timeout, after-11:30 confirmation and corrective-swing algorithm are unresolved.

## 15. Stop Loss

Status: conceptually `confirmed`; deterministic geometry remains `human_validation_required`.

```yaml
buy_reference: structural_5m_hl
sell_reference: structural_5m_lh
invalidation_principle: trade_idea_loses_structural_meaning
selection_algorithm: null
buffer_points: null
status: confirmed_concept
human_validation_required: true
```

Manual source-video re-verification resolves the prior competing interpretation involving the sweep extreme. The confirmed conceptual reference is the structural 5M HL for buys and structural 5M LH for sells, placed where the trade idea loses structural meaning.

This reconciliation does not define how to detect HL/LH, select the relevant structural swing or translate “loses structural meaning” into deterministic geometry. Body/wick treatment, buffer, spread, slippage, maximum Stop Loss, oversized-stop behavior, pre-entry invalidation and risk reduction remain open. Human validation is still required and fully automatic demo execution remains blocked.

## 16. Break Even

The entry swing and Break Even swing are different. After entry, identify the first relevant 1M swing. Break Even activates after a 1M candle breaks and closes beyond that post-entry swing; Stop Loss then moves to entry.

An earlier explanation used “touch”; the later “break and close” explanation is treated as more specific. `mandatory_for_every_trade: candidate`. Swing algorithm, multiple swings, universality, commissions, spread, exact entry/cost basis, pre-confirmation retracement and subsequent management remain open.

## 17. Take Profit

```yaml
buy_target_reference: important_high
sell_target_reference: important_low
importance_algorithm: null
target_priority: null
fixed_risk_reward: null
partials: null
status: confirmed_concept
human_validation_required: true
```

Manual source-video re-verification confirms important highs as the target concept for buys and important lows for sells. The algorithm that determines importance and the priority among multiple candidates remain unresolved, so target selection still requires human validation.

Asia Low and opposite session extremes remain `context_specific` examples that may satisfy the directional concept in their original context; they are not universal targets. Fixed ratio, partials, trailing, manual close and 1H/4H target priority are not confirmed.

## 18. Risk

```yaml
maximum_risk_per_trade_percent: 1.0
status: confirmed
evidence_timestamp: "50:15"
daily_loss_limit_percent: null
daily_loss_limit_status: candidate
```

The 1% daily loss limit is `candidate` pending manual evidence validation. Account minimum, sizing, points/ticks formula, tick value, costs, trades/losses per day, post-BE risk, risk reduction, kill switch and rejected-order handling are unresolved.

## 19. News

```yaml
observed_news_time: "10:00"
observed_close_time: "09:45"
observed_action: close_all
status: context_specific
universal_close_minutes_before: null
```

This single example does not establish a universal 15-minute rule. Impact definition, calendar, events, before/after windows, open positions, new entries and resumption are unresolved.

## 20. Reentries

```yaml
after_break_even: null
after_stop_loss: null
same_setup: null
independent_new_setup: null
maximum_attempts: null
status: unresolved
```

The phrase similar to “if it takes you out, do not seek re-entry” lacks sufficient scope. It does not confirm “no re-entry after any exit.”

## 21. Waiting states

Return `waiting` before 08:30 `America/Bogota`, before liquidity is taken, while inversion or candle close is pending, without a continuation FVG, during the 1M correction, before realignment, or while required post-entry management evidence has not formed. Return `data_unavailable` for missing required closed 1H session candles, other missing candles or unavailable timestamps. Return `human_validation_required` at subjective gates; later stages remain blocked.

## 22. No-trade conditions

Confirmed:

- No entry without a liquidity sweep.
- No entry without confirmed 5M inversion.
- A wick is not a 5M structural change.
- No trade without a continuation 5M FVG.
- No trade when FVG strength is insufficient according to human review.
- No entry without 1M retracement and realignment.
- No entry before 08:30 `America/Bogota`.
- No new entry after 11:30 `America/Bogota`.

FVG size/quality, displacement, Wickfill/Fakeout, structural HL/LH selection and important-high/important-low selection require human validation. News, oversized stops, target distance, reentries, operation counts, daily limit, data quality, lateral markets and holidays remain unresolved.

## 23. Human-validation points

Human validation is required for 4H HH/LL swings, Breakout/Wickfill/Fakeout geometry, liquidity selection and sweep details, 5M pivots, IFVG, FVG quality, 1M correction/swing, order mechanics, structural Stop Loss selection, Break Even swing, important target selection, sizing, daily limit, news and reentries. Timezone/DST interpretation is no longer a human-validation point for 08:00/08:30/11:30.

## 24. Automation readiness

The evidence supports assisted analysis, manual backtesting and supervised paper trading. Semi-automatic backtesting remains partial. The trading window, Asia/London session boundaries and extrema calculation are deterministically specified; the prior Stop Loss reference contradiction is resolved conceptually, and directional target concepts are confirmed. The session-liquidity evaluator is not yet implemented. Subjective FVG quality, swing algorithms, structural Stop Loss geometry, target-importance selection, unresolved news/reentries and incomplete risk controls still prohibit fully automatic backtesting and autonomous execution.

## 25. Traceability requirements

Record at minimum: `Strategy`, `Strategy version`, `Instrument`, `Data source`, `Timestamp`, `Timezone`, `Market snapshot`, `Rules evaluated`, `Rule statuses`, `Evaluation results`, `Evidence`, `Evidence timestamp`, `Final verdict`, `Human validations`, `Entry`, `Stop Loss`, `Take Profit`, `Result`, `Failure classification` and `Trace identifier`. Preserve candle-close ordering and exclude future data.
