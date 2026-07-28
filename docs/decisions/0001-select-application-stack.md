# ADR 0001: Select application stack

## Status

Proposed.

## Date

2026-07-28.

## Context

MoneyWay Trading System comienza como una aplicación personal, local-first y mantenida por una sola persona. Debe avanzar mediante entregas incrementales desde documentación versionada y análisis asistido hacia evaluación determinista, replay histórico vela por vela, backtesting sin future-data leakage, shadow mode, paper trading y ejecución supervisada en cuenta demo. La operación con dinero real está fuera del alcance inicial.

El sistema necesita reglas deterministas y testeables, trazabilidad completa de decisiones, estrategias inmutables y versionadas, y capacidad futura para procesar tareas intensivas en background. También deberá integrar proveedores de market data y brokers demo mediante límites externos, sin acoplarlos al motor de estrategia.

El desarrollo principal ocurre en Windows y VS Code. El desarrollador tiene experiencia profesional full-stack, mayor experiencia con React que con Angular y experiencia con .NET y Entity Framework Core. El proyecto debe favorecer strong typing, ejecución y depuración local sencillas, rapidez de desarrollo y bajo coste operativo. No necesita escalamiento masivo ni una arquitectura distribuida en sus primeras etapas.

El análisis mediante LLM debe permanecer separado de la evaluación determinista y de cualquier ejecución. La IA puede explicar, diagnosticar y proponer candidatos, pero no puede autorizar o enviar órdenes, modificar estrategias activas ni utilizar información futura.

## Decision drivers

- Development speed.
- Maintainability.
- Deterministic testing.
- Strong typing.
- Local development.
- Operational simplicity.
- Backtesting performance.
- Extensibility.
- Auditability.
- Separation between deterministic execution and AI analysis.
- Low infrastructure cost.
- Ease of debugging.
- Incremental delivery.

## Considered options

### Option A — ASP.NET Core modular monolith, React + TypeScript, PostgreSQL

Combines the developer's .NET and React experience, end-to-end strong typing, mature persistence tooling and a simple local operational model. Logical module boundaries can evolve without paying distributed-system costs upfront.

### Option B — ASP.NET Core modular monolith, Angular, PostgreSQL

Provides comparable backend, persistence, testing and operational characteristics to Option A. Angular offers a structured frontend but is less aligned with the developer's stronger React experience, increasing initial learning and delivery cost without a current project requirement that offsets it.

### Option C — Python backend, FastAPI or equivalent, React + TypeScript, PostgreSQL

Could reduce future friction for research and machine learning. For the current deterministic core, it weakens the fit with existing .NET experience and introduces a less uniform typing story. Python remains available later behind an explicit boundary if a demonstrated research or model-serving need appears.

### Option D — Microservices from the start

Allows independent deployment and scaling but introduces service coordination, networking, distributed tracing, failure handling, deployment and data-consistency costs before the project has workloads or team boundaries that justify them.

### Comparison

| Criterion | Option A | Option B | Option C | Option D |
|---|---|---|---|---|
| Single-developer productivity | High; matches .NET and React experience | Medium-high; frontend learning cost | Medium; backend context shift | Low; distributed overhead |
| Operational complexity | Low | Low | Low-medium; additional ecosystem | High |
| Testing ease | High; cohesive typed boundaries | High | Medium-high; good tools but mixed typing/runtime | Low-medium; distributed tests required |
| Backtesting performance potential | High for an initial in-process compiled engine | High | Medium-high; viable but may need optimization earlier | Potentially high, but coordination overhead is premature |
| Typing | Strong across C# and TypeScript | Strong across C# and TypeScript | Mixed; Python typing is less enforceable at runtime | Depends on services; contracts add complexity |
| Maintainability | High if module boundaries are enforced | High if module boundaries are enforced | Medium-high; two backend-oriented ecosystems may emerge later | Low initially for one maintainer |
| Broker integration | Good through external adapters | Good through external adapters | Good through external adapters | Flexible but operationally expensive |
| Background processing | Good through a worker in the same solution and separable process | Good through a worker in the same solution and separable process | Good, with separate process conventions | Strong isolation, excessive initial cost |
| Learning curve | Low | Medium | Medium | High |
| Overarchitecture risk | Low-medium | Low-medium | Medium if Python is chosen for hypothetical ML | Very high |
| Local execution | Simple | Simple | Medium; more runtime/tooling variation | Complex |
| Developer experience fit | Best | Good backend, weaker frontend fit | Partial | Poor for current project constraints |
| Future machine learning | Integrate Python later when justified | Integrate Python later when justified | Strongest direct path | Flexible but distributed by default |
| Traceability | High with one transactional system | High with one transactional system | High, with discipline across dynamic boundaries | Harder; requires distributed correlation |
| Initial operating cost | Low | Low | Low-medium | High |

