# Architecture

## Current status

- [ADR 0001](../decisions/0001-select-application-stack.md) accepted the initial application stack.
- [ADR 0002](../decisions/0002-select-bootstrap-toolchain.md) accepted the bootstrap toolchain and physical solution structure.
- [ADR 0003](../decisions/0003-define-canonical-replay-prerequisite-gating.md) places explicit, strategy-owned prerequisite gating in canonical replay orchestration while preserving stateless evaluators and raw observations.
- [ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md) preserves candle-only replay while allowing optional, provider-neutral chronological market-price observations and explicit resolution insufficiency in the same canonical pipeline.
- [ADR 0005](../decisions/0005-transport-strategy-owned-evidence-to-replay-lifecycle-policies.md) defines immutable, replay-local and instance-bound transport of typed strategy-owned evidence from canonical replay orchestration to lifecycle policies without coupling generic infrastructure to Nasdaq.
- [ADR 0006](../decisions/0006-define-nasdaq-preparation-replay-input-contract.md) specifies the future source-backed, immutable preparation-completion input for `NQ-TIME-003` through the bounded canonical replay context.
- [ADR 0011](../decisions/0011-define-nasdaq-h4-reconstruction-replay-lifecycle.md) defines the pre-evaluation, replay-local Nasdaq H4 structural snapshot boundary needed to preserve post-invalidation episodes across frames without evaluator state.
- [ADR 0012](../decisions/0012-define-nasdaq-h4-reconstruction-snapshot-variants.md) closes the typed Nasdaq H4 snapshot variant set, including invalidation awaiting origin and terminal completed reconstruction evidence.
- [ADR 0013](../decisions/0013-define-nasdaq-post-invalidation-candidate-transition-contract.md) refines the Candidate cursor and completion/breakout payloads needed for its closed-candle transition matrix; the eight snapshot variants remain unchanged.
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
