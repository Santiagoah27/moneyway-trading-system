# MoneyWay Nasdaq strategy specification

## 1. Metadata

| Field | Value |
|---|---|
| Strategy | MoneyWay Nasdaq |
| Specification version | `nasdaq-0.1.0-draft` |
| Status | draft |
| Source material | Complete audited analysis of the 58:24 mentorship video, reviewed in five intervals |
| Real-money trading | Prohibited |
| Autonomous execution | Not approved |

## 2. Purpose and scope

Esta especificación conserva exclusivamente la evidencia auditada para MoneyWay Nasdaq. Describe análisis, backtesting y paper trading supervisado; no contiene reglas de MoneyWay Forex ni autoriza ejecución autónoma.

## 3. Evidence policy

Cada regla usa `confirmed`, `candidate`, `context_specific`, `visual_only`, `human_validation_required`, `unresolved` o `rejected_ai_inference`. Una regla `confirmed` puede ser no automatizable si depende de juicio subjetivo. Los valores no definidos permanecen `null`, `unresolved` o `human_validation_required`.

Los resultados permitidos son `passed`, `failed`, `waiting`, `not_applicable`, `human_validation_required` y `data_unavailable`. El veredicto general es `ready`, `wait`, `no_trade`, `human_validation_required` o `data_unavailable`.

## 4. Source coverage

La documentación representa la evidencia actualmente auditada del video de 58:24, consolidada desde cinco intervalos con contradicciones y variables abiertas preservadas. Los timestamps críticos deberán verificarse manualmente contra el video original antes de habilitar ejecución demo. Salvo `50:15` para riesgo máximo por operación, los timestamps de reglas individuales son `null` cuando no fueron proporcionados.

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

1. Start 4H context analysis.
2. Classify context using Break, Wick or Fake.
3. Mark relevant liquidity levels.
4. Mark Asia High and Asia Low.
5. Mark London High and London Low.
6. Prepare analysis before the operating start.
7. Do not open entries before 08:30.
8. Wait for a liquidity sweep.
9. Move to 5M.
10. Confirm inversion through traditional structural change `OR` IFVG.
11. Require a 5M candle close to validate inversion.
12. Require a continuation FVG on 5M.
13. Move to 1M.
14. Wait for a retracement against the new 5M move.
15. Wait for 1M structural realignment.
16. Require break and candle-body close beyond the corrective 1M swing.
17. Execute only after 1M confirmation; order mechanics remain unresolved.
18. Define Stop Loss through human validation because selection is unresolved.
19. Manage Break Even using a distinct post-entry swing.
20. Define Take Profit through human validation because its general rule is unresolved.
21. Do not open new entries after 11:30.
22. Apply approved risk controls.

The workflow is sequential. A later mandatory stage cannot be `passed` while an earlier stage is `failed`, `waiting`, `data_unavailable` or `human_validation_required`.

