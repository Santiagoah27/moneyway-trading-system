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

La documentación representa la evidencia actualmente auditada del video de 58:24, consolidada desde cinco intervalos y re-verificaciones humanas directas de los videos fuente. Estas re-verificaciones confirmaron la secuencia de seis etapas, 08:00/08:30/11:30 en hora Colombia, las sesiones Asia/London y sus extremos 1H, la guía de Stop Loss 5M HL/LH, los targets en important highs/lows y los conceptos estructurales 4H descritos en la sección 9. Una revisión humana del segundo video aclaró la confirmación retrospectiva y selección visual de puntos estructurales aproximadamente en `03:05–04:55` y `06:20–07:35`, el uso de cuerpos aproximadamente en `11:10–11:45` y el filtro higher-timeframe aproximadamente en `08:35–09:20`. La revisión humana posterior, especialmente de Video 3, confirmó el workflow end-to-end como una secuencia de gates obligatorios y aclaró la separación entre 4H context, session liquidity, 5M trigger, 5M FVG confirmation, 1M realignment y management. Los timestamps 4H suministrados son aproximados; no se inventan título, URL ni líneas de transcript. Salvo esos intervalos aproximados y `50:15` para riesgo máximo por operación, los timestamps de reglas individuales siguen siendo `null` cuando no fueron proporcionados.

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

1. At 08:00 `America/Bogota`, use the closed 4H candle to reconstruct the current HH/HL or LL/LH context from a human-selected major visible extreme, classify Breakout `OR` Wickfill `OR` Fakeout and determine the direction permitted for the subsequent setup.
2. Separately mark Asia High/Low, London High/Low and relevant 1H/4H structural liquidity references. Session extrema do not initialize 4H market structure.
3. From 08:30, wait for price to exceed a directionally relevant marked High or go below a directionally relevant marked Low. This liquidity take is mandatory.
4. Only after Step 3, move to 5M and require Structural Change/ChoCH/MSS `OR` IFVG, whichever occurs first.
5. After the Step-4 trigger, separately require the displacement in the intended direction to leave the mandatory 5M FVG confirmation. A Step-4 IFVG does not automatically satisfy Step 5.
6. Only after Step 5, move to 1M, require a counter-direction pullback toward/into the relevant 5M FVG and then require structural realignment with the intended direction before entry eligibility.

Trading starts at 08:30 and ends at 11:30, always in `America/Bogota`. Entry order mechanics remain unresolved. For buys, the Stop Loss anchor is beyond the wick of the latest relevant structural 5M HL and target concepts include relevant upside Asia/London highs. For sells, the Stop Loss anchor is beyond the wick of the latest relevant structural 5M LH and target concepts include relevant downside Asia/London lows. When price reaches/touches the first important favorable liquidity target, the confirmed Break-Even concept moves Stop Loss to entry. Exact structural detection, offsets, target priority and Break-Even trigger geometry remain unresolved and require human validation.

The workflow is strictly sequential. A downstream signal observed in isolation is not valid for this strategy unless every mandatory prerequisite occurred in chronological order. This dependency statement does not itself assign `passed`, `failed`, `waiting` or `not_applicable`; runtime status mapping requires a separate audit.

### 6.1 Workflow dependency matrix

| Gate | Prerequisite | Dependency semantics |
|---|---|---|
| Step 1: 4H context | Information observable through the 08:00 closed 4H candle | Establishes context and permitted direction through human validation |
| Step 2: liquidity marking | Preparation workflow and required session/structural inputs | Produces liquidity references; does not seed 4H structure |
| Step 3: liquidity take | A relevant Step-2 level and the 08:30 operational start | Enables Step 4 only after the relevant High is exceeded or Low is broken below |
| Step 4: 5M trigger | Valid Step-3 liquidity take | Structural Change `OR` IFVG, whichever occurs first |
| Step 5: 5M FVG confirmation | Valid Step-4 trigger | Separate mandatory directional FVG gate |
| Step 6: 1M pullback and realignment | Valid Step-5 FVG | Countertrend pullback into/toward the FVG, followed by intended-direction realignment |
| Entry concept | All mandatory preceding gates | Eligibility only; no order type or execution is defined |

At `StrategyReplayContext.AsOfUtc`, only prerequisites and confirming events observable at or before that timestamp may participate. A future liquidity take cannot activate an earlier Step 4, a future 5M FVG cannot activate an earlier Step 6, and a future 1M realignment cannot create past entry eligibility. Backtesting must not find a later successful pattern and retroactively assume its earlier gates were valid.

