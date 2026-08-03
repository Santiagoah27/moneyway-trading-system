# ADR 0002: Select bootstrap toolchain and physical solution structure

## Status

Accepted.

The decision was reviewed and approved as the technical bootstrap baseline. Exact patch versions will be selected and recorded during bootstrap, while deferred decisions remain outside its scope.

## Date

2026-08-03.

## Context

[ADR 0001](0001-select-application-stack.md) accepted a local-first modular monolith using ASP.NET Core, React with TypeScript, PostgreSQL, Entity Framework Core, REST and a background worker. The logical boundaries now need a reproducible technical bootstrap and a minimal physical structure without implementing trading behavior.

The repository needs supported version families, deterministic tests, strong typing, controlled dependency updates and minimal differences between developer environments. Primary development occurs on Windows with VS Code, so the toolchain must be easy to run and debug locally. Tooling without a demonstrated need must remain deferred.

Forex and Nasdaq strategy definitions remain separate, versioned and auditable. Their deterministic rules require unit tests, replay and backtesting must prevent future-data leakage, and subjective or unresolved rules cannot authorize automated execution. The bootstrap must preserve those constraints without implementing strategy rules.

## Environment inspection

Inspection performed on 2026-08-03 using read-only commands.

| Tool | Detected state | Target or prerequisite difference |
|---|---|---|
| Operating system | Windows `10.0.26200`, `win-x64` | Compatible with the proposed local workflow |
| .NET SDK | `10.0.300-preview.0.26177.108` | A supported stable .NET 10 SDK is required before bootstrap; the detected SDK is a preview |
| .NET host/runtime | Host `10.0.5`; ASP.NET Core and .NET runtimes `10.0.5` and `8.0.25` detected | Runtime 10 is present, but it does not replace the stable SDK prerequisite |
| Node.js | `22.19.0` | Node.js 24 LTS is required for the proposed frontend bootstrap |
| npm | `10.9.3` detected through `npm.cmd --version` | `npm --version` through the PowerShell wrapper was blocked by local execution policy; npm itself is present |
| Git | `2.51.0.windows.1` | Present; no target change required by this ADR |
| PostgreSQL CLI (`psql`) | Not found | PostgreSQL 18 tooling/database is a later prerequisite; absence does not block this documentation feature |

No tool was installed or updated. Exact versions used by the future bootstrap must be recorded in repository configuration and lockfiles rather than inferred from this inspection.

## Decision drivers

- Supported versions.
- Reproducible builds.
- Strong typing.
- Developer familiarity.
- Windows compatibility.
- Local debugging.
- Testing.
- Dependency control.
- Low operational complexity.
- Incremental evolution.
- Auditability.

## Considered alternatives

### Backend runtime

| Option | Assessment |
|---|---|
| .NET 10 LTS | Selected family. Aligns the new repository with a supported long-term baseline and avoids an early major migration. |
| .NET 9 STS | Not selected. Its shorter support horizon is a poor baseline for a new long-lived system. |
| .NET 8 LTS | Viable and mature, but starts the repository on an older major and would require an earlier planned upgrade. |

### Node.js

| Option | Assessment |
|---|---|
| Node.js 24 LTS | Selected. Provides an LTS baseline for React and TypeScript tooling. |
| Node.js 26 Current | Not selected. Current releases are not the stability baseline for initial bootstrap. |
| Node.js 22 LTS | Supported but older; the detected local version does not override the selected Node 24 baseline. |

### Frontend build

| Option | Assessment |
|---|---|
| Vite | Selected for a client-side React application with a small configuration surface and fast local feedback. |
| Framework-based full-stack solution | Not selected because SSR and frontend server features are not required. |
| Custom bundler configuration | Not selected because it adds maintenance without a demonstrated need. |

### Package manager

| Option | Assessment |
|---|---|
| npm | Selected. Bundled with Node, familiar, adequate for one frontend application and supported by lockfiles. |
| pnpm | Efficient but adds a separate tool and workflow without a current monorepo scale need. |
| Yarn | Capable but provides no current advantage that justifies another package-manager choice. |

### Physical backend structure

| Option | Assessment |
|---|---|
| One backend project | Too little enforcement of dependency and responsibility boundaries. |
| Layered projects within one modular monolith | Selected. Enforces core boundaries without distributed or per-module project overhead. |
| Project per every business module from day one | Too granular before module behavior and change patterns are known. |

The selected structure is intentionally intermediate: more enforceable separation than a single project and less ceremony than one project per business module.

## Decision

### Backend

- Runtime and SDK family: .NET 10 LTS.
- Target framework: `net10.0`.
- Web framework: ASP.NET Core.
- Language: C# using the version supported by the selected .NET 10 SDK.
- ORM family: Entity Framework Core 10.
- PostgreSQL provider: Npgsql compatible with Entity Framework Core 10.
- Nullable reference types: enabled.
- Implicit usings: enabled.
- Async APIs for I/O-bound operations.
- REST API with OpenAPI metadata.
- Background worker using the .NET Worker Service model.

