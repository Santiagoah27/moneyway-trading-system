# Nasdaq H4 context continuity research V3

`EXP-NQ-H4-CONTEXT-CONTINUITY-V3` / `3.0.0` es un diagnóstico experimental de MoneyWay bajo [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md). Proyecta las observaciones V2 ya calculadas con el mismo replay sincronizado. La vigencia del contexto y la autorización de dirección para la sesión siguen siendo decisiones separadas. Ambas banderas de autorización del informe son `false`.

## Semántica auditada y diferencia con V2

En Step 1, la última H4 cerrada a las 08:00 Bogotá se revisa contra el par estructural existente HH/HL o LL/LH. Un evento estructural cualificado puede cambiar ese par. El cambio de día por sí solo no reinicializa el estado válido ni expira el ancla aceptada. El catálogo conserva la clasificación contextual Breakout, Wickfill o Fakeout y la evaluación de la dirección permitida como evidencias propias del Step 1. [Step 1 y sección 9](../strategies/nasdaq/strategy-specification.md#9-4h-context) · [Catálogo](../strategies/nasdaq/rule-catalog.md)

| Pregunta | Evidencia y alcance |
| --- | --- |
| A y B. ¿Qué exige la ausencia o presencia de un evento nuevo? | El mentor revisa el estado existente y lo evoluciona cuando corresponde. Ausencia de una ruptura nueva no implica invalidación estructural ni prueba vigencia del contexto originario. V2 exige un evento cualificado en el último cierre para conceder la alineación de su candidato, una restricción experimental más estrecha que preservar y revisar un estado existente. |
| C. ¿Qué puede conservarse? | El evento observado, el instante de confirmación, la vela de origen, el pivote usado y su confirmación, y el historial de velas cerradas posteriores. La comparación de los cierres con la referencia congelada es evidencia descriptiva. |
| D y E. ¿Qué invalidación está definida? | Sobre un par **ya validado**, el cierre estricto por debajo de su HL invalida la estructura alcista. El cierre estricto por encima de su LH invalida la bajista. La comparación es determinista con las coordenadas validadas. Igualdad y mecha sola quedan fuera. Esas reglas estructurales no autorizan reutilizar un pivote experimental como HL/LH canónico. V1/V2 también registran una invalidación **experimental** por retorno de cierre dentro del horizonte explícito de seguimiento de un breakout; V3 conserva esa observación con el mismo alcance y timestamp. |
| F y G. ¿Un evento confirma tendencia o dirección de sesión? | Una interacción con un nivel puede ser observable mientras la estructura sigue provisional. La polaridad descriptiva del evento previo no establece dirección de sesión. |
| H. ¿Qué queda abierto? | Selección del ancla y de la zona cuando falta un par validado, criterios de vigencia y precedencia para contextos coexistentes, y puente causal entre el evento y la dirección aplicable a una sesión posterior. Las definiciones experimentales de pivote y horizonte no resuelven esas decisiones. |

En [V2](nasdaq-h4-causal-context-research-v2.md), `EvaluateH4CausalContextResearchUseCase.Execute` construye `ContextEvents` históricos, selecciona los del último cierre y, sin un evento nuevo, puede emitir `NoQualifiedEvent`. `ContextAlignedDirection` requiere que la única interacción actual cualificada sea compatible con estructura confirmada. `CompareNasdaqH4CausalContextUseCase` usa ese valor en el filtro de completitud H4→M1. El caso sintético con estructura confirmada y cierre exactamente en la referencia demuestra que V1 puede aceptar el contexto donde V2 queda sin un evento nuevo. Esa diferencia es un límite del gate V2 y no determina una regla alternativa de entrada. [Preguntas abiertas de H4](../strategies/nasdaq/open-questions.md)

## Hipótesis implementada

La hipótesis diagnóstica es que la **continuidad de la evidencia** puede reproducirse de forma causal por identidad de evento y referencia, incluso cuando no se puede determinar la vigencia de un contexto. V3 identifica eventos previos y del último cierre, agrupa todos los eventos simultáneos en ese cierre y preserva las observaciones posteriores con sus tiempos. Un cierre arriba, abajo o igual a la referencia queda como relación de precios. Una referencia más reciente, una brecha o un evento contrario se registran como hechos separados. Los pivotes provisionales permanecen provisionales y los gaps no se rellenan.

- `ContextValidityUnresolved` conserva la incertidumbre. Un seguimiento `HorizonCompleted` informa hasta dónde llegó la observación del experimento existente. No asigna un TTL y no prueba `StillValidContext`.
- `TrackedBreakoutInvalidated` exige un registro existente de V2 vinculado por identidad de pivote y tiempos del breakout. Conserva también el rechazo posterior como evento propio. Un cruce fuera del horizonte sigue visible como relación de cierre, con vigencia sin determinar.
- V3 muestra por separado la dirección estructural confirmada, la dirección observada de cada evento y la alineación publicada por V2. La cadena reporta las asociaciones V2 sin recalificarlas. Los horizontes 1 y 2 conservan únicamente su propósito experimental original.

Las velas del diagnóstico son las H4 cerradas de V2 con `CloseTimeUtc <= AsOfUtc`. Las confirmaciones no se trasladan al momento del extremo, y la lista de eventos posteriores compara instantes estrictamente posteriores. Los eventos simultáneos no reciben un orden intrabar.

## Metodologías externas

- La exposición histórica de Hamilton proporciona un marco para distinguir un movimiento persistente de un retroceso. V3 toma esa distinción como motivación, sin trasladar sus criterios de tendencia primaria, confirmación entre índices o duración al H4 de US100. [The Stock Market Barometer](https://archive.org/details/stockmarketbarom00hami)
- Un breakout de rango compara con un nivel anterior. La literatura cuantitativa formaliza cruces de rangos como reglas parametrizadas. Ese cruce observable y un cambio de estructura sostenido son hipótesis diferentes. V3 conserva los cruces ya detectados por V2 y no introduce una regla de Market Structure Shift o un lookback externo. [Brock, Lakonishok y LeBaron](https://doi.org/10.1111/j.1540-6261.1992.tb04681.x)
- ZigZag distingue extremos, pivotes confirmados y proyecciones. Su pivote derecho informa tarde y su presentación puede volver a dibujar el último tramo. V3 conserva la fecha de confirmación original sin usar retrospectivamente la geometría final. [TradingView](https://www.tradingview.com/support/solutions/43000591664-zigzag-indicator/)
- Directional Change separa un extremo del retorno posterior que confirma la reversión. Su umbral y sus observaciones ordenadas pertenecen a otra metodología. V3 conserva los cierres y la incertidumbre intrabar de H4 sin importar un umbral ajeno. [Tsang, Ma y Chinthalapati](https://doi.org/10.1002/isaf.1552)

## Development del 23 de junio de 2026

Fuente original OANDA Global Markets MT5 Demo US100. Preparación a las 13:00 UTC (08:00 Bogotá), informe a las 16:30 UTC. Se reprodujeron los hashes publicados de V1 y V2. Las diez velas H4 son continuas. Los resultados de ambos horizontes coinciden salvo la hora de finalización del seguimiento 1/1 existente.

| Perfil | Evidencia V2 en preparación | Continuidad V3 | H4→M1 |
| --- | --- | --- | --- |
| 1/1 · horizonte 1 | 2 High / 1 Low, estructura provisional, `NoQualifiedEvent` | Rechazo inferior BUY a 01:00 UTC y breakout SELL a 05:00 UTC, ambos contra 30268.0, pivote confirmado el 22 de junio a 21:00 UTC. Los cierres posteriores 29593.1 y 29499.3 quedaron bajo la misma referencia. El seguimiento completó su horizonte a 09:00 UTC sin invalidación registrada. Vigencia indeterminada. | 0 |
| 1/1 · horizonte 2 | Mismos pivotes y estado | Mismos eventos y cierres. El seguimiento completó su horizonte a 13:00 UTC sin invalidación registrada. Vigencia indeterminada. | 0 |
| 2/2 · horizontes 1 y 2 | 1 High / 0 Low, estructura provisional, `NoQualifiedEvent` | Sin evento H4 cualificado previamente observado en este prefijo. | 0 |
| 3/3 · horizontes 1 y 2 | 1 High / 0 Low, estructura provisional, `NoQualifiedEvent` | Sin evento H4 cualificado previamente observado en este prefijo. | 0 |

El cierre de preparación en 1/1 conserva la interacción `ContinuedBeyond`. Esa observación aporta evidencia de continuidad de precio bajo la referencia, con estructura sin confirmar. El mínimo **29472.7** permanece provisional. El informe enseña exactamente qué ocurrió antes, qué aportó el último cierre y qué no se pudo decidir sobre vigencia. Cero cadenas no mide la calidad de una regla ni permite inferir rentabilidad.

## Reproducción

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$out = Join-Path $env:TEMP ('moneyway-h4-continuity-' + [guid]::NewGuid())
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $out --compare-h4-context-continuity-v3 --cutoff-utc 2026-06-23T16:30:00Z
```

Salida `nasdaq-h4-context-continuity-v3.json`. El Worker restringe esta variante al paquete Development validado por SHA-256. Los tests comparan la proyección a 13:00 y 16:30, y mutan o agregan velas futuras fuera del límite. V1, V2, el protocolo congelado y la admisión canónica conservan sus contratos. Las 29 velas H4 adicionales permanecen fuera por el bloqueo documentado de mapeo histórico.

**Siguiente feature prioritaria.** Comparar prefijos H4 únicamente tras acreditar el mapeo de la fuente añadida. Para avanzar sobre vigencia de contexto, reunir ejemplos auditados de una preparación con estructura ya validada y sin evento nuevo, y casos contrastantes con retorno o invalidación de la referencia original. Vincular cada transición al cierre y al par que la sustentan antes de convertirla en un nuevo gate experimental.
