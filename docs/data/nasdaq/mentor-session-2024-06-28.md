# Primera sesión Nasdaq del mentor: evidencia y preparación de datos

## Estado

**NEEDS_TIME_SYNCHRONIZATION_EVIDENCE**. Los campos históricos y las observaciones indicados abajo están confirmados documentalmente por revisión humana. La adquisición OHLC final sigue detenida: falta mapping UTC de MetaTrader y validar el identificador exacto del instrumento de análisis.

Baseline auditado: `1b62376520a8638dfb9f381a0cf4b7f0c00203a9`. SessionId: `nasdaq-mentor-2024-06-28-v2-v3`. Este manifiesto es metadata, no input ejecutable ni aprobación de una operación. No se crearon CSV reales, `ValidatedInput.cs`, RuleFacts ni `report.json`; no se ejecutó replay real. No cambia la capacidad 32 RuleIds / 14 Required / 16 evaluators / 0 Required gaps / full=true.

Estados por campo: `confirmed`, `human_validation_required`, `unresolved`. `null` significa valor no establecido, no cero ni dato aproximado. Confirmación de revisión humana no significa que esta tarea haya inspeccionado independientemente los videos originales.

## Fuentes

| ID | Procedencia y alcance |
| --- | --- |
| S1 | [ADR 0025](../../decisions/0025-define-nasdaq-demo-human-evidence-contract.md): contrato humano, causality y provenance. Reseña previa de Video 2 aprox. 13:10–13:25 y Video 3 aprox. 13:15–13:40, con Open/Close incompletos. S7 aporta precisión documental nueva; no modifica el contrato. |
| S2 | [Especificación Nasdaq](../../strategies/nasdaq/strategy-specification.md): sesiones, TIME y targets normativos. No es dataset de esta operación. |
| S3 | Solicitud anterior “FIRST REAL MENTOR SESSION DATA PACK”; hallazgos previos incompletos, precisados por S7. |
| S4 | [Guía Feature 29](../../backtesting/nasdaq-mentor-session-local-run.md): input, cuatro CSV, disponibilidad auténtica y binding canónico. |
| S5 | [Auditoría de preparación](../../roadmap/nasdaq-first-real-session-readiness.md): capacidad de código separada de disponibilidad de datos. |
| S6 | Inventario previo del repositorio: único CSV identificado, `samples/market-data/replay-demo.synthetic.csv`, provider `historical-fixture`, symbol `DEMO`, 5M, 2026-01-01. Sintético y excluido del pack. No se identificaron originales ni OHLC reales en el árbol inspeccionado. |
| S7 | Actualización del usuario “FIRST REAL NASDAQ MENTOR SESSION — Human Evidence Update + Analysis Feed / Execution Feed Audit”, recibida el 2026-10-06. Revisión humana de originales: apartados 1 (TradingView), 2 (ticket), 3 (reloj desconocido), 4–5 (dominios), 6–7 (Video 3 y precisión), 8 (TIME). Fuente retenida en el mensaje/adjunto de esta tarea. |

Video 2/3 siguen siendo etiquetas de revisión: título/URL/archivo original y hash permanecen `unresolved`. Para un adapter futuro se necesita referencia estable al material retenido y ubicación de cada evidencia; no se fabrican execution IDs ni disponibilidad histórica con la fecha de esta revisión. S7 confirma asociación humana de ambos dominios con el mismo workflow, no equivalencia de instrumentos.

## Identidad y relojes