## 7. Buy workflow

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation requirement | Open variables |
|---:|---|---|---|---|---|---|
| 1. 4H HH/LL context | `confirmed` + `human_validation_required` | Closed 4H candles | Trend, active structural pair, scenario and permitted direction reviewed | Blocks all lower stages | Yes | Visual bootstrap, exact body coordinate, retracement boundaries |
| 2. Breakout/Wickfill/Fakeout | `confirmed` conceptually | 4H context and relevant level | One classification recorded | Unclassified context blocks | Yes | Exact geometries and tolerances |
| 3. Relevant liquidity | `confirmed` + `human_validation_required` | Session levels and 1H/4H context | Asia/London extrema and structural coincidences recorded separately from 4H bootstrap | Missing levels block sweep evaluation | Yes | Structural-point algorithm, coincidence tolerance and priority |
| 4. Asia High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the Asia interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 5. London High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the London interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 6. Preparation | `confirmed` | Clock, context and levels | Preparation begins at 08:00 `America/Bogota` | Incomplete preparation blocks entry | Yes for analysis content | Calendar eligibility |
| 7. Trading-window start | `confirmed` | Clock in `America/Bogota` | `waiting` before 08:30 | Entry prohibited before start | No for timezone/DST | None for timezone/DST |
| 8. Liquidity sweep | `confirmed` conceptually | Directionally relevant marked high/low and price | Price exceeds the selected High or goes below the selected Low; remaining details human-approved | Without the mandatory take, no downstream setup is valid | Yes | Level priority, rejection, pre-08:30 validity |
| 9. Move to 5M | `confirmed` | Approved sweep, 5M data | 5M review enabled | Blocks inversion without sweep | No | Data alignment |
| 10. 5M inversion OR | `confirmed` | Approved liquidity take and 5M structure/FVG evidence | First occurring Structural Change/ChoCH/MSS `OR` IFVG validated | Cannot precede the liquidity take and does not satisfy continuation FVG | Yes | Swing algorithm, strong/decisive threshold and IFVG geometry |
| 11. 5M close | `confirmed` | 5M candle close | Close beyond required level validated | Wick alone is `failed`; open candle is `waiting` | Yes for marginal close | Minimum distance |
| 12. Continuation FVG | `confirmed` + `human_validation_required` | Three 5M candles after the Step-4 trigger | Separate directional, clear FVG validated | Without this mandatory gate, Step 6 is not enabled | Yes | Minimum size, quality and lifecycle |
| 13. Move to 1M | `confirmed` | Approved FVG, 1M data | 1M review enabled | Blocks entry if data absent | No | Data alignment |
| 14. Corrective retracement | `confirmed` + `human_validation_required` | Approved Step-5 FVG and countertrend 1M candles | Pullback toward/into the relevant 5M FVG identified | Without the pullback/FVG interaction, no realignment gate | Yes | Interaction depth, correction boundaries and timeout |
| 15. 1M realignment | `confirmed` + `human_validation_required` | Corrective microstructure | Candidate realignment identified | No realignment means no entry | Yes | Corrective swing algorithm |
| 16. Corrective swing break | `confirmed` | Entry swing and closed 1M candle | Body closes beyond corrective swing | Wick/open candle does not pass | Yes for swing selection | Marginal close distance |
| 17. Entry | `confirmed` + `unresolved` | All earlier stages approved | `human_validation_required` | No automatic order creation | Yes | Order type, timing, slippage, attempts |
| 18. Stop Loss | `confirmed` conceptually + `human_validation_required` | Latest relevant structural 5M HL and entry | Human validates SL below/beyond the HL wick | Missing/ambiguous structural SL blocks management automation | Yes | HL algorithm, exact offset, spread and costs |
| 19. Break Even | `confirmed` conceptually + `human_validation_required` | Entry and first important favorable upside liquidity target | On source-confirmed reach/touch, SL moves to entry | Not evaluated before entry or target interaction | Yes | First-important selection, touch semantics, costs and later management |
| 20. Take Profit | `confirmed` conceptually + `human_validation_required` | Relevant upside liquidity highs and position state | Important target, including relevant Asia/London High, selected | No automatic target selection | Yes | Priority among candidates, ratio and partials |
| 21. Trading-window end | `confirmed` | Clock in `America/Bogota` | `failed` for new entry after 11:30 | New entries prohibited | No for timezone/DST | Open-position management |
| 22. Risk controls | `confirmed` max trade risk + open controls | Entry, Stop Loss, sizing inputs | At most 1% risk after human validation | Missing sizing/control data blocks execution | Yes | Position size, daily limit, kill switch |

