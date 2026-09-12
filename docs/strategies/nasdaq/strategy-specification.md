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

The latest direct review of Video 3 approximately `03:10–03:25` and `05:55–06:40`, Video 1 approximately `10:10–10:35`, and Video 2 approximately `08:35–09:20` and `11:10–11:45` partially defines that structural fallback. For sells, source-supported candidates are relevant unmitigated support represented by a validated 1H/4H HL or LL, plus Previous Day Low. For buys, they are relevant unmitigated resistance represented by a validated 1H/4H LH or HH, plus Previous Day High. In reviewed paths containing distinct relevant 1H and 4H levels, 1H acts as the first objective or management level and the farther 4H level as the final or extended objective; 4H is treated as structurally stronger. This observed hierarchy is not generalized beyond the reviewed evidence. Structural zones are marked from candle bodies rather than an isolated long wick. The selected-turn candidate-HL and LH body coordinates are now defined as documented below, but initial candidate-turn detection and coordinates for other structural-point classes remain unresolved. No title, URL or transcript wording is inferred from these approximate references.

Direct human review of Video 2 approximately `03:05–04:10`, `06:15–06:45` and `08:35–09:15` confirms the causal validation rule for structural swings on the relevant 1H or 4H timeframe. A candidate HL/LH becomes validated only after the impulse originating from it produces a body close beyond the preceding HH/LL, and a new HH/LL requires that formally closed candle rather than wick-only penetration. Candidate detection and exact body-zone price remain separate unresolved boundaries.

Direct human review of Video 2 approximately `01:40–01:55`, `03:15–04:50` and `13:15–13:40`, plus Video 3 approximately `07:40–07:46`, partially defines the bullish correction window used for candidate-HL selection. From the prior HH peak, once the bullish impulse stops producing HHs, the first bearish candle starts the correction and the prior HH remains its upper structural boundary. The correction remains active through internal zigzags and deeper lows until a formally closed candle has `Close > prior HH structural level`; only at that causal close does the deepest candidate become a validated HL. The candidate is the deepest low/base of the correction and of the impulse that produces the break; preliminary/intermediate lows are replaced by a deeper low, and one- or two-candle pauses inside that impulse do not create another HL. For the selected turn, the structural reference uses the lowest `Open` or `Close` among its candle bodies, not an isolated wick. Doji/gap handling, exact inclusivity of the prior-HH boundary, turn membership, candidate-LH boundaries and candle-identity tie-breaking remain unresolved. Equal lowest body coordinates can still determine one unambiguous price when only the price is needed; they do not choose a candle identity.

Direct human review of Video 2 approximately `05:40–06:15`, `06:40`, `11:10–11:35` and `13:20`, plus Video 3 approximately `04:25–04:35`, `04:30`, `07:15–07:45` and `07:25–07:35`, explicitly confirms the bearish causal-validation path, complex candidate-LH identity and structural LH coordinate. A bearish retracement originates from the prior LL area, but its candidate high remains unvalidated while that LL lacks a confirmed break. Within one recognized bearish correction, a later higher candidate high replaces an earlier lower one; superseded intermediate highs do not survive as the LH candidate. Only a formally closed candle with `Close < prior LL structural level` confirms the new LL and validates that preceding highest candidate high as LH from that causal `AsOfUtc`; wick-only penetration and an open candle are insufficient. For an already selected LH turn, the structural coordinate is the highest candle-body edge in that turn: `max(Open, Close)` across its member candles. Candle color does not govern the coordinate, and a higher wick does not redefine it. Bearish doji/gap edges, correction segmentation and exact turn membership remain partial or unresolved. Separately, the sell risk example places Stop Loss above the highest wick/tail of the relevant LH. That SL/invalidation anchor remains distinct from the body-based structural coordinate used for target selection, reconstruction or comparisons.

Direct human review of Video 2 approximately `03:20–04:10` and `05:55–06:15`, plus Video 3 approximately `07:40–07:46` and `09:00–09:15`, confirms the role of the causal break candle in both directions. The formally closed candle with `Close > prior HH` or `Close < prior LL` is excluded from the correction-candle set scanned retrospectively for the candidate HL/LH. It belongs to the expansion/impulsive leg and its close confirms the new HH/LL, closes the correction, validates the preceding candidate and establishes the causal `AsOfUtc`. In the demonstrated single-expansive-candle case, that same candle may contain evidence of the impulse origin at the selected turn while remaining excluded as an ordinary correction member. This coexistence does not define the origin's exact price coordinate, an intrabar path or a synthetic order among the candle's OHLC events.

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

