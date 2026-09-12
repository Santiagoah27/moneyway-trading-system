# MoneyWay Nasdaq open questions

No answer is proposed from external trading theory. `Blocking level` is the earliest affected capability.

## 4H structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-H4-001 | ¿Qué reproducible viewport/boundary selecciona el major visible extreme que originó el movimiento relevante al iniciar desde raw closed 4H candles? | The visual/contextual bootstrap and subsequent reconstruction are confirmed, but an arbitrary raw series cannot select the same starting extreme automatically. | `human_validation_required` | `semi_automatic_backtesting` | Annotated starting histories showing included/excluded extremes and the exact observation boundary |
| NQ-Q-H4-002 | ¿Cómo se manejan dojis/gaps, la inclusividad exacta de los prior HH/LL boundaries y la membresía del turn dentro de las correction windows? | Confirming-candle exclusion and the single-candle `Open` origin coordinate are confirmed. The remaining correction-edge semantics are not reproducible. | `PARTIALLY_DEFINED` | `semi_automatic_backtesting` | Annotated bullish and bearish doji/gap, boundary-inclusivity and turn-membership cases |
| NQ-Q-H4-003 | ¿Cuándo runtime necesita identidad de candle/timestamp para el body anchor y cuáles son las coordenadas para structural points distintos de los candidate HL/LH? | Equal body coordinates preserve one candidate price when only price is needed, but tie identity, turn membership and other-point coordinates remain undefined. For a selected LH turn, the highest `Open`/`Close` body edge is confirmed; the separate sell SL highest-wick anchor does not alter it. | `PARTIALLY_DEFINED` | `semi_automatic_backtesting` | Annotated equal-coordinate identity cases, turn-membership edges and direct coordinates for other structural-point classes |

Confirmed or materially narrowed by the new human source review:

- On the relevant 1H or 4H timeframe, HH/LL require a formally closed candle body beyond the prior human-selected structural High/Low; wick-only penetration is insufficient and may instead be classified contextually as liquidity take, Wickfill or Fakeout.
- A candidate low becomes a validated HL only when the impulse originating from it later produces a formally closed body above the prior HH on the same relevant timeframe. A rebound without that close does not validate the low as structural HL.
- A candidate high becomes a validated LH only when the impulse originating from it later produces a formally closed body below the prior LL on the same relevant timeframe. A decline without that close does not validate the high as structural LH.
- Before the confirming break candle closes, the preceding retracement remains candidate/unconfirmed. An open 1H/4H candle that temporarily trades beyond the level cannot validate it.
- For candidate HL, the mentor looks backward across the human-delimited corrective phase and discards preliminary/intermediate lows. The selected candidate is the deepest low of that correction and the base from which the impulse that later closes above the prior HH originates.
- Small one- or two-candle pauses inside that impulse do not create another candidate HL.
- For bullish candidate HL, the prior HH remains the upper structural boundary. Once HH production stops, the first bearish candle starts the correction; it remains active through deeper lows/zigzags and ends only when a formally closed candle has `Close > prior HH`.
- A deeper low inside that active correction replaces a shallower candidate. The deepest candidate becomes validated only from the causal close `AsOfUtc`; later data cannot alter an earlier historical evaluation.
- Direct bearish evidence shows the retracement originating from the prior LL area. Its candidate high remains provisional until a formally closed candle has `Close < prior LL`; only then are the new LL and preceding LH validated from that causal `AsOfUtc`. Wick-only or open-candle penetration is insufficient.
- Within one recognized bearish correction, the candidate LH is the highest candidate high. A later higher high replaces the prior candidate, and superseded intermediate highs do not survive. This complex candidate-LH identity and highest-high replacement are directly confirmed; the candidate becomes validated only from the causal closed `Close < prior LL` `AsOfUtc`.
- Bearish correction edge semantics, including dojis, gaps, exact boundary inclusivity and correction segmentation, remain partially defined. The causal break candle is confirmed excluded from correction membership/scanning and belongs to the impulsive leg. Within an already selected LH turn, the structural coordinate and bearish body anchor are confirmed as the highest `Open`/`Close` body edge among member candles.
- The sell risk example anchors Stop Loss above the highest wick/tail of the relevant LH. This wick anchor remains separate from the confirmed body-based structural LH coordinate for target selection, reconstruction or comparisons.
- Within the selected candidate-HL turn, the structural level is the lowest `Open` or `Close` of the included candle bodies. An isolated wick does not define it.
- Doji/gap behavior, exact prior-HH/LL boundary inclusivity and turn membership remain unresolved. Equal body coordinates do not make the selected price ambiguous when only price is needed, but candle/timestamp identity remains unresolved.
- In the general case, the causal break candle is excluded from the correction set and retrospective correction scan. Its close confirms the new HH/LL, ends the correction and validates the preceding candidate from that `AsOfUtc`.
- In the single-expansive-candle case, the same candle may contain impulse-origin evidence and the confirming close without becoming an ordinary correction member. Its `Open` is the structural HL/LH origin coordinate and its `Close` is the causal validation event. The buy protection anchor remains its `Low`/lowest wick and the sell protection anchor its `High`/highest wick; no intrabar chronology is inferred.
- At 08:00 `America/Bogota`, use only information observable through the closed 4H candle. Lower-timeframe noise does not independently create 4H structural points.