## 8. Sell workflow

Solo se incluyen relaciones direccionales expresamente documentadas. No se completan objetivos, Stop Loss ni gestión por simetría automática.

| Step | Rule status | Required inputs | Evaluation result | Blocking behavior | Human validation requirement | Open variables |
|---:|---|---|---|---|---|---|
| 1. 4H HH/LL context | `confirmed` + `human_validation_required` | Closed 4H candles | Trend, active structural pair, scenario and permitted direction reviewed | Blocks all lower stages | Yes | Visual bootstrap, exact body coordinate, retracement boundaries |
| 2. Breakout/Wickfill/Fakeout | `confirmed` conceptually | 4H context and relevant level | One classification recorded | Unclassified context blocks | Yes | Exact geometries and tolerances |
| 3. Relevant liquidity | `confirmed` + `human_validation_required` | Session levels and 1H/4H context | Asia/London extrema and structural coincidences recorded separately from 4H bootstrap | Missing levels block sweep evaluation | Yes | Structural-point algorithm, coincidence tolerance and priority |
| 4. Asia High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the Asia interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 5. London High/Low | `confirmed` | Completed 1H candles with local `OpenTime` in the London interval | Maximum `High` and minimum `Low` recorded | Missing required data returns `data_unavailable` | No | Data completeness policy for non-nominal sessions |
| 6. Preparation | `confirmed` | Clock, context and levels | Preparation begins at 08:00 `America/Bogota` | Incomplete preparation blocks entry | Yes for analysis content | Calendar eligibility |
| 7. Trading-window start | `confirmed` | Clock in `America/Bogota` | `waiting` before 08:30 | Entry prohibited before start | No for timezone/DST | None for timezone/DST |
| 8. Liquidity sweep | `confirmed` conceptually | Directionally relevant marked high/low and price | Price exceeds the selected High or goes below the selected Low; remaining details human-approved | Without the mandatory take, no downstream setup is valid | Yes | Level priority, rejection, pre-08:30 validity |
| 9. Move to 5M | `confirmed` | Approved sweep, 5M data | 5M review enabled | Blocks inversion without sweep | No | Data alignment |
| 10. 5M inversion OR | `confirmed` | Approved liquidity take and 5M structure/FVG evidence | First occurring Structural Change/ChoCH/MSS `OR` IFVG validated | Cannot precede the liquidity take and does not satisfy continuation FVG | Yes | Swing algorithm, strong/decisive threshold and IFVG geometry |
| 11. 5M close | `confirmed` | Bullish-structure swing and 5M close | Candle body closes beyond relevant swing | Wick alone is `failed`; open candle is `waiting` | Yes for swing selection/marginal close | Pivot and minimum distance |
| 12. Continuation FVG | `confirmed` + `human_validation_required` | Three 5M candles after the Step-4 trigger | Separate clear FVG favors the new sell-side move | Without this mandatory gate, Step 6 is not enabled | Yes | Minimum size, quality and lifecycle |
| 13. Move to 1M | `confirmed` | Approved FVG, 1M data | 1M review enabled | Blocks entry if data absent | No | Data alignment |
| 14. Corrective retracement | `confirmed` + `human_validation_required` | Approved Step-5 FVG and countertrend 1M candles | Pullback toward/into the relevant 5M FVG identified | Without the pullback/FVG interaction, no realignment gate | Yes | Interaction depth, correction boundaries and timeout |
| 15. 1M realignment | `confirmed` + `human_validation_required` | Corrective microstructure | Candidate sell realignment identified | No realignment means no entry | Yes | Corrective swing algorithm |
| 16. Corrective swing break | `confirmed` | Entry swing and closed 1M candle | Body closes beyond corrective swing | Wick/open candle does not pass | Yes for swing selection | Marginal close distance |
| 17. Entry | `confirmed` + `unresolved` | All earlier stages approved | `human_validation_required` | No automatic order creation | Yes | Order type, timing, slippage, attempts |
| 18. Stop Loss | `confirmed` conceptually + `human_validation_required` | Latest relevant structural 5M LH and entry | Human validates SL above/beyond the LH wick | Missing/ambiguous structural SL blocks management automation | Yes | LH algorithm, exact offset, spread and costs |
| 19. Break Even | `confirmed` conceptually + `human_validation_required` | Entry and first important favorable downside liquidity target | On source-confirmed reach/touch, SL moves to entry | Not evaluated before entry or target interaction | Yes | First-important selection, touch semantics, costs and later management |
| 20. Take Profit | `confirmed` conceptually + `human_validation_required` | Relevant downside liquidity lows and position state | Important target, including relevant Asia/London Low, selected | No automatic target selection | Yes | Priority among candidates, ratio and partials |
| 21. Trading-window end | `confirmed` | Clock in `America/Bogota` | `failed` for new entry after 11:30 | New entries prohibited | No for timezone/DST | Open-position management |
| 22. Risk controls | `confirmed` max trade risk + open controls | Entry, Stop Loss, sizing inputs | At most 1% risk after human validation | Missing sizing/control data blocks execution | Yes | Position size, daily limit, kill switch |

