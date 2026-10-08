# Candidato Nasdaq 2024: evidencia rechazada

## Disposición y trazabilidad

**SUPERSEDED — rejected_ai_inference**. El candidato `nasdaq-mentor-2024-06-28-v2-v3` queda retirado como evidencia de sesión. No representa una operación histórica validada ni un dataset pendiente de completar. Lo reemplaza [nasdaq-mentor-2026-06-29-videos](mentor-session-2026-06-29.md), sesión humana revisada **NO_TRADE**.

Fuente de corrección C1: solicitud del usuario “SOURCE-OF-TRUTH CORRECTION — Replace Invalid 2024 Mentor Session Evidence with Human-Reviewed 2026-06-29 NO-TRADE Session”, recibida el 2026-10-07. La revisión humana de alta resolución del original contradice la extracción AI previa: fecha 2026-06-29, sin retroceso 1M y sin entrada; no muestra terminal ni ticket MetaTrader. Esta tarea registra la revisión suministrada, sin afirmar inspección independiente del video.

Las versiones previas no se borran del historial Git: `1b62376520a8638dfb9f381a0cf4b7f0c00203a9` (gaps iniciales), `8065955032bc70b7730687feeb035620b16d329e` (promoción errónea) y `c132b84066415990ba896776a29c1a1804497bc0` (intento de acceso 2024). Este ledger sustituye los estados activos erróneos; los textos completos y sus fuentes S1–S10 permanecen en esos commits. Las solicitudes anteriores presentaban la extracción como revisión humana: C1 corrige ahora su atribución y validez. No se trasladan los valores al nuevo caso ni se afirma que correspondan a otra operación real.

## Valores rechazados del candidato anterior

Todos los campos siguientes tienen estado **rejected_ai_inference**, por C1. Se conservan exclusivamente para auditoría.

| Campo / afirmación anterior | Valor rechazado | Estado |
| --- | --- | --- |
| Fecha de sesión / ejecución | `2024-06-28` | rejected_ai_inference |
| Ticket / existencia de historial MetaTrader | `35079234` / ejecución atribuida | rejected_ai_inference |
| Símbolo ejecutado | `NDX100` | rejected_ai_inference |
| SELL ejecutado | Supuesta venta histórica | rejected_ai_inference |
| Volumen | `0.25 lots` | rejected_ai_inference |
| OpenDisplayTime | `2024-06-28 14:02:18`; antes `14:02:xx` | rejected_ai_inference |
| CloseDisplayTime | `2024-06-28 14:03:05`; antes `14:03:xx` | rejected_ai_inference |
| EntryPrice | `20019.14` | rejected_ai_inference |
| StopLoss | `20034.50` | rejected_ai_inference |
| TakeProfit / ExitPrice | `19972.10` / `19972.10` | rejected_ai_inference |
| Commission / Swap | `0.00` / `0.00` | rejected_ai_inference |
| DisplayedProfit / moneda | `+1181.88 USD` | rejected_ai_inference |
| DocumentedEntryBeforeExit / resolución | Secuencia Open → Close y segundos atribuida a video | rejected_ai_inference |
| ExitScope / ExitReason | Full / salida completa al target programado | rejected_ai_inference |
| SL/target vinculados a estructuras | LH/wick 5M, lows Asia/London coincidentes cerca de `19972.xx` | rejected_ai_inference |
| Retroceso 1M / realignment para ejecución | Supuestos eventos cumplidos antes del SELL | rejected_ai_inference |
| Instrumento/feed del caso | OANDA / US 100 / US100 / US100.cash / candidato OANDA:NAS100USD atribuidos al video | rejected_ai_inference |

Los demás relatos del setup de 2024 quedan igualmente retirados como evidencia de ese candidato. Las observaciones de H4/liquidez/5M que C1 respalda se registran de nuevo solo en el manifiesto de 2026 y con su propia precisión. La dirección SELL considerada no demuestra ejecución.

## Qué conserva validez y qué deja de aplicar

- America/Bogota, UTC-5 sin DST, preparación 08:00–08:30 y trading 08:30–11:00 permanecen como reglas generales existentes; no dependen de la extracción rechazada.
- La consulta de catálogo OANDA:NAS100USD y el límite de 5.000 barras observado al intentar 1M de 2024 el 2026-10-06 fueron acciones técnicas reales. No identifican el ticker del video ni prueban acceso, identidad o precio de la sesión de 2026.
- La ventana planificada `[2024-06-17T00:00:00Z,2024-06-29T00:00:00Z)` se retira como plan de adquisición del mentor. No se adquirieron raw/CSV, hashes, adapter ni replay real para ese candidato.
- El supuesto bloqueo UTC MetaTrader queda retirado del caso vigente. Para la sesión NO_TRADE, ejecución y alineación MetaTrader son **not_applicable**. No se intenta sincronizar un ticket inexistente en el material.
- [ADR 0025](../../decisions/0025-define-nasdaq-demo-human-evidence-contract.md) y [especificación Nasdaq](../../strategies/nasdaq/strategy-specification.md) conservan los contratos generales implementados. Sus referencias al ejemplo de ejecución 2024 se anotan como rechazadas; no se cambia causalidad, engine ni StrategyVerdict.

No usar este archivo para construir Session, observaciones ejecutables, Entry/SL/TP/riesgo/Exit, trade snapshot o P/L. El siguiente pendiente está en el manifiesto de 2026: identidad legible de instrumento y proveedor.
