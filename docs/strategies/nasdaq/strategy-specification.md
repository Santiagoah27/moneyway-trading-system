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

La documentación representa la evidencia actualmente auditada del video de 58:24, consolidada desde cinco intervalos y re-verificaciones humanas directas de los videos fuente. Estas re-verificaciones confirmaron la secuencia de seis etapas, preparación a las 08:00 y workflow operativo `[08:30, 11:00)` en hora Colombia, las sesiones Asia/London y sus extremos 1H, la guía de Stop Loss 5M HL/LH, los targets en important highs/lows y los conceptos estructurales 4H descritos en la sección 9. La evidencia humana directa más reciente corrige 11:30 como una interpretación anterior incorrecta: a las 11:00 comienza la "zona muerta" y no se continúa buscando o completando una entrada. Video 3 aproximadamente `04:15–04:30` también evidencia días sin liquidity take o sin confirmación estructural/inversa, para los cuales no hay trade y se vuelve al siguiente día operativo. Una revisión humana del segundo video aclaró la confirmación retrospectiva y selección visual de puntos estructurales aproximadamente en `03:05–04:55` y `06:20–07:35`, el uso de cuerpos aproximadamente en `11:10–11:45` y el filtro higher-timeframe aproximadamente en `08:35–09:20`. La revisión humana posterior, especialmente de Video 3, confirmó el workflow end-to-end como una secuencia de gates obligatorios y aclaró la separación entre 4H context, session liquidity, 5M trigger, 5M FVG confirmation, 1M realignment y management. A further direct human review of Video 3 at approximately `06:20–06:40` and `10:30–10:55` clarified the direction-dependent lifecycle of multiple liquidity takes: same-side extension updates the active reference within the same pending setup, whereas opposite-side target consumption cancels it and can only support a new setup when aligned with Step-1 4H context. Direct human review of Video 3 at approximately `09:40–10:55` and `13:25–13:40` narrowed the target role to the directionally relevant Asia/London session extrema and confirmed exact touch semantics for cancellation and management. Review at approximately `13:10–13:45` confirmed that the target is an absolute price level and its touch is a timeframe-independent quote-level event; the 1M chart is the mentor's visual post-entry management surface, not part of the target definition. Los timestamps suministrados son aproximados; no se inventan título, URL ni líneas de transcript. Salvo esos intervalos aproximados y `50:15` para riesgo máximo por operación, los timestamps de reglas individuales siguen siendo `null` cuando no fueron proporcionados.

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

The operational entry-acquisition workflow starts at 08:30 and ends at 11:00, always in `America/Bogota`, with interval `[08:30, 11:00)`. Times before 11:00 may remain actionable; 11:00 and later are the mentor's "zona muerta" and are not actionable for a new or unfinished setup. Entry order mechanics remain unresolved. For buys, the Stop Loss anchor is beyond the wick of the latest relevant structural 5M HL and the target candidates are London High and Asia High. For sells, the Stop Loss anchor is beyond the wick of the latest relevant structural 5M LH and the target candidates are London Low and Asia Low. When candidates differ, the immediate target is the first relevant level price encounters in the expected direction of travel; neither session has fixed priority. The target is an absolute numeric price level. Its semantic event is a price quote reaching that exact level, independently of chart timeframe, without body close, candle-close confirmation or tolerance. After entry, this first favorable target touch supports moving Stop Loss to entry. Exact structural detection, Stop Loss offsets, target edge cases, same-observation ordering under coarse historical data and final Take Profit behavior remain unresolved or limited as described below.

The workflow is strictly sequential. A downstream signal observed in isolation is not valid for this strategy unless every mandatory prerequisite occurred in chronological order. This dependency statement does not itself assign `passed`, `failed`, `waiting` or `not_applicable`; runtime status mapping requires a separate audit.

Direct human source evidence states the governing gate principle conceptually: **“Cada vez que yo hago un paso, eso me habilita a ir al siguiente. Si ese paso no está chequeado, no puedo ir al siguiente. La estrategia no me permite saltarme los pasos.”** A later market event therefore cannot authorize skipping Step 4, Step 5 or Step 6.

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

