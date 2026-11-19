# Experimental M5 inclusive confirmation extreme

Human-approved MoneyWay research hypothesis under [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md), not a mentor-confirmed strategy rule. This extends the [V1 bounded experiment](m5-swing-comparison-v1.md), without changing its default output or introducing a new research route.

## Calculation contract

`CompareM5SwingHypothesesUseCase.Execute` remains exclusive V1. `ExecuteInclusive` selects the independently identified inclusive variants. `CompareM5SwingPoliciesUseCase` applies both policies to the same immutable input and returns `M5PairedPolicyReport`, schema `m5-paired-policy-1`.

| Hypothesis | Ascending start | Descending start | Exclusive ID / version | Inclusive ID / version |
| --- | --- | --- | --- | --- |
| A | Close < Open | Close > Open | EXP-M5-SWING-A-V1 / 1.0.0 | EXP-M5-SWING-A-V2 / 2.0.0 |
| B | Low < previous Low | High > previous High | EXP-M5-SWING-B-V1 / 1.0.0 | EXP-M5-SWING-B-V2 / 2.0.0 |
| C | A AND B | A AND B | EXP-M5-SWING-C-V1 / 1.0.0 | EXP-M5-SWING-C-V2 / 2.0.0 |
| D | A OR B | A OR B | EXP-M5-SWING-D-V1 / 1.0.0 | EXP-M5-SWING-D-V2 / 2.0.0 |

Both use the supplied fixed anchor High ascending / Low descending, and the first strict-close continuation. Equality or a wick-only crossing does not confirm continuation. After an established pullback, V1 aggregates only through the preceding candle; V2 also aggregates the closed confirmation candle:

- Ascending: `min(existing candidate Low, confirmation Low)`.
- Descending: `max(existing candidate High, confirmation High)`.

Policy IDs are `EXP-M5-BOUNDED-CONTINUATION-V1` and `EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2`. The existing `CandleBodyDirectionCalculator`, `StructuralCandidateExtremeCalculator` and `StructuralBodyCloseBreakCalculator` remain the exact-decimal primitives. Ties retain every actual candle identity. No tolerance, reset, second cycle, automatic anchor or intrabar path is introduced.

Only closed candles at the explicit cutoff enter calculation. A confirmed reference becomes known at the continuation candle's **close**, even when its price comes from an earlier candle. A candle that first satisfies start and continuation without an established pullback still abstains; inclusion cannot resolve its unknown intrabar order. Missing, mismatched, unavailable or late initialization, wrong timeframe/instrument and missing continuation retain the V1 safety behavior. The strict Worker input accepts native instrument prices only; an unsupported price-unit field is rejected.

The paired output preserves both complete result sets, start/reference/confirmation candle identities, availability, reason and policy. Confirmed pairs expose signed `V2 minus V1`, absolute difference, price change and reference change (price or identities, including new ties). Comparisons without two confirmed references use null differences rather than an invented zero.

## Actual bounded comparisons

All times below are UTC candle opens unless explicitly called close/known time. All four hypotheses have the same real Development result in each row; synthetic C differs under V1.

| Input | V1 A / B / C / D | V2 A / B / C / D | V1 reference opens | V2 reference opens | Confirmation open / known at close | V2 minus V1 |
| --- | --- | --- | --- | --- | --- | --- |
| Synthetic, 2026-11-19 | 90 / 90 / 93 / 90 | 80 / 80 / 80 / 80 | A/B/D 13:10; C 13:20 | 13:25 | 13:25 / 13:30 | -10 / -10 / -13 / -10 |
| Ascending Development, 2026-06-23 | 29441.2 each | 29381.7 each | 13:25 | 13:30 | 13:30 / 13:35 | -59.5 each |
| Descending Development, 2026-06-23 | 29811.6 each | 29811.6 each | 14:20 | 14:20 | 14:35 / 14:40 | 0 each |

Ascending confirmation Low is 29381.7, below the preceding candidate 29441.2. Descending confirmation High is 29762.4, below the existing candidate High 29811.6, so inclusion does not change the reference. Confirmation times do not change between policies.

Reproducible paired artifacts, each with all eight variants:

- [Synthetic Markdown](../data/strategy-lab/m5-inclusive-v2/synthetic/policy-comparison.md) and [JSON](../data/strategy-lab/m5-inclusive-v2/synthetic/policy-comparison.json), from the [original synthetic input](../data/strategy-lab/m5-swing-v1/synthetic-disagreement.json).
- [Ascending Markdown](../data/strategy-lab/m5-inclusive-v2/development-ascending/policy-comparison.md) and [JSON](../data/strategy-lab/m5-inclusive-v2/development-ascending/policy-comparison.json), from the [original ascending input](../data/strategy-lab/m5-blind-v1/inputs/m5-20260623-1300-ascending-v1.json).
- [Descending Markdown](../data/strategy-lab/m5-inclusive-v2/development-descending/policy-comparison.md) and [JSON](../data/strategy-lab/m5-inclusive-v2/development-descending/policy-comparison.json), from the [original descending input](../data/strategy-lab/m5-blind-v1/inputs/m5-20260623-1400-descending-v1.json).

## Original human reference integrity

The optional post-calculation annotation uses the existing [public Development projection](../data/strategy-lab/context-v1/development-review.json), SHA256 `698a327b9cf0f784afd62ad960db880def0485ffb816f4687c7753f12a1f97f1`. Its original export identity remains `8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861`. No private reviewer/device details are copied. Original revision 1, dataset identity, exposure, input hash, V1 report hash, cutoff, native OHLC price and full selected candle set are checked. An unsupported revision, scope, price or binding fails without publishing a report.

