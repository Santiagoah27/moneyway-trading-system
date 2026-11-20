# Descriptive London–Asia H1 interaction

Source: direct user task `67059360-6f02-4d85-ba08-62b73a7a7d3b`, 2026-11-19,
under [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md).
`EXP-NQ-LONDON-ASIA-INTERACTION-V1`, version `1.0.0`, is isolated descriptive
research. Its OHLC predicates are specified by the user. No mentor-confirmed
mapping to direction, liquidity relevance, a valid take, H4 context or entry is
established. Neither published candidate is changed or activated.

## Deterministic contract

`AnalyzeLondonAsiaInteractionUseCase` consumes homogeneous exact H1 candles,
explicit Bogotá operating date, UTC cutoff, research data-origin declaration and
source provenance. It reuses `NasdaqSessionLiquidityCalculator.CalculateExplicitSessions`
and the immutable candle/series contracts. Session date ownership, timezone,
complete coverage and tied extrema identities follow [ADR 0028](../decisions/0028-define-candidate-nasdaq-session-windows.md):
Asia [D-1 17:00,D 02:00), London [D 02:00,D 08:00), America/Bogota.
There is no new session calculator, candle interpolation or timeframe fallback.

Only complete observable Asia references permit interaction analysis. Every
eligible closed London H1 supplies independent exact-decimal observations:

| Predicate | Asia High reference AH | Asia Low reference AL |
| --- | --- | --- |
| Exact extreme contact | High == AH | Low == AL |
| Strict penetration | High > AH | Low < AL |
| Close beyond | Close > AH | Close < AL |
| Penetration without close beyond | High > AH AND Close <= AH | Low < AL AND Close >= AL |
| Opens beyond reference | Open > AH | Open < AL |
| Descriptive magnitude | max(0, High - AH) | max(0, AL - Low) |

No tolerance, minimum distance, color, ATR, body-size or volume filter is added.
An extreme strictly beyond a level is not an exact-extreme contact. A candle may
open and remain wholly beyond a reference: its range/close predicates remain
true without proving any crossing of that reference during that candle. Repeated
rows beyond AL are not automatically repeated liquidity takes or rejections.

Only a completed, continuously covered London window produces one of
`NoAsiaExtremePenetrated`, `AsiaHighOnlyPenetrated`, `AsiaLowOnlyPenetrated` or
`BothAsiaExtremesPenetrated`. Final upper/lower magnitudes equal
max(0,LH-AH) / max(0,AL-LL). Exact contacts remain separate, including the case of
contact without penetration. No-penetration is not a trading setup verdict.

Before completion, observed rows/magnitudes may be reported as `Partial` or
`PartialWithMissingData`; completed classification and completed magnitudes stay
null. At completion, a source hole or invalid interval yields `LondonIncomplete`.
Unavailable/incomplete Asia yields `AsiaUnavailable`, no interaction rows and no
classification. Missing data never becomes proof of no penetration.

## Provenance, availability and ordering

The immutable result carries operating date, source provider/instrument/H1,
source provenance and origin, complete Asia reference with all source/tied candle
identities, London coverage, observed candle OHLC/open/close, exact predicates,
magnitudes and first observed contacts/penetrations/closing-beyond rows separately.
It implements no replay-input, rule-fact or evaluator interface.

First means first chronological **observable supplied** H1, not an event in a
missing interval. Separate candle intervals permit candle-level ordering. Both
first penetrations in the same H1 yield `BothInSameCandle_OrderUnknown`, regardless
of body color. No M5/M1 refinement, exact touch time, intrabar trajectory or rejection
mechanism is inferred.

`ObservableAtUtc` is the explicit evaluated prefix, not an invented earliest
provider publication. A row cannot appear before its candle closes and before
complete Asia is observable. Each generated prefix is evaluated independently;
later exports/reviews do not authorize an earlier event. Original nominal UTC
mapping remains documentary. `CanonicalAdmissionPermitted=false` and
`OperationalSignal=false` accompany the result and report. Authenticated admission
remains blocked. No trusted strategy fact, rule ID, approval or order is created.

## Actual June 23 evidence

