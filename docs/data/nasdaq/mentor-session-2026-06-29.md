# Sesión Nasdaq del mentor 2026-06-29: ejemplo NO-TRADE

## Estado y fuente

**NEEDS_INSTRUMENT_IDENTITY_EVIDENCE**. SessionId: `nasdaq-mentor-2026-06-29-videos`. SessionType: **NO_TRADE historical mentor session**. No hubo retroceso correctivo 1M requerido ni entrada, según revisión humana. No se representa una ejecución ni se fabrican hechos posteriores.

Baseline: `c132b84066415990ba896776a29c1a1804497bc0`. Fuente C1: solicitud del usuario “SOURCE-OF-TRUTH CORRECTION — Replace Invalid 2024 Mentor Session Evidence with Human-Reviewed 2026-06-29 NO-TRADE Session”, recibida el 2026-10-07, revisión humana de alta resolución de video original. Apartados Critical new evidence, Confirmed session facts, Instrument identity y MetaTrader. Material retenido en mensaje/adjunto; no inspección independiente del video en esta tarea.

El [ledger del candidato 2024](mentor-session-2024-06-28.md) preserva valores rechazados y commits originales. No son inputs de esta sesión. Título/URL/hash y ubicaciones exactas del original: `unresolved`; necesitan referencias estables antes de cualquier transcripción ejecutable.

Los estados de evidencia son `confirmed`, `human_validation_required`, `unresolved`, `rejected_ai_inference`. `not_applicable` identifica ausencia de aplicación de datos de ejecución, según taxonomía de resultados; no es un nuevo estado de regla. `null` significa valor no establecido, sin rellenar ceros ni precios. No se ha ejecutado replay ni emitido un StrategyVerdict: NO_TRADE describe el resultado documental del ejemplo.

## Identidad y tiempo

| Campo | Valor | Estado | Procedencia / límite |
| --- | --- | --- | --- |
| SessionId | `nasdaq-mentor-2026-06-29-videos` | confirmed | Identificador administrativo solicitado en C1. |
| SessionType | `NO_TRADE historical mentor session` | confirmed | C1: mentor no ejecuta porque no ocurre retroceso 1M. |
| SessionDate / LocalStrategyDate | `2026-06-29` | confirmed | Fecha del ejemplo revisado C1; no fecha broker. |
| Strategy / version | `moneyway-nasdaq` / `nasdaq-0.1.0-draft` | confirmed | Contrato existente de [Feature 29](../../backtesting/nasdaq-mentor-session-local-run.md). |
| AnalysisPlatform | `TradingView` | confirmed | C1 describe chart revisado. |
| ChartTimezone | `UTC-5` | confirmed | Visible según C1; no America/New_York. |
| StrategyTimeStatus / StrategyTimezone | `CONFIRMED` / `America/Bogota` | confirmed | Código/spec existentes y C1; UTC-5 sin DST. |
| PreparationWindow | `08:00–08:30` Bogotá; `13:00–13:30 UTC` | confirmed | Regla general; no confirma que preparación del caso se completara en ventana. |
| TradingWindow | `08:30–11:00` Bogotá; `13:30–16:00 UTC` | confirmed | Regla general sin cambios. |
| ConsideredDirection | `SELL` | confirmed | Dirección considerada en C1; no orden. |
| ExactInstrumentIdentity / Replay symbol | null / null | human_validation_required | Nasdaq verbal, región visual cercana a 29.000 y ticker/header insuficientemente legible. No resolver por precios ni familia verbal. |
| AnalysisProvider / Replay provider_id | null / null | human_validation_required | OANDA no queda confirmado por esta revisión; requiere texto fuente legible. |
| InstrumentType / PriceScale / QuoteBasis | null / null / null | unresolved | Pendientes del instrumento y metadata auténticos. |
| ReplayStartUtc / ReplayEndUtc | null / null | unresolved | Sin series aceptadas ni cobertura estructural validada. |

La existencia de OANDA:NAS100USD en un catálogo no establece este ticker para el video. No se asigna proveedor/símbolo al input del harness, no se heredan US100/US100.cash/NDX100 ni se sustituye NQ/cash/otro CFD.

## Evidencia de progresión del caso

| Campo / observación | Valor | Estado | Procedencia / precisión pendiente |
| --- | --- | --- | --- |
| H4/context | Bearish después de high-side liquidity take; buscando lows | confirmed | C1, solo alcance expresado; miembros estructurales/episode identity y timestamps exactos null. |
| LiquidityMarked | London High, London Low, Asia High, Asia Low | confirmed | Visiblemente marcados según C1; cuatro valores decimales independientes null. |
| RelevantLiquidityTake | London High excedido/tomado | confirmed | C1, referencia y evento revisados; precio/miembros exactos null. |
| TakeChartTime | `2026-06-29 08:30 UTC-5` | confirmed | C1: precisión de minuto; no timestamp intrabar con segundos inventados. |
| TakeUtcMinute | `2026-06-29 13:30 UTC` (precisión de minuto) | confirmed | Conversión del reloj confirmado; no especifica segundo efectivo para constructor. |
| M5Trigger | Bearish structural change / inverse | confirmed | Descripción C1; no elegir silenciosamente enum StructuralChange vs IFVG ni orden/miembros exactos. |
| M5TriggerKind / EffectiveAtUtc | null / null | human_validation_required | Resolver clasificación exacta y fuentes cerradas antes de input tipado. |
| M5Fvg | Bearish FVG | confirmed | C1; tres miembros, wick bounds y selección exacta null. |
| FvgReviewedTiming | Alrededor de vela `09:05 UTC-5` | confirmed | Aproximación de C1, equivalente aproximado 14:05 UTC; no cierre exacto ni EffectiveAtUtc inferido. |
| FvgQualityReview | null | unresolved | Visibilidad FVG no equivale a Approved; faltan rationale/tiempos. |
| RequiredM1CorrectiveRetracement | No ocurrió en el ejemplo revisado | confirmed | Mentor explica movimiento directo hacia abajo sin retroceso exigido, C1. No inventar evento negativo tipado ni regla temporal nueva. |
| HistoricalEntry | No entrada | confirmed | Conclusión explícita C1, independiente de OHLC aún ausentes. |
| HistoricalExecution | No operación ejecutada | confirmed | C1; no execution record en el material. |
| PreparationCompletion | null | unresolved | Falta evidencia del cumplimiento real de preparación y disponibilidad. |
| SourceMembers / AuthenticObservedAtUtc | null / null | unresolved | Por registro; revisión posterior no backdatea conocimiento histórico. |
| ReviewedNoRetracementIntervalEnd | null | unresolved | Límites exactos del tramo revisado antes de adapter/replay; no afirmar ausencia universal fuera del video. |