| Campo | Valor | Estado | Procedencia / pendiente |
| --- | --- | --- | --- |
| SessionId | `nasdaq-mentor-2024-06-28-v2-v3` | confirmed | Identificador administrativo S3; no ticket ni identidad de ejecución. |
| Strategy / version | `moneyway-nasdaq` / `nasdaq-0.1.0-draft` | confirmed | Contrato registrado S4. |
| DisplayedTradeDate | `2024-06-28` | confirmed | S7 §2. |
| LocalStrategyDate | null; candidata `2024-06-28` | human_validation_required | Validar ejecución UTC y día America/Bogota; no copiar fecha broker. |
| AnalysisPlatform / AnalysisProviderDisplayed | `TradingView` / `OANDA` | confirmed | S7 §1. |
| AnalysisInstrumentDisplay | `US 100` | confirmed | S7 §1; headers `US 100 · 15 · OANDA`, `US 100 · 1h · OANDA`. |
| AnalysisLabelsObserved | `US100` / `US100.cash` | confirmed | Variantes visibles/referenciadas S7 §1; no alias canónico definido. |
| AnalysisChartTimezone | `UTC-5` | confirmed | Revisión del reloj inferior derecho, S7 §1. Offset mostrado; no America/New_York. |
| Replay provider_id / symbol | null / null; candidato provider `OANDA` | human_validation_required | Confirmar identificador exacto de la fuente de exportación y chart revisado. No elegir automáticamente entre variantes ni inventar namespace. |
| ExactAnalysisInstrumentDescription | null | unresolved | Identificador cualificado, contrato/CFD, escala, base de cotización y fronteras nativas verificables de esa fuente. |
| ExecutionPlatform / ExecutionSymbolDisplayed | `MetaTrader` / `NDX100` | confirmed | S7 §2. |
| MetaTraderBroker / Server | null / null | unresolved | S7 §3, sin identidad directa del broker/servidor. |
| MetaTraderServerTimezone / UTCMappingEvidence | null / null | unresolved | S7 §3; no UTC+2/+3, EET/EEST inferido. |
| MetaTraderOpenUtc / MetaTraderCloseUtc | null / null | unresolved | Segundos conocidos, conversión aún no probada. |
| ExactExecutionInstrumentDescription | null | unresolved | Especificación del símbolo ligada al broker/servidor. |
| StrategyTimezone | `America/Bogota` | confirmed | Código y S2; independiente de reloj MetaTrader. |
| ReplayStartUtc / ReplayEndUtc | null / null | unresolved | Mapping, miembros estructurales y cobertura suficientes pendientes. |
| MarketDataSourceReference | null | unresolved | Fuente candidata conocida; ningún dataset aceptado/adquirido. |

No se declara `OANDA US100 == MetaTrader NDX100`. Tampoco se rechaza la asociación humana únicamente por nombres diferentes. CME NQ, cash/index y CFD de otros proveedores no son sustitutos aprobados.

## Ticket histórico: valores literales

Todos los valores confirmados siguientes proceden de S7 §2; scope/reason de S7 §6. Estos son campos históricos mostrados, no cálculo ni resultado económico del engine.