The operational entry-acquisition workflow starts at 08:30 and ends at 11:00, always in `America/Bogota`, with interval `[08:30, 11:00)`. Times before 11:00 may remain actionable; 11:00 and later are the mentor's "zona muerta" and are not actionable for a new or unfinished setup. After 08:30, the first valid take of a relevant Low or High is the effective Step 3 of the scenario it may orient; there is no separate target-consumption interval before Step 3. Taking a Low can orient a possible buy progression and taking a High can orient a possible sell progression, but Step 1 remains mandatory and only its 4H permitted direction can authorize progression toward Step 4. Entry order mechanics remain unresolved. For buys, the Stop Loss anchor is beyond the wick of the latest relevant structural 5M HL and the primary session target candidates are London High and Asia High. For sells, the Stop Loss anchor is beyond the wick of the latest relevant structural 5M LH and the primary session target candidates are London Low and Asia Low. When two future-valid candidates differ, the immediate target is the first relevant level price encounters in the expected direction; neither session has fixed priority. Equal candidates form one shared level, not two targets, and the reviewed case treats it as final Take Profit without a distinct intermediate BE target. If a relevant session level was consumed before the operational period or is otherwise no longer a future objective before the operational scenario, Step-2 structural context supplies a conceptual fallback. For a sell, the candidates are relevant unmitigated support at a validated 1H/4H HL or LL and Previous Day Low; for a buy, they are relevant unmitigated resistance at a validated 1H/4H LH or HH and Previous Day High. In the reviewed multi-level paths, a distinct 1H level acts as the first objective or management level and the farther, structurally stronger 4H level as the final or extended objective. This is an evidence-bounded hierarchy, not a universal ranking formula. That fallback does not rescue an originally imagined scenario invalidated by an opposite-liquidity take after 08:30. Every selected target must ultimately be an absolute numeric price level whose touch is timeframe-independent and requires no body close, candle close or tolerance. Exact structural detection, mitigation geometry, structural coordinates beyond the documented selected-turn HL/LH body anchors, PDH/PDL ranking, Stop Loss offsets, same-observation ordering under coarse historical data and universal final Take Profit behavior remain unresolved or limited as described below.

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

Rule ownership remains separated: `NQ-H4-001` owns the Step-1 4H context and permitted direction; `NQ-LIQ-002` owns human-approved relevant liquidity marking and any pre-operational structural-fallback input; `NQ-LIQ-003` owns the first valid post-08:30 take, its effective Step-3 orientation and its required alignment with Step 1; `NQ-M5-001` owns only the Step-4 Structural Change `OR` IFVG gate after an aligned Step 3. Discarding the originally imagined pre-Step-3 scenario belongs to the Step-3 interpretation. Cancelling an already-active setup after its selected target is touched remains a separate lifecycle concern.

At `StrategyReplayContext.AsOfUtc`, only prerequisites and confirming events observable at or before that timestamp may participate. A future liquidity take cannot activate an earlier Step 4, a future 5M FVG cannot activate an earlier Step 6, and a future 1M realignment cannot create past entry eligibility. Backtesting must not find a later successful pattern and retroactively assume its earlier gates were valid.

### 6.2 Pre-entry lifecycle and cutoff