## 9. 4H context

Preparation begins at 08:00 `America/Bogota`, coinciding with the close of the 4H candle used for the review. Only information observable through that closed candle may be used. The mentor reviews 4H to read the structure already developed, determine the direction permitted for the later setup and filter lower-timeframe oscillations/noise; 1M/5M internal movement does not independently create 4H structural points.

The mentor visually/contextually starts from the major visible extreme that originated the current relevant movement and reconstructs the recent push, retracement, structural break, next push and current structure. Asia/London session highs and lows are not used to initialize this 4H structure. No rule defines a deterministic viewport or initializes from the first candle, first two candles, fixed lookback or artificial initial H/L; this remains a human visual bootstrap.

### 9.1 Confirmed structural concepts

- Bullish confirmation progresses as `prior structural High → relevant retracement candidate → subsequent body-close break above that High/new HH → prior relevant retracement becomes confirmed HL`.
- Bearish confirmation progresses symmetrically as `prior structural Low → relevant retracement candidate → subsequent body-close break below that Low/new LL → prior relevant retracement becomes confirmed LH`.
- `HH` is a structural High above the previous structural High. A wick above is insufficient: `Candle.Close > previous structural High` confirms the bullish structural break and the new HH only after that candle closes.
- `LL` is a structural Low below the previous structural Low. A wick below is insufficient: `Candle.Close < previous structural Low` confirms the bearish structural break and the new LL only after that candle closes.
- After an HH, the relevant low of the slowdown/retracement is provisional. It becomes a confirmed `HL` only when a later 4H candle closes above the previous HH and forms the next HH; the confirmed HL is above the corresponding prior structural Low.
- After an LL, the relevant high of the slowdown/retracement is provisional. It becomes a confirmed `LH` only when a later 4H candle closes below the previous LL and forms the next LL; the confirmed LH is below the corresponding prior structural High.
- Minor fluctuations, pauses, local extrema and direction changes inside the retracement do not automatically become structural points.
- After the confirming break, the mentor looks backward to the retracement preceding the breakout impulse and identifies the relevant turning extreme where that impulse originated. Human source evidence describes it as **"el punto exacto donde el precio desaceleró y rebotó"**.
- In bullish context, the visual selection is the lowest relevant point of the retracement/contraction associated with that turn; minor intermediate pauses are discarded. The bearish relationship remains symmetric for the relevant high preceding the subsequent LL break.
- Structural points and relevant turning/stop zones are marked on candle bodies. An isolated wick neither defines strong structural confirmation nor replaces the required body-close break.
- After the recent structure is reconstructed, the active current pair is the latest confirmed HH and HL in bullish context, or the latest confirmed LL and LH in bearish context. Older points remain in the historical audit trail even when they are less relevant to the current decision.

The source now materially clarifies how a retracement candidate is confirmed and selected retrospectively after an already-supplied structural level is broken. It does not define the exact boundaries of the relevant retracement interval or the exact OHLC/body coordinate that numerically represents a structural High/Low or turning point. No `Min(Candle.Low)`, `Min(Open, Close)`, `Max(Candle.High)`, `Max(Open, Close)` or equivalent formula is inferred.

