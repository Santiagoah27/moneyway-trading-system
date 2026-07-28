# MoneyWay Forex strategy specification

## 1. Metadata

| Field | Value |
|---|---|
| Strategy | MoneyWay Forex |
| Specification version | `forex-0.1.0-draft` |
| Status | draft |
| Source material | Análisis auditado de videos teóricos y prácticos de la mentoría MoneyWay |
| Automation approval | Not approved |

## 2. Purpose and scope

Esta especificación documenta el flujo observado para análisis asistido y evaluación histórica. No define algoritmos ausentes, no autoriza ejecución autónoma y no cubre MoneyWay Nasdaq.

## 3. Evidence policy

Cada regla usa uno de estos estados: `confirmed`, `candidate`, `context_specific`, `visual_only`, `human_validation_required`, `unresolved` o `rejected_ai_inference`. Una observación subjetiva no se convierte en condición automática. Los detalles ausentes permanecen como `null`, `unresolved` o `human_validation_required`.

Los resultados de evaluación permitidos son `passed`, `failed`, `waiting`, `not_applicable`, `human_validation_required` y `data_unavailable`. El veredicto general es `ready`, `wait`, `no_trade`, `human_validation_required` o `data_unavailable`.

## 4. Supported operating modes

| Mode | Readiness |
|---|---|
| Assisted analysis | Sufficient |
| Manual backtesting | Sufficient |
| Semi-automatic backtesting | Partially sufficient; human validation remains mandatory |
| Fully automatic backtesting | Not sufficient |
| Autonomous execution | Not sufficient and prohibited |
| Paper trading / supervised demo | Future testing only, after risk and execution gaps are resolved |

## 5. High-level sequence

1. Determine weekly direction or context.
2. Identify a relevant weekly zone.
3. Wait for price interaction with that zone.
4. Review the daily timeframe.
5. Confirm daily alignment with weekly context.
6. Find one allowed 4H pattern (`OR`).
7. Wait for pattern completion.
8. Wait for breakout of the relevant zone or level.
9. Do not enter on the breakout candle.
10. Wait for the retest.
11. Find an entry signal on 2H, 1H or 30M (`OR`).
12. Accept one valid signal from any allowed entry timeframe; no priority is defined.
13. Wait for the signal candle close.
14. Enter only after confirmation; exact order mechanics are unresolved.
15. Place Stop Loss using 4H structure.
16. Use 2:1 as the reference Take Profit ratio; its mandatory character is unresolved.

The sequence is blocking: a later mandatory condition cannot be `passed` while an earlier one is `waiting`, `failed` or `data_unavailable`. A critical subjective step returns `human_validation_required`, never automatic approval.

## 6. Buy workflow