- A valid liquidity take activates waiting for Step 4; it is not entry eligibility.
- When price takes another relevant level on the same side or establishes a farther extreme in the same manipulation direction before Step 4, the same single pending setup remains active at Step 3. The second take neither advances the workflow counter nor creates a concurrent setup.
- For that same-side continuation, the newer extreme replaces the current active 5M structural reference from that observation onward. In the reviewed bearish-reversal example, the newer High becomes active and the newer relevant preceding 5M Low becomes the level watched for a later bearish body-close break. Prior references remain in the audit history; they are not deleted.
- Repeated valid same-side extensions may continue updating the current active reference before Step 4. The source defines no numeric maximum.
- A wick-only break does not confirm Step 4 and does not alone cancel the setup. Before 11:00, the strategy continues waiting when no other audited cancellation occurred.
- Before the operational scenario, a session target already consumed or otherwise no longer future-valid is a target-selection input: the source falls back conceptually to the next relevant structural 1H/4H point. This is distinct from cancellation and does not define a deterministic structural-point selector. At or after 08:30, however, the first valid opposite-liquidity take is itself the effective Step 3 for the newly oriented scenario; it is not a pre-Step-3 target-consumption edge and structural fallback does not rescue the originally imagined scenario.
- A valid Low take can orient a possible buy progression and a valid High take can orient a possible sell progression. That orientation may continue toward Step 4 only when it matches the existing Step-1 4H permitted direction. If it does not match, Step 4 remains disabled, the prior imagined scenario remains discarded, and there is no countertrend trade, revival or chase.
- If price instead touches the intended opposite-side target before completed pre-entry progression, the active setup is cancelled from that observation onward because the expected move occurred without valid entry eligibility. For sells the candidates are London Low and Asia Low; for buys they are London High and Asia High. When distinct, the immediate target is the first relevant candidate encountered in the expected price path. Exact contact with the horizontal session-extreme level is sufficient, including wick contact; no body close, candle close, penetration or tolerance is required. Price must not be chased. A later 5M or 1M pattern cannot revive or retroactively validate the cancelled setup.
- Opposite-side liquidity consumption does not automatically reverse the permitted trade direction. Liquidity direction alone is not trade direction. If the newly taken opposite-side liquidity is aligned with the existing Step-1 4H permitted direction, it may activate Step 3 of a new chronological setup; otherwise it authorizes no countertrend setup. The new setup is not a revival of the cancelled one.
- The source does not define how same-side active-reference updates affect the IFVG branch specifically. Step 4 remains Structural Change `OR` IFVG, whichever occurs first, and no IFVG reset rule is inferred.
- All Steps 3–6 and entry eligibility must complete before 11:00. At 11:00, unfinished pre-entry progression expires, there is no entry for that day, and the strategy returns the next operational day. A later dead-zone signal cannot revive the setup. For example, a 10:55 liquidity take followed by an 11:03 structural change cannot complete that expired setup.
- This cutoff governs entry acquisition. It does not establish forced closure at 11:00 for a conceptual trade entered earlier; post-entry Stop Loss, Take Profit and Break-Even management remain separate.

### 6.3 Target-consumption boundary

Step-3 liquidity take, selected-target interaction and structural-break confirmation use different geometries. Step 3 retains the audited requirement that price exceed a selected relevant High or go below a selected relevant Low; equality-only contact is not confirmed. Step-1 and Step-4 structural breaks retain their audited body-close requirements. Selected-target and management interaction is instead confirmed by a price quote touching the exact absolute target level, without a close or tolerance. This `TARGET_TOUCH_EVENT` is timeframe-independent. The mentor visually monitors and manages the post-entry trade on 1M, but the chart timeframe does not define the target; a platform/broker order may react to the fixed price level without manual chart interaction.

With historical candles, a range containing the target establishes only `TARGET_WAS_TOUCHED_DURING_INTERVAL`: a downside target can be observed through the candle Low and an upside target through the candle High. This is an observational property of OHLC, not a candle-based target definition. A closed candle does not expose the exact quote timestamp or intrabar path. Therefore one minimum-granularity candle that both touches the target and supplies the final event needed to complete `NQ-M1-003` does not establish which happened first. No Open→High→Low→Close path, Open→Low→High→Close path, candle-direction ordering, interpolation, synthetic tick or later-candle reconstruction is permitted. `SAME_OBSERVATION_ORDERING_UNRESOLVED` is retained as a market-data resolution limitation rather than an unresolved target-timeframe rule. Equal Asia/London prices form one target occurrence. A session target consumed before the operational period or otherwise no longer future-valid before the operational scenario invokes the conceptual structural fallback; deterministic structural selection remains unresolved. A valid post-08:30 take instead defines Step 3 and does not invoke fallback to preserve the discarded scenario.

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

This section governs the Step-1 4H context. The causal swing-validation rule below also applies to validated 1H structural points used by the documented target fallback; it does not make candidate detection or exact price placement deterministic.

### 9.1 Confirmed structural concepts