“Price slows down” remains a visual source criterion. It does not define candle count, minimum retracement, pivot width, percentage, ATR or candle-size thresholds. The source also does not define how to obtain the initial previous structural High/Low from an arbitrary raw 4H series without a human annotation or pre-seeded level. Full structural detection therefore remains `human_validation_required` and blocked for deterministic evaluation.

### 9.2 Retrospective confirmation and historical replay

HL/LH confirmation is retrospective but must not leak future information. At replay time `T1`, before the subsequent HH/LL break candle has closed, the prior retracement remains candidate/unconfirmed and minor fluctuations remain non-structural. From `T2`, the `AsOfUtc` at which the subsequent body-close break confirms the new HH/LL, the earlier relevant retracement may be recorded as the confirmed HL/LH. Historical evaluation at `T1` must not use the `T2` candle, and a future candle must never change what was considered confirmed at an earlier replay timestamp.

Historical automation of `NQ-H4-001` therefore requires reconstruction using only information observable through each `AsOfUtc`. The source defines the causal confirmation order, but not a complete detector: bootstrap of the prior reference level, exact retracement boundaries and exact numeric body coordinates remain unresolved.

### 9.3 Breakout

`Breakout` is conceptually `confirmed`. The reviewed 4H evidence at approximately `02:20–02:40` requires a candle-body close beyond the relevant prior structural level or zone; a wick alone does not confirm it. For a bullish context, the 4H candle closes above relevant structural resistance, which may be the prior HH. For a bearish context, it closes below relevant structural support.

The exact support/resistance selection, zone construction and width, tolerance, choice among multiple levels and any minimum penetration or confirming-candle count remain unresolved. Consequently, the complete Breakout detector is not deterministic.

### 9.4 Wickfill

`Wickfill` is conceptually `confirmed` and operationally `human_validation_required`. At approximately `04:23–04:32`, the mentor describes it as filling the wick/tail. The confirmed progression is: a Breakout has occurred, the extension leaves a wick/extreme, price moves away or retraces, and a later impulse seeks to travel through that space again and reach or exceed the earlier wick extreme. The example at approximately `06:55–07:25` shows a retracement, ideally toward support/old resistance, followed by that renewed impulse.

The source does not yet establish whether touching or exceeding the extreme completes Wickfill, whether a close is required, which wick applies when several exist, the exact retracement requirement, the support/resistance selection or tolerance. No exact comparison is inferred from “reach or exceed.”

### 9.5 Fakeout

`Fakeout` is conceptually `confirmed` and operationally `human_validation_required`. At approximately `10:40`, it is described as a Breakout that did not complete. The reviewed example at approximately `10:55–11:25` confirms this progression: price exits a relevant support, resistance, range or structural zone; the break fails; price returns; and a new candle closes back inside the relevant zone. A wick returning inside is insufficient when no candle has closed back inside.

The allowed time/candle count, penetration tolerance, exact zone width and selection, which returning candle applies, numeric invalidation and effect on bias remain unresolved.

### 9.6 Context relationship and implementation boundary

At the 08:00 review, `Breakout`, `Wickfill` and `Fakeout` are three alternative current context/scenario classifications: `Breakout OR Wickfill OR Fakeout`, not an `AND` gate. They can nevertheless have causal history: Wickfill may follow an extended Breakout, while Fakeout is a failed Breakout that closes back inside structure. The source does not define formal mutual exclusivity or precedence when visual conditions appear to coexist, so classification remains human-validated.

These structural and context relationships are confirmed strategy concepts, not a complete swing, pivot or scenario-classification engine. No fractal, fixed pivot width, ZigZag, percentage/ATR swing, point threshold, nearest local extreme or fixed lookback is part of the audited rule.

## 10. Liquidity

Mark Asia High, Asia Low, London High and London Low. Also review structural points on 1H/4H, especially when they coincide with those session extrema. These horizontal liquidity references are a separate step and do not bootstrap 4H HH/HL/LL/LH structure. Coincidence may make a level more relevant in the mentor's reading, but no automatic hierarchy is defined. A liquidity level must be taken before searching for 5M inversion; the take alone is never an entry.

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