The original [H1 contextual source](../../src/frontend/moneyway-web/src/research/context/data/june23-context.json)
is unchanged, SHA256
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`.
The [generated interaction report](../../src/frontend/moneyway-web/src/research/context/data/june23-london-asia-interaction.json)
binds that exact input hash, H1 raw/normalized source provenance and original
documentary mapping. All prices and identities below are calculated from that
source, not algorithm constants or new human labels.

Asia is complete at 07:00Z: AH **30463.9**, AL **29715.2**, 9/9 H1. London at
13:00Z is complete, 6/6 H1: LH **29818.6**, LL **29472.7**.
Classification: **AsiaLowOnlyPenetrated**, upper magnitude **0**, lower **242.5**.
Neither Asia boundary has exact-extreme equality among these London rows; high
penetration and upper closing-beyond are unobserved.

The same first source H1 supplies the first lower penetration and first lower
closing-beyond observation:

| Open UTC | Close UTC | Open | High | Low | Close | Reference AL | Lower magnitude |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 2026-06-23 07:00 | 2026-06-23 08:00 | 29734.0 | 29818.6 | 29653.8 | 29672.6 | 29715.2 | 61.4 |

This is 02:00–03:00 Bogotá, observable in the documentary evaluated prefix at
08:00Z, not before. It does not prove the exact intrabar penetration time
or an operational SELL. At 07:00Z London has zero closed rows and no final
classification. At 08:00Z the first row is partial research. At 12:59Z five rows
are observable, with maximum observed lower magnitude 198.0; the final 242.5 is
not available. At 13:00Z all six rows support the completed descriptive result.

## Reproduce and inspect

After `dotnet build MoneyWay.sln`, from repository root:

```powershell
$source=(Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination=Join-Path $env:TEMP ('moneyway-london-asia-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --analyze-interaction
```

The existing Worker publishes `london-asia-interaction.json` in a new directory.
The original five-argument session command and published session report reproduce
unchanged. No service or new research route is introduced.

The existing `#strategy-lab-context` view has a compact read-only interaction
summary, separate first observations and expandable OHLC predicates. It consumes
C# outputs and the shared cutoff; TypeScript calculates no price predicates,
extrema, magnitudes, classifications or strategy interpretation. Moving backward
removes later events and final classifications. Source/version/cutoff mismatches
fail closed. Original M5 labels remain gated at their own later cutoffs.

For manual acceptance, run `npm run dev` in `src/frontend/moneyway-web` and open
the existing route on the actual Vite port. Check cutoffs Bogotá 02:00 (no closed
London rows), 03:00 (first partial lower observation, 61.4), 07:59 (partial, 198.0)
and 08:00 (complete, 242.5). Move backward and verify later values disappear.
The previous session-panel visual acceptance is separately recorded in the
[session specification](../strategies/nasdaq/explicit-session-candidate.md).
Browser automation previously blocked by policy is not retried here.

The smallest next experimental decision is an explicitly versioned, source-bound
mapping from this descriptive context to a **candidate set** of relevant liquidity
and M5 references: define which observed reference/interval is eligible, its causal
availability and replacement/invalidation rules. No direction, relevance ranking,
swing-selection heuristic or valid-take authorization follows from this category.

## Executed validation, 2026-11-19

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application Nasdaq/replay/structural/body scopes | 2297 | 0 / 0 |
| Integration Nasdaq and M5 command/viewer/dataset scopes | 384 | 0 / 0 |
| Explicit authentic June/Replay #2 and prospective-correction regressions | 6 | 0 / 0 |
| Backtesting Nasdaq/Replay/MultiTimeframe | 35 | 0 / 0 |
| Domain Replay/StrategyVersion | 26 | 0 / 0 |
| Relevant backend total | 2748 | 0 / 0 |
| Complete frontend, 14 files, maxWorkers=2 | 183 | 0 / 0 |

New coverage: 25 deterministic unit cases, four Worker integration cases and 17
frontend cases. Focused runs overlap these totals. Tests cover exact equality,
both sides, wick-only/close/equality/open-beyond predicates, same-bar unknown
ordering, independent first events, all Asia ties, missing/partial sessions,
boundaries, decimal precision, source identity and first-event provenance,
immutable prefixes, repeated results and isolated interfaces/no-admission flags.
Worker tests reproduce the new report and preserve original session report bytes.

The first concurrent frontend run had five 5-second timeouts (176 passed/5 failed),
including unchanged supervised-capture and blind-review tests. The final complete
run was separated from backend tests with `--maxWorkers=2`; all 183 passed using
unchanged timeout limits and assertions. No unrelated tests/configuration were
weakened. Backend scopes all passed. The six authentic regressions used the
existing local `MONEYWAY_MENTOR_INPUT_SOURCE` and did not readmit documentary data.

Full .NET build passed with zero warnings/errors. Frontend build, ESLint, Prettier,
scoped C# whitespace verification, relative documentation links and
`git diff --check` passed. Original context/labels, published strategy definitions,
canonical evaluators, swing algorithms and data partitions remain unchanged.
Original external June/Replay #2 report hashes were independently checked and
preserved. The 11:00/11:30 runtime discrepancy remains unresolved and unchanged.
No new browser visual acceptance is claimed; blocked browser automation was not
attempted. Nasdaq Demo v0 is not complete.

An owned temporary Vite process on port 5182 served page, summary module, original
context and generated interaction report with HTTP 200. The served source hash
matched the report binding and the returned 13:00Z frame was Complete /
AsiaLowOnlyPenetrated / upper 0 / lower 242.5. The process was stopped afterward;
HTTP availability is not browser visual acceptance.

Reproduce from repository root:

```powershell
dotnet test tests/unit/MoneyWay.Application.UnitTests --filter FullyQualifiedName~LondonAsiaInteractionTests
dotnet test tests/integration/MoneyWay.IntegrationTests --filter FullyQualifiedName~NasdaqSessionLiquidityResearchCommandTests
```

From the frontend directory:

```powershell
npm run test:run -- --maxWorkers=2
npm run build
npm run lint
npm run format:check
```