No exact patch is fixed for .NET, Entity Framework Core or Npgsql. The future implementation must select mutually compatible versions within these families and record exact versions in repository-controlled files.

### Frontend

- Node.js 24 LTS.
- Package manager: npm.
- React 19 with baseline `19.2`.
- TypeScript 6 with strict mode enabled.
- Vite.
- Client-side React application; no server-side rendering.
- ESLint and Prettier.
- Vitest, React Testing Library and Testing Library `user-event`.

Next.js, Remix, Angular, Redux, MobX, Nx, Turborepo, Yarn, pnpm, Bun and server-side rendering are excluded from the initial bootstrap. Reconsideration requires a demonstrated need and, when architectural, a subsequent ADR.

### Database

- PostgreSQL major 18.
- Use a supported current minor release within PostgreSQL 18.
- Entity Framework Core migrations will manage future ORM migrations.
- Persist timestamps in UTC and store original or market timezone separately when relevant.

This decision does not install PostgreSQL, create a database, define connection strings, design tables or create migrations.

## Version policy

### Major versions

Selected major versions change only through a dedicated feature and, when architecture or compatibility is affected, through an ADR.

### Minor and patch versions

- Update through an independent feature.
- Run relevant build, tests and validations.
- Never update silently as part of unrelated work.
- Do not use floating ranges or wildcards.
- Preserve and version lockfiles.

### .NET SDK

The future bootstrap will create `global.json` to record the exact stable .NET 10 SDK used by the repository, prevent automatic movement to a later major and permit compatible patch movement only according to an explicit roll-forward policy. This ADR selects the family, not an exact SDK patch, and does not create `global.json`.

### NuGet

The future bootstrap will use central package management through `Directory.Packages.props`. Shared package versions must be centralized when multiple projects consume them. Exact package versions will be committed without wildcards. This ADR does not create the file.

### Node.js

The future bootstrap will create `.nvmrc` containing the selected Node.js 24 LTS line. `.nvmrc` is chosen as the single marker because it is widely recognized by Node version tooling and editor workflows, including common Windows and VS Code setups. It must prevent Node 26 Current from becoming the implicit bootstrap baseline. This ADR does not create `.nvmrc`.

### npm

- Commit `package-lock.json`.
- Do not install in a way that discards or bypasses the lockfile.
- Prefer `npm ci` for reproducible installs once a lockfile exists.
- Do not use wildcard dependency versions.

### PostgreSQL

Fix major version 18. Update supported minor releases through controlled maintenance tasks. Do not move automatically to PostgreSQL 19 when it becomes stable.

## Physical solution structure

The proposed initial structure is:

```text
moneyway-trading-system/
├── MoneyWay.sln
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── src/
│   ├── backend/
│   │   ├── MoneyWay.Domain/
│   │   ├── MoneyWay.Application/
│   │   ├── MoneyWay.Infrastructure/
│   │   ├── MoneyWay.Api/
│   │   └── MoneyWay.Worker/
│   └── frontend/
│       └── moneyway-web/
├── tests/
│   ├── unit/
│   │   ├── MoneyWay.Domain.UnitTests/
│   │   └── MoneyWay.Application.UnitTests/
│   ├── integration/
│   │   └── MoneyWay.IntegrationTests/
│   └── backtesting/
│       └── MoneyWay.Backtesting.Tests/
└── docs/
```

These names are a physical-structure proposal pending acceptance of this ADR. They remain projects within one modular monolith, not microservices.

### Project responsibilities

- **MoneyWay.Domain:** entities, value objects, applicable domain contracts, rule/evaluation states, shared strategy concepts and deterministic invariants. It has no internal project dependencies.
- **MoneyWay.Application:** use cases, application services, orchestration, ports for external capabilities and necessary internal DTOs. It depends on Domain.
- **MoneyWay.Infrastructure:** future Entity Framework Core and PostgreSQL persistence, market-data adapters, demo broker adapters and external service implementations. It depends on Application and Domain as their contracts require and contains no strategy rules.
- **MoneyWay.Api:** REST endpoints, composition root, dependency injection, OpenAPI exposure and request/response mapping. It contains no trading rules.
- **MoneyWay.Worker:** future scheduled analysis, historical imports, replay, backtesting and shadow-mode jobs. It will not enable demo execution during bootstrap.
- **moneyway-web:** React UI for strategy checklists, analysis, backtesting and trade-journal views. It contains no rule engine or trading rules.
- **MoneyWay.Domain.UnitTests:** deterministic domain rules and invariants.
- **MoneyWay.Application.UnitTests:** use-case orchestration.
- **MoneyWay.IntegrationTests:** persistence, API and adapter integration behavior.
- **MoneyWay.Backtesting.Tests:** replay, chronological processing and future-data leakage prevention.

## Dependency direction

