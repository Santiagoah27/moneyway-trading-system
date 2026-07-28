# MoneyWay Forex open questions

No proposed trading answer is included. `Blocking level` identifies the earliest affected capability.

## Weekly context

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-WC-001 | ¿Cuál es la definición exacta de tendencia semanal y cómo se tratan los rangos? | Determina dirección/contexto antes de continuar. | `unresolved` | `semi_automatic_backtesting` | Explicación explícita del mentor y casos anotados de tendencia/rango |

## Weekly zones

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-WZ-001 | ¿Cómo se construye matemáticamente una zona semanal? | Sin límites reproducibles no puede evaluarse interacción. | `human_validation_required` | `semi_automatic_backtesting` | Reglas explícitas y ejemplos anotados sobre cuerpos, mechas, velas, anchura y buffer |
| FX-Q-WZ-002 | ¿Qué tolerancia define llegada o interacción con la zona? | Controla cuándo se habilita el análisis diario. | `unresolved` | `semi_automatic_backtesting` | Casos positivos, negativos y de borde validados por el mentor |

## Daily alignment

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-D-001 | ¿Cómo se calcula la alineación diaria con semanal? | La falta de alineación produce `no_trade`. | `unresolved` | `semi_automatic_backtesting` | Definición explícita, ejemplos de estructura, cierres, retrocesos y lateralidad |

## 4H patterns

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-H4-001 | ¿Cuáles son la geometría, pivotes, simetría, tolerancias, velas mínimas, neckline e invalidaciones de cada patrón? | La detección y finalización son hoy visuales. | `human_validation_required` | `semi_automatic_backtesting` | Catálogo de patrones anotados, contraejemplos y explicación explícita |

## Breakout

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-BO-001 | ¿Qué constituye ruptura: cuerpo, mecha, cierre, timeframe y penetración mínima? | Define el evento que habilita el retest. | `unresolved` | `semi_automatic_backtesting` | Definición verbal y casos de borde anotados |

## Retest

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-RT-001 | ¿Cuál es la tolerancia, overshoot, cantidad, plazo e invalidación exacta del retest? | Sin ello no puede habilitarse una señal automáticamente. | `unresolved` | `semi_automatic_backtesting` | Ejemplos positivos/negativos y reglas explícitas de tiempo y precio |

## Entry signals

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-E-001 | ¿Cuál es la geometría exacta de engulfing y patrones tipo star? | La validez de la señal es subjetiva. | `human_validation_required` | `semi_automatic_backtesting` | Definiciones de cuerpos/mechas, porcentaje, tamaño, velas y contraejemplos |
| FX-Q-E-002 | ¿Cómo se resuelven señales contradictorias y existe prioridad entre 2H, 1H y 30M? | Evita decisiones arbitrarias entre temporalidades `OR`. | `unresolved` | `demo_execution` | Política explícita y casos con señales simultáneas/conflictivas |
| FX-Q-E-003 | ¿Cuál es el momento, tipo de orden y distancia máxima después del cierre? | Define la entrada ejecutable. | `unresolved` | `demo_execution` | Reglas operativas auditadas para Market, Limit o Stop |

## Stop Loss

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-SL-001 | ¿Qué swing 4H se selecciona y se usa cuerpo, mecha, buffer o spread? | Determina invalidación y riesgo. | `human_validation_required` | `semi_automatic_backtesting` | Casos anotados con múltiples swings y regla explícita de selección |
| FX-Q-SL-002 | ¿Existe distancia máxima, excepción estructural o gestión posterior? | Puede invalidar el setup y cambiar el riesgo. | `unresolved` | `demo_execution` | Política operativa y casos de Stop Loss amplio |

## Take Profit

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-TP-001 | ¿2:1 es fijo o mínimo; se permiten extensiones, parciales, liquidez o salida ante zona contraria? | Define el resultado y gestión de salida. | `unresolved` | `semi_automatic_backtesting` | Declaración explícita y casos completos de gestión; no basta observar 4:1/6:1 |

## Break Even

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-BE-001 | ¿Qué evento/timeframe activa Break Even y cómo trata cierre, spread, obligatoriedad y gestión posterior? | El único caso EURUSD es contextual. | `unresolved` | `demo_execution` | Política explícita y múltiples casos auditados |

## Risk

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-R-001 | ¿Cuáles son riesgo por operación, tamaño de posición, límite diario, máximo de operaciones/pérdidas, reentrada y kill switch? | Sin controles no puede ejecutarse con seguridad. | `unresolved` | `demo_execution` | Política Forex aprobada, independiente de Nasdaq, y pruebas de límites |

## News

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-N-001 | ¿Hay noticias prohibidas, ventanas, cierres o suspensión de entradas y cómo se define alto impacto? | CPI/PPI solo fueron comentarios contextuales. | `unresolved` | `demo_execution` | Política explícita y fuente de calendario definida |

## Trading schedule

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-T-001 | ¿Qué sesiones, días, horarios y timezone permiten análisis o entrada? | Afecta cierres de vela y reproducibilidad. | `unresolved` | `semi_automatic_backtesting` | Horario oficial, timezone y tratamiento de DST/feriados |

## Market data

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-MD-001 | ¿Cómo se normalizan símbolos, proveedores, timestamps y cierres manteniendo consistencia con FOREXCOM? | Los feeds pueden producir diferencias visuales. | `unresolved` | `semi_automatic_backtesting` | Mapeo aprobado, comparaciones de feeds y política de timezone |

## Automation

| Question ID | Question | Why it matters | Current status | Blocking level | Proposed evidence needed |
|---|---|---|---|---|---|
| FX-Q-A-001 | ¿Qué reglas subjetivas pueden formalizarse y con qué precisión validada? | Limita backtesting automático y cualquier ejecución. | `unresolved` | `semi_automatic_backtesting` | Dataset auditado, criterios cuantitativos aprobados y pruebas fuera de muestra |
| FX-Q-A-002 | ¿Qué aprobación humana y controles son obligatorios antes de demo supervisada? | Evita promoción automática y órdenes inseguras. | `unresolved` | `demo_execution` | Protocolo de aprobación, trazabilidad, rollback y failure-path tests |
| FX-Q-A-003 | ¿Qué evidencia permitiría considerar ejecución autónoma? | El modo está actualmente prohibido y no listo. | `unresolved` | `autonomous_execution` | Decisión humana futura; arquitectura, riesgo y estrategia completamente auditados |
