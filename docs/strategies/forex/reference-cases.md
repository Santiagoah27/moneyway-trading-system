# MoneyWay Forex reference cases

Los casos preservan evidencia cualitativa. No incluyen precios o fechas no proporcionados ni convierten decisiones contextuales en reglas generales.

## NZDCAD

- Status: `reference_case`.
- Instrument: NZDCAD.
- Scenario: búsqueda del primer impulso después de un retroceso; el movimiento no debía perseguirse si ya estaba extendido. Un Head and Shoulders (HCH) seguía siendo una posibilidad en formación, no una entrada inmediata.
- Rules demonstrated: macro maturity review, no anticipation, pattern completion before entry.
- Rules not demonstrated: exact definition of clean impulse, extension threshold, pattern geometry, entry, Stop Loss or Take Profit mechanics.
- Context-specific decisions: esperar la terminación del HCH observado.
- Generalization risks: convertir niveles, distancias o apariencia visual del caso en umbrales universales.
- Human-review notes: confirmar madurez, ubicación y terminación del patrón.

## EURAUD

- Status: `positive_reference_case`.
- Instrument: EURAUD.
- Scenario: contexto macro bajista, retroceso hacia una zona alta de resistencia, formación de Head and Shoulders y espera del retest. El mentor lo señaló como caso favorito o modelo.
- Rules demonstrated: macro context precedes pattern, pattern completion, breakout/retest sequence.
- Rules not demonstrated: mathematical resistance zone, pattern tolerances, breakout geometry, exact entry or risk rules.
- Context-specific decisions: clasificación como caso favorito/modelo.
- Generalization risks: copiar niveles o tratar toda forma similar como válida.
- Human-review notes: conservar la evaluación visual y la secuencia bloqueante.

## USDCHF

- Status: `rejected_setup_reference_case`.
- Instrument: USDCHF.
- Scenario: un patrón 4H atractivo no bastó; la compra fue rechazada por estar en la parte alta o resistencia de un rango semanal.
- Rules demonstrated: macro location can invalidate a lower-timeframe pattern; an attractive 4H pattern alone is insufficient.
- Rules not demonstrated: mathematical range thirds, programmable resistance boundary or exact rejection threshold.
- Context-specific decisions: rechazo de la compra observada.
- Generalization risks: inventar un “tercio superior”, buffers o una regla simétrica automática para ventas.
- Human-review notes: vender en la parte baja del rango es solo `candidate` si se plantea por posible simetría; no está confirmado por este caso.

## AUDUSD

- Status: `market_data_reference_case`.
- Instrument: AUDUSD.
- Scenario: se observaron diferencias visuales entre feeds; se utilizó FOREXCOM en TradingView para mantener consistencia con gráficos y cierres de la metodología del mentor.
- Rules demonstrated: data-source consistency and traceability.
- Rules not demonstrated: absolute provider quality or universal symbol normalization.
- Context-specific decisions: elección metodológica de FOREXCOM.
- Generalization risks: declarar OANDA universalmente incorrecto.
- Human-review notes: documentar proveedor, símbolo, timestamp y timezone; la normalización continúa abierta.

## EURUSD

- Status: `reference_case`.
- Instrument: EURUSD.
- Scenario: el Stop Loss cubrió máximos de semanas anteriores y la operación fue descrita en Break Even.
- Rules demonstrated: Stop Loss must protect relevant structure; Break Even occurred in one managed example.
- Rules not demonstrated: universal two-week lookback, Break Even trigger, timeframe, costs, mandatory character or later management.
- Context-specific decisions: cubrir esos máximos concretos y gestionar esa operación en Break Even.
- Generalization risks: usar siempre máximos de dos semanas o implementar Break Even universal.
- Human-review notes: ambas decisiones permanecen contextuales hasta obtener evidencia adicional.

## AUDJPY

- Status: `reference_case`.
- Instrument: AUDJPY.
- Scenario: patrón posible o tentativo; era necesario observar cómo terminaba de formarse y cómo rompía.
- Rules demonstrated: no entry from an incomplete pattern and no anticipatory execution.
- Rules not demonstrated: completion geometry, breakout validation, retest tolerance or entry signal.
- Context-specific decisions: esperar evolución y ruptura del patrón observado.
- Generalization risks: tratar una posibilidad visual como señal confirmada.
- Human-review notes: mantener `waiting` hasta completar y validar las etapas previas.

## Cross-case boundaries

- Los niveles concretos de cualquier caso no son reglas generales.
- Los casos no resuelven tolerancias, buffers, geometría, riesgo ni ejecución.
- Una observación visual necesita revisión humana y no puede autorizar automáticamente una operación.