## 7. Buy workflow

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation requirement | Open variables |
|---:|---|---|---|---|---|---|
| 1. 4H context | `confirmed` + `human_validation_required` | 4H candles | Context reviewed | Blocks all lower stages | Yes | Structural swing algorithm |
| 2. Break/Wick/Fake | `confirmed` conceptually | 4H context and relevant level | One classification recorded | Unclassified context blocks | Yes for Wick/Fake and marginal Break | Exact Wick/Fake criteria, tolerances |
| 3. Relevant liquidity | `confirmed` + `human_validation_required` | Context and marked levels | Levels recorded | Missing levels block sweep evaluation | Yes | Internal/external and structural priority |
| 4. Asia High/Low | `confirmed` | Session candles | Both levels recorded | Missing data returns `data_unavailable` | No after session boundary known | Timezone/session boundary |
| 5. London High/Low | `confirmed` | Session candles | Both levels recorded | Missing data returns `data_unavailable` | No after session boundary known | Timezone/session boundary |
| 6. Preparation | `candidate` + `confirmed` intent | Context and levels before operations | Preparation recorded | Incomplete preparation blocks entry | Yes | Exact analysis-start time |
| 7. Entry start | `confirmed` | Clock and timezone configuration | `waiting` before 08:30 | Entry prohibited before start | Yes while timezone is `null` | Timezone and DST |
| 8. Liquidity sweep | `confirmed` conceptually | Marked high/low and price | Human-approved sweep | Without sweep, remain `waiting` | Yes | Penetration, rejection, pre-08:30 validity |
| 9. Move to 5M | `confirmed` | Approved sweep, 5M data | 5M review enabled | Blocks inversion without sweep | No | Data alignment |
| 10. 5M inversion OR | `confirmed` | 5M structure/FVG evidence | Traditional change `OR` IFVG validated | Without either, `waiting` | Yes | Swing algorithm and IFVG geometry |
| 11. 5M close | `confirmed` | 5M candle close | Close beyond required level validated | Wick alone is `failed`; open candle is `waiting` | Yes for marginal close | Minimum distance |
| 12. Continuation FVG | `confirmed` + `human_validation_required` | Three 5M candles after inversion | Directional, clear FVG validated | Missing/weak FVG means `no_trade` | Yes | Minimum size and quality threshold |
| 13. Move to 1M | `confirmed` | Approved FVG, 1M data | 1M review enabled | Blocks entry if data absent | No | Data alignment |
| 14. Corrective retracement | `confirmed` + `human_validation_required` | 1M candles against new 5M move | Retracement identified | No retracement means `waiting` | Yes | Correction boundaries |
| 15. 1M realignment | `confirmed` + `human_validation_required` | Corrective microstructure | Candidate realignment identified | No realignment means no entry | Yes | Corrective swing algorithm |
| 16. Corrective swing break | `confirmed` | Entry swing and closed 1M candle | Body closes beyond corrective swing | Wick/open candle does not pass | Yes for swing selection | Marginal close distance |
| 17. Entry | `confirmed` + `unresolved` | All earlier stages approved | `human_validation_required` | No automatic order creation | Yes | Order type, timing, slippage, attempts |
| 18. Stop Loss | `unresolved` | Sweep extreme, 5M HL candidate, entry | Human-selected Stop Loss | Missing/ambiguous SL means `no_trade` | Yes | Contradictory selection rule and thresholds |
| 19. Break Even | `confirmed` mechanism + `candidate` universality | Post-entry 1M swing and closed candle | SL moved to entry after break and close | Not evaluated before entry | Yes for post-entry swing | Costs, applicability, later management |
| 20. Take Profit | `unresolved` | Liquidity targets and position state | `human_validation_required` | No automatic target selection | Yes | General target, priority, ratio, partials |
| 21. Latest entry | `confirmed` | Clock and timezone configuration | `failed` for new entry after 11:30 | New entries prohibited | Yes while timezone is `null` | Open-position management |
| 22. Risk controls | `confirmed` max trade risk + open controls | Entry, Stop Loss, sizing inputs | At most 1% risk after human validation | Missing sizing/control data blocks execution | Yes | Position size, daily limit, kill switch |

## 8. Sell workflow