The original V1 JSON reports reproduce byte-for-byte, including ascending SHA256 `ce6c5daceb3ccd2eee01c1e26478144c4982595505da1efe8305746a7ee20615` and descending `e58763062315d53553d9c21c780193f09ed621a870ac4b37612fbc33990e9277`. New JSON includes input, original report and evidence byte identities. These are integrity bindings, not cryptographic authentication of acquisition or historical availability.

Each V1 hypothesis retains its original **1/2 exact agreements**; each V2 hypothesis has **2/2** on the same two already exposed Development labels. Exact agreement requires price and the full reference candle set. Synthetic data is excluded from these counts. Labels and old scores are unchanged. Independent human confirmation and relative human latency remain unavailable/null. No relabeling, independent validation, general accuracy, profitability or winner is inferred.

June 25 Validation and June 30 HeldOut are unchanged and were not evaluated with V2. These three new artifact directories contain only synthetic and June 23 Development runs.

## Reproduce locally

From repository root after `dotnet build MoneyWay.sln`:

```powershell
$inputFile=(Resolve-Path 'docs/data/strategy-lab/m5-blind-v1/inputs/m5-20260623-1300-ascending-v1.json').Path
$evidence=(Resolve-Path 'docs/data/strategy-lab/context-v1/development-review.json').Path
$newReport=Join-Path $env:TEMP ('moneyway-inclusive-'+[guid]::NewGuid().ToString('N'))
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Worker -- m5-swing-lab --input $inputFile --output $newReport --compare-inclusive --human-evidence $evidence
```

For descending, substitute the other linked Development input. For synthetic, substitute its input and omit `--human-evidence $evidence`. Each publishes `policy-comparison.json` and `policy-comparison.md` atomically in a new directory; existing directories cannot be overwritten. Omitting `--compare-inclusive` retains the original four-V1 command and `comparison.json` / `.md` bytes. The optional human label enters only the output annotation, never either algorithm.

## Boundaries and validation

Both deterministic calculations are stateless, without filesystem, network, database, clock or randomness. Tests check the tick before closure, exact equality/wick rejection, mirrored directions, decimal precision, lower/higher/equal confirmation extremes, multiple ties, initialization failures, same-candle ambiguity, repeat determinism and every earlier prefix after appending future candles. Worker integration tests reproduce all six new artifacts and original V1 outputs, preserve label/source hashes, and reject invalid annotation bindings. Existing Standard/WF2, canonical replay and authentic June/Replay #2 regressions are included in validation.

`CanonicalAdmissionPermitted=false` and `OperationalSignal=false` throughout. No replay fact, trusted observation, operational evaluator, order or eligibility is created. ACTIVE definitions, inactive `nasdaq-0.2.0-sc-candidate`, Standard/WF2 and temporal admission remain unchanged. Historical documentary timestamps do not establish authenticated causal admission.

This only changes extreme aggregation in a fixed-anchor bounded experiment. Generic candidate identification, contextual Nasdaq relevance and valid Structural Change are separate problems. Asia/London definitions, H4/H1 relevance, liquidity selection and the existing 11:00 versus 11:30 America/Bogota conflict are not resolved. Nasdaq Demo v0 is not complete.

The next smallest contextual algorithm experiment would test one explicitly specified, versioned relationship between this candidate and a caller-supplied H1/H4 or liquidity reference. The relationship and its pre-event availability must be defined before implementation; this result does not authorize automatic contextual reference selection or new session boundaries.

## Executed validation, 2026-11-19

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application: Nasdaq, StrategyReplay and structural/body primitives | 2249 | 0 / 0 |
| Integration: Nasdaq and existing M5 command/viewer/dataset | 368 | 0 / 0 |
| Explicit authentic June / Replay #2 and prospective-correction regressions | 6 | 0 / 0 |
| Backtesting: Nasdaq, Replay and MultiTimeframe | 35 | 0 / 0 |
| Domain: Replay and StrategyVersion | 26 | 0 / 0 |
| Total across these scopes | 2684 | 0 / 0 |

This includes 27 new deterministic unit cases and 11 new Worker integration cases.
The focused old/new detector run passed 49 cases; the focused old/new command and
viewer run passed 23. Those overlap the total above and are not counted twice.
The six opt-in authentic regressions used the existing local
`MONEYWAY_MENTOR_INPUT_SOURCE=C:\MoneyWay\session\ValidatedInput.cs`.
Original external June/Replay #2 report, registered June package, public label
projection and private original export hashes were checked and remained unchanged.

Full .NET build passed with zero warnings/errors. Scoped C# whitespace verification,
relative documentation links and `git diff --check` passed. Frontend source and
assets are unchanged; frontend tests/build and browser acceptance were not run in
this feature. The previous V4 visual acceptance remains the separately attributed
human report in the [V4 document](nasdaq-contextual-session-v4.md).

Focused reproducible tests from repository root:

```powershell
dotnet test tests/unit/MoneyWay.Application.UnitTests --filter 'FullyQualifiedName~M5InclusivePolicyTests|FullyQualifiedName~M5SwingExperimentTests'
dotnet test tests/integration/MoneyWay.IntegrationTests --filter 'FullyQualifiedName~NasdaqM5InclusivePolicyCommandTests|FullyQualifiedName~M5SwingLabCommandTests|FullyQualifiedName~M5SwingViewerReportTests'
```