The latest human review further confirms that 4H bootstrap starts visually/contextually from the major visible extreme that originated the current relevant movement, then reconstructs push, retracement, structural break and current structure. Asia/London session extrema do not seed 4H structure. The current decision focuses on the latest active HH/HL or LL/LH pair without deleting older audit history.

Current boundaries: `STRUCTURAL_VALIDATION_RULE = CONFIRMED`; `LH_CAUSAL_VALIDATION = CONFIRMED`; `BEARISH_CORRECTION_ORIGIN = CONFIRMED`; `CANDIDATE_HL_SELECTION = PARTIALLY_DEFINED`; `BULLISH_CORRECTION_START = PARTIALLY_DEFINED`; `BULLISH_CORRECTION_END = CONFIRMED`; `BULLISH_CORRECTION_WINDOW = PARTIALLY_DEFINED`; `CANDIDATE_HL_PRICE_SELECTION = PARTIALLY_DEFINED`; `CANDIDATE_HL_IDENTITY_SELECTION = UNRESOLVED`; `DOJI_BOUNDARY_BEHAVIOR = UNRESOLVED`; `COMPLEX_CANDIDATE_LH_SELECTION = CONFIRMED`; `HIGHEST_HIGH_REPLACEMENT = CONFIRMED`; `BEARISH_CORRECTION_WINDOW = PARTIALLY_DEFINED`; `CANDIDATE_LH_STRUCTURAL_PRICE = CONFIRMED`; `BEARISH_STRUCTURAL_BODY_ANCHOR = CONFIRMED`; `SELL_LH_SL_ANCHOR = CONFIRMED`; `STRUCTURAL_IDENTITY_VS_PRICE_SEPARATION = CONFIRMED`; `CONFIRMING_CANDLE_CORRECTION_MEMBERSHIP = CONFIRMED_EXCLUDED`; `CONFIRMING_CANDLE_VALIDATION_ROLE = CONFIRMED`; `IMPULSE_LEG_MEMBERSHIP = PARTIALLY_DEFINED`; `SINGLE_CANDLE_BREAK_ORIGIN_CASE = CONFIRMED`; `CONFIRMING_CANDLE_SCAN_INCLUSIVITY = CONFIRMED_EXCLUDED`; `SINGLE_CANDLE_ORIGIN_PRICE_COORDINATE = CONFIRMED`; `SINGLE_CANDLE_HL_STRUCTURAL_PRICE = CONFIRMED`; `SINGLE_CANDLE_LH_STRUCTURAL_PRICE = CONFIRMED`; `BUY_HL_PROTECTION_ANCHOR = CONFIRMED`; `SELL_LH_PROTECTION_ANCHOR = CONFIRMED`; `ACTUAL_SL_OFFSET_FROM_ANCHOR = UNRESOLVED`; `STRUCTURE_VS_PROTECTION_SEPARATION = CONFIRMED`; `CORRECTION_PHASE_BOUNDARIES = PARTIALLY_DEFINED`; `STRUCTURAL_BODY_ANCHOR = PARTIALLY_DEFINED`; `BODY_ANCHOR_TIE_BREAKING = UNRESOLVED`; `STRUCTURAL_CANDIDATE_DETECTION = PARTIALLY_DEFINED`.