Solo se incluyen relaciones direccionales expresamente documentadas. No se completan objetivos, Stop Loss ni gestión por simetría automática.

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation requirement | Open variables |
|---:|---|---|---|---|---|---|
| 1. 4H context | `confirmed` + `human_validation_required` | 4H candles | Context reviewed | Blocks all lower stages | Yes | Structural swing algorithm |
| 2. Break/Wick/Fake | `confirmed` conceptually | 4H context and relevant level | One classification recorded | Unclassified context blocks | Yes for Wick/Fake and marginal Break | Exact Wick/Fake criteria, tolerances |
| 3. Relevant liquidity | `confirmed` + `human_validation_required` | Context and marked levels | Levels recorded | Missing levels block sweep evaluation | Yes | Internal/external and structural priority |
| 4. Asia High/Low | `confirmed` | Session candles | Both levels recorded | Missing data returns `data_unavailable` | No after session boundary known | Timezone/session boundary |
| 5. London High/Low | `confirmed` | Session candles | Both levels recorded | Missing data returns `data_unavailable` | No after session boundary known | Timezone/session boundary |
| 6. Preparation | `candidate` + `confirmed` intent | Context and levels before operations | Preparation recorded | Incomplete preparation blocks entry | Yes | Exact analysis-start time |
| 7. Entry start | `confirmed` | Clock and timezone configuration | `waiting` before 08:30 | Entry prohibited before start | Yes while timezone is `null` | Timezone and DST |
| 8. Liquidity sweep | `confirmed` conceptually | Marked high/low and price | Human-approved sweep | Without sweep, remain `waiting` | Yes | Penetration, rejection, pre-08:30 validity |
| 9. Move to 5M | `confirmed` | Approved sweep, 5M data | 5M review enabled | Blocks inversion without sweep | No | Data alignment |
| 10. 5M inversion OR | `confirmed` | 5M structure/FVG evidence | Traditional change `OR` IFVG validated | Without either, `waiting` | Yes | Swing algorithm and IFVG geometry |
| 11. 5M close | `confirmed` | Bullish-structure swing and 5M close | Candle body closes beyond relevant swing | Wick alone is `failed`; open candle is `waiting` | Yes for swing selection/marginal close | Pivot and minimum distance |
| 12. Continuation FVG | `confirmed` + `human_validation_required` | Three 5M candles after inversion | FVG favors new sell-side move and is clear | Missing/weak FVG means `no_trade` | Yes | Minimum size and quality threshold |
| 13. Move to 1M | `confirmed` | Approved FVG, 1M data | 1M review enabled | Blocks entry if data absent | No | Data alignment |
| 14. Corrective retracement | `confirmed` + `human_validation_required` | 1M candles against new 5M move | Retracement identified | No retracement means `waiting` | Yes | Correction boundaries |
| 15. 1M realignment | `confirmed` + `human_validation_required` | Corrective microstructure | Candidate sell realignment identified | No realignment means no entry | Yes | Corrective swing algorithm |
| 16. Corrective swing break | `confirmed` | Entry swing and closed 1M candle | Body closes beyond corrective swing | Wick/open candle does not pass | Yes for swing selection | Marginal close distance |
| 17. Entry | `confirmed` + `unresolved` | All earlier stages approved | `human_validation_required` | No automatic order creation | Yes | Order type, timing, slippage, attempts |
| 18. Stop Loss | `unresolved` | Sweep extreme, 5M LH candidate, entry | Human-selected Stop Loss | Missing/ambiguous SL means `no_trade` | Yes | Contradictory selection rule and thresholds |
| 19. Break Even | `confirmed` mechanism + `candidate` universality | Post-entry 1M swing and closed candle | SL moved to entry after break and close | Not evaluated before entry | Yes for post-entry swing | Costs, applicability, later management |
| 20. Take Profit | `unresolved` | Liquidity targets and position state | `human_validation_required` | No automatic target selection | Yes | General target, priority, ratio, partials |
| 21. Latest entry | `confirmed` | Clock and timezone configuration | `failed` for new entry after 11:30 | New entries prohibited | Yes while timezone is `null` | Open-position management |
| 22. Risk controls | `confirmed` max trade risk + open controls | Entry, Stop Loss, sizing inputs | At most 1% risk after human validation | Missing sizing/control data blocks execution | Yes | Position size, daily limit, kill switch |

## 9. 4H context

Analysis begins on 4H and establishes daily macro context.

- `Break` — `confirmed`: the candle body must close beyond the relevant structural level; a wick without close is not a Break. Swing selection, structural algorithm, candle count, tolerances and marginal closes are unresolved.
- `Wick` — conceptually `confirmed`, operationally `human_validation_required`: price interacts with or exceeds the level by wick and must not automatically be called Break. Wick versus sweep, Wickfill, fill amount, effect and invalidation remain open.
- `Fake` — conceptually `confirmed`, operationally `human_validation_required`: price attempts to break and returns to the prior range. Re-entry timeframe/close, distance, allowed candles, distinction from sweep and bias effect remain open.

These concepts must not be collapsed into one body-close rule.

## 10. Liquidity

Mark Asia High, Asia Low, London High and London Low. A liquidity level must be taken before searching for 5M inversion; the sweep alone is never an entry.

Priority between sessions and 1H/4H structure, reuse after sweep, multiple sweeps, internal/external liquidity, equal levels, prior-day levels and pre-08:30 sweeps are unresolved. A wick as sweep alert is `candidate`; penetration and confirmation remain `human_validation_required`.

## 11. Operating schedule