| Campo | Valor | Estado | Alcance |
| --- | --- | --- | --- |
| Ticket | `35079234` | confirmed | Ticket mostrado; no source-qualified EntryExecutionId/ExitExecutionId construido. |
| OpenDisplayTime | `2024-06-28 14:02:18` | confirmed | Reloj broker-display; no UTC. |
| CloseDisplayTime | `2024-06-28 14:03:05` | confirmed | Reloj broker-display; no UTC. |
| TimestampResolution | segundos | confirmed | Este historial; no generalización a proveedores. |
| DocumentedEntryBeforeExit | Open anterior a Close del ticket revisado | confirmed | Secuencia documental; binding a snapshot y UTC todavía pendientes. |
| Direction | `SELL` | confirmed | Literal. |
| Quantity / QuantityUnit | `0.25` / `lots` | confirmed | Volumen mostrado; no inferir cantidad de cierre separada. |
| ExecutionSymbolDisplayed | `NDX100` | confirmed | Literal. |
| EntryPrice | `20019.14` | confirmed | Ejecución histórica; no operand de riesgo planeado inferido. |
| StopLoss | `20034.50` | confirmed | Literal, anchor exacto pendiente. |
| TakeProfit | `19972.10` | confirmed | Literal; no low independiente derivado. |
| ExitPrice | `19972.10` | confirmed | Literal, igual decimal al TP mostrado. |
| Commission / Swap | `0.00` / `0.00` | confirmed | Campos mostrados; no implica todos los costos planeados cero. |
| DisplayedProfit / Currency | `+1181.88` / `USD` | confirmed | Preservado sin recomputar, sin clasificación Win/Loss ni economic P&L. |
| DocumentedExitScope | `Full` | confirmed | Mentor describe salida completa en este escenario S7 §6, no regla universal. |
| DocumentedExitReason | descripción revisada: salida completa cuando precio tocó target programado | confirmed | Explicación humana S7 §6; no cita textual ni código mecánico del broker. No se infiere de igualdad de precios. |
| BrokerExitReasonCode | null | unresolved | No suministrado. |
| ExitQuantity / ExitQuantityUnit | null / null | unresolved | No hay transcripción separada de cantidad de salida. |
| EntryExecutionId / ExitExecutionId | null / null | unresolved | Requieren namespace fuente y relación evento/ticket inequívoca. |
| EntryEffectiveAtUtc / ExitEffectiveAtUtc | null / null | unresolved | Mapping MetaTrader pendiente. |
| ExecutionSourceReference / AssertionSourceReference | S7 documental; referencia material ejecutable null | human_validation_required | Retener source location estable antes de adapter; no resolver fuentes ocultamente. |
| EvidenceObservedAtUtc (cada registro) | null | unresolved | Disponibilidad auténtica por fuente; no backdate desde revisión posterior. |
| AccountBasis / Currency / BasisReference | null / null / null | unresolved | Base del assessment no conocida; USD profit no establece base de cuenta. |
| PlannedMaximumLoss / RiskCalculationReference | null / null | unresolved | Documentación de riesgo planeado pendiente. |
| PlannedEntryPrice / CostTreatment | null / null | unresolved | No copiar EntryPrice ni calcular riesgo/P&L a partir del ticket. |

## Observaciones de estrategia de esta sesión

Confirmación documental de observaciones S7 §6 no significa Passed ni creación de RuleFacts. Todos los miembros de velas, effective/observed UTC y referencias canónicas aún necesitan validación.

| Observación del caso | Estado | Exactitud / pendiente |
| --- | --- | --- |
| H4 bearish context/direction, continuación hacia mínimos semanales | confirmed | Faltan miembros HH/HL o LL/LH activos, episode/context bindings y tiempos cerrados. |
| London High marcado | confirmed | LondonHigh exacto y miembros 1H null. |
| London Low / Asia Low descritos coincidentes cerca de `19972.xx` | confirmed | Contexto aproximado; AsiaLow y LondonLow exactos independientes null. |
| Precio excedió London High antes del setup bajista | confirmed | Evento, referencia y timestamp/precio exactos null; no instante intrabar inventado. |
| Gran desplazamiento bearish 5M, body close bajo low/estructura 5M previa | confirmed | Miembros, nivel y cierre exactos null; no detector nuevo. |
| FVG bearish visible | confirmed | Tres miembros, wick data y selección exacta null; calidad Approved no inferida por visibilidad. |
| Retroceso 1M hacia FVG y realignment bearish usado para SELL | confirmed | Swing/level correctivo, vela de confirmación, source order y UTC null. |
| SL `20034.50`, protección sobre LH/wick 5M revisado | confirmed | Anchor exacto, miembros y precio estructural null. |
| TP `19972.10` explicado como target compartido Asia/London low | confirmed | Referencias seleccionadas y lows exactos independientes null. |
| Entrada y salida histórica del ticket | confirmed | Valores arriba; tiempos UTC, disponibilidad y binding exacto pendientes. |
| TIME / preparación completada en ventana | unresolved | Sin evidencia exacta de revisión H4/marcación completadas en [08:00,08:30) Bogotá. |
| FVG quality Approved/Rejected | unresolved | Revisión específica, rationale y tiempos no suministrados. |
| NQ-M1-003 | unresolved | Derivar únicamente de realignment canónico; no segunda señal humana ni RuleFact manual. |
| Risk assessment | unresolved | Base/operand/unidades/costos documentados faltantes. |