- Bullish confirmation progresses as `prior structural High → relevant retracement candidate → subsequent body-close break above that High/new HH → prior relevant retracement becomes confirmed HL`.
- Directly demonstrated bearish confirmation progresses as `prior structural LL area → retracement candidate high remains provisional → subsequent formally closed Close below the prior LL/new LL → preceding candidate high becomes confirmed LH from that close AsOfUtc`.
- `HH` is a structural High above the previous structural High. On the relevant 1H or 4H structure, a wick above is insufficient: `Candle.Close > previous structural High` confirms the bullish structural break and the new HH only after that candle formally closes.
- `LL` is a structural Low below the previous structural Low. On the relevant 1H or 4H structure, a wick below is insufficient: `Candle.Close < previous structural Low` confirms the bearish structural break and the new LL only after that candle formally closes.
- After an HH, the relevant low of the slowdown/retracement is provisional. It becomes a confirmed `HL` only when the later impulse originating from that candidate produces a formally closed body above the previous HH on the same relevant timeframe and forms the next HH. A rebound that does not achieve that body-close break does not validate the candidate as structural HL.
- After an LL, the bearish retracement is directly shown originating from the prior LL area. Its relevant candidate high remains provisional while the prior LL has no confirmed break. It becomes a confirmed `LH` only when a formally closed candle has `Close < prior LL structural level` on the same relevant timeframe and forms the next LL. A wick-only penetration, an open candle or a decline without that close does not validate the candidate; validation exists only from the causal close `AsOfUtc`.
- For bullish candidate-HL selection, the correction starts at the prior HH peak once the bullish impulse stops producing HHs and the first contrary/bearish candle begins the retracement. The prior HH remains the upper structural boundary.
- While no formally closed candle has `Close > prior HH structural level`, that bullish correction remains active. It can contain zigzags, internal oscillations and deeper lows; preliminary/intermediate lows do not survive as HLs, and a deeper low replaces a shallower candidate.
- For candidate-HL selection inside that active correction, the candidate is the deepest low of the correction and must be the base from which the impulse that later produces the confirming body-close break above the prior HH originates.
- Small pauses of one or two candles inside that impulse do not create a new candidate HL.
- The bullish correction does not formally end merely because price rebounds. It ends when a formally closed candle has `Close > prior HH structural level`; only from that causal close `AsOfUtc` onward does the deepest candidate become a validated HL.
- LH causal validation, prior-LL correction origin, complex candidate-LH selection, highest-high replacement and the selected-turn structural coordinate are directly confirmed, not inferred. Within one recognized bearish correction, a later higher candidate replaces a lower one and superseded intermediate highs do not survive. Candidate validation still occurs only from the causal formally closed `Close < prior LL` `AsOfUtc`. For the already selected LH turn, the structural price is the highest `Open`/`Close` body edge among its member candles; a higher wick does not redefine it. Bearish correction edge construction remains non-deterministic. The sell highest-wick/tail Stop Loss anchor is a separate risk/invalidation semantic, not the structural coordinate.
- For the selected candidate-HL turn, mark the structural reference at the lowest candle-body coordinate: the lowest `Open` or `Close` among the bodies belonging to that turn. An isolated wick does not define the structural level.
- If several included bodies share that same lowest `Open`/`Close`, the structural price is still unambiguous when the strategy needs only that price; the identity of a particular candle/timestamp remains unresolved. Doji behavior before the first bearish candle, gaps, exact prior-HH/LL boundary inclusivity, turn membership and the selected-candle identity remain unresolved.
- The causal break candle is not an ordinary correction member and is excluded from the retrospective correction scan. Its close ends the correction, confirms the new HH/LL and validates the preceding candidate from that observable `AsOfUtc`. Candles expanding from the prior turn toward the break belong to the impulsive leg rather than the correction phase.
- A single expansive candle may both contain evidence of the impulse origin at the selected turn and produce the confirming close. It remains excluded from correction membership, but its origin evidence must not be discarded. The exact origin-price coordinate inside that candle is unresolved; no `Open`, wick, body coordinate, intrabar chronology or synthetic OHLC path is inferred.
- After the recent structure is reconstructed, the active current pair is the latest confirmed HH and HL in bullish context, or the latest confirmed LL and LH in bearish context. Older points remain in the historical audit trail even when they are less relevant to the current decision.

The source now materially clarifies a retrospective bullish correction window: prior HH → first bearish correction candle → correction observations → impulsive expansion → formally closed body above the prior HH. The causal breakout candle is excluded from correction membership and correction-candidate scanning. The window remains partially defined because doji/gap behavior, exact boundary inclusivity and turn membership are not supplied. It defines the lowest `Open`/`Close` price for a selected candidate-HL turn, including a price-preserving equal-coordinate tie, but not turn membership or candle identity. On the bearish side, direct evidence confirms complex highest-candidate selection and replacement within one recognized correction, but not every correction edge. In the special single-candle case, the confirming candle can contain impulse-origin evidence without becoming a correction candle; its exact origin-price coordinate remains unresolved. No intrabar path, fixed pivot or fixed lookback is inferred, and the sell Stop Loss wick anchor remains separate.

