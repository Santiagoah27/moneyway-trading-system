# Strategy Lab V3: multi-episode blind retrospective review

Source: explicit user task dated 2026-11-19, external attachment
`b58b1a15-96fe-4595-882a-a93efd3119df`. Implements the isolated research boundary of
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md).
The [V1 hypotheses](m5-swing-comparison-v1.md) and
[V2 viewer](m5-swing-viewer-v2.md) remain available, without algorithm tuning.

## Dataset and selection, before detector execution

Dataset `m5-june-documentary-blind-v1`, version 1. The full reproducible
[manifest](../data/strategy-lab/m5-blind-v1/dataset.json) records source identity,
each input/report digest, original native opens, nominal UTC mapping, explicit
direction/anchor, fixed cutoff, partition and label availability. All labels are
initially `Unlabelled`; no human judgment is shipped as a market label.

Actual registered source files were present and read without modification:

| Source | SHA256 |
| --- | --- |
| `C:/MoneyWay/session/raw/2026-06-29/US100_M5_202606220100_202606302355.csv` | `4f468275b5644d4298ba0c085802f04574c765f7dfa662df1ee99456eb9fcf46` |
| `C:/MoneyWay/session/normalized/2026-06-29/5M.csv` | `0133f473592a004535bac89437c96b1c369a753205792459c4e4e12c1835c21a` |

Provider `oanda-global-markets-mt5-demo`, original instrument `US100`, M5. The
existing June documentary mapping is native server minus three hours, nominal
close = open + five minutes. It authenticates neither UTC publication nor observed
availability. Original OHLC/tick volume and every selected native timestamp were
matched exactly between raw and normalized sources; no tolerance or gap filling.
No substitution of TradingView/OANDA prices or USTEC occurred.

Fixed calendar plan chosen before the new detector runs: June 23/25/30, each with
13:00–14:00Z ascending and 14:00–15:00Z descending (nominal documentary UTC). Each
contains 12 contiguous source candles. The first candle's High (ascending) / Low
(descending) is the explicitly supplied research anchor. Direction alternation
and first-candle initialization are convenience assumptions, not validated impulse
geometry, autonomous strategy initialization or mentor labels. Declared anchor
availability at nominal close is a research calculation assumption only.

| Group | Episodes | Unique authentic candles | Original native opens |
| --- | ---: | ---: | --- |
| Development: June 23, two one-hour windows | 2 | 24 | 16:00–17:55 server |
| Validation: June 25, two one-hour windows | 2 | 24 | 16:00–17:55 server |
| HeldOut: June 30, two one-hour windows | 2 | 24 | 16:00–17:55 server |
| PreviouslyExposed: V2 June 29, 13:00–15:00Z | 1 | 24 | 16:00–17:55 server |
| SoftwareAcceptance: original synthetic fixture | 1 | 0 | synthetic |
| Total | 8 | 96 | four source dates |

Six newly curated episodes plus the two previously shown fixtures. Seven authentic
documentary episodes, one synthetic. Normal review offers four new authentic
episodes, the known June 29 episode and the software fixture. Final held-out inputs
remain unavailable there. Primary chronological groups have one ascending and one
descending research episode each; the known V2 episode is ascending.

Underlying source observations do not overlap, including touching window boundaries
(half-open ranges). Development precedes validation, which precedes held-out by
source date; previously exposed June 29 is a separate group, not validation.
The manifest generator and tests reject overlaps and inconsistent identity/anchors.
These are only three chronological groups in one provider/instrument/week, with
short bounded contexts. The split is explicitly
`InsufficientForIndependentGeneralizationClaims`; partition names alone do not
establish independent out-of-sample validity or market-regime robustness.

No selection depended on algorithm agreement, later price, profitability or a
desired result. Ordinary report resources were generated only after the fixed
plan. This document deliberately does not disclose their per-episode detector
outcomes before review. Shallow, ambiguous and other subjective structure
categories remain unlabelled; no coverage claim is made for all requested
categories. No held-out detector result was run.

## Blind interaction and trust model

Route `#strategy-lab-blind`, linked from Strategy Lab V2. It reuses the SVG chart,
source candle contracts and C# Worker reports. No new API, database, engine, broker
connection, trading form or dependency is introduced.

The ordinary chart payload contains only context/explicit initialization and
cutoff-bounded OHLC. It contains no algorithm result, suggested reference, future
rows, price outcome or full-future statistics. Chart scales use only the selected
episode's visible candles. Clicking/Enter selects an exact source Low ascending /
High descending; timestamps, prices, identities and provenance are derived.
Additional equal-price candles may be selected explicitly without an automatic
preferred tie. A confirmation candle can be marked independently by another chart
click. It must close after the selected formation candles; this is a declared
research annotation validation rule, not a trading rule. Missing confirmation stays
null; the algorithm's confirmation is never copied into a human label.

Compact flow: Select → Confirm → Reveal → Next. Reference, NoReference, Ambiguous
and Skip are separate choices. A skipped episode can be revisited and revised before
revelation. One local reviewer name is reused, without authentication. Known V2 /
synthetic fixtures are always `PreviouslyExposed`. Other prior familiarity must be
declared with the checkbox; self-declared independence is not independently verified.

