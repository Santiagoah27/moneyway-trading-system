# Project purpose

MoneyWay Trading System será una plataforma supervisada y auditable para:

- Analizar las estrategias MoneyWay Forex y Nasdaq.
- Evaluar reglas deterministas.
- Realizar replay y backtesting sin información futura.
- Registrar todas las decisiones y sus evidencias.
- Ejecutar posteriormente únicamente en paper trading o cuenta demo.
- Evaluar operaciones y diagnosticar sus resultados.
- Proponer mejoras candidatas que requieran aprobación humana.

No implementar operación con dinero real.

# Source of truth

Usar este orden de precedencia:

1. Código y tests existentes.
2. Decisiones documentadas en docs/decisions/.
3. Arquitectura documentada en docs/architecture/.
4. Estrategias auditadas en docs/strategies/.
5. Instrucción actual del usuario.

Nunca inventar reglas de trading ni completar vacíos usando conocimiento general.

Cuando una regla sea ambigua, contradictoria, visual o subjetiva:

- No seleccionar una interpretación silenciosamente.
- Marcarla como unresolved o human_validation_required.
- No usarla para autorizar automáticamente una operación.

# Development behavior

Antes de modificar código:

- Inspeccionar los archivos relevantes.
- Leer la documentación relacionada.
- Comprender las convenciones existentes.
- Identificar riesgos de regresión.
- Realizar el cambio mínimo necesario.

Después de modificar código:

- Ejecutar las pruebas relacionadas.
- Informar cuáles pruebas realmente se ejecutaron.
- No afirmar que algo funciona sin evidencia.
- Resumir archivos y comportamiento modificados.
- Señalar riesgos o pendientes reales.

No realizar refactors no relacionados.

No cambiar arquitectura, framework o dependencias principales sin justificación y aprobación.

No crear toda la aplicación en una sola tarea.

# Work modes

Cuando el usuario escriba "ajuste rápido":

- Revisar únicamente los archivos necesarios.
- Hacer el cambio mínimo.
- Evitar refactors amplios.
- Ejecutar pruebas relacionadas.
- Responder brevemente.

Cuando el usuario escriba "analiza primero":

- No modificar archivos.
- Inspeccionar el repositorio.
- Explicar el problema.
- Identificar archivos afectados.
- Proponer un plan.
- Señalar decisiones abiertas.
- Esperar aprobación.

En modo normal:

- Analizar lo necesario.
- Implementar directamente cuando el alcance sea claro.
- Preguntar únicamente si la decisión afecta materialmente la arquitectura, las estrategias o la seguridad.

# Architecture principles

Mantener separados:

- Market data.
- Strategy definitions.
- Deterministic strategy engine.
- Backtesting and replay.
- Analysis and explanations.
- Demo execution.
- Risk management.
- Learning and evaluation.
- API.
- User interface.

El LLM puede explicar, diagnosticar y proponer hipótesis.

El LLM no puede:

- Crear órdenes directamente.
- Modificar estrategias activas.
- Promover automáticamente versiones candidatas.
- Inventar reglas.
- Decidir usando información futura.

# Strategy rules

Toda regla deberá tener uno de estos estados:

- confirmed
- candidate
- context_specific
- visual_only
- human_validation_required
- unresolved
- rejected_ai_inference

Toda evaluación deberá devolver:

- passed
- failed
- waiting
- not_applicable
- human_validation_required
- data_unavailable

El análisis general deberá devolver:

- ready
- wait
- no_trade
- human_validation_required
- data_unavailable

No devolver ready cuando falte una condición obligatoria, los datos estén incompletos o exista una regla crítica unresolved.

Mantener completamente separadas las reglas Forex y Nasdaq.

# Learning principles

Una operación perdedora no significa automáticamente que la estrategia esté equivocada.

Clasificar los resultados como:

- valid_strategy_loss
- rule_violation
- execution_error
- market_data_error
- interpretation_error
- risk_management_error
- market_regime_mismatch
- inconclusive

Las estrategias activas serán inmutables y versionadas.

Las mejoras deberán crearse como versiones candidate, probarse por separado y requerir aprobación humana.

No modificar reglas como reacción a una única operación.

No optimizar usando datos futuros.

No ocultar resultados negativos.

# Safety and execution

Durante las primeras etapas solo permitir:

- Análisis asistido.
- Backtesting.
- Replay histórico.
- Shadow mode.
- Paper trading.
- Cuenta demo supervisada.

No permitir:

- Credenciales live.
- Endpoints live.
- Operación con dinero real.
- Órdenes sin Stop Loss.
- Órdenes basadas en reglas unresolved.
- Exposición de secretos en código o logs.

Ante cualquier duda, no ejecutar y devolver human_validation_required o data_unavailable.

# Testing

Toda regla determinista debe tener pruebas.

Incluir según corresponda:

- Unit tests.
- Integration tests.
- Replay tests.
- Backtesting tests.
- Timezone tests.
- Boundary tests.
- Failure-path tests.
- Idempotency tests.

Nunca eliminar pruebas para hacer pasar una implementación.

Nunca afirmar que una prueba fue ejecutada si no se ejecutó.

# Traceability

Toda decisión deberá registrar como mínimo:

- Strategy.
- Strategy version.
- Instrument.
- Data source.
- Timestamp.
- Timezone.
- Market snapshot.
- Rules evaluated.
- Rule statuses.
- Evaluation results.
- Evidence.
- Final verdict.
- Human validations.
- Entry, Stop Loss y Take Profit cuando existan.
- Result.
- Failure classification.
- Trace identifier.

# Communication

- Comunicarse con el usuario en español.
- Usar inglés para código, nombres técnicos, clases y propiedades.
- Ser directo y evitar explicaciones innecesariamente largas.
- No presentar suposiciones como hechos.
- Señalar claramente lo que no pudo verificarse.