“Price slows down” remains a visual source criterion. It does not define candle count, minimum retracement, pivot width, percentage, ATR or candle-size thresholds. The source also does not define how to obtain the initial previous structural High/Low from an arbitrary raw 4H series without a human annotation or pre-seeded level. Full structural detection therefore remains `human_validation_required` and blocked for deterministic evaluation.

### 9.2 Retrospective confirmation and historical replay

HL/LH confirmation is retrospective and causal but must not leak future information. At replay time `T1`, before the subsequent 1H/4H HH/LL break candle has formally closed, the prior retracement remains candidate/unconfirmed and minor fluctuations remain non-structural even if the open candle temporarily trades beyond the level. From `T2`, the `AsOfUtc` at which the subsequent body-close break confirms the new HH/LL, the earlier relevant retracement may be recorded as the confirmed HL/LH. Historical evaluation at `T1` must not use the `T2` close or relabel the candidate as already confirmed; a future candle must never change what was considered confirmed at an earlier replay timestamp.

Historical automation of `NQ-H4-001` therefore requires reconstruction using only information observable through each `AsOfUtc`. Before the causal bullish or bearish close, the candidate remains unvalidated; later observations cannot alter an earlier historical evaluation. The source defines both causal confirmation orders, excludes the confirming candle from correction membership/scanning, recognizes its validation role and single-candle impulse-origin case, directly confirms the bearish prior-LL origin, complex candidate-LH selection, highest-high replacement and selected-turn highest-body-edge LH coordinate, and partially defines the bullish correction window, candidate-HL price selection and body anchoring. It does not define a complete detector: bootstrap of the prior reference level, doji/gap and boundary-inclusivity rules, turn membership, tie identity, bearish correction edges, the single-candle origin-price coordinate and coordinates for other structural-point classes remain unresolved or partial.