- `MoneyWay.Domain` → no internal project dependencies.
- `MoneyWay.Application` → `MoneyWay.Domain`.
- `MoneyWay.Infrastructure` → `MoneyWay.Application` and `MoneyWay.Domain`.
- `MoneyWay.Api` → `MoneyWay.Application` and `MoneyWay.Infrastructure`.
- `MoneyWay.Worker` → `MoneyWay.Application` and `MoneyWay.Infrastructure`.
- `MoneyWay.Domain.UnitTests` → `MoneyWay.Domain`.
- `MoneyWay.Application.UnitTests` → `MoneyWay.Application` and `MoneyWay.Domain`.
- `MoneyWay.IntegrationTests` → API, Application and Infrastructure as required by the test scope.
- `MoneyWay.Backtesting.Tests` → Domain, Application and required test adapters.

API and Worker are separate composition roots. Domain knows nothing about PostgreSQL, brokers, HTTP or LLMs. Infrastructure cannot introduce strategy rules. Strategy evaluation does not depend on brokers. Demo execution remains disabled during initial bootstrap.

## Testing toolchain

### Backend

- xUnit for unit and integration tests.
- Test projects separated by responsibility.
- Coverlet collector when coverage collection is configured.
- `WebApplicationFactory` for API integration tests when applicable.
- No mocking library selected initially.
- Prefer manual fakes for simple contracts; introduce a mocking library only through a concrete need.

### Frontend

- Vitest as test runner.
- React Testing Library for component behavior.
- Testing Library `user-event` for user interactions.
- jsdom when a browser-like test environment is required.
- Test externally observable user behavior, not implementation details.

### End-to-end

E2E tooling remains deferred. Playwright, Cypress or another framework will be evaluated only when an actual user flow justifies end-to-end tests.

## Quality baseline

### Backend

- Nullable reference types enabled.
- Implicit usings enabled.
- Warnings reviewed and never silently suppressed.
- `TreatWarningsAsErrors` enabled from bootstrap. A new repository has no legacy warning debt, so enforcing the baseline immediately prevents drift; any exception must be narrow, documented and justified.
- Formatting compatible with `dotnet format`.
- Shared build settings may use `Directory.Build.props` during bootstrap.
- Deterministic rule code requires unit tests.
- No trading logic in API controllers.
- No provider-specific logic in Domain.

### Frontend

- TypeScript strict mode enabled.
- ESLint and Prettier enabled.
- No `any` without explicit justification.
- Components tested through externally observable behavior.
- Strongly typed API contracts.
- No trading rules in UI components.

### Repository

- UTF-8.
- Existing `.editorconfig` remains the formatting-default source.
- No generated artifacts committed unless explicitly required.
- Secrets and environment-specific values excluded from Git.

## Bootstrap scope

After this ADR is accepted, the next bootstrap feature may create:

- Solution.
- Projects.
- Project references.
- Shared build files.
- React application.
- Test configuration.
- Minimal health endpoint.
- Minimal frontend shell.
- Build and test commands.

It may not create:

- Domain trading rules.
- Database schema or migrations.
- Broker integrations.
- Market-data integrations.
- Backtesting implementation.
- Demo execution.
- LLM integration.

## Consequences

### Positive

- Supported target families and reproducible version markers.
- Strong typing and strict compiler/tooling baselines.
- Enforceable dependency boundaries within the modular monolith.
- Test responsibilities separated without per-module project proliferation.
- Familiar Windows and VS Code workflow.
- Low initial operational complexity.
- Clear path from documentation to a minimal buildable shell.

### Negative

- The detected local SDK and Node.js versions do not yet meet the proposed stable targets.
- Multiple .NET projects add build and reference-management overhead compared with one project.
- Frontend and backend retain separate toolchains.
- `TreatWarningsAsErrors` may require deliberate handling when external analyzers introduce new warnings.
- PostgreSQL 18 must be provisioned later before persistence work.
- React, TypeScript and Node major upgrades require controlled maintenance.

## Deferred decisions

- Database schema and physical entities.
- Entity Framework Core migrations.
- PostgreSQL installation and connection configuration.
- Authentication and authorization.
- Docker and containers.
- CI/CD.
- Cloud or production deployment.
- Market-data provider and integration.
- Demo broker selection, integration and credentials.
- LLM provider and integration.
- Scheduling framework.
- Messaging.
- Cache.
- Observability platform.
- UI component library.
- State-management library.
- Data-fetching library.
- E2E testing framework.
- Machine learning.
- Python.
- Demo execution.
- Real-money execution.
- Mocking library.

## Review conditions

Review this ADR when:

- A selected version approaches end of support.
- The frontend requires server-side rendering.
- npm no longer meets repository needs.
- The physical structure causes improper references or excessive friction.
- Backtesting requires specialized processes.
- Python becomes necessary for research or machine learning.
- PostgreSQL no longer meets persistence requirements.
- The project requires production deployment.