`Inputs` are required evidence; unavailable inputs produce `data_unavailable`. `Open variables` are never filled by inference.

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation | Open variables |
|---:|---|---|---|---|---|---|
| 1. Weekly context | `confirmed` + `human_validation_required` | Weekly candles and direction assessment | `passed` only after assessment | Blocks lower timeframes | Yes | Exact trend and range definitions |
| 2. Weekly zone | `human_validation_required` | Weekly candles and selected zone | Human-approved zone or `waiting` | Blocks zone interaction | Yes | Bodies/wicks, candle count, width, buffer |
| 3. Zone interaction | `confirmed` + `unresolved` | Price and approved zone | `passed`, `waiting` or `data_unavailable` | Blocks daily review until interaction | Yes | Entry tolerance |
| 4. Daily review | `confirmed` | Daily candles after interaction | Review recorded | Blocks alignment decision | No for sequencing | Required daily close |
| 5. Daily alignment | `human_validation_required` | Weekly assessment and daily structure | Human-approved `passed` or `failed` | `failed` means `no_trade` | Yes | Mathematical alignment, lateral structure |
| 6. Allowed 4H pattern | `confirmed` + `visual_only` | 4H candles | One of six groups selected (`OR`) | Absence means `waiting` | Yes | Geometry and tolerances |
| 7. Pattern completion | `confirmed` + `human_validation_required` | Selected pattern and later 4H candles | `passed` only when complete | Incomplete pattern blocks breakout evaluation | Yes | Completion and invalidation geometry |
| 8. Breakout | `confirmed` + `unresolved` | Relevant level/zone and price action | `passed`, `waiting` or `human_validation_required` | Blocks retest | Yes | Close/wick, timeframe, penetration |
| 9. Breakout-candle exclusion | `confirmed` | Breakout candle state | `waiting` during that candle | Entry prohibited on breakout candle | No | Candle boundary follows unresolved breakout timeframe |
| 10. Retest | `confirmed` + `unresolved` | Approved breakout and subsequent price action | `passed`, `waiting` or `human_validation_required` | Blocks entry-signal search | Yes | Tolerance, count, timeout, invalidation |
| 11. Entry signal search | `confirmed` + `human_validation_required` | 2H, 1H and/or 30M closed candles | One valid timeframe may pass (`OR`) | No signal means `waiting` | Yes | Signal geometry and contradictions |
| 12. Timeframe OR | `confirmed` | Validated 2H/1H/30M signals | `passed` when any one is valid | No automatic priority | Yes if signals conflict | Priority and conflict handling |
| 13. Signal close | `confirmed` | Signal candle close timestamp | `waiting` until close, then evaluable | Entry before close prohibited | No | Exact feed/session boundaries |
| 14. Entry | `confirmed` + `unresolved` | Completed prior steps and closed signal | `human_validation_required` | No order may be created automatically | Yes | Timing, order type, maximum distance |
| 15. Stop Loss | `confirmed` + `human_validation_required` | 4H structure and structural Higher Low | Human-approved level | Missing Stop Loss means `no_trade` | Yes | Swing choice, body/wick, buffer, spread, management |
| 16. Take Profit | `confirmed` + `unresolved` | Entry, Stop Loss and risk distance | 2:1 reference recorded | Exact exit plan requires validation | Yes | Fixed/minimum, extensions, partials, opposing zone |

## 7. Sell workflow

Solo se documentan elementos confirmados sin completar reglas por simetría. En particular, no se infiere una prohibición automática de venta en la parte baja de un rango.

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation | Open variables |
|---:|---|---|---|---|---|---|
| 1. Weekly context | `confirmed` + `human_validation_required` | Weekly candles and direction assessment | `passed` only after assessment | Blocks lower timeframes | Yes | Exact trend and range definitions |
| 2. Weekly zone | `human_validation_required` | Weekly candles and selected zone | Human-approved zone or `waiting` | Blocks zone interaction | Yes | Bodies/wicks, candle count, width, buffer |
| 3. Zone interaction | `confirmed` + `unresolved` | Price and approved zone | `passed`, `waiting` or `data_unavailable` | Blocks daily review until interaction | Yes | Entry tolerance |
| 4. Daily review | `confirmed` | Daily candles after interaction | Review recorded | Blocks alignment decision | No for sequencing | Required daily close |
| 5. Daily alignment | `human_validation_required` | Weekly assessment and daily structure | Human-approved `passed` or `failed` | `failed` means `no_trade` | Yes | Mathematical alignment, lateral structure |
| 6. Allowed 4H pattern | `confirmed` + `visual_only` | 4H candles | One of six groups selected (`OR`) | Absence means `waiting` | Yes | Geometry and tolerances |
| 7. Pattern completion | `confirmed` + `human_validation_required` | Selected pattern and later 4H candles | `passed` only when complete | Incomplete pattern blocks breakout evaluation | Yes | Completion and invalidation geometry |
| 8. Breakout | `confirmed` + `unresolved` | Relevant level/zone and price action | `passed`, `waiting` or `human_validation_required` | Blocks retest | Yes | Close/wick, timeframe, penetration |
| 9. Breakout-candle exclusion | `confirmed` | Breakout candle state | `waiting` during that candle | Entry prohibited on breakout candle | No | Candle boundary follows unresolved breakout timeframe |
| 10. Retest | `confirmed` + `unresolved` | Approved breakout and subsequent price action | `passed`, `waiting` or `human_validation_required` | Blocks entry-signal search | Yes | Tolerance, count, timeout, invalidation |
| 11. Entry signal search | `confirmed` + `human_validation_required` | 2H, 1H and/or 30M closed candles | One valid timeframe may pass (`OR`) | No signal means `waiting` | Yes | Signal geometry and contradictions |
| 12. Timeframe OR | `confirmed` | Validated 2H/1H/30M signals | `passed` when any one is valid | No automatic priority | Yes if signals conflict | Priority and conflict handling |
| 13. Signal close | `confirmed` | Signal candle close timestamp | `waiting` until close, then evaluable | Entry before close prohibited | No | Exact feed/session boundaries |
| 14. Entry | `confirmed` + `unresolved` | Completed prior steps and closed signal | `human_validation_required` | No order may be created automatically | Yes | Timing, order type, maximum distance |
| 15. Stop Loss | `confirmed` + `human_validation_required` | 4H structure and structural Lower High | Human-approved level | Missing Stop Loss means `no_trade` | Yes | Swing choice, body/wick, buffer, spread, management |
| 16. Take Profit | `confirmed` + `unresolved` | Entry, Stop Loss and risk distance | 2:1 reference recorded | Exact exit plan requires validation | Yes | Fixed/minimum, extensions, partials, opposing zone |

