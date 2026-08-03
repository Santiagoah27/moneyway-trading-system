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

El repositorio contiene el bootstrap técnico aprobado. Todavía no implementa reglas de estrategia, persistencia, replay, backtesting ni ejecución.

## Prerequisites

- .NET SDK 10.0.302, recorded in `global.json`.
- Node.js 24.19.0 LTS, recorded in `.nvmrc`.
- npm 11.17.0, recorded in the frontend `packageManager` field.

## Local development

Backend validation:

```powershell
dotnet restore MoneyWay.sln
dotnet build MoneyWay.sln --no-restore
dotnet test MoneyWay.sln --no-build
```

Run the API at `http://localhost:5080`:

```powershell
dotnet run --project src/backend/MoneyWay.Api
```

Available bootstrap endpoints:

- `GET http://localhost:5080/health`
- `GET http://localhost:5080/api/system/status`
- Development OpenAPI document at `http://localhost:5080/openapi/v1.json`

Run the idle worker (execution remains disabled):

```powershell
dotnet run --project src/backend/MoneyWay.Worker
```

Frontend setup and validation:

```powershell
cd src/frontend/moneyway-web
npm ci
npm run format:check
npm run lint
npm run test:run
npm run build
npm run dev
```

Copy `.env.example` to a local `.env` only when the API URL must be customized. For a manual visual check, start the API and frontend in separate terminals, open `http://localhost:5173`, verify connected and disconnected states, and resize to 320 px. Do not enter credentials; this bootstrap has no broker or live configuration.

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