### 6.2 Pre-entry lifecycle and cutoff

- A valid liquidity take activates waiting for Step 4; it is not entry eligibility.
- When price takes another relevant level on the same side or establishes a farther extreme in the same manipulation direction before Step 4, the same single pending setup remains active at Step 3. The second take neither advances the workflow counter nor creates a concurrent setup.
- For that same-side continuation, the newer extreme replaces the current active 5M structural reference from that observation onward. In the reviewed bearish-reversal example, the newer High becomes active and the newer relevant preceding 5M Low becomes the level watched for a later bearish body-close break. Prior references remain in the audit history; they are not deleted.
- Repeated valid same-side extensions may continue updating the current active reference before Step 4. The source defines no numeric maximum.
- A wick-only break does not confirm Step 4 and does not alone cancel the setup. Before 11:00, the strategy continues waiting when no other audited cancellation occurred.
- If price instead touches the intended opposite-side target before completed pre-entry progression, the active setup is cancelled from that observation onward because the expected move occurred without valid entry eligibility. For sells the candidates are London Low and Asia Low; for buys they are London High and Asia High. When distinct, the immediate target is the first relevant candidate encountered in the expected price path. Exact contact with the horizontal session-extreme level is sufficient, including wick contact; no body close, candle close, penetration or tolerance is required. Price must not be chased. A later 5M or 1M pattern cannot revive or retroactively validate the cancelled setup.
- Opposite-side liquidity consumption does not automatically reverse the permitted trade direction. Liquidity direction alone is not trade direction. If the newly taken opposite-side liquidity is aligned with the existing Step-1 4H permitted direction, it may activate Step 3 of a new chronological setup; otherwise it authorizes no countertrend setup. The new setup is not a revival of the cancelled one.
- The source does not define how same-side active-reference updates affect the IFVG branch specifically. Step 4 remains Structural Change `OR` IFVG, whichever occurs first, and no IFVG reset rule is inferred.
- All Steps 3–6 and entry eligibility must complete before 11:00. At 11:00, unfinished pre-entry progression expires, there is no entry for that day, and the strategy returns the next operational day. A later dead-zone signal cannot revive the setup. For example, a 10:55 liquidity take followed by an 11:03 structural change cannot complete that expired setup.
- This cutoff governs entry acquisition. It does not establish forced closure at 11:00 for a conceptual trade entered earlier; post-entry Stop Loss, Take Profit and Break-Even management remain separate.

### 6.3 Target-consumption boundary

Target interaction and structural-break confirmation use different geometries. Step-1 and Step-4 structural breaks retain their audited body-close requirements. Target, liquidity and management interaction is confirmed by a price quote touching the exact absolute session-extreme level, without a close or tolerance. This `TARGET_TOUCH_EVENT` is timeframe-independent. The mentor visually monitors and manages the post-entry trade on 1M, but the chart timeframe does not define the target; a platform/broker order may react to the fixed price level without manual chart interaction.

With historical candles, a range containing the target establishes only `TARGET_WAS_TOUCHED_DURING_INTERVAL`: a downside target can be observed through the candle Low and an upside target through the candle High. This is an observational property of OHLC, not a candle-based target definition. A closed candle does not expose the exact quote timestamp or intrabar path. Therefore one minimum-granularity candle that both touches the target and supplies the final event needed to complete `NQ-M1-003` does not establish which happened first. No Open→High→Low→Close path, Open→Low→High→Close path, candle-direction ordering, interpolation, synthetic tick or later-candle reconstruction is permitted. `SAME_OBSERVATION_ORDERING_UNRESOLVED` is retained as a market-data resolution limitation rather than an unresolved target-timeframe rule. Handling remains a strategy-source question when a candidate was crossed before setup activation, both candidates coincide, one session level is unavailable or price is already beyond a candidate when the setup becomes active.