| Structural concern | Status | Boundary |
|---|---|---|
| `STRUCTURAL_VALIDATION_RULE` | `CONFIRMED` | HL/LH validation is caused by a later formally closed body beyond the prior HH/LL; HH/LL also require that body-close break on the same relevant 1H/4H timeframe. |
| `LH_CAUSAL_VALIDATION` | `CONFIRMED` | A preceding candidate high becomes a validated LH/new LL only from a formally closed `Close < prior LL`; wick-only and open-candle penetration are insufficient. |
| `BEARISH_CORRECTION_ORIGIN` | `CONFIRMED` | The demonstrated bearish retracement starts from the prior LL area; no additional geometry is inferred. |
| `CANDIDATE_HL_SELECTION` | `PARTIALLY_DEFINED` | Deepest low of the human-delimited correction and impulse origin are confirmed; algorithmic correction/turn boundaries are not. |
| `BULLISH_CORRECTION_START` | `PARTIALLY_DEFINED` | The prior HH peak and first bearish correction candle are source-defined; doji/gap behavior and exact HH-candle inclusivity are not. |
| `BULLISH_CORRECTION_END` | `CONFIRMED` | The correction ends only when a formally closed candle has `Close > prior HH structural level`. |
| `BULLISH_CORRECTION_WINDOW` | `PARTIALLY_DEFINED` | Retrospective window is prior HH → first bearish candle → correction observations → impulsive expansion → confirming closed-body break; boundary edge cases and turn membership remain unresolved. |
| `CANDIDATE_HL_PRICE_SELECTION` | `PARTIALLY_DEFINED` | Within the selected correction/turn, the deepest candidate's lowest body `Open`/`Close` determines the price; window/turn membership remains unresolved. |
| `CANDIDATE_HL_IDENTITY_SELECTION` | `UNRESOLVED` | Equal lowest body coordinates do not make the price ambiguous, but no rule chooses a candle/timestamp identity. |
| `COMPLEX_CANDIDATE_LH_SELECTION` | `CONFIRMED` | Within one recognized bearish correction, the highest candidate high is selected; superseded intermediate highs do not survive. |
| `HIGHEST_HIGH_REPLACEMENT` | `CONFIRMED` | A later higher candidate high replaces the earlier lower candidate before the causal closed break below the prior LL. |
| `BEARISH_CORRECTION_WINDOW` | `PARTIALLY_DEFINED` | Prior-LL origin, causal closed `Close < prior LL` end, highest-candidate replacement and exclusion of the confirming candle from correction scanning are confirmed; doji/gap, start/boundary inclusivity and segmentation remain unresolved. |
| `CANDIDATE_LH_STRUCTURAL_PRICE` | `CONFIRMED` | For an already selected LH turn, use the highest `Open`/`Close` body edge among its member candles; candle color is non-normative and a higher wick is excluded. |
| `BEARISH_STRUCTURAL_BODY_ANCHOR` | `CONFIRMED` | The structural LH line is anchored where candle bodies stop, at the selected turn's highest body edge. |
| `CORRECTION_PHASE_BOUNDARIES` | `PARTIALLY_DEFINED` | Bullish start/end are materially narrowed; doji/gap handling, exact inclusivity and turn segmentation remain undefined. |
| `STRUCTURAL_BODY_ANCHOR` | `PARTIALLY_DEFINED` | For a selected candidate-HL turn, use the lowest `Open` or `Close` among its bodies; unresolved turn membership prevents a complete deterministic selector. |
| `BODY_ANCHOR_TIE_BREAKING` | `UNRESOLVED` | No rule selects candle identity when several bodies share the same extreme coordinate. |
| `DOJI_BOUNDARY_BEHAVIOR` | `UNRESOLVED` | No rule says whether a doji before the first bearish candle starts the correction. |
| `SELL_LH_SL_ANCHOR` | `CONFIRMED` | For sell risk management, Stop Loss is above the highest wick/tail of the relevant LH; this is not a structural target/reconstruction coordinate. |
| `STRUCTURAL_IDENTITY_VS_PRICE_SEPARATION` | `CONFIRMED` | LH identity selects the swing, the highest selected-turn body edge supplies its structural coordinate, and the separate sell SL anchor remains above the highest wick/tail. |
| `CONFIRMING_CANDLE_CORRECTION_MEMBERSHIP` | `CONFIRMED_EXCLUDED` | In the general and single-expansive-candle cases, the causal break candle is not an ordinary correction member. |
| `CONFIRMING_CANDLE_VALIDATION_ROLE` | `CONFIRMED` | Its formally observable close confirms the new HH/LL, ends the correction, validates the preceding candidate and establishes the causal `AsOfUtc`. |
| `IMPULSE_LEG_MEMBERSHIP` | `PARTIALLY_DEFINED` | Expansion toward the causal break, including the confirming candle, belongs to the impulsive leg rather than the correction; exact turn/leg segmentation remains unresolved. |
| `SINGLE_CANDLE_BREAK_ORIGIN_CASE` | `CONFIRMED` | One expansive candle may contain impulse-origin evidence and also produce the causal confirming close without becoming a correction candle. |
| `CONFIRMING_CANDLE_SCAN_INCLUSIVITY` | `CONFIRMED_EXCLUDED` | The confirming candle is excluded from the correction-candidate scan; its origin evidence is retained separately in the single-candle case. |
| `SINGLE_CANDLE_ORIGIN_PRICE_COORDINATE` | `UNRESOLVED` | Evidence does not choose `Open`, wick or another coordinate, and does not establish intrabar chronology. |
| `STRUCTURAL_CANDIDATE_DETECTION` | `PARTIALLY_DEFINED` | Confirming-candle membership/validation roles and the single-candle origin case are now defined, alongside materially narrowed bullish/bearish candidate rules; edge boundaries, exact segmentation, origin coordinate and bootstrap remain non-reproducible. |
| `EXACT_STRUCTURAL_PRICE_COORDINATE` | `PARTIALLY_DEFINED` | Candidate-HL and candidate-LH selected-turn body anchors are defined, but turn membership and coordinates for other structural-point classes remain unresolved. |

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

These definitions make `NQ-LIQ-001` deterministically specified; this documentation change does not alter its existing runtime evaluator. Equal relevant Asia/London prices can be compared deterministically and collapse into one target. If a relevant session target is no longer a future objective before the operational scenario, the source uses structural fallback from Step 2. The fallback candidate classes, causal validation rule, selected-turn HL/LH body coordinates and reviewed 1H-first/4H-extended relationship are source-supported, but initial candidate detection, correction-edge semantics, exact mitigation, other structural-point coordinates, PDH/PDL ranking, coincidences, ties and reuse validity remain unresolved or require human validation. The post-08:30 edge is resolved: the first valid take is Step 3, subject to Step-1 4H alignment, and does not use fallback to save the prior imagined scenario. Multiple-take setup association is direction-dependent and resolved in section 6.2. The confirmed high-level take condition is only that price exceeds an identified High or goes below an identified Low; no extra penetration threshold, point/tick count, close-back, rejection, displacement or tolerance is inferred. The reviewed use of "touch/take" does not establish equality-only Step-3 activation, so Step-3 geometry remains `CONFIRMED_EXCEEDS` and stays distinct from exact selected-target touch.

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