Still unresolved: a reproducible viewport for the visual bootstrap, doji/gap handling, exact prior-HH/LL boundary inclusivity, turn membership, equal-coordinate body identity, other structural-point coordinates, mitigation semantics and the executable Stop Loss offset beyond the confirmed protection anchor. Confirming-candle correction membership, scan exclusion and the single-candle `Open` origin coordinate are no longer open. No fixed-lookback, fractal, ZigZag, N-left/N-right, ATR, percentage-swing, candle-count threshold, tolerance or fixed-point initialization is supplied. Replay must not label candidate HL or LH as confirmed before the corresponding break closes; at the causal `AsOfUtc`, the already-observed `Open` may become the validated structural origin coordinate while `Close` supplies validation, without reconstructing an intrabar path.

## Resolved workflow relationships

- Canonical order is Step 1 4H context → Step 2 liquidity marking → Step 3 liquidity take → Step 4 5M Structural Change `OR` IFVG → Step 5 separate mandatory 5M FVG → Step 6 1M pullback/FVG interaction and realignment → entry eligibility.
- Every downstream gate requires its prerequisites in chronological order; a later pattern cannot repair a missing earlier gate through hindsight.
- Step 1 market structure and Step 2 session-liquidity references are separate.
- A Step-4 IFVG is not a direct entry and does not automatically satisfy the Step-5 FVG gate.
- At `StrategyReplayContext.AsOfUtc`, future liquidity takes, FVGs or 1M realignments cannot change a prior eligibility state.
- Preparation remains 08:00 and the operational entry-acquisition interval is `[08:30, 11:00)` in `America/Bogota`. The former 11:30 cutoff was an incorrect interpretation and is not a second limit.
- At 11:00, an unfinished pre-entry setup expires. A dead-zone signal cannot revive it; no forced close of an already-entered conceptual trade is established.
- At or after 08:30, the first valid event that exceeds a relevant High or goes below a relevant Low is the effective Step 3 for the scenario it may orient; it is not target consumption before Step 3.
- A Low take can orient a possible buy progression and a High take can orient a possible sell progression only when aligned with the Step-1 4H permitted direction. Without alignment, Step 4 is blocked, the prior imagined scenario remains discarded and price is not chased.
- A valid liquidity take activates waiting for Step 4. Continued manipulation-direction movement and a wick-only break do not confirm Step 4 and do not alone cancel the setup.
- Before Step 4 and before cutoff, each valid farther same-side take keeps one active setup at Step 3 and updates its current 5M structural reference while prior observations remain auditable. It does not create a concurrent setup or satisfy Step 4.
- Once Step 3 has activated a setup, exact touch of its already-selected intended target before completed pre-entry progression cancels that setup; wick contact is sufficient without close or tolerance. Do not chase price. Later evidence cannot revive it. This active-setup cancellation is distinct from selecting a structural fallback when a session target was consumed before the operational setup context.
- The opposite take never reverses permitted direction automatically. When aligned with the existing Step-1 4H direction, it may activate Step 3 of a new chronological setup; otherwise it creates no countertrend setup.
- Exact runtime mapping of an unmet prerequisite to `failed`, `waiting` or `not_applicable` remains unaudited.