Algorithm reports are lazy-loaded only after commitment AND persistent reveal.
They are checked against the manifest SHA256, input identity, exact chart context
and cutoff using the unchanged C# V1 output. Before then, their overlays, cards,
comparisons and aggregate summaries are absent from the review interaction.
Ordinary browser blindness is cooperative: a developer can deliberately inspect
separate development-report assets. It is not a security boundary or a certification
that the reviewer never saw the material.

Held-out INPUTS are stored only under
`docs/data/strategy-lab/m5-blind-v1/held-out/`. No held-out result exists; held-out
OHLC, anchor values and episode IDs are excluded from frontend resources and build
payloads, not merely hidden with CSS. The public manifest exposes only the count
and policy. A trusted repository owner can still open source files; this feature
does not claim access control against that owner. Release needs a separate frozen
evaluation decision. Do not tune repeatedly on the held-out group.

## Persistence, revisions and export

Browser `localStorage`, key `moneyway.research.m5-blind.v1`, bound to dataset ID,
version and content digest, episode/input identity and expected report digest.
Refresh/restart at the same origin restores committed labels and reveal state.
Original revision remains intact. Pre-reveal revisions append; first reveal freezes
the latest pre-reveal revision for metrics. A post-reveal correction appends with
`PostRevealCorrection` and never counts as another independent observation.
Exposure can become PreviouslyExposed, never be downgraded.

Device review/reveal timestamps are documentary device times, separate from the
fixed historical evaluation cutoff and optional human confirmation. No claim of
authenticated capture or historical causal availability is made.

Quota/storage failures prevent a false successful commitment/reveal. Corrupt or
incompatible records fail closed without automatic deletion or migration. A
compare-before-write check rejects a detected stale-tab write. This is best-effort
local conflict detection, not atomic cross-tab locking. Export downloads the
manifest, original labels, all revisions, exposure and reveal state in JSON; a
corrupt raw record can also be downloaded without parsing.

Local storage can be erased/edited, is scoped to browser/profile/origin/port and
is not authenticated, tamper-proof, server-immutable or a backup. Export after each
batch. Import/recovery from an exported file is outside this feature. Keep the
original export independently; no complex user management is introduced.

## Descriptive metrics

Zero real human labels are committed by implementation. Actual accuracy is
unavailable until the user reviews episodes. Synthetic test judgments demonstrate
software behavior only; they are not mentor labels or measured market accuracy.

The summary keeps partition, origin and exposure in separate cohorts. Synthetic
and previously exposed records are excluded by default. Revealing the currently
selected episode gates summary visibility; no summary recommends a pre-commit pick.
Each episode counts once, using the frozen last-pre-reveal revision.

- Reviewed counts every committed episode. Eligible labels are Reference /
  NoReference; ambiguity and skips are counted separately, not negatives.
- Compared eligible count requires an available revealed report; pending reports /
  unrevealed labels are reported separately, not silently treated as detections.
- Detection means experimentally confirmed reference; pending provisional values
  are not detections. Formal abstentions and unconfirmed results are separate.
- Coverage = confirmed detections / compared eligible episodes. Conditional exact
  agreement = same native price AND same full selected candle-identity set / cases
  where both human and algorithm supply a confirmed/comparable reference.
- Price agreement and candle-set agreement have that same comparable-reference
  denominator, independently. Equal-extreme agreement uses only cases where either
  side retained multiple candles, requiring exact price and full-set agreement.
  A partial tie-set match is not silently counted as full identity agreement.
  Equal price with an intersecting partial tie-set is `TieAmbiguity`, counted
  separately rather than inventing a preferred candle or a wrong reference.
- Incorrect references require both selections but a differing price and/or set,
  excluding the explicit partial-tie ambiguity above.
  False detection means confirmed algorithm reference versus human NoReference;
  missed detection means human Reference versus no confirmed algorithm reference.
  Agreement on no reference is also counted. These are agreement-relative names,
  not declarations that a human judgment is ground truth.
- Signed price difference = algorithm − human in original native price units;
  absolute difference is separate. Base-10 integer arithmetic avoids binary
  floating-point subtraction noise; no tolerance is invented.
- Both confirmation timestamps are preserved; latency = algorithm confirmation
  minus independently selected human confirmation only when both exist. Otherwise
  unavailable. Formation time is never substituted for human confirmation.
- A zero denominator yields unavailable, not 0%. No P&L, ranking or winner.

V2's synthetic confirmation candle Low 80 extends beyond reference Lows 90/93.
This is recorded as an experimental limitation for a future independent hypothesis.
V1 deliberately excludes the continuation candle; this feature does not fix/tune it.
ACTIVE, SC candidate, Standard/WF2, temporal admission and trading-window behavior
remain unchanged. Every research report/label retains no-admission/no-signal scope.

## Startup, generation and manual acceptance

From repository root, no API/MT5/Docker/PostgreSQL needed:

```powershell
Set-Location src/frontend/moneyway-web
npm ci # only if dependencies are absent
npm run dev -- --host 127.0.0.1 --port 5178 --strictPort
```

Open `http://127.0.0.1:5178/#strategy-lab-blind`. Use the same origin/port for saved
research. The old `#strategy-lab` remains explicit non-blind exploration.

To reproduce curation from the authorized local registered sources, from root:

```powershell
dotnet build MoneyWay.sln
$newDataset=Join-Path $env:TEMP ('moneyway-blind-'+[guid]::NewGuid().ToString('N'))
python tools/research/build_m5_blind_dataset.py --session-root C:\MoneyWay\session --output $newDataset
```

The generator requires the exact registered raw/normalized hashes, fixed complete
windows and a new output directory. It invokes the Worker only for ordinary inputs;
held-out inputs have no report digest/result. All outputs are reproducible without
wall-clock fields or random sampling. Random temporary names do not enter content.

Manual acceptance:

1. Open the blind route. Enter your reviewer name once. Development offers two
   episodes; no algorithm overlay, result or summary appears. Verify the explicit
   research anchor, cutoff and documentary warning.
2. Inspect/click a candle (or focus then Enter). The source Low/High and timestamp
   appear automatically. Optionally select equal extremes or independently mark a
   later confirmation candle. Try the NoReference/Ambiguous/Skip alternatives.
3. Confirm. Refresh: original label and revision must survive, without automatic
   reveal. Click Reveal; only then inspect the four C# results and comparisons.
4. Register a correction after reveal. Verify original r1 is retained and the
   comparison remains attached to the frozen pre-reveal revision. Advance to the
   next episode without retyping source or reviewer metadata.
5. Review validation only under the unchanged frozen V1 policy. Known V2 material
   must say PreviouslyExposed. The synthetic partition must remain outside the
   default authentic summary. No held-out selector or OHLC is present.
6. Export at batch end; inspect original/revisions, dataset/input/report identity,
   exposure and reveal state. Confirm exact/price/candle agreement and coverage
   denominators are separate, and missing human-confirmation latency is unavailable.

Browser automation previously failed policy review in V2. Per the current request,
the prohibited headless launch was not retried. V2's user-reported eight-check manual
acceptance is recorded in its document; V3 browser visual acceptance remains manual.
Component tests do not constitute automated browser acceptance.

## Executed validation

Final relevant scopes on 2026-11-19:

| Scope | Passed | Failed / skipped |
| --- | ---: | ---: |
| Application: Nasdaq, StrategyReplay and reused V1 structural/body primitives | 2222 | 0 / 0 |
| Integration: Nasdaq and M5 lab/viewer/dataset classes, excluding environment-gated local sessions | 353 | 0 / 0 |
| Explicitly enabled authentic June / Replay #2 and prospective-correction regressions | 6 | 0 / 0 |
| Backtesting: Nasdaq, Replay and MultiTimeframe | 35 | 0 / 0 |
| Domain: Replay and StrategyVersion | 26 | 0 / 0 |
| Backend total across these scopes | 2642 | 0 / 0 |
| Complete frontend, 11 files | 128 | 0 / 0 |

Eight new backend cases verify manifest counts/identities/anchors, source continuity,
chronological separation, absence of overlapping observations, held-out exclusion,
and byte-exact regeneration of all six ordinary C# report resources. No held-out
detector is invoked by these tests. Thirty-seven new frontend cases cover dataset
failure paths, cutoff/scale restrictions, source High/Low selection, equal extremes,
all label kinds, missing/independent confirmation, persistence/reload, unavailable
reports, corrupt/quota/stale-tab storage, revision freeze, exposure, metric
denominators/cohorts, decimal differences, reveal gating and export interaction.
All inserted judgments are explicitly synthetic test input, not real human labels.

The dataset generator was executed twice in new temporary directories and all
three manifests/projections plus eight inputs and six reports matched byte for byte.
The production build was inspected for held-out episode IDs and timestamp payloads:
none are present. Complete .NET/frontend builds, ESLint, frontend Prettier,
scoped C# whitespace checks, documentation links and `git diff --check` passed.
.NET build reported zero warnings/errors. Existing June/Replay #2 report hashes
and the documentary package hash remained unchanged. V1 algorithms, strategy
definitions and evaluator source files have no diff.

Reproduce focused tests from repository root:

```powershell
dotnet test tests/integration/MoneyWay.IntegrationTests --filter 'FullyQualifiedName~M5BlindDatasetTests|FullyQualifiedName~M5SwingViewerReportTests|FullyQualifiedName~M5SwingLabCommandTests'
```

From `src/frontend/moneyway-web`:

```powershell
npm run test:run
npm run build
npm run lint
npm run format:check
```

To begin the first actual review batch, use Development with a consistent local
reviewer identity; declare any prior exposure, commit before reveal, continue to
the next episode and export the batch. Keep V1 fixed before using Validation.
HeldOut remains reserved for a separate frozen-release decision. No profitability,
candidate activation or completed Nasdaq Demo v0 claim follows from this feature.
