# ADR 0027: Permit isolated experimental strategy hypotheses

## Status

Accepted by explicit user instruction, 2026-11-19. This accepts a research
methodology, not any experimental trading rule or promotion.

## Decision

The user-approved Strategy Lab task, external attachment
`6f0b2bd0-9e8e-41e7-bd93-4aeb9d64db1e`, authorizes MoneyWay-developed hypotheses
when visual mentor concepts lack sufficient deterministic specification.
Hypotheses require explicit identifiers, versions, assumptions, reproducible
inputs/cutoffs, discrepancy review and independent validation. They must not be
presented as mentor-confirmed rules. Directional mirroring in the M5 experiment
is an experimental symmetry assumption, not independently verified BUY evidence.

Research calculations belong in an isolated Application research namespace and
reuse existing market-data primitives. A one-shot command in the existing Worker
may read bounded inputs and publish reports. No separate engine, application,
database or service is introduced. Calculations may not access wall clock, network,
filesystem, database, Random or hidden mutable state.

Outputs implement neither replay input nor rule-fact/evaluator interfaces. They
are not registered with canonical strategy catalogs, fed into the published M5
candidate or accepted as operational signals. ACTIVE behavior and
`nasdaq-0.2.0-sc-candidate` remain immutable. Experimental classification and
explicit no-admission/no-signal limitations accompany every result.

Retrospective documentary data may support separate research with explicit
unauthenticated historical availability. Neither a candle close nor a report/hash
authenticates publication. Existing MT5 temporal admission and June 29's
documentary-only restriction remain unchanged.

## Promotion requirements

Before a research hypothesis can become an approved strategy candidate, require:

- Independently reviewed reference labels and retained source provenance.
- Explicit versioned assumptions and parameter definitions.
- Chronological development/validation separation and an untouched final dataset.
- Agreement, false positives, missed detections, coverage and latency analysis.
- Robustness across market regimes and disclosure of inconclusive/negative cases.
- Authentically admitted independent backtesting where required.
- Explicit human approval and the approved candidate-version lifecycle before
  separate activation.

No numeric acceptance threshold is invented here. Do not repeatedly optimize
against the final evaluation dataset. Agreement with visual labels does not prove
economic profitability. A hypothesis can be rejected, revised or inconclusive.

See [the bounded M5 comparison](../research/m5-swing-comparison-v1.md) for the first
four hypotheses and their additional shared experimental assumptions.