These definitions make `NQ-LIQ-001` deterministically specified; this documentation change does not alter its existing runtime evaluator. Structural-point detection, coincidence tolerance, liquidity priority, reuse after sweep, multiple sweeps, internal/external liquidity, equal levels, prior-day levels and pre-08:30 sweeps remain unresolved or require human validation. The confirmed high-level take condition is only that price exceeds an identified High or goes below an identified Low; no extra penetration threshold, point/tick count, close-back, rejection, displacement or tolerance is inferred.

Directional source examples support waiting for downside liquidity such as an Asia/London Low before bullish continuation, and upside liquidity such as an Asia/London High before bearish continuation. They do not define automatic priority among Asia, London or structural liquidity references.

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

Only after the required liquidity take, two Step-4 alternatives are valid with `OR`: traditional Structural Change/ChoCH/MSS or IFVG, whichever occurs first. Both alternatives are not required and neither has a fixed priority. The extreme left by the liquidity-taking event can serve as the first 5M High/Low reference from which the mentor begins reading subsequent 5M structure; this observation does not define a complete 5M structural detector and does not solve 4H bootstrap.

- Traditional change — for buys, break bearish structure; for sells, break bullish structure. A strong/decisive 5M candle-body close beyond the relevant prior swing is required. A wick does not confirm. “Strong/decisive,” pivot selection and marginal-close thresholds require human validation and have no numeric threshold.
- IFVG — conceptually `confirmed`: candle-body action invalidating a prior FVG in the opposite direction may replace traditional structural change. Geometry, direction, partial/full close, mitigation, confirming candle, expiry and displacement relation are unresolved. No external IFVG definition applies. A Step-4 IFVG is not a direct entry and does not automatically satisfy Step 5.

## 13. 5M continuation FVG

```yaml
required: true
candle_count: 3
minimum_size_points: null
quality_validation: human_validation_required
```

This is a separate mandatory gate after the Step-4 trigger. The displacement responsible for the new direction must leave a 5M FVG favoring that move; without it, Step 6 is not enabled. A Step-4 IFVG does not satisfy this requirement merely by being an IFVG.

The previously audited specification describes the FVG as a three-candle space between the first and third candle wicks. One or two points were described as insufficient evidence of strength, but this qualitative observation does not establish a three-point minimum. Clear/evident size, volatility relation, fills, invalidation, lifespan, multiple-FVG selection and whether any Step-4 IFVG can also form a distinct Step-5 FVG remain open.

## 14. 1M entry

Only after the mandatory Step-5 FVG, move to 1M. Wait for a countertrend retracement toward/into that relevant 5M FVG and then for 1M structural realignment with the intended direction. For sells, the pullback is bullish and the realignment bearish; for buys, the pullback is bearish and the realignment bullish. Reaching the 5M FVG alone does not authorize entry. The last corrective swing must break with a candle-body close in the intended direction; if 1M never realigns, no entry concept becomes eligible.

### Entry swing

The last swing of the corrective 1M structure whose break and close enables entry.

### Earlier post-entry swing observation

Earlier evidence distinguished a post-entry management swing from the corrective entry swing. The new authoritative Break-Even evidence uses first important favorable liquidity instead; whether the earlier swing retains a separate role remains unresolved and it is not the canonical trigger in this specification.

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

Manual source-video re-verification resolves the prior competing interpretation involving the sweep extreme. The confirmed conceptual reference is beyond the wick/tail of the latest relevant structural 5M HL for buys and beyond the wick/tail of the latest relevant structural 5M LH for sells, where the trade idea loses structural meaning.

This reconciliation does not define how to detect HL/LH, select the latest relevant structural swing or translate “beyond the wick” into an executable price. No one-tick, one-point, fixed, percentage, ATR, spread or broker-minimum offset is inferred. Spread, slippage, maximum Stop Loss, oversized-stop behavior, pre-entry invalidation and risk reduction remain open. Human validation is still required and fully automatic demo execution remains blocked.

## 16. Break Even

The newly confirmed management concept uses favorable liquidity rather than treating an isolated post-entry swing as the canonical trigger. For a sell, when price reaches/touches the first important downside liquidity/minimum target, move Stop Loss to entry. For a buy, when price reaches/touches the first important upside liquidity/maximum target, move Stop Loss to entry.

The relationship between this authoritative trigger and the earlier documented post-entry swing break is unresolved; the earlier swing rule must not replace the first-important-liquidity concept automatically. “First important,” reach/touch semantics, wick/body/close requirement, universality, commissions, spread, exact entry/cost basis, BE offset, timing delay, partial close, retained Take Profit and subsequent management remain open. The strategy concept is `confirmed`; a deterministic Break-Even algorithm is not defined.