No se han evaluado gates canónicos ni el primer blocker de replay. Faltan OHLC, selección y ancestry de H4/liquidez/take/trigger/FVG/quality/pullback/realignment/SL/TP, riesgo y disponibilidad auténtica. La evidencia cualitativa confirmada se conserva aunque no sea todavía input suficiente.

## Auditoría: propiedad del mercado y procedencia de ejecución

Código inspeccionado, sin modificaciones:

- [CandleSeries](../../../src/backend/MoneyWay.Domain/MarketData/CandleSeries.cs): provider/symbol homogéneos por serie.
- [Importador de sesión Feature 29](../../../src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/RunLocalNasdaqMentorSessionUseCase.cs): cuatro series deben coincidir exactamente con timeframe/provider/symbol de Session.
- [Binder](../../../src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqMentorSessionReplay.cs): observaciones deben coincidir en strategy/version/provider/symbol/disponibilidad; downstream references pertenecen exactamente a este run.
- [Snapshot](../../../src/backend/MoneyWay.Application/Strategies/Nasdaq/HistoricalTrades/NasdaqHistoricalTradeSnapshot.cs): Session heredada de la elegibilidad canónica; preserva entrada, SL, TP y riesgo.
- [Entrada histórica](../../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqHistoricalObservedEntry.cs) y [salida documentada](../../../src/backend/MoneyWay.Application/Strategies/Nasdaq/HistoricalTrades/NasdaqHistoricalDocumentedExit.cs): execution ID source-qualified y ExecutionSourceReference retenidos; no fields independientes ExecutionProviderId/ExecutionSymbol. SupportingCandles opcionales deben coincidir exactamente con Session.

ADR 0025 define Session como **analysis scope** y exige provenance hacia material retenido, sin lookup oculto. Sus contratos de entrada/salida preservan ejecución documental, no sintetizan fills con velas.

**A. Sí, de forma condicionada:** series OANDA con identificador exacto validado pueden ser la identidad de análisis; MetaTrader/NDX100 puede permanecer explícito en material/provenance de ejecución. La identidad tipada de cada observación de entrada/salida sigue siendo la Session de análisis; no asignar NDX100 al campo Symbol de esa observación si Session usa otro símbolo.

**B. No existe comparación independiente entre identidad de broker y análisis:** el contrato no tiene campos tipados de provider/symbol de ejecución separados. Sí exige igualdad de series, Session, observaciones y candles de apoyo. Esto no constituye soporte general de replay multifeed, alias resolver ni validación automática de compatibilidad económica entre instrumentos. No relabelar velas broker como OANDA.

**C. OANDA/TradingView es la candidata de análisis respaldada por S7**, junto con analysis scope del ADR y contratos anteriores. No hay requisito de elegir OHLC del broker desconocido solo porque ejecutó la orden. Identificador exacto, fuente de exportación, escala/base, fronteras y compatibilidad temporal/material deben validarse; aceptación del constructor no prueba equivalencia ni fill.

## Sincronización UTC: evidencia humana mínima

La zona del chart `UTC-5` está confirmada y permite convertir un timestamp de chart completo y pertinente. No convierte por sí sola los campos del historial MetaTrader. Se necesita al menos una prueba retenida y aplicable a ese servidor/fecha:

1. Un **mismo evento simultáneo e inequívoco**, con fecha/hora TradingView UTC-5 y su contraparte MetaTrader, que determine el offset. No basta mostrar reloj de pantalla junto a un registro histórico de otro instante. Documentar precisión/incertidumbre y aplicación a Open/Close; corroborar ambos si la referencia no cubre todo el intervalo.
2. Zona/offset explícito del servidor visible en material original, con significado de timestamp y aplicabilidad a 2024-06-28.
3. Broker/servidor identificado más documentación autoritativa de su timezone/offset para esa fecha, incluido cualquier cambio estacional.
4. Otro punto de sincronización autoritativo que vincule un evento MetaTrader a UTC y pruebe el offset aplicable.