## Break

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BR-001 | ¿Cómo se selecciona el structural support/resistance level o zone relevante y qué width, tolerance o minimum penetration se aplica a un marginal close? | A 4H body close beyond the selected level/zone is confirmed and wick-only penetration is rejected, but the structural input and numeric boundary remain undefined. | `unresolved` | `semi_automatic_backtesting` | Annotated level/zone selections and positive, negative and marginal close cases |

## Wick

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-WI-001 | ¿Qué prior wick extreme completa Wickfill, basta tocarlo o debe superarse, se requiere close y qué retracement/zone/tolerance aplica? | The post-Breakout wickfill progression is confirmed, but its completion and selected inputs are not deterministic. | `human_validation_required` | `analysis` | Annotated complete/incomplete Wickfill sequences, including multiple extrema and boundary cases |

Confirmed by manual source-video re-verification: Wickfill follows an extended Breakout that left a wick/extreme; after price moves away or retraces, a later impulse seeks to travel through that space and reach or exceed the prior extreme. Approximate evidence: definition `04:23–04:32`, example `06:55–07:25`. “Reach or exceed” does not resolve the exact completion comparison.

## Fake

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-FA-001 | ¿Cómo se selecciona el relevant 4H range/zone, cuánto puede tardar el retorno y qué tolerance, returning-candle choice, invalidation y bias effect aplican? | A new candle closing back inside after a failed Breakout is confirmed, but zone selection and lifecycle remain non-deterministic. | `human_validation_required` | `analysis` | Annotated positive, negative, marginal and overlapping-context examples |

Confirmed by manual source-video re-verification: Fakeout is a Breakout that did not complete; price returns and a new candle closes back inside the relevant range/zone. Wick-only return is insufficient. Approximate evidence: definition `10:40`, return example `10:55–11:25`.

## 4H context relationship

Confirmed conceptually: at 08:00 `America/Bogota`, Breakout `OR` Wickfill `OR` Fakeout are alternative current 4H context classifications. Causally, Wickfill may follow an extended Breakout and Fakeout is a failed Breakout that closes back inside. Formal mutual exclusivity and precedence remain `human_validation_required` when visual conditions appear to coexist.

## Liquidity

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-LQ-001 | ¿Cómo se manejan dojis/gaps, boundary inclusivity y turn membership dentro de las correction windows; qué significa no mitigado y cómo se ordenan PDH/PDL frente a 1H/4H? | Confirming-candle correction membership/scan exclusion, its causal validation role and the single-candle `Open` origin coordinate are confirmed. Window edges, mitigation and cross-class ranking remain unresolved or partial. | `PARTIALLY_DEFINED` | `semi_automatic_backtesting` | Annotated bullish/bearish edge cases, other structural classes, mitigation boundaries, PDH/PDL coexistence and ranking ties |
| NQ-Q-LQ-002 | ¿Un nivel ya barrido expira o puede reutilizarse como future relevant level, y altera su consumo previo alguna validity/probability/risk consideration? | Same-side setup association is resolved, but level reuse and any probability/risk effect remain undefined. | `unresolved` | `semi_automatic_backtesting` | Repeated-level cases with explicit validity, probability and risk outcomes |
| NQ-Q-LQ-004 | ¿Qué algoritmo detecta los candidate turning points 1H/4H y qué tolerancia define que los niveles ya validados coinciden con un extremo de sesión? | Causal validation and structural confluence are confirmed, but candidate detection and coincidence geometry are not deterministic. | `human_validation_required` | `semi_automatic_backtesting` | Annotated candidate turns, confirming closes and coincidence boundary cases |