```yaml
entry_start_time: "08:30"
latest_new_entry_time: "11:30"
timezone: null
daylight_saving_rule: null
```

Pre-analysis around 08:00 is `candidate`; preparing context and levels before operations is supported. No entry before 08:30 and no new entry after 11:30 are `confirmed`. Country/market reference, DST, allowed days, holidays, early closes, low-liquidity sessions and management after 11:30 remain unresolved. `America/New_York` must not be inferred.

## 12. 5M inversion

Two alternatives are valid with `OR`: traditional structural change or IFVG. Both are not required.

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

Status: `unresolved`; `human_validation_required: true`.

```yaml
possible_references:
  - liquidity_sweep_extreme
  - structural_5m_hl_or_lh
selection_rule: null
sweep_size_threshold_points: null
buffer_points: null
status: unresolved
human_validation_required: true
```

Contradictory reports remain unresolved:

- Version A: wide sweep → sweep wick; short sweep → 5M HL/LH.
- Version B: wide sweep → 5M HL/LH; short sweep → sweep wick.

Neither version is selected. Body/wick treatment, spread, slippage, maximum Stop Loss, oversized-stop behavior, pre-entry invalidation and risk reduction are open. This contradiction blocks fully automatic demo execution.

## 16. Break Even

The entry swing and Break Even swing are different. After entry, identify the first relevant 1M swing. Break Even activates after a 1M candle breaks and closes beyond that post-entry swing; Stop Loss then moves to entry.

An earlier explanation used “touch”; the later “break and close” explanation is treated as more specific. `mandatory_for_every_trade: candidate`. Swing algorithm, multiple swings, universality, commissions, spread, exact entry/cost basis, pre-confirmation retracement and subsequent management remain open.

## 17. Take Profit

```yaml
general_target_rule: null
target_priority: null
fixed_risk_reward: null
partials: null
status: unresolved
```

Asia Low, opposite session extremes and important liquidity appeared only as `context_specific` targets. Always targeting Asia/London/opposite extreme, fixed ratio, partials, trailing, manual close and 1H/4H objectives are not confirmed.

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

Return `waiting` before 08:30, before liquidity is taken, while inversion or candle close is pending, without a continuation FVG, during the 1M correction, before realignment, or while required post-entry management evidence has not formed. Return `data_unavailable` for missing candles, session boundaries or timestamps. Return `human_validation_required` at subjective or contradictory gates; later stages remain blocked.

## 22. No-trade conditions

Confirmed:

- No entry without a liquidity sweep.
- No entry without confirmed 5M inversion.
- A wick is not a 5M structural change.
- No trade without a continuation 5M FVG.
- No trade when FVG strength is insufficient according to human review.
- No entry without 1M retracement and realignment.
- No entry before 08:30.
- No new entry after 11:30.

FVG size/quality, displacement, Wick/Fake, Stop Loss selection and target availability require human validation. News, oversized stops, target distance, reentries, operation counts, daily limit, data quality, lateral markets and holidays remain unresolved.

## 23. Human-validation points

Human validation is required for 4H swings, Wick/Fake, liquidity selection and sweep, timezone interpretation, 5M pivots, IFVG, FVG quality, 1M correction/swing, order mechanics, Stop Loss, Break Even swing, Take Profit, sizing, daily limit, news and reentries.

## 24. Automation readiness

The evidence supports assisted analysis, manual backtesting and supervised paper trading. Semi-automatic backtesting remains partial. Subjective FVG quality, swing algorithms, null timezone, contradictory Stop Loss, unresolved Take Profit/news/reentries and incomplete risk controls prohibit fully automatic backtesting and autonomous execution. Critical timestamps require manual comparison with the original video before supervised demo execution.

## 25. Traceability requirements

Record at minimum: `Strategy`, `Strategy version`, `Instrument`, `Data source`, `Timestamp`, `Timezone`, `Market snapshot`, `Rules evaluated`, `Rule statuses`, `Evaluation results`, `Evidence`, `Evidence timestamp`, `Final verdict`, `Human validations`, `Entry`, `Stop Loss`, `Take Profit`, `Result`, `Failure classification` and `Trace identifier`. Preserve candle-close ordering and exclude future data.