The relationship between this authoritative trigger and the earlier documented post-entry swing break is unresolved; the earlier swing rule must not replace the first-encountered session-liquidity concept automatically. When the Asia and London target prices coincide, the reviewed scenario uses their one shared level as final Take Profit. It does not create a distinct earlier BE target or a fabricated move-to-BE followed by exit at the same price. The source says to move Stop Loss to entry/Break-Even in the distinct-target case; it does not define BE plus ticks, spread or commission adjustment. Universality, commissions, spread, exact entry/cost basis, later management and whether the first target is final Take Profit outside the reviewed equal-target case remain open. Historical same-candle ordering is a data-resolution limitation, not an unresolved BE timeframe. The strategy concept is `confirmed`; a deterministic Break-Even algorithm is not yet fully defined.

## 17. Take Profit

```yaml
buy_target_reference: first_relevant_asia_or_london_high_encountered_upward
sell_target_reference: first_relevant_asia_or_london_low_encountered_downward
structural_fallback_readiness: partially_defined
importance_algorithm: null
target_priority: first_encountered_in_expected_price_path
fixed_risk_reward: null
partials: null
status: confirmed_concept
human_validation_required: true
```

Manual source-video re-verification confirms the primary session candidates for this role: Asia High and London High for buys, and Asia Low and London Low for sells. When two future-valid levels differ, the immediate target is the first relevant level price encounters in the expected direction of travel. When their prices are equal, they collapse into one unique target; the reviewed equal-target scenario treats that level as final Take Profit. If a relevant session target was already consumed before the operational period or is otherwise unavailable as a future objective, the source uses structural fallback. A sell may target relevant unmitigated support represented by a validated 1H/4H HL or LL, or Previous Day Low. A buy may target relevant unmitigated resistance represented by a validated 1H/4H LH or HH, or Previous Day High. In reviewed paths with distinct relevant levels, 1H is the first objective or management level and the farther 4H level is the final or extended objective; 4H is structurally stronger. This hierarchy is confirmed only within that observed scope and is not a universal formula. Arbitrary structural points do not become coequal primary candidates while valid session targets remain available.

### 17.1 Structural fallback evidence boundary

| Point | Evidence status | Audited boundary |
|---|---|---|
| Sell fallback candidates: relevant unmitigated support at a validated 1H/4H HL or LL | `CONFIRMED` | Candidate classes and causal swing-validation rule are confirmed; initial candidate detection remains human-validated. |
| Buy fallback candidates: relevant unmitigated resistance at a validated 1H/4H LH or HH | `CONFIRMED` | Candidate classes and causal swing-validation rule are confirmed; initial candidate detection remains human-validated. |
| Previous Day Low for sells and Previous Day High for buys | `CONFIRMED` | Inclusion is confirmed; ranking against 1H/4H candidates is not. |
| Distinct 1H first objective/management level followed by farther 4H final/extended objective | `CONFIRMED` | Limited to reviewed paths; not a universal target formula. |
| 4H is structurally stronger than 1H | `CONFIRMED` | Conceptual strength does not by itself select a numeric target. |
| Mark structural support/resistance from candle bodies, not an isolated long wick | `CONFIRMED` | Body-zone concept only. |
| Meaning of relevant and not mitigated | `PARTIALLY_DEFINED` | Directional role is confirmed; exact OHLC mitigation test and threshold are unresolved. |
| Candle/body used to place the line | `PARTIALLY_DEFINED` | A selected candidate-HL turn uses its lowest body `Open`/`Close`, while a selected candidate-LH turn uses its highest body `Open`/`Close`; turn membership, equal-coordinate identity and other structural-point coordinates remain unresolved. |
| HL/LL/LH/HH causal validation after a formally closed body break | `CONFIRMED` | Applies only after the confirming close; it cannot validate the candidate retroactively at earlier replay timestamps. |
| Initial HL/LL/LH/HH candidate detection | `PARTIALLY_DEFINED` | Bullish candidate-HL window/deepest-low/base selection and price anchor are materially narrowed; bearish origin/causal validation, complex highest-candidate selection and selected-turn highest-body-edge coordinate are confirmed. Bearish correction edges, doji/gap boundaries, turn membership, other class selection, pivots and lookbacks remain undefined. |
| Reduction of other body zones to one decimal price | `UNRESOLVED` | Selected-turn HL/LH extrema are defined without a tolerance; no coordinate rule is established for other structural-point classes. |
| PDH/PDL priority against 1H/4H candidates | `UNRESOLVED` | No ranking rule is defined when several candidates coexist. |
| Coincidence or ties among PDH/PDL and 1H/4H structure | `UNRESOLVED` | No merge or tie-break rule is defined. |