Option A is preferred for the current stage. The alternatives are not equivalent: Option B adds avoidable frontend friction, Option C optimizes for an unproven future Python need, and Option D creates substantial operational complexity without current scaling pressure.

## Decision

Propose an initial modular monolith with:

- ASP.NET Core backend written in C#.
- React frontend written in TypeScript.
- PostgreSQL as primary persistence.
- Entity Framework Core as ORM.
- REST API for initial frontend/backend communication.
- Background worker within the same solution, executable as a separate process when required.
- Adapter pattern for external broker integrations, limited to demo or sandbox, and market data providers.
- LLM capabilities outside deterministic strategy evaluation and order submission.
- Python deferred until a demonstrated technical need exists.
- Containers deferred until a concrete need exists.
- Local-only initial deployment.

This combination balances delivery speed, strong typing, deterministic testing, compiled performance, maintainability and local debugging. A modular monolith keeps operational complexity and infrastructure cost low while preserving logical boundaries that can later be separated when measured needs justify it.

No concrete framework versions are selected here. Supported, stable and mutually compatible versions will be chosen during technical bootstrap against the local environment.

## Initial solution boundaries

The following are logical boundaries inside one modular monolith, not microservices:

- **Domain:** core concepts and invariants independent of external systems.
- **Application:** use cases and orchestration over domain behavior and internal contracts.
- **Infrastructure:** persistence and implementations of external-facing contracts.
- **API:** transport boundary that exposes application use cases.
- **Background processing:** scheduled or long-running application work, runnable separately when needed.
- **Web frontend:** local user experience consuming API contracts.
- **Strategy definitions:** immutable, versioned and evidence-backed specifications.
- **Deterministic strategy evaluation:** reproducible evaluation of confirmed rules and blocking states.
- **Market data:** normalized acquisition and identification of provider data.
- **Backtesting and replay:** time-ordered historical evaluation without future-data leakage.
- **Risk management:** mandatory validation before any demo order submission.
- **Demo execution:** supervised paper/demo order workflow behind external adapters.
- **Trade evaluation:** outcome assessment and failure classification.
- **Learning proposals:** candidate improvements that require separate testing and human approval.

This ADR does not define physical projects, namespaces or final folder structure.

## Dependency direction

- Domain does not depend on Infrastructure.
- Application depends on Domain.
- Infrastructure implements contracts defined by inner layers.
- API coordinates application use cases but contains no trading rules.
- Web frontend consumes API contracts.
- Strategy evaluation does not depend on brokers.
- Backtesting does not depend on demo execution.
- Demo execution cannot modify strategy definitions.
- Risk management must validate a proposed order before submission.
- LLM analysis cannot authorize or send orders.
- Learning proposals cannot promote strategy versions automatically.

Dependencies should point toward deterministic domain and application policies. External adapters remain replaceable and outside the strategy engine.

## Data strategy

- PostgreSQL is the primary persistence store.
- Schema migrations are versioned.
- Temporal persistence uses UTC.
- Original timezone is retained when relevant.
- Market data is identified by provider, symbol and timeframe.
- Candle timestamp semantics must be explicit.
- Immutable snapshots or immutable references must allow reconstruction of decisions.
- Strategy version is stored with every analysis and operation.
- Secrets are not stored directly without an approved secure mechanism.
- Large historical datasets may require a different storage strategy later.

