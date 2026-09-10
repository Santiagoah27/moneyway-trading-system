# Architecture

## Current status

- [ADR 0001](../decisions/0001-select-application-stack.md) accepted the initial application stack.
- [ADR 0002](../decisions/0002-select-bootstrap-toolchain.md) accepted the bootstrap toolchain and physical solution structure.
- [ADR 0003](../decisions/0003-define-canonical-replay-prerequisite-gating.md) places explicit, strategy-owned prerequisite gating in canonical replay orchestration while preserving stateless evaluators and raw observations.
- [ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md) preserves candle-only replay while allowing optional, provider-neutral chronological market-price observations and explicit resolution insufficiency in the same canonical pipeline.
- The technical bootstrap is now authorized.
- Detailed domain architecture and trading implementation remain pending.

## Topics to define

- System boundaries.
- Components.
- Data flow.
- Persistence.
- Market data.
- Backtesting.
- Demo execution.
- Learning and evaluation.
- API.
- User interface.

La arquitectura no debe decidirse implícitamente durante una implementación. Toda decisión material deberá documentarse y aprobarse antes de aplicarla.