### Resolved session-liquidity question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-LQ-003 | For day `D`, Asia is `[D-1 17:00, D 02:00)` and London is `[D 02:00, D 07:00)` in `America/Bogota`, start-inclusive and end-exclusive. Completed 1H candles belong by local `OpenTime`; session High/Low are maximum `High`/minimum `Low`. | `confirmed` | Manual source-video re-verification |

## Session schedule

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SC-001 | ¿Qué días, feriados, cierres o sesiones se permiten? | Preparation is confirmed at 08:00 `America/Bogota`, but calendar eligibility remains undefined. | `unresolved` | `paper_trading` | Verified calendar policy and examples |
| NQ-Q-SC-002 | ¿Cómo se gestionan posiciones abiertas después de las 11:00? | The 11:00 cutoff governs acquisition of new entries; no forced close of an already-entered conceptual trade is confirmed. | `unresolved` | `paper_trading` | Explicit post-entry management examples after cutoff |

## Liquidity sweep

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SW-001 | ¿Qué close-back, rechazo o desplazamiento adicional, si alguno, califica un take después de superar el selected High o bajar del selected Low? | Direction-dependent setup association is now confirmed, but any qualification beyond exceed/break-below remains undefined. | `human_validation_required` | `semi_automatic_backtesting` | Positive, negative and marginal take examples |
| NQ-Q-SW-002 | ¿Puede utilizarse una toma anterior a 08:30? | Affects operating sequence. | `unresolved` | `paper_trading` | Explicit pre-open examples and mentor decision |

Resolved by direct human review of Video 3 at approximately `06:20–06:40` and `10:30–10:55`: same-side subsequent takes retain one pending setup at Step 3 and roll its active 5M reference; opposite-side target consumption cancels the setup; the opposite take can become Step 3 of a new setup only when aligned with Step-1 4H context. There is no source support for concurrent same-direction setups or automatic bias reversal.

### Resolved post-08:30 Step-3 orientation question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-SW-006 | After 08:30, the first valid take of relevant liquidity is itself the effective Step 3; it is not target consumption in a separate interval before Step 3. Taking a relevant Low can orient a possible buy progression, and taking a relevant High can orient a possible sell progression, only when aligned with the Step-1 4H permitted direction. Without alignment, Step 4 is not enabled, the originally imagined scenario remains discarded and price is not chased. | `confirmed` | Direct human review of Video 3 approx. `07:50–09:15`; alignment reference approx. `04:25` |

The confirmed Step-3 boundary remains `price exceeds the selected relevant High` or `price goes below the selected relevant Low`. The reviewed conversational words "touch/take" do not unequivocally establish equality-only activation, and no threshold or tolerance is inferred. This `CONFIRMED_EXCEEDS` geometry is distinct from `TARGET_TOUCH_EVENT`, where exact contact is sufficient for an already-selected target.

### Resolved target-consumption question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-SW-003 | The candidates are London Low/Asia Low for sells and London High/Asia High for buys. When distinct, the immediate target is the first relevant level encountered in the expected direction of travel. Exact contact with the horizontal session-extreme wick level is sufficient, including wick contact; no body close, candle close, tolerance or penetration buffer applies. A pre-entry touch cancels the active setup. | `confirmed` | Direct human review of Video 3 approx. `09:40–10:55` and `13:25–13:40` |
| NQ-Q-SW-005 | No candle timeframe is normative. The target is an absolute Asia/London price level and `TARGET_TOUCH_EVENT` is a timeframe-independent price-quote touch. The mentor's 1M chart is the visual post-entry management surface, not part of the target definition. | `confirmed` | Direct human review of Video 3 approx. `13:10–13:45` |

### Resolved target edge and fallback semantics