### 6.4 Conceptual setup lifecycle and chronology

These labels classify source-supported concepts; they are not runtime enum names:

| Conceptual state | Source-supported transition |
|---|---|
| `ACTIVE` | The first directionally valid Step-3 liquidity take creates one pending setup waiting for Step 4. |
| `REFERENCE_UPDATED` | At a later timestamp, a farther same-side take keeps that setup active at Step 3 and replaces only its current structural reference. |
| `CANCELLED` | Opposite-side target consumption before formal entry ends the old setup from that timestamp onward. |
| `NEW_SETUP` | After cancellation, the opposite take may activate a separate new Step 3 only when it agrees with the existing Step-1 4H permitted direction. |

Chronology is strict. If `T1` activates the setup and `T2` supplies a farther same-side extreme, observations before `T2` retain the old reference and only observations from `T2` onward use the updated reference. A Step-4 confirmation at `T3` must be evaluated against the reference active at `T3`; it cannot rewrite `T1` or `T2`. If cancellation occurs at `Tcancel`, later patterns cannot retroactively create an entry for the cancelled setup. Any source-permitted activation after `Tcancel` is a new progression, not revival. The 11:00 cutoff expires every unfinished pre-entry progression regardless of prior reference updates.

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
| 19. Break Even | `confirmed` conceptually + `human_validation_required` | Entry, London High, Asia High and observable price | On exact quote-level touch of the first encountered favorable session High, SL moves to entry | Not evaluated before entry; same-candle intrabar ordering may be unobservable | Yes | Data resolution, costs, universality and later management |
| 20. Take Profit | `confirmed` conceptually + `human_validation_required` | London High, Asia High and position state | Immediate target follows the first encountered relevant session High; one reviewed case exits completely on exact quote-level touch | No universal final-exit rule is established | Yes | First-target/final-TP relation, data resolution, edge cases, ratio and partials |
| 21. Trading-window end | `confirmed` | Clock in `America/Bogota` | Pre-entry workflow is outside `[08:30, 11:00)` at or after 11:00 | New or unfinished entry setup expires at 11:00 | No for timezone/DST | Post-entry management remains separately unresolved |
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
| 19. Break Even | `confirmed` conceptually + `human_validation_required` | Entry, London Low, Asia Low and observable price | On exact quote-level touch of the first encountered favorable session Low, SL moves to entry | Not evaluated before entry; same-candle intrabar ordering may be unobservable | Yes | Data resolution, costs, universality and later management |
| 20. Take Profit | `confirmed` conceptually + `human_validation_required` | London Low, Asia Low and position state | Immediate target follows the first encountered relevant session Low; one reviewed case exits completely on exact quote-level touch | No universal final-exit rule is established | Yes | First-target/final-TP relation, data resolution, edge cases, ratio and partials |
| 21. Trading-window end | `confirmed` | Clock in `America/Bogota` | Pre-entry workflow is outside `[08:30, 11:00)` at or after 11:00 | New or unfinished entry setup expires at 11:00 | No for timezone/DST | Post-entry management remains separately unresolved |
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

These definitions make `NQ-LIQ-001` deterministically specified; this documentation change does not alter its existing runtime evaluator. Structural-point detection, coincidence tolerance, liquidity priority, reuse of an already consumed level, internal/external liquidity, equal levels, prior-day levels and pre-08:30 sweeps remain unresolved or require human validation. Multiple-take setup association is direction-dependent and resolved in section 6.2. The confirmed high-level take condition is only that price exceeds an identified High or goes below an identified Low; no extra penetration threshold, point/tick count, close-back, rejection, displacement or tolerance is inferred.

Directional source examples support waiting for downside liquidity such as an Asia/London Low before bullish continuation, and upside liquidity such as an Asia/London High before bearish continuation. They do not define automatic priority among Asia, London or structural liquidity references.

## 11. Operating schedule

```yaml
preparation_start_time: "08:00"
entry_start_time: "08:30"
trading_window_end_time: "11:00"
timezone: "America/Bogota"
daylight_saving_adjustment: false
```