This ADR does not design tables or physical entities.

## Testing strategy

- Unit tests for deterministic rules.
- Integration tests for persistence.
- Replay tests.
- Backtesting tests.
- Adapter contract tests.
- Idempotency tests for demo execution.
- Timezone tests.
- Boundary tests.
- Failure-path tests.
- No future-data leakage tests.
- Strategy version regression tests.

Concrete testing libraries remain deferred unless required by technical bootstrap.

## AI boundary

The LLM-based component may:

- Explain decisions.
- Analyze failures.
- Propose hypotheses.
- Summarize results.
- Compare operations.
- Assist review of subjective rules.
- Generate candidate proposals for evaluation.

It may not:

- Replace the deterministic engine.
- Authorize orders.
- Send orders.
- Unilaterally calculate operational risk.
- Modify active strategies.
- Promote candidate versions.
- Access credentials directly.
- Approve operations with `unresolved` rules.
- Use future information to explain a historical decision.

## Execution boundary

- Execution is initially disabled.
- Initial phases cover analysis, replay and backtesting.
- Shadow mode may be enabled later.
- Paper trading may follow after validation.
- Supervised demo execution is a later phase.
- Broker adapters must reject live configuration by default.
- Real-money execution is outside the initial scope.
- Every demo order requires deterministic validation, risk approval, complete traceability and idempotency.

## Consequences

### Positive

- Lower initial complexity.
- One repository and operating model.
- Strong typing.
- Simpler testing.
- Incremental evolution.
- Straightforward local debugging.
- Reuse of existing experience.
- Lower operating cost.
- Logical boundaries without distributed-system complexity.

### Negative

- Backtesting may compete with API resources.
- The monolith may grow poorly if module boundaries are ignored.
- React and .NET require maintained frontend/backend contracts.
- Python will require additional integration if introduced later.
- Large historical datasets may require optimization or different storage.
- Intensive workers may eventually need independent separation or scaling.
- Frontend and backend use different toolchains.

## Deferred decisions

- Exact framework versions.
- Exact .NET runtime.
- Frontend creation tool.
- UI libraries.
- State management.
- Frontend data fetching.
- Concrete testing frameworks.
- Market data provider.
- Demo broker.
- Scheduling mechanism.
- Messaging.
- Cache.
- Observability.
- Authentication.
- Deployment beyond the initial local-only constraint.
- Containers.
- Large historical data strategy.
- Python usage.
- Machine learning.
- LLM provider.
- Secrets policy and mechanism.
- Physical database design.
- Contract generation between backend and frontend.
- Market data import strategy.
- Parallel backtest execution strategy.

## Rejected approaches

- **Microservices:** independent scaling does not justify current coordination, deployment and observability costs.
- **Event-driven distributed system:** introduces delivery semantics, brokers and distributed failure modes before they are required.
- **Python as the complete core:** optimizes for hypothetical machine learning needs and is less aligned with the current deterministic, strongly typed core and existing .NET experience.
- **LLM-driven execution:** violates deterministic evaluation, auditability and safety boundaries.
- **Separate database per module:** adds cross-module consistency and operational complexity without isolation requirements.
- **Cloud-first architecture:** adds cost and deployment concerns while the application is personal and local-first.
- **Multiple repositories from the start:** complicates coordinated changes and local development for one maintainer.

Some approaches may be reconsidered if measurable technical or organizational conditions change.

## Review conditions

Review this ADR when:

- Backtesting cannot achieve acceptable performance.
- Multiple users are introduced.
- High availability becomes necessary.
- Workers need independent scaling.
- Intensive machine learning is introduced.
- Market data volume exceeds the initial design.
- An external integration requires operational isolation.
- Operational failures justify process separation.
- Any operation outside demo is considered.
- Module complexity cannot be maintained inside the monolith.