No seleccionar offset por costumbre UTC+2/+3, EET/EEST, encaje en ventana Bogotá, parecido de precios ni reloj actual. Hasta esa prueba, MetaTraderOpenUtc/CloseUtc, LocalStrategyDate y rango final permanecen pendientes.

Para identidad de análisis falta, mínimamente, detalle de símbolo cualificado o metadata de exportación del **chart OANDA revisado** que distinga US100/US100.cash y su instrumento/base/fronteras. No se exige reinterpretar ambos dominios como un solo símbolo.

## Ventana y adquisición OHLC

Reglas existentes S2, sin cambios: Asia [D-1 17:00,D 02:00), London [D 02:00,D 07:00), preparación [D 08:00,D 08:30), entrada [D 08:30,D 11:00), todas America/Bogota. Nominalmente 9 y 5 velas 1H de sesión.

Solo si D se valida como 2024-06-28: Asia [2024-06-27T22:00Z,2024-06-28T07:00Z), London [07:00Z,12:00Z), preparación [13:00Z,13:30Z), entrada [13:30Z,16:00Z). Estas son conversiones de estrategia, **no** del ticket. No se fuerza salida a las 11:00.

Inicio suficiente: todas las velas de sesión y miembros/anchors H4/1H anteriores realmente seleccionados; ningún lookback fijo inventado. Final: salida real y fronteras canónicas necesarias. Fronteras 4H deben ser las de la fuente, no desplazadas para encajar. ReplayStartUtc/EndUtc siguen null.

OANDA es técnicamente admisible como candidata; **no está liberada la adquisición OHLC final** por mapping UTC e identidad exacta pendientes. Ningún dataset fue descargado o aceptado. Acceso histórico 2024, licencia/permisos y fuente concreta de exportación permanecen unresolved; no se almacenan secretos.

| Archivo requerido | Estado | Validación importador |
| --- | --- | --- |
| 4H.csv | unresolved: ausente | No ejecutada |
| 1H.csv | unresolved: ausente | No ejecutada |
| 5M.csv | unresolved: ausente | No ejecutada |
| 1M.csv | unresolved: ausente | No ejecutada |

Schema existente:

```csv
provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume
```

Usar importador existente: sintaxis UTC, identidad homogénea, orden/duplicados/overlap/OHLC. Separadamente validar completitud, gaps, escala/base, fronteras nativas y cobertura; importación correcta no demuestra compatibilidad. Volume vacío es desconocido.

Price/time cross-check no ejecutado: hay precios, faltan UTC y velas. Después contrastar ambas fuentes con su base documentada, sin tolerancia/spread inventado, fit de timestamps ni modificación de valores humanos. Un rango compatible no prueba fill/orden intrabar. Contradicción material exige investigación, no cambio de TIME ni equivalencia automática.

## Validación y tratamiento Git

Actualización limitada a este manifiesto; también se restauró su texto UTF-8, previamente con caracteres de acentos sustituidos por signos de interrogación. Se revisaron estados/provenance, contratos, referencias locales y campos exactos. Se comprobaron ausencia de offset broker confirmado, alias automático, lows exactos inventados, cambios TIME y cálculo P&L. `git diff --check` y validación de referencias/formato documental: realizadas antes del commit.

No se ejecutaron tests de engine, build, importación real, cross-source checks ni harness: no hubo código ni dataset nuevo. No se presenta una suite anterior como validación de datos reales.

Material original/licenciado, OHLC y adapters privados deben permanecer fuera de Git o en `local-data/` ignorado, según derechos. Este commit contiene metadata autorizada, sin secretos, paths personales ni dataset redistribuible. Disponibilidad histórica no se backdatea desde la revisión actual.

**NEEDS_TIME_SYNCHRONIZATION_EVIDENCE**