- When the relevant Asia and London targets have the same absolute price, they collapse into one unique target rather than two sequential targets. In the reviewed scenario, the shared level is final Take Profit and does not create an artificial intermediate Break-Even transition.
- When a relevant session target was already consumed before the operational setup context, or otherwise no longer remains the next future-valid target before that context, the source confirms structural fallback from Step 2. Sell candidates include relevant unmitigated support represented by a validated 1H/4H HL or LL and Previous Day Low. Buy candidates include relevant unmitigated resistance represented by a validated 1H/4H LH or HH and Previous Day High.
- In reviewed paths where distinct relevant 1H and 4H fallback levels coexist, 1H acts as the first objective or management level and the farther 4H level as the final or extended objective; 4H is structurally stronger. This evidence-bounded relationship is not a universal ranking formula.
- Structural support/resistance is marked from candle bodies, not an isolated long wick. Exact HL/LL/LH/HH detection, OHLC mitigation semantics, candle/body selection, reduction of a body zone to one decimal, PDH/PDL priority and cross-class ties remain unresolved. The fallback is therefore `PARTIALLY_DEFINED`, not deterministic.
- Structural fallback does not rescue an originally imagined scenario after an opposite-liquidity take at or after 08:30.
- Once a setup is active with a selected target, touching that target before entry completion cancels the setup. This is not a fallback event: do not chase price and do not revive the cancelled setup with later evidence.
- Direct source trace for the latest fallback reconciliation: Video 3 approx. `03:10–03:25` and `05:55–06:40`; Video 1 approx. `10:10–10:35`; Video 2 approx. `08:35–09:20` and `11:10–11:45`.
- At or after 08:30, the first valid opposite-liquidity take is the effective Step 3 for the newly oriented scenario. The prior imagined scenario is discarded. Progression to Step 4 requires alignment with Step-1 4H; otherwise there is no new setup, revival or chase.

### Replay data-resolution limitation

`NQ-Q-SW-004` is reclassified as `MARKET_DATA_RESOLUTION_LIMITATION`, not an unresolved strategy timeframe rule. Current canonical replay exposes closed OHLC `CandleSeries`. A candle range can establish `TARGET_WAS_TOUCHED_DURING_INTERVAL`, but when the same minimum-granularity candle also completes `NQ-M1-003`, OHLC cannot establish intrabar order. The audit categories are `UNAMBIGUOUS_CANDLE_TOUCH` for a target first observed in a later candle after entry progression was already established, and `AMBIGUOUS_SAME_OBSERVATION_TOUCH` when touch and final confirmation share one minimum observation. No runtime result or verdict mapping is selected.

## 5M structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-M5-001 | ¿Cómo se convierte el extreme left by the liquidity take en la primera referencia 5M y cómo se seleccionan después swing, pivots, internal swings, updated active references y marginal closes? | A newer extreme may update the active reference before Step 4, but exact reference/associated-low selection and strong/decisive close criteria are not reproducible. | `human_validation_required` | `semi_automatic_backtesting` | Annotated post-take structure with accepted/rejected initial and updated references, associated levels and closes |

## IFVG

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-IF-001 | ¿Cuál es la geometría, dirección, close, mitigation, confirming candle, expiry y relación con displacement del IFVG? | IFVG is an OR alternative but lacks operational definition. | `unresolved` | `semi_automatic_backtesting` | Audited mentor definition; no external ICT/SMC source |
| NQ-Q-IF-002 | ¿Cómo afecta una actualización same-side de la active 5M reference a un IFVG candidate previo o en formación? | The setup-level reference update is confirmed, but branch-specific reset/retention behavior was not stated. | `unresolved` | `semi_automatic_backtesting` | Sequenced same-side update examples that include the IFVG branch |

Resolved: IFVG is only an alternative inside Step 4. It does not enable direct entry, bypass Step 5 or automatically count as the mandatory Step-5 FVG.

## Continuation FVG

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-FVG-001 | ¿Qué tamaño relativo/absoluto hace el FVG claro y fuerte? | One/two points are insufficient, but minimum is null. | `human_validation_required` | `semi_automatic_backtesting` | Annotated accepted/rejected FVGs across volatility conditions |
| NQ-Q-FVG-002 | ¿Cómo se tratan fill, invalidation, lifetime y multiple FVG selection? | Controls whether the mandatory FVG remains usable. | `unresolved` | `semi_automatic_backtesting` | Sequenced examples and explicit lifecycle rules |

