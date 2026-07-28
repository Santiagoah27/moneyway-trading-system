# MoneyWay Trading System

## Project

MoneyWay Trading System.

## Purpose

Plataforma supervisada, auditable y versionada para analizar y evaluar las estrategias MoneyWay Forex y Nasdaq.

## Initial capabilities

- Strategy documentation.
- Assisted market analysis.
- Deterministic rule evaluation.
- Historical replay.
- Backtesting.
- Shadow mode.
- Paper trading.
- Supervised demo execution.
- Trade evaluation.
- Failure diagnosis.
- Candidate strategy improvements subject to human approval.

## Explicit non-goals for the initial stages

- Real-money trading.
- Live broker credentials.
- Autonomous modification of active strategies.
- LLM-generated order execution.
- Automatic use of unresolved rules.
- Production deployment.

## Repository status

El repositorio está en etapa de bootstrap. Aún no se ha seleccionado ni generado el stack tecnológico.

## Documentation map

- `docs/architecture`: diseño vigente y límites del sistema.
- `docs/decisions`: Architecture Decision Records (ADRs).
- `docs/roadmap`: fases, entregables y criterios de avance.
- `docs/strategies/forex`: especificaciones auditadas de MoneyWay Forex.
- `docs/strategies/nasdaq`: especificaciones auditadas de MoneyWay Nasdaq.
- `src`: ubicación reservada para componentes de aplicación y workers.
- `tests`: ubicación reservada para pruebas unitarias, de integración y backtesting.
- `infrastructure`: ubicación reservada para recursos de base de datos y contenedores.
- `samples`: fixtures pequeños, sanitizados y explícitamente versionados.

## Development principles

- Auditability.
- Deterministic evaluation.
- No future-data leakage.
- Human validation for subjective rules.
- Versioned strategies.
- Demo-first execution.
- Complete traceability.