Progresión documental: H4/context → liquidez relevante → toma London High → trigger bearish 5M → FVG bearish → espera del retroceso 1M → no ocurre → **NO ENTRY**. Estas observaciones no son gates Passed ni hechos canónicos emitidos. La clasificación exacta del trigger sigue pendiente aunque la descripción revisada esté confirmada.

## Ejecución: no aplicable

| Dominio / campo | Valor | Aplicabilidad | Procedencia |
| --- | --- | --- | --- |
| MetaTraderExecutionEvidence | null; no terminal/ticket mostrado | not_applicable | C1: ninguna ejecución en esta sesión. |
| MetaTraderExecutionUtcAlignmentStatus | not_applicable | not_applicable | No evento de ejecución que sincronizar; no blocker MetaTrader. |
| Ticket / ExecutionSymbol / Quantity | null / null / null | not_applicable | No heredar extracción rechazada. |
| EntryPrice / EntryEffectiveAtUtc | null / null | not_applicable | No entrada. |
| StructuralStopLossInstance / StopPrice | null / null | not_applicable | No instancia de SL de operación. |
| TakeProfitInstance / TakeProfitPrice | null / null | not_applicable | No instancia de TP de operación. |
| RiskInstance | null | not_applicable | No assessment de operación creado. |
| DocumentedExit / ExitPrice / ExitEffectiveAtUtc | null / null / null | not_applicable | No cierre. |
| TradeSnapshot / PnL / Commission / Swap | null / null / null / null | not_applicable | No trade ni resultado económico. |

La ausencia de ejecución no significa pérdida, fallo de estrategia ni cero P/L. No se intenta alcanzar snapshot/exit/factual Available ni se inventa realignment. Feature 29 puede conservar artefactos Unavailable cuando falten prerequisites; ninguna semántica de StrategyVerdict se modifica para este ejemplo documental.

## Preparación de datos y pendientes humanos

AnalysisFeedStatus: **human_validation_required**.
MarketDataAcquisitionStatus: **blocked — NEEDS_INSTRUMENT_IDENTITY_EVIDENCE**.
MetaTraderExecutionUtcAlignmentStatus: **not_applicable**.

No descargar OHLC finales hasta revisión de header/ticker y proveedor legibles del chart original. Mínimo requerido: captura o transcripción humana inequívoca del símbolo cualificado y feed, con ubicación del video. La región visual ~29.000 es contexto aproximado, no criterio de identidad. Después validar contrato/tipo/escala/base, timestamps/fronteras y acceso/licencia desde esa fuente.

También faltan precios exactos de las cuatro referencias de liquidez, miembros H4/1H/5M, clasificación trigger, FVG y su calidad, tiempos efectivos y disponibilidad auténtica, preparación y alcance temporal revisado de ausencia de retroceso. No deducirlos de narrativa ni crear Entry/SL/TP/riesgo/Exit para cubrir gaps.

4H / 1H / 5M / 1M: archivos ausentes, importador no ejecutado, fronteras/gaps/duplicados/OHLC/cross-timeframe no evaluados. AcquisitionStartUtc/EndUtc: null; no trasladar el buffer 2024. Raw/normalized/hashes/adapter/report: null, no generados. La adquisición futura podrá ser solo mercado/estrategia hasta donde la evidencia lo permita.

## Contratos y verificación

[ADR 0025](../../decisions/0025-define-nasdaq-demo-human-evidence-contract.md), [especificación](../../strategies/nasdaq/strategy-specification.md), [guía Feature 29](../../backtesting/nasdaq-mentor-session-local-run.md) y [readiness](../../roadmap/nasdaq-first-real-session-readiness.md) conservan infraestructura/reglas existentes y anotan la atribución 2024 rechazada donde aparecía. No cambia zona, schedule, RuleIds, evaluadores, selectores, replay, primitives ni veredictos. Capacidad existente: 32 RuleIds / 14 Required / 16 evaluators / 0 Required gaps / full=true.

Validación documental antes del commit: estados/procedencia, ledger rechazado, fecha NO_TRADE, ausencia de valores de ejecución positivos, MetaTrader no aplicable, referencias locales, UTF-8/LF y `git diff --check`. No tests de engine ni replay/importación: solo documentación, sin datos adquiridos. No secretos ni originales licenciados publicados.

**NEEDS_INSTRUMENT_IDENTITY_EVIDENCE**
