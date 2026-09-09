# MoneyWay Nasdaq open questions

No answer is proposed from external trading theory. `Blocking level` is the earliest affected capability.

## 4H structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-H4-001 | ¿Cómo se obtiene el initial previous structural High/Low desde una serie arbitraria de raw closed 4H candles, sin human seed, arbitrary pivot o generic TA inference? | The next structural point can be confirmed once a prior reference exists, but the raw-series bootstrap cannot yet be reproduced. | `unresolved` | `semi_automatic_backtesting` | Explicit mentor initialization method plus annotated starting-history examples |
| NQ-Q-H4-002 | ¿Dónde comienzan y terminan exactamente el retracement/contraction relevante cuyo turning extreme se revisa después del confirming break? | The source rejects minor pauses and identifies the relevant turn retrospectively, but does not provide reproducible interval boundaries. | `human_validation_required` | `semi_automatic_backtesting` | Annotated interval boundaries with included and discarded pauses |
| NQ-Q-H4-003 | ¿Qué exact body price representa numéricamente un structural High/Low o retracement turning point? | The source marks structure on candle bodies but does not define an OHLC/body formula. | `unresolved` | `semi_automatic_backtesting` | Explicit coordinate rule plus marginal multi-candle/body-zone examples |

Confirmed or materially narrowed by the new human source review:

- HH/LL require a 4H body close beyond the prior human-selected structural High/Low; wick-only breaks are insufficient.
- Before that subsequent break closes, the preceding retracement is candidate/unconfirmed. Only after the close may the relevant prior retracement be confirmed retrospectively as HL/LH.
- The mentor then looks backward to the retracement preceding the breakout impulse and identifies **"el punto exacto donde el precio desaceleró y rebotó"**. In bullish context this is the lowest relevant turning point of that retracement/contraction; the bearish relation is symmetric.
- Minor fluctuations, intermediate pauses, local extrema and direction changes do not automatically become structural points.
- Structural points and relevant turning/stop zones are marked on candle bodies; isolated wicks are not strong structural confirmation.
- At 08:00 `America/Bogota`, use only information observable through the closed 4H candle. Lower-timeframe noise does not independently create 4H structural points.

Still unresolved: the initial prior-level bootstrap from raw 4H history, exact retracement interval boundaries and exact numeric body coordinate. No first-candle, first-two-candle, fixed-lookback, fractal, ZigZag, N-left/N-right, ATR, percentage-swing or fixed-point initialization is supplied. Replay must not label the retracement as confirmed before the subsequent break closes.

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
| NQ-Q-LQ-001 | ¿Qué prioridad existe entre Asia, London, 1H/4H, equal highs/lows and prior-day liquidity? | Determines which level can enable the setup. | `unresolved` | `semi_automatic_backtesting` | Ranked examples and explicit mentor rationale |
| NQ-Q-LQ-002 | ¿Un nivel barrido expira, puede reutilizarse o cambia algo al barrer varios niveles? | Avoids invented validity/probability/risk rules. | `unresolved` | `semi_automatic_backtesting` | Repeated-level and multi-sweep cases with explicit outcomes |
| NQ-Q-LQ-004 | ¿Qué algoritmo identifica puntos estructurales 1H/4H y qué tolerancia define que coinciden con un extremo de sesión? | Structural confluence is confirmed but not deterministic. | `human_validation_required` | `semi_automatic_backtesting` | Annotated structural points and boundary cases |

### Resolved session-liquidity question

| Question ID | Resolution | Current status | Evidence |
|---|---|---|---|
| NQ-Q-LQ-003 | For day `D`, Asia is `[D-1 17:00, D 02:00)` and London is `[D 02:00, D 07:00)` in `America/Bogota`, start-inclusive and end-exclusive. Completed 1H candles belong by local `OpenTime`; session High/Low are maximum `High`/minimum `Low`. | `confirmed` | Manual source-video re-verification |

## Session schedule

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SC-001 | ¿Qué días, feriados, cierres o sesiones se permiten? | Preparation is confirmed at 08:00 `America/Bogota`, but calendar eligibility remains undefined. | `unresolved` | `paper_trading` | Verified calendar policy and examples |
| NQ-Q-SC-002 | ¿Cómo se gestionan posiciones abiertas después de las 11:30? | 11:30 only confirms the latest new entry. | `unresolved` | `paper_trading` | Explicit open-position examples after cutoff |

