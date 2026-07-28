# MoneyWay Nasdaq open questions

No answer is proposed from external trading theory. `Blocking level` is the earliest affected capability.

## 4H structure

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-H4-001 | ¿Qué swing es estructural y qué algoritmo, velas y tolerancias lo identifican? | Controls Break/Wick/Fake classification. | `human_validation_required` | `semi_automatic_backtesting` | Mentor definition plus annotated positive, negative and marginal examples |

## Break

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BR-001 | ¿Qué distancia de cierre valida un Break y cómo se trata un cierre marginal? | Body-close concept is confirmed but threshold is not. | `unresolved` | `semi_automatic_backtesting` | Manually verified timestamp and boundary cases |

## Wick

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-WI-001 | ¿Cómo se distingue Wick de sweep y qué define Wickfill, fill e invalidación? | Prevents collapsing Wick into Break or liquidity take. | `human_validation_required` | `analysis` | Explicit explanation and annotated Wick/non-Wick cases |

## Fake

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-FA-001 | ¿Qué timeframe, cierre, distancia y número de velas confirman Fake y cómo afecta el bias? | Fake classification remains conceptual. | `human_validation_required` | `analysis` | Explicit criteria, sweep comparison and invalidation examples |

## Liquidity

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-LQ-001 | ¿Qué prioridad existe entre Asia, London, 1H/4H, equal highs/lows and prior-day liquidity? | Determines which level can enable the setup. | `unresolved` | `semi_automatic_backtesting` | Ranked examples and explicit mentor rationale |
| NQ-Q-LQ-002 | ¿Un nivel barrido expira, puede reutilizarse o cambia algo al barrer varios niveles? | Avoids invented validity/probability/risk rules. | `unresolved` | `semi_automatic_backtesting` | Repeated-level and multi-sweep cases with explicit outcomes |

## Session schedule

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SC-001 | ¿Cuál es la hora exacta de preparación y qué días, feriados, cierres o sesiones se permiten? | Controls reproducibility and no-trade dates. | `unresolved` | `paper_trading` | Verified schedule policy and calendar examples |
| NQ-Q-SC-002 | ¿Cómo se gestionan posiciones abiertas después de las 11:30? | 11:30 only confirms the latest new entry. | `unresolved` | `paper_trading` | Explicit open-position examples after cutoff |

## Timezone and DST

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-TZ-001 | ¿Qué timezone formal gobierna 08:30 y 11:30 y cómo se maneja DST? | `timezone: null` makes schedule automation unsafe. | `unresolved` | `semi_automatic_backtesting` | Original-video context plus explicit timezone/DST policy |

## Liquidity sweep

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-SW-001 | ¿Qué penetración, close-back, rechazo, desplazamiento, plazo e invalidación definen el sweep? | Sweep is mandatory before 5M inversion. | `human_validation_required` | `semi_automatic_backtesting` | Positive/negative/boundary sweeps with timestamps |
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
| NQ-Q-SL-001 | ¿Qué versión selecciona sweep extreme versus 5M HL/LH para wide/short sweeps? | Contradictory evidence blocks automatic demo execution. | `unresolved` | `paper_trading` | Manual review of original timestamps and additional unambiguous examples |
| NQ-Q-SL-002 | ¿Cómo se definen sweep size, body/wick, buffer, spread, maximum Stop and oversized-stop behavior? | Determines invalidation and risk. | `unresolved` | `paper_trading` | Approved quantitative policy and boundary cases |

## Break Even

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-BE-001 | ¿Cómo se identifica el primer post-entry swing y se elige entre varios? | Break-and-close depends on this distinct swing. | `human_validation_required` | `semi_automatic_backtesting` | Annotated post-entry swing sequences |
| NQ-Q-BE-002 | ¿BE es universal y mueve a entry o entry plus costs; qué gestión sigue? | `mandatory_for_every_trade` is only candidate. | `unresolved` | `paper_trading` | Multiple complete trade-management examples |

## Take Profit

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-TP-001 | ¿Cuál es la general target rule and priority among session/important liquidity or higher-timeframe targets? | Take Profit remains unresolved. | `unresolved` | `paper_trading` | Explicit target-selection policy and counterexamples |
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
| NQ-Q-MD-001 | ¿Qué instrument, symbol, provider, session candles and timestamp normalization are authoritative? | Required for Asia/London levels, closes and replay. | `unresolved` | `semi_automatic_backtesting` | Provider mapping, timezone policy and candle comparisons |

## Automation

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| NQ-Q-AU-001 | ¿Qué subjective criteria can become deterministic with validated accuracy? | Swing, context, sweep and FVG quality block full automation. | `unresolved` | `semi_automatic_backtesting` | Audited dataset, approved labels and out-of-sample tests |
| NQ-Q-AU-002 | ¿Qué manual timestamp verification and approvals enable demo execution? | Critical source claims require comparison with original video. | `unresolved` | `demo_execution` | Timestamp audit, approval protocol and traceability review |
| NQ-Q-AU-003 | ¿Qué evidence could ever permit autonomous execution? | The current version explicitly prohibits it. | `unresolved` | `autonomous_execution` | Future human decision after all rules, risk and controls are audited |