## 17. Take Profit

```yaml
buy_target_reference: relevant_upside_liquidity_including_asia_or_london_high
sell_target_reference: relevant_downside_liquidity_including_asia_or_london_low
importance_algorithm: null
target_priority: null
fixed_risk_reward: null
partials: null
status: confirmed_concept
human_validation_required: true
```

Manual source-video re-verification confirms important upside liquidity/highs for buys, specifically including relevant Asia High and London High, and important downside liquidity/lows for sells, specifically including relevant Asia Low and London Low. These are source-supported target candidates, not an automatic instruction to choose every session extreme.

The algorithm that determines relevance/importance and priority among multiple candidates remains unresolved, so target selection still requires human validation. Neither Asia nor London always wins; nearest/farthest priority, fixed ratio, partials, trailing, manual close and 1H/4H target priority are not confirmed.

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

Existing audited status semantics remain: return `waiting` before 08:30 `America/Bogota`, while an expected event is pending, `data_unavailable` for missing required closed candles/timestamps and `human_validation_required` at subjective gates. The new strict dependency model does not by itself decide whether an unmet prerequisite maps to `failed`, `waiting` or `not_applicable`; that runtime mapping remains to be audited separately. Regardless of status label, downstream eligibility is not established while a mandatory prior gate is absent.

## 22. Entry-ineligibility conditions

Confirmed:

- No entry without a liquidity sweep.
- No entry without confirmed 5M inversion.
- A wick is not a 5M structural change.
- No entry eligibility without a continuation 5M FVG.
- FVG strength must be approved by human review before downstream eligibility.
- No entry without 1M retracement and realignment.
- No entry before 08:30 `America/Bogota`.
- No new entry after 11:30 `America/Bogota`.

These dependency statements do not select a runtime final verdict. FVG size/quality, displacement, Wickfill/Fakeout, structural HL/LH selection and important-high/important-low selection require human validation. News, oversized stops, target distance, reentries, operation counts, daily limit, data quality, lateral markets and holidays remain unresolved.

## 23. Human-validation points

Human validation is required for 4H visual bootstrap, delimiting the relevant retracement, translating body-marked structural points into exact prices, Breakout/Wickfill/Fakeout geometry/classification precedence, liquidity selection, 1H/4H coincidence, 5M pivots, “strong/decisive” close, IFVG, FVG quality/lifecycle, 1M FVG interaction/corrective swing, order mechanics, structural Stop Loss selection/offset, liquidity-target priority, first-important-liquidity Break-Even semantics, sizing, daily limit, news and reentries. Timezone/DST interpretation, the 08:00 closed-4H filter, rejection of minor fluctuations, mandatory step order and confirmed body-close/retrospective-confirmation sequence are not human-validation points; selecting their unresolved inputs remains one.

## 24. Automation readiness

The evidence supports assisted analysis, manual backtesting and supervised paper trading. Semi-automatic backtesting remains partial. The end-to-end gate order, trading window, Asia/London boundaries and session-extrema calculation are confirmed; Step-1 and Step-2 inputs remain explicitly separate. For 4H structure, the closed-candle filter, body-close requirement, retrospective confirmation order, active-pair concept and rejection of minor fluctuations are confirmed, while visual bootstrap and exact geometry remain non-deterministic. Step-3 exceed semantics are confirmed but relevant-level priority is unresolved. Step 4 remains blocked by 5M structural/IFVG geometry; Step 5 remains human-validated for FVG quality/lifecycle; Step 6 remains human-validated for FVG interaction and 1M structural realignment. The Stop Loss wick anchor, directional session-liquidity targets and Break-Even liquidity-target concept are confirmed, while their exact executable selection and offsets remain unresolved. These gaps prohibit fully automatic backtesting and autonomous execution.

## 25. Traceability requirements

Record at minimum: `Strategy`, `Strategy version`, `Instrument`, `Data source`, `Timestamp`, `Timezone`, `Market snapshot`, `Rules evaluated`, `Rule statuses`, `Evaluation results`, `Evidence`, `Evidence timestamp`, `Final verdict`, `Human validations`, `Entry`, `Stop Loss`, `Take Profit`, `Result`, `Failure classification` and `Trace identifier`. Preserve candle-close ordering and exclude future data.