## Liquidity sweep

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SW-001 | ¿Qué penetración mínima, close-back, rechazo, desplazamiento, plazo e invalidación aplican después de superar el high/low? | Exceeding the identified high/low is confirmed, but boundary and lifecycle details remain open. | `human_validation_required` | `semi_automatic_backtesting` | Positive/negative/boundary sweeps with timestamps |
| NQ-Q-SW-002 | ¿Puede utilizarse una toma anterior a 08:30? | Affects operating sequence. | `unresolved` | `paper_trading` | Explicit pre-open examples and mentor decision |

## 5M structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-M5-001 | ¿Cómo se selecciona el swing relevante, incluidos pivotes, swings internos y cierres marginales? | Traditional inversion requires a reproducible swing and close. | `human_validation_required` | `semi_automatic_backtesting` | Annotated structural changes and rejected alternatives |

## IFVG

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-IF-001 | ¿Cuál es la geometría, dirección, close, mitigation, confirming candle, expiry y relación con displacement del IFVG? | IFVG is an OR alternative but lacks operational definition. | `unresolved` | `semi_automatic_backtesting` | Audited mentor definition; no external ICT/SMC source |
| NQ-Q-IF-002 | ¿Puede IFVG habilitar una entrada directa? | Prevents bypassing required continuation and 1M stages. | `unresolved` | `paper_trading` | Explicit full-sequence examples |

## Continuation FVG

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-FVG-001 | ¿Qué tamaño relativo/absoluto hace el FVG claro y fuerte? | One/two points are insufficient, but minimum is null. | `human_validation_required` | `semi_automatic_backtesting` | Annotated accepted/rejected FVGs across volatility conditions |
| NQ-Q-FVG-002 | ¿Cómo se tratan fill, invalidation, lifetime y multiple FVG selection? | Controls whether the mandatory FVG remains usable. | `unresolved` | `semi_automatic_backtesting` | Sequenced examples and explicit lifecycle rules |

## 1M entry

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-M1-001 | ¿Cómo se selecciona el último swing correctivo y cuánto puede tardar la realineación? | This swing gates entry. | `human_validation_required` | `semi_automatic_backtesting` | Annotated corrections, pivots, failures and timeouts |
| NQ-Q-M1-002 | ¿Qué order type, timing, slippage, distance and maximum attempts apply? | Defines executable entry mechanics. | `unresolved` | `paper_trading` | Audited execution examples and explicit limits |
| NQ-Q-M1-003 | ¿Qué ocurre si la confirmación aparece después de las 11:30? | Cutoff applies to new entries. | `unresolved` | `paper_trading` | Explicit after-cutoff examples |

## Stop Loss

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SL-001 | ¿Qué algoritmo identifica el structural 5M HL para buys y el structural 5M LH para sells? | The conceptual reference is confirmed, but deterministic swing selection is not. | `human_validation_required` | `semi_automatic_backtesting` | Annotated HL/LH selections and rejected alternatives |
| NQ-Q-SL-002 | ¿Cómo se formaliza el punto donde el trade pierde sentido, incluidos body/wick, buffer, spread, maximum Stop and oversized-stop behavior? | Conceptual invalidation is confirmed but not quantitatively reproducible. | `unresolved` | `paper_trading` | Approved quantitative policy and boundary cases |

## Break Even

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BE-001 | ¿Cómo se identifica el primer post-entry swing y se elige entre varios? | Break-and-close depends on this distinct swing. | `human_validation_required` | `semi_automatic_backtesting` | Annotated post-entry swing sequences |
| NQ-Q-BE-002 | ¿BE es universal y mueve a entry o entry plus costs; qué gestión sigue? | `mandatory_for_every_trade` is only candidate. | `unresolved` | `paper_trading` | Multiple complete trade-management examples |

## Take Profit

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-TP-001 | ¿Qué algoritmo convierte un high en important para buys o un low en important para sells y cómo se priorizan varios candidatos? | Target direction is confirmed but exact selection remains unresolved. | `human_validation_required` | `paper_trading` | Annotated important/non-important levels and priority counterexamples |
| NQ-Q-TP-002 | ¿Existe fixed RR, partials, trailing or manual close? | Required for reproducible results and management. | `unresolved` | `paper_trading` | Complete audited trades and explicit management statements |

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