## 1M entry

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-M1-001 | ¿Qué exact interaction depth/tolerance con el Step-5 FVG, corrective swing selection y timeout confirman el pullback y la realineación 1M? | Countertrend pullback toward/into the FVG followed by intended-direction realignment is confirmed, but its geometry is not reproducible. | `human_validation_required` | `semi_automatic_backtesting` | Annotated FVG interactions, corrections, pivots, failures and timeouts |
| NQ-Q-M1-002 | ¿Qué order type, timing, slippage, distance and maximum attempts apply? | Defines executable entry mechanics. | `unresolved` | `paper_trading` | Audited execution examples and explicit limits |

### Resolved entry-cutoff question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-M1-003 | A confirmation at or after 11:00 cannot complete or revive an unfinished pre-entry setup. The operational entry-acquisition interval is `[08:30, 11:00)`; after cutoff, do not chase and return the next operational day. This does not define forced closure of a trade entered before cutoff. | `confirmed` | Direct human source re-review; Video 3 approx. `04:15–04:30` |

## Stop Loss

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SL-001 | ¿Qué algoritmo identifica el structural 5M HL para buys y el structural 5M LH para sells? | The conceptual reference is confirmed, including the buy `Low`/lowest-wick and sell `High`/highest-wick protection anchors after the relevant turn is selected, but deterministic swing selection is not. | `human_validation_required` | `semi_automatic_backtesting` | Annotated HL/LH selections and rejected alternatives |
| NQ-Q-SL-002 | ¿Qué offset exacto más allá de la wick estructural aplica, incluidos spread, maximum Stop and oversized-stop behavior? | The buy `Low` and sell `High` protection anchors are confirmed, but “below/above” does not define an executable price, buffer, spread/cost adjustment or broker order semantics. This does not redefine structural coordinates. | `unresolved` | `paper_trading` | Approved quantitative offset/cost policy and boundary cases |

## Break Even

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BE-003 | ¿Qué relación conserva el earlier post-entry swing observation con el nuevo first-important-liquidity trigger? | The authoritative trigger changed; silently combining or substituting both would invent management behavior. | `unresolved` | `paper_trading` | Complete examples showing both events and the mentor's chosen trigger |
| NQ-Q-BE-004 | ¿Mover a entry/Break-Even usa el entry price bruto o incorpora costos, y el trigger aplica universalmente a toda operación? | Touch and target selection are narrowed, but exact executable BE price and universality are not established. | `unresolved` | `paper_trading` | Approved cost-basis policy and diverse complete trades |

### Resolved Break-Even questions

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-BE-001 | For distinct targets, a sell uses the first relevant selected Low encountered downward and a buy uses the first relevant selected High encountered upward. When the relevant Asia/London prices are equal, they are one final target in the reviewed scenario and do not create an intermediate Break-Even step. Structural-fallback management beyond its target role remains unaudited. | `confirmed` for the reviewed session-target cases | Direct human review of Video 3 approx. `03:10–03:25`, `08:50–10:55` and `13:25–13:40`; Video 1 approx. `10:10–10:30` |
| NQ-Q-BE-002 | Exact touch is sufficient, including wick contact, without body close, candle close or tolerance. The source supports moving Stop Loss to entry/Break-Even; executable cost basis remains covered by `NQ-Q-BE-004`. | `confirmed` | Direct human review of Video 3 approx. `09:40–10:55` and `13:25–13:40` |