Exact quote-level touch of an already-selected absolute price is sufficient; no body close, candle close, chart timeframe or tolerance is required. That touch rule does not define how a structural body zone becomes the selected decimal price. One reviewed example around Video 3 `13:25–13:40` supports complete exit when its selected level is touched, and the latest review confirms final-TP treatment for the reviewed coincident-target scenario. Neither establishes that every first distinct target is final TP or defines universal full-exit behavior. Historical same-candle ordering remains limited by market-data resolution. Structural fallback remains `PARTIALLY_DEFINED`; fixed ratio, partials, trailing and other final-target hierarchy remain unresolved source questions. The resolved post-08:30 Step-3 orientation does not change these target-management semantics.

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

Human validation is required for 4H visual bootstrap, delimiting the relevant retracement, translating body-marked structural points into exact prices, Breakout/Wickfill/Fakeout geometry/classification precedence, liquidity selection for Step 3, structural fallback detection, mitigation and PDH/PDL ranking, 5M pivots, “strong/decisive” close, IFVG, FVG quality/lifecycle, 1M FVG interaction/corrective swing, order mechanics, structural Stop Loss selection/offset, unresolved target edges, general final-TP behavior, sizing, daily limit, news and reentries. The evidence-bounded 1H-first/4H-extended relationship and body-over-isolated-wick marking principle are confirmed concepts, but do not remove that validation requirement. Timezone/DST interpretation, the 08:00 closed-4H filter, rejection of minor fluctuations, mandatory step order, timeframe-independent target touch semantics, equality collapse and confirmed body-close/retrospective-confirmation sequence are not human-validation points. Same-candle ordering with OHLC remains a data limitation and has no inferred runtime mapping.

## 24. Automation readiness

The evidence supports assisted analysis, manual backtesting and supervised paper trading. Semi-automatic backtesting remains partial. The end-to-end gate order, trading window, Asia/London boundaries and session-extrema calculation are confirmed; Step-1 and Step-2 inputs remain explicitly separate. The first valid post-08:30 take as Step 3, mandatory Step-1 4H alignment, same-side reference replacement and opposite-side active-target cancellation are source-supported concepts. Runtime supports generic lifecycle transitions and Nasdaq time expiration, but target-consumption behavior and the newly reconciled Step-3 orientation are not implemented. Canonical historical replay exposes closed OHLC `CandleSeries` and can optionally expose normalized high-resolution price observations through `StrategyReplayContext`; the generic deterministic price-level touch primitive consumes an already-selected target but does not select targets or alter lifecycle. Candle evidence can establish occurrence within an interval, while high-resolution evidence can establish an exact source timestamp when supplied. Unsupported same-observation ordering remains a market-data limitation. Distinct and equal session-target semantics are source-defined. Structural fallback remains `PARTIALLY_DEFINED`: candidate classes, both causal swing-validation paths, the bearish prior-LL origin, confirmed complex candidate-LH selection/highest-high replacement, selected-turn highest-body-edge LH coordinate, the partially defined bullish correction window, candidate-HL deepest-low/base selection and selected-turn lowest-body price, plus the bounded 1H-first/4H-extended relationship, are source-supported. Bearish correction edges remain source gaps; the sell highest-wick/tail Stop Loss anchor is separate risk evidence, not the body-based target coordinate. Doji/gap and candle-inclusivity boundaries, turn membership, mitigation, remaining coordinates and PDH/PDL ranking remain source gaps. General final-TP behavior also remains unresolved.

## 25. Traceability requirements

Record at minimum: `Strategy`, `Strategy version`, `Instrument`, `Data source`, `Timestamp`, `Timezone`, `Market snapshot`, `Rules evaluated`, `Rule statuses`, `Evaluation results`, `Evidence`, `Evidence timestamp`, `Final verdict`, `Human validations`, `Entry`, `Stop Loss`, `Take Profit`, `Result`, `Failure classification` and `Trace identifier`. Preserve candle-close ordering and exclude future data.