Preparation/analysis starts at 08:00. The operational entry-acquisition window is `[08:30, 11:00)`: it starts at 08:30 and ends when 11:00 is reached. All three are local Colombia times governed normatively by `America/Bogota`, which does not apply seasonal DST adjustments to these limits.

The strategy operates the New York market, but that market reference does not change its clock to `America/New_York`, EST or EDT. The same confirmed timezone governs the Asia and London definitions in section 10. Allowed days, holidays, early closes, low-liquidity sessions and management of positions after a pre-11:00 entry remain unresolved. No forced exit at 11:00 is confirmed.

## 12. 5M inversion

Only after the required liquidity take, two Step-4 alternatives are valid with `OR`: traditional Structural Change/ChoCH/MSS or IFVG, whichever occurs first. Both alternatives are not required and neither has a fixed priority. The extreme left by the liquidity-taking event can serve as the first 5M High/Low reference from which the mentor begins reading subsequent 5M structure; this observation does not define a complete 5M structural detector and does not solve 4H bootstrap.

Until Step 4 is confirmed and while local time remains before 11:00, a valid farther same-side extreme replaces the current active 5M structural reference within the same single setup; Step 3 remains the last satisfied gate and prior observations remain in the audit history. Continued movement in the manipulation direction and a wick-only structural break do not confirm Step 4 and do not alone cancel the setup. Separately, exact wick contact with the first encountered opposite-side Asia/London target cancels the setup before completed pre-entry progression. The setup expires at 11:00 under a distinct time-based condition. The source does not define branch-specific IFVG reset or reference-replacement behavior.

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

Earlier evidence distinguished a post-entry management swing from the corrective entry swing. The authoritative Break-Even evidence now uses the first encountered favorable Asia/London session target instead; whether the earlier swing retains a separate role remains unresolved and it is not the canonical trigger in this specification.

Order type, close-versus-next-open timing, slippage, maximum entry distance, attempts, reentries and corrective-swing algorithm are unresolved. The time boundary is resolved: all pre-entry confirmation must complete before 11:00, and price must not be chased when the intended target was already consumed.

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

The newly confirmed management concept uses favorable session liquidity rather than treating an isolated post-entry swing as the canonical trigger. For a sell, the candidates are London Low and Asia Low; when a price quote touches the first relevant candidate encountered downward, move Stop Loss to entry. For a buy, the candidates are London High and Asia High; when a price quote touches the first relevant candidate encountered upward, move Stop Loss to entry. This event is independent of chart timeframe. Exact contact with the horizontal wick extreme is sufficient without body close, candle-close confirmation, tolerance or penetration buffer. Video 3 approximately `13:20–13:35` shows the mentor observing the touch on a 1M chart, while the fixed target/order level itself is handled by price interaction rather than defined by that chart timeframe.

The relationship between this authoritative trigger and the earlier documented post-entry swing break is unresolved; the earlier swing rule must not replace the first-encountered session-liquidity concept automatically. The source says to move Stop Loss to entry/Break-Even; it does not define BE plus ticks, spread or commission adjustment. Universality, commissions, spread, exact entry/cost basis, later management and whether this first target is also the final Take Profit remain open. Historical same-candle ordering is a data-resolution limitation, not an unresolved BE timeframe. The strategy concept is `confirmed`; a deterministic Break-Even algorithm is not yet fully defined.

## 17. Take Profit

```yaml
buy_target_reference: first_relevant_asia_or_london_high_encountered_upward
sell_target_reference: first_relevant_asia_or_london_low_encountered_downward
importance_algorithm: null
target_priority: first_encountered_in_expected_price_path
fixed_risk_reward: null
partials: null
status: confirmed_concept
human_validation_required: true
```