## Take Profit

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-TP-001 | ¿El primer target Asia/London encontrado es siempre el TP final y exige salida completa, o puede ser solo un target de gestión previo? | One reviewed case supports full exit on touch, but it does not establish universal equivalence between the first BE target and final TP. | `unresolved` | `paper_trading` | Complete trades distinguishing first management target from final exit |
| NQ-Q-TP-002 | ¿Existe fixed RR, partials, trailing or manual close? | Required for reproducible results and management. | `unresolved` | `paper_trading` | Complete audited trades and explicit management statements |

Confirmed target scope: primary buy candidates are London High and Asia High; primary sell candidates are London Low and Asia Low. When distinct and future-valid, first encountered in the expected price path determines the immediate target. Equal relevant session prices collapse into one unique target and the reviewed equal case treats it as final Take Profit without an intermediate Break-Even transition. If no relevant session candidate remains future-valid before the operational setup context, structural fallback is `PARTIALLY_DEFINED`: sells may use relevant unmitigated support at a validated 1H/4H HL or LL or Previous Day Low; buys may use relevant unmitigated resistance at a validated 1H/4H LH or HH or Previous Day High. In reviewed multi-level paths, 1H is the first objective or management level and the farther 4H level is final or extended, without establishing a universal formula. Candidate-HL and candidate-LH selected-turn body coordinates are confirmed, while deterministic detection, correction-edge handling, mitigation, other structural-point coordinates and PDH/PDL ranking remain unresolved or partial. The selected target is an absolute price and its touch is a timeframe-independent quote-level event without close or tolerance. Video 3 approximately `13:25–13:40` supports complete exit in one additional distinct-target case only, so universal final-exit behavior remains unresolved. Closed-candle replay can observe occurrence from OHLC range but not same-candle intrabar order.

## Risk

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-R-001 | ¿Cómo se calcula position size using points/ticks, tick value, spread, commissions and account size? | A confirmed 1% cap is not executable without sizing. | `unresolved` | `paper_trading` | Nasdaq-specific approved formula and calculation cases |
| NQ-Q-R-002 | ¿Qué trade-count, consecutive-loss, reduction, kill-switch and rejected-order controls apply? | Missing controls block safe automation. | `unresolved` | `demo_execution` | Approved risk policy and failure-path tests |

## Daily limits

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-DL-001 | ¿El límite diario de pérdida de 1% es correcto, qué incluye y cuándo se reinicia? | Current evidence is candidate only. | `candidate` | `paper_trading` | Manual verification of closing segment and explicit scope |

## Reentries

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-RE-001 | ¿La frase de no reentry aplica tras BE, SL, same setup, whole day or independent setup, and how many attempts? | Prevents an unsupported universal ban. | `unresolved` | `paper_trading` | Full quoted context and multiple exit/reentry examples |

## News

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-NE-001 | ¿Qué events/source/windows/open-position/new-entry/resumption policy applies? | The 10:00/09:45 example is contextual only. | `unresolved` | `paper_trading` | Explicit policy across multiple news cases and calendar source |

## Market data

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-MD-001 | ¿Qué instrument, symbol, provider and timestamp normalization are authoritative, and how is missing session data reported? | Required for trustworthy Asia/London levels, closes and replay; timezone, session intervals and 1H membership are already confirmed. | `unresolved` | `semi_automatic_backtesting` | Provider mapping, timestamp comparisons and missing-data cases |

## Automation

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-AU-001 | ¿Qué subjective criteria can become deterministic with validated accuracy? | Swing, context, sweep and FVG quality block full automation. | `unresolved` | `semi_automatic_backtesting` | Audited dataset, approved labels and out-of-sample tests |
| NQ-Q-AU-002 | ¿Qué manual timestamp verification and approvals enable demo execution? | Critical source claims require comparison with original video. | `unresolved` | `demo_execution` | Timestamp audit, approval protocol and traceability review |
| NQ-Q-AU-003 | ¿Qué evidence could ever permit autonomous execution? | The current version explicitly prohibits it. | `unresolved` | `autonomous_execution` | Future human decision after all rules, risk and controls are audited |
