# Primera sesi?n Nasdaq del mentor: manifiesto de adquisici?n

## Estado de este primer pase

**NEEDS_HUMAN_EVIDENCE**. Adquisici?n OHLC detenida: instrumento exacto, broker/proveedor y correspondencia UTC no establecidos. No se generaron CSV reales, adapter `ValidatedInput.cs`, RuleFacts ni `report.json`. No se ejecut? el harness con datos reales.

Baseline auditado: `4b8a08a` (`feat: run Nasdaq mentor session replay`). Este documento es metadata de adquisici?n, no un input ejecutable ni una modificaci?n de estrategia. La capacidad 32 RuleIds / 14 Required / 16 evaluators / 0 Required gaps / full=true permanece intacta.

Cada campo se clasifica como `confirmed`, `human_validation_required` o `unresolved`. `null` significa valor no establecido: no cero, cadena vac?a, timestamp aproximado ni valor inferido. Una confirmaci?n documental de una revisi?n humana no implica haber inspeccionado aqu? el video original.

## Fuentes y trazabilidad

| ID | Procedencia y alcance |
| --- | --- |
| S1 | [ADR 0025: Historical documented-exit causality](../../decisions/0025-define-nasdaq-demo-human-evidence-contract.md#historical-documented-exit-causality-under-limited-timestamp-resolution). Conserva revisi?n humana de Video 2 aprox. 13:10?13:25 y Video 3 aprox. 13:15?13:40; Open/Close separados, fecha y minutos con segundos no transcritos. No conserva broker, s?mbolo exacto, precios ni UTC. |
| S2 | [Especificaci?n Nasdaq](../../strategies/nasdaq/strategy-specification.md), secci?n 10 (sesiones), 11 (horario), Historical documented-exit causality y secci?n 21 (targets). Normativa y rese?as; no dataset de esta operaci?n. La secci?n Source coverage no suministra t?tulo/URL/transcript original. |
| S3 | Solicitud actual ?FIRST REAL MENTOR SESSION DATA PACK?. Reporta SELL y 0.25 como hallazgos previos a verificar; no son valores finalizados del pack. |
| S4 | [Gu?a de ejecuci?n Feature 29](../../backtesting/nasdaq-mentor-session-local-run.md). Contrato del input, cuatro CSV, disponibilidad aut?ntica y binding a hechos can?nicos. |
| S5 | [Auditor?a de preparaci?n](../../roadmap/nasdaq-first-real-session-readiness.md). No identifica dataset real validado y diferencia la dependencia de datos de la capacidad del c?digo. |
| S6 | Inspecci?n de archivos del repositorio, incluidos ignorados y excluyendo Git/build/node_modules/secretos. ?nico CSV identificado: `samples/market-data/replay-demo.synthetic.csv`, provider `historical-fixture`, symbol `DEMO`, 5M, 2026-01-01. Es sint?tico, otra fecha/instrumento; excluido del pack real. No se identificaron videos, capturas, historial broker ni OHLC reales en el ?rbol inspeccionado. No se inspeccionaron carpetas personales ajenas al proyecto. |

Video 2 / Video 3 son etiquetas de la revisi?n, no identidades ?nicas del archivo original. Sus t?tulos, enlaces/archivos, hashes y permiso de acceso/redistribuci?n permanecen `unresolved`. No se buscaron videos por un t?tulo inventado. No hay evidencia nueva contradictoria: hay datos faltantes.

## Manifest: identidad, instrumento y reloj

| Campo | Valor | Estado | Procedencia / condici?n pendiente |
| --- | --- | --- | --- |
| SessionId | `nasdaq-mentor-2024-06-28-v2-v3` | confirmed | Identificador administrativo creado para esta solicitud S3; no es broker ticket, execution ID ni prueba de matching entre videos. |
| Strategy / version | `moneyway-nasdaq` / `nasdaq-0.1.0-draft` | confirmed | Definition y contrato existentes S4. |
| DisplayedTradeDate | `2024-06-28` | confirmed | Fecha conservada por revisi?n documental S1; falta captura original para verificar transcripci?n. |
| LocalStrategyDate | null (candidata `2024-06-28`) | human_validation_required | Resolver reloj de origen y comprobar el d?a America/Bogota; no copiar autom?ticamente DisplayedTradeDate. |
| provider_id | null | unresolved | Identificar broker, servidor y fuente hist?rica efectivamente operados. `fixture`/`historical-fixture` no son candidatos reales. |
| symbol | null | unresolved | Obtener s?mbolo literal del ticket y de los charts; ?Nasdaq? es familia, no especificaci?n de instrumento. |
| ExactInstrumentDescription | null | unresolved | Nombre/tipo de contrato o CFD, escala, unidad, especificaci?n y base de cotizaci?n, ligados al s?mbolo/servidor. |
| Broker / Server | null / null | unresolved | Identidad del broker y servidor correspondiente a la cuenta/sesi?n; sin credenciales. |
| SourceTimezone / UTCMappingEvidence | null / null | unresolved | Prueba autoritativa ligada al servidor y a 2024-06-28, o sincronizaci?n validada; no offset elegido por plausibilidad. |
| StrategyTimezone | `America/Bogota` | confirmed | C?digo de NasdaqSessionLiquidityCalculator y S2. Independiente del reloj MetaTrader. |
| ReplayStartUtc / ReplayEndUtc | null / null | unresolved | Primero identificar fuentes, tiempos UTC y miembros estructurales; v?ase ventana m?nima abajo. |
| MarketDataSourceReference | null | unresolved | Ninguna fuente/dataset aceptado ni adquirido. |

No se consideran equivalentes CME NQ, Nasdaq-100 cash/index, NAS100/US100/USTEC ni s?mbolos CFD de brokers distintos. La aprobaci?n requiere compatibilidad de instrumento, proveedor, escala, cotizaci?n, fronteras de velas y sesiones; no ?nicamente una etiqueta Nasdaq.

## Manifest: ejecuci?n, protecci?n, target, riesgo y salida

| Campo | Valor | Estado | Procedencia / qu? falta |
| --- | --- | --- | --- |
| EntryDisplayTimePartial | `2024-06-28 14:02:xx` | confirmed | S1, rese?a documental; `xx` no es timestamp ejecutable ni segundo cero. |
| ExitDisplayTimePartial | `2024-06-28 14:03:xx` | confirmed | S1, mismo l?mite. |
| TimestampResolution | segundos mostrados en el ejemplo | confirmed | S1; no se generaliza a otras fuentes. |
| DocumentedEntryBeforeExit | Open anterior a Close en la rese?a | confirmed | S1; falta vincular ticket/snapshot/eventos exactos. No se crea prueba tipada con identidades imaginarias. |
| Direction | null (reporte previo: SELL) | human_validation_required | S3. No se encontr? respaldo final de SELL para este evento en S1/S2/casos; transcribir el ticket legible. |
| Quantity / QuantityUnit | null / null (reporte previo: 0.25) | human_validation_required | S3. No asumir lots, contracts ni cantidad de salida. |
| EntryExecutionId / ExitExecutionId | null / null | unresolved | Identidades source-qualified y relaci?n inequ?voca entre apertura y cierre. |
| EntryDisplayTimeExact / ExitDisplayTimeExact | null / null | unresolved | Capturar segundos originales completos; no completar `xx`. |
| EntryEffectiveAtUtc / ExitEffectiveAtUtc | null / null | unresolved | Segundos completos y mapping UTC validado. |
| EntryPrice / ExitPrice | null / null | unresolved | Existen campos separados seg?n S1, pero valores no conservados. |
| StopPrice / StructuralAnchorEvidence | null / null | unresolved | Par?metro literal y miembros/precio del LH/HL 5M real seleccionado; regla conceptual no basta. |
| TakeProfitPrice / TargetReferenceEvidence | null / null | unresolved | Nivel exacto y fuente elegida; igualdad target-to-TP normativa no determina su valor. |
| AccountBasis / Currency / BasisReference | null / null / null | unresolved | Base documental de esta cuenta/assessment; no inferir balance de 0.25 ni de regla 1%. |
| PlannedMaximumLoss / RiskCalculationReference | null / null | unresolved | P?rdida m?xima planeada documentada y procedencia; sin PnL inferido. |
| PlannedEntryPrice / CostTreatment | null / null | unresolved | Operand y tratamiento documental de costos; no asumir costos cero ni copiar autom?ticamente EntryPrice. |
| DocumentedExitScope | null | unresolved | Full/Partial para este evento, no generalizar intenci?n de complete exit de otros ejemplos en S2. |
| DocumentedExitReason | null | unresolved | Raz?n literal solo si documentada; no inferir TP/SL por igualdad de precio. |
| ExitQuantity / ExitQuantityUnit | null / null | unresolved | Transcripci?n espec?fica de la salida, si existe. |
| EvidenceObservedAtUtc (cada registro) | null | unresolved | Disponibilidad hist?rica aut?ntica y procedencia; no igualar autom?ticamente al effective time ni a la fecha de esta revisi?n. |

## Evidencia de estrategia para esta operaci?n

Las reglas generales de S2 est?n auditadas; no son evidencia de que esta operaci?n cumpliera cada gate. Cada fila mantiene sus precios/timestamps/miembros/selecciones y disponibilidad en `null` hasta disponer de respaldo del caso.

| Etapa | Estado del caso | Evidencia necesaria |
| --- | --- | --- |
| TIME / preparaci?n | unresolved | Fecha Bogot? validada, revisi?n H4 y marcaci?n de liquidez completadas realmente en [08:00,08:30); disponibilidad y material. |
| H4 context | unresolved | HH/HL o LL/LH activos, fuentes cerradas, breakout/wickfill/fakeout y direcci?n revisada. |
| Structural/session liquidity | unresolved | OHLC 1H completos Asia/London y referencias estructurales 1H/4H seleccionadas; miembros y valores exactos. |
| Relevant liquidity take | unresolved | Referencia exacta, evento/precio/tiempo y relevancia revisada; no inferir instante intrabar desde OHLC. |
| M5 trigger | unresolved | Evento StructuralChange OR IFVG, fuentes y confirmaci?n cerrada del setup. |
| Mandatory M5 FVG | unresolved | Tres velas/miembros y confirmaci?n exacta del desequilibrio. |
| FVG quality | unresolved | Aprobaci?n/rechazo fuente de ese FVG, effective/observed times; no umbral inventado. |
| M1 corrective retracement | unresolved | Selecci?n de evento/velas para el FVG aprobado y disponibilidad real. |
| M1 realignment | unresolved | ?ltimo swing/level correctivo elegido, direcci?n, vela cerrada y orden causal cuando mismo cierre. |
| NQ-M1-003 | unresolved | Derivado exclusivamente del realignment can?nico; no crear anotaci?n humana adicional. |
| Historical observed entry | unresolved | Ticket, precio, UTC y origen, ligados a la elegibilidad real. |
| Structural SL | unresolved | Anchor 5M y StopPrice literal, con evidencia/fechas fuente. |
| TP target | unresolved | Referencia seleccionada y TakeProfitPrice exactos. |
| Risk | unresolved | Assessment, base monetaria/unidades/costos y par?metros documentados. |
| Documented historical exit | unresolved | Cierre inequ?voco del mismo trade con precio/UTC/identidad/alcance/raz?n cuando documentados. |

**Evidencia espec?fica confirmada:** solo fecha mostrada, minutos Open/Close incompletos, resoluci?n y precedencia documental anteriores. Ninguna evaluaci?n can?nica ni aprobaci?n de gate se ha producido para esta sesi?n.

## Ventana de datos: restricciones conocidas, sin rango final inventado

Las fronteras existentes son `confirmed` (c?digo y S2), por OpenTime local de cada vela 1H:

- Asia: [D-1 17:00, D 02:00), nominalmente 9 velas 1H.
- London: [D 02:00, D 07:00), nominalmente 5 velas 1H.
- Preparaci?n: [D 08:00, D 08:30); adquisici?n de entrada: [D 08:30, D 11:00).

**Solo si D se valida como 2024-06-28**, las ventanas de estrategia son Asia [2024-06-27T22:00:00Z, 2024-06-28T07:00:00Z), London [2024-06-28T07:00:00Z, 2024-06-28T12:00:00Z), preparaci?n [13:00Z,13:30Z) y adquisici?n de entrada [13:30Z,16:00Z). Estas conversiones del reloj de estrategia NO convierten el Open/Close MetaTrader ni prueban compatibilidad horaria de la operaci?n.

El comienzo suficiente debe cubrir todas las velas cerradas de sesi?n y los miembros/anchors H4/1H anteriores realmente seleccionados. No existe lookback fijo que permita escoger una fecha H4 arbitraria. 2024-06-27T22:00Z es una frontera condicional de cobertura Asia, no un comienzo suficiente demostrado del pack. El final debe cubrir la salida real y las fronteras can?nicas posteriores necesarias para transportar hechos/documentar artefactos; no se impone cierre de posici?n a las 11:00. Las velas 4H deben conservar las fronteras verificadas del proveedor, no desplazarse para encajar en la estrategia.

**Rango final elegido: unresolved.** Faltan mapping UTC, fuentes estructurales, precisi?n del cierre y cobertura hist?rica real. No se descarg? solo la entrada ni un rango enorme especulativo.

## Fuente de mercado y validaci?n de CSV

Fuente aceptada: ninguna. Candidatas condicionadas a identificar broker/instrumento: exportaci?n hist?rica del mismo broker/servidor de la operaci?n, o archivos originales retenidos de esa sesi?n con procedencia verificable. No se seleccion? vendor alternativo; no se adquiri? NQ/cash/CFD gen?rico como sustituto. Disponibilidad de 2024, credenciales/subscripci?n, permisos de acceso y licencia de redistribuci?n: `unresolved`. No se solicitaron ni almacenaron secretos.

| Archivo requerido | Estado | Validaci?n con importador |
| --- | --- | --- |
| 4H.csv | unresolved: no archivo real | No ejecutada. |
| 1H.csv | unresolved: no archivo real | No ejecutada. |
| 5M.csv | unresolved: no archivo real | No ejecutada. |
| 1M.csv | unresolved: no archivo real | No ejecutada. |

Schema existente, sin otro parser ni s?ntesis:

```csv
provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume
```

Cuando se suministren archivos, usar `CsvMarketDataImporter.ImportFile` existente para validar sintaxis/UTC/identidad homog?nea/orden/duplicados/overlap/OHLC. Separadamente verificar escala, base de cotizaci?n, fronteras nativas de cada timeframe, gaps/completitud y cobertura suficiente: importaci?n correcta no demuestra ausencia de velas faltantes ni compatibilidad con el trade. Volume vac?o es desconocido, no cero. Revisar calendario y sesiones reales sin rellenar datos.

**Price/time cross-check: no ejecutado.** Faltan precios, UTC y velas. Con ellos, contrastar EntryPrice/ExitPrice y niveles relevantes en sus intervalos UTC y fuente/base de cotizaci?n documentada, conservar Entry -> Exit y evaluar Bogot? independientemente. No inventar tolerancia/spread ni modificar precios del mentor para encajar. Compatibilidad con rango no demuestra un fill ni orden intrabar. Toda diferencia material inexplicada impide aprobar el dataset y exige investigar instrumento/proveedor/fecha/reloj/transcripci?n. Una contradicci?n fuente real con estrategia activa debe reportarse NEW_EVIDENCE_CONFLICT, sin cambiar reglas.

## Siguiente evidencia m?nima y tratamiento Git

Para liberar la adquisici?n se necesita primero:

1. Videos 2/3 originales o enlaces accesibles identificables y captura legible del historial con s?mbolo literal, Open/Close completos, precios, volumen y ticket/relaci?n de ejecuci?n.
2. Identidad del broker/servidor correspondiente a esa cuenta/sesi?n y especificaci?n del instrumento/escala/unidad.
3. Evidencia de zona horaria/UTC aplicable en esa fecha o una referencia sincronizada humana validada. No bastan un offset plausible ni la coincidencia con horario de estrategia.

Despu?s, completar las selecciones/miembros y provenance de las etapas pendientes, adquirir los cuatro timeframes nativos de la fuente compatible y efectuar validaciones/cross-checks. Crear el adapter S4 ?nicamente con datos suficientemente respaldados; podr? conservar Missing/Conflict/Unavailable sin fabricar registros para alcanzar Available.

`ValidatedInput.cs`: no preparado. Harness real: no ejecutado. `report.json`: no generado. Primer blocker can?nico: no evaluado, porque no hubo replay; el bloqueo actual es de evidencia/identificaci?n previa a adquisici?n, no un fallo de regla ni de engine.

Guardar material broker/video/licenciado, CSV y adapters privados fuera de Git o en `local-data/` (ya ignorado) seg?n derechos reales. Este documento solo contiene metadata segura de la revisi?n y pendientes; no contiene credenciales, paths absolutos personales, originales multimedia ni series redistribuibles. Licencias originales no establecidas: no autorizar redistribuci?n. La pol?tica del encargo permite un commit de esta metadata documental; ning?n raw dataset se publica.

## Verificaci?n realizada en este pase

Se revisaron c?digo/contratos existentes, ADR, especificaci?n/casos y archivos locales del proyecto. Se comprobaron los estados permitidos y la procedencia expl?cita de los campos del manifest, cuatro enlaces locales y formato UTF-8/LF. Se verificaron las fronteras UTC condicionales de estrategia usando la zona Windows que identifica Bogot? (`SA Pacific Standard Time`); esto no verifica ni propone un timezone del broker. `git diff --check` y la comprobaci?n del diff staged pasaron. No se ejecutaron importaci?n CSV real, pruebas sobre un dataset real, cross-source checks ni replay real por falta de fuentes. No se modific? c?digo y no se repiti? la suite de engine como sustituto de validaci?n de datos.