Manual source-video re-verification confirms the target candidates for this role: Asia High and London High for buys, and Asia Low and London Low for sells. When the two levels differ, the immediate target is the first relevant level price encounters in the expected direction of travel. This is not fixed Asia priority, fixed London priority, session-age priority, the most extreme level or the farthest level. Arbitrary 1H/4H structural points, previous-day levels and other liquidity are not added to this exact target role.

Exact quote-level touch of the selected horizontal session-extreme price is sufficient; no body close, candle close, chart timeframe or tolerance is required. One reviewed example around Video 3 `13:25–13:40` supports complete exit when its selected level is touched. It does not establish that every first target is the final TP or define universal full-exit behavior. Historical same-candle ordering is limited by market-data resolution. Already-crossed/equal/unavailable/initially-beyond candidates, fixed ratio, partials, trailing and other final-target hierarchy remain unresolved source questions.

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
- No new or unfinished pre-entry setup may continue at or after 11:00 `America/Bogota`.
- No entry when the first encountered intended Asia/London target was touched before completed pre-entry progression; exact wick contact is sufficient and no close or tolerance applies. Do not chase price.
- No automatic direction reversal after opposite-side target consumption; a new Step 3 requires alignment with the Step-1 4H permitted direction.
- No revival of a cancelled setup through later 5M or 1M evidence.

These dependency statements do not select a runtime final verdict. FVG size/quality, displacement, Wickfill/Fakeout, structural HL/LH selection and important-high/important-low selection require human validation. News, oversized stops, target distance, reentries, operation counts, daily limit, data quality, lateral markets and holidays remain unresolved.

## 23. Human-validation points

Human validation is required for 4H visual bootstrap, delimiting the relevant retracement, translating body-marked structural points into exact prices, Breakout/Wickfill/Fakeout geometry/classification precedence, liquidity selection for Step 3, 1H/4H coincidence, 5M pivots, “strong/decisive” close, IFVG, FVG quality/lifecycle, 1M FVG interaction/corrective swing, order mechanics, structural Stop Loss selection/offset, unresolved target edge cases, final-TP behavior, sizing, daily limit, news and reentries. Timezone/DST interpretation, the 08:00 closed-4H filter, rejection of minor fluctuations, mandatory step order, timeframe-independent target touch semantics and confirmed body-close/retrospective-confirmation sequence are not human-validation points. Same-candle ordering with OHLC remains a data limitation and has no inferred runtime mapping.

## 24. Automation readiness

The evidence supports assisted analysis, manual backtesting and supervised paper trading. Semi-automatic backtesting remains partial. The end-to-end gate order, trading window, Asia/London boundaries and session-extrema calculation are confirmed; Step-1 and Step-2 inputs remain explicitly separate. Same-side reference replacement, opposite-side target-consumption cancellation and 4H-gated new Step-3 activation are source-supported lifecycle concepts. Runtime supports generic lifecycle transitions and Nasdaq time expiration, but target-consumption behavior is not implemented. Canonical historical replay currently exposes closed OHLC `CandleSeries` through `ReplayFrame`, `MultiTimeframeReplayFrame` and `StrategyReplayContext`; it has no quote/tick/event-level contract. Its finest timeframe is determined by configured input, and current Nasdaq replay scenarios commonly include 1M. A later candle touch, after entry progression was established in a prior observation, is causally observable from candle range. A target touch and final entry event inside the same minimum-granularity candle cannot be ordered from current data. Directional targets, first-encountered priority and quote-level touch semantics are confirmed, while target edge cases and final TP behavior remain source gaps. Full-fidelity same-observation replay would require finer event data or an explicitly accepted candle-ambiguity policy; neither is selected here.

## 25. Traceability requirements

Record at minimum: `Strategy`, `Strategy version`, `Instrument`, `Data source`, `Timestamp`, `Timezone`, `Market snapshot`, `Rules evaluated`, `Rule statuses`, `Evaluation results`, `Evidence`, `Evidence timestamp`, `Final verdict`, `Human validations`, `Entry`, `Stop Loss`, `Take Profit`, `Result`, `Failure classification` and `Trace identifier`. Preserve candle-close ordering and exclude future data.