## 8. Waiting states

Use `wait` when price has not reached the weekly zone, a pattern remains incomplete, breakout or retest has not occurred, no valid signal is present, or the signal candle is still open. Use `data_unavailable` when required market evidence is missing. No later step may bypass either result.

## 9. No-trade conditions

- Weekly and daily contexts are not aligned.
- A mandatory earlier step is `failed`.
- The setup is in an unsuitable macro location, as determined by human review.
- The move is considered too extended or the setup insufficiently mature by human review.
- No Stop Loss is defined.
- A critical rule remains `unresolved` without human validation.
- Required evidence is unavailable.

The USDCHF buy rejection supports macro-location review. A symmetric sell rule is only `candidate`, not confirmed.

## 10. Human-validation points

Human review is required for weekly direction and zones, daily alignment, 4H pattern geometry and completion, breakout, retest, entry-signal geometry, conflicting signals, macro location, maturity, Stop Loss selection and the exact exit plan. EMA 50 has `required: false`, `role: optional_confluence` and `exact_evaluation: unresolved`.

## 11. Risk limitations

Risk per trade, daily limit, maximum trades, consecutive-loss limit, position sizing, re-entry, post-loss management and kill switch are `unresolved`. No Nasdaq risk rule may be reused. Execution cannot proceed without an approved risk policy and Stop Loss.

Break Even is `context_specific`: it appeared in one EURUSD example, but trigger, timeframe, mandatory character, costs and subsequent management are unresolved. CPI and PPI comments are also `context_specific`, not universal no-trade rules.

## 12. Automation readiness

The workflow supports assisted analysis and manual backtesting. Subjective pattern recognition and unresolved thresholds prevent fully automatic backtesting and any autonomous execution. Future tests are restricted to paper trading or supervised demo accounts after approval.

For consistency with the observed MoneyWay charts and closes, the mentorship used the `FOREXCOM` TradingView feed. This is methodological consistency, not a claim that OANDA is universally incorrect. Symbol/provider normalization remains unresolved.

## 13. Traceability requirements

Each assessment must record: `Strategy`, `Strategy version`, `Instrument`, `Data source`, `Timestamp`, `Timezone`, `Market snapshot`, `Rules evaluated`, `Rule statuses`, `Evaluation results`, `Evidence`, `Final verdict`, `Human validations`, `Entry`, `Stop Loss`, `Take Profit`, `Result`, `Failure classification` and `Trace identifier`. Evidence must preserve candle-close ordering and prevent future-data leakage.
