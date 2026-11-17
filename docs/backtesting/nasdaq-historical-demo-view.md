# Nasdaq historical report in the supervised local demo

## WF2 historical entry evidence and causal diagnostic (2026-11-17)

Initial clean synchronized checkpoint: `main` at
`c4c220a581e81b73f2fa0863f09afa80edb96cc5`. The functional increment adds
documentary entry assessment to the existing canonical historical report/API/UI.
It introduces no strategy RuleId, evaluator, active metadata, price mapping,
clock admission, broker endpoint, exit or PnL.

### Source and behavior

The existing `NasdaqHistoricalObservedEntry` is an exact Standard-eligibility-bound
execution representation and requires execution at/after that eligibility. It
is preserved. S14 cannot be forced into it. Instead, immutable diagnostic
`NasdaqHistoricalEntryEvidence` records travel as optional bounded metadata in
`NasdaqMentorSessionReplayInput` / its report. The binder never transports them
to strategy evaluators. The writer/reader associate them with frozen completed
canonical frames; they do not rerun a market cursor or strategy.

The causal selector retains original platform/provider/symbol, event kind, human
review ID/validation source, original clock text, explicit UTC mapping provenance,
time precision/bounds, original optional price/units/deal reference, documented
review date and historical availability. Missing values remain null. Known
future evidence is excluded. Unknown historical availability is explicitly
RetrospectiveOnly/NotAdmitted and cannot change causal selection. An approximate
interval is not an exact broker fill; no nearest/later frame is substituted for
an absent original boundary. All required and optional rule states, coverage,
Step/AsOfUtc and verdict from the actual original exact frame remain frozen.

`HistoricalEntry` in the lifecycle view is separate from `Entry` in the existing
coherent trade snapshot. The panel displays original instrument/price/time and
provenance, native-record gaps, original canonical rules, current SL/risk/TP
statuses and explicit mismatch codes. EntryAuthorization always remains
NotGrantedByDiagnostic; classification stays inconclusive. NativeRecordDocumented
describes a complete supplied documentary record, not independent verification:
NativeExecutionIndependentlyVerified stays false. An observed order, position or
execution does not change StrategyVerdict or authorize an order.

The v2 incremental reader retains at most sixteen exact entry-frame projections
for the bounded reviews and checks the persisted assessment against actual
frozen source frames. The bounded canonical-window reader applies the same
diagnostic validation. Neither trusts a serialized Approved/native-verification
claim or later WF2 state. Reports without entry metadata remain compatible.

### Authentic S14 and the original chronology

The reviewed source is [ADR 0026 S14](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md#214-human-confirmed-replay-2-initial-ustec-sl-price-2026-11-05-s14),
the human-reviewed MT5 position row from the original Replay #2 mentor material.
The original external screenshot bytes are unavailable here; no image path or
hash is invented. Source instrument is **USTEC**, SELL. S14 confirms source entry
price **29604.00** and position-opening field **2026.09.08 16:32:00 server**.
Its documented September-8-only mapping gives **13:32Z / 08:32 America/Bogota**
for that field. This is not an independently verified US100 native execution.
Provider/deal identity, monetary units and authentic historical assertion
availability remain unknown. The review is documented on 2026-11-05; no exact
review-creation or historical availability instant is synthesized.

The exporter adds S14 as a clearly retrospective annotation after canonical
evaluation and preserves the adapter/CSV evidence. At 13:32Z current WF2 evidence
is incomplete. WF2-001 is established at 13:34Z and WF2-002 at 13:35Z; at 13:36Z
SL requires human validation, risk/TP wait and verdict is HumanValidationRequired.
The original 13:32 rule snapshot is not replaced by those later passes. Current
M5/M1 belong to the separately classified Standard branch; old timing claims
about Standard M5 passing around 13:35 are not authoritative current WF2 results.
The artifact records the actual statuses instead. The timing discrepancy is
explicit; its cause and strategy-violation classification are not established.

| UTC frame                   | WF2-001                 | WF2-002 | M5/M1 Standard | SL                      | Risk/TP | Verdict                 |
| --------------------------- | ----------------------- | ------- | -------------- | ----------------------- | ------- | ----------------------- |
| 13:32 original S14 field    | HumanValidationRequired | Waiting | NotApplicable  | Waiting                 | Waiting | HumanValidationRequired |
| 13:35 later canonical frame | Passed                  | Passed  | NotApplicable  | Waiting                 | Waiting | Wait                    |
| 13:36 later canonical frame | Passed                  | Passed  | NotApplicable  | HumanValidationRequired | Waiting | HumanValidationRequired |

Primary artifact: `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-dfb31f824fbe4357a0d96d9683a7b3e1.json`.
Independent repeated emission: `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-972cdd8f2c484a8e9a69e89e99b67e4d.json`.
Both are 4,469,993 bytes and have SHA256
`193fd37ac52f433a501f3dca673865bb459d5489e2dcf3e6a64028ec073d7244`.
The full prefix contains 7,596 frames from August 31 22:01Z; an independent
7,597-frame prefix preserves common rule/evidence, canonical diagnostic and
entry-assessment fingerprints. The exported six frames cover 13:31Z-13:36Z.
The extra 13:37 frame tests prefix stability, not an exit/fill interpretation.
The manifest retains base commit `c4c220a581e81b73f2fa0863f09afa80edb96cc5`,
26 actual runtime code hashes and the S14 repository-document version hash.
Adapter, four CSVs and four protected prior reports retain their nine original
hashes. No prior artifact is overwritten and no market acquisition is rerun.

Validation: 2,050 Application Nasdaq/workflow/canonical cases, 131 Nasdaq
integration cases, 19 domain replay cases, one regenerated authentic canonical/
API case and one legacy authentic API case: **2,202 .NET passed, zero failed or
skipped**. Focused 14-unit and 35-integration selections overlap those totals.
All 35 frontend tests passed. Full .NET/frontend builds, ESLint, scoped
formatting, local documentation links and `git diff --check` passed. No manual
browser test or independent original-video/screenshot/deal verification was
performed; source facts are the existing human-reviewed repository evidence.

### Remaining critical path

| Capability                                           | State and exact limit                                                                                                                                                                                        |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Historical human entry observation/causal diagnostic | Implemented, independently of missing SL; includes original source precision, original frame, retrospective/unavailable evidence and native mismatch.                                                        |
| Entry authorization                                  | Not granted by this diagnostic. Canonical original eligibility/readiness remains authoritative; later passes do not backdate an entry.                                                                       |
| Native execution evidence                            | HumanOnly: original matching US100 deal ID, price/unit, event kind, source-qualified time/mapping and authentic availability; S14 USTEC is not that record.                                                  |
| Native initial SL, risk and TP                       | HumanOnly: exact native protective reference/initial stop and times, account basis/currency/loss/calculation evidence, and original selected important Low/TP. Existing consumers are implemented.           |
| WF2 supervised execution snapshot                    | NotImplemented: compose compatible native entry/SL/risk/TP records without deriving fills or authorizing broker orders. Missing native SL prevents a complete authentic snapshot.                            |
| WF2 exit evidence and economic outcome               | NotImplemented/BlockedByUnresolvedSpecification: original stop-management/exit execution, causal ordering, valuation/quantity/costs and outcome policy remain separate; none is inferred from price contact. |
| Session completion and no-reentry                    | BlockedByUnresolvedSpecification: unresolved NQ-RISK-003/NQ-REENTRY-001 are not replaced with a universal one-trade policy. Existing terminal/cutoff guards remain implemented.                              |

Without the missing historical SL, the entry diagnostic, source reconciliation,
API/UI and synthetic contract tests can be completed. A complete original trade
snapshot, compliant execution assessment, exit/outcome and completed supervised
session require their actual source evidence and applicable approved contracts.

**One next smallest functional feature:** WF2 supervised historical snapshot
assembly from compatible documentary native entry and existing SL/risk/TP facts,
with explicit unavailable/incompatible diagnostics and no broker execution.
It must not turn this retrospective USTEC observation into an authorized US100 fill.

**Critical human source question:** is there an original US100 execution/deal
record for this same operation, with deal identity, original price/units,
source-qualified time and evidence of when it became observable? If it only
exists as USTEC testimony, retain that limitation. The entry-versus-S13 chronology
also needs original source evidence before choosing any causal explanation.

## WF2-to-NQ-TP-001 canonical convergence (2026-11-17)

Initial checkpoint: clean synchronized `main` at
`713a88747a6f502fa6ad5bdf747b6eaa3ff406c2`. This increment supersedes the older
Standard-only TP limitation below. No active version, definition, dependency
edge, Required flag or Sequence changes; the definition remains
`nasdaq-0.1.0-draft`.

### Audited contract and functional behavior

NQ-TP-001 establishes a human-selected target: BUY High, SELL Low, native exact
`TakeProfitPrice == TargetReferencePrice`. Existing types are
`NasdaqHumanTakeProfitTarget` / `NasdaqLiquidityTakeReference` with SessionLevel
or Structural provenance. Selection of importance/relevance is human evidence,
not the mere presence of a session endpoint or a general structural detector.
Existing session validity/fallback restrictions are not replaced with a nearest
low, timeframe priority, offset, buffer or reward/risk formula. See
[ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md) and
[ADR 0026](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md).

The existing `MoneyWayNasdaqHumanTakeProfitEvaluator` now routes actual WF2 SELL
SL ancestry to `NasdaqWickfillScenario2TakeProfitObservationSelector`. It consumes
`NasdaqWickfillScenario2TakeProfitObservation` with the original prior canonical
SL owner and the existing immutable target. Unique usable evidence publishes
`NasdaqWickfillScenario2TakeProfitRuleFact`, retaining that original SL object,
exact concrete target reference and all compatible supporting observations.
Standard's observation, selector, fact and evaluation branch remain unchanged.
The unchanged SL-owner check is shared from the SL fact rather than coupling TP
to the risk input. The exact existing 1% comparison is untouched.

The workflow already requires prior eligible NQ-SL-001, not risk or observed
entry; display order does not add dependencies. No same-frame positive SL is
consumed. The mentor binder validates session, actual active Start, exact prior
SL owner and its original fact. Current SL applicability/reversal/protective
sources and target sources must remain compatible and observable. Original
target-selection time follows target-source availability; assertion availability
follows selection and source SL availability. Target selection can precede SL
emission. Binding/evaluation times never replace those original semantic times.

Missing eligible SL stays Waiting. Missing valid original target/important-low
selection after eligible SL requires HumanValidationRequired; conflicting human
targets require review. Relevant unavailable exact sources prevent Passed.
Wrong native provider/symbol (including USTEC), wrong side, foreign runs, future
selection/availability and subsequent revisions cannot authorize an earlier
frame. The existing canonical terminal liquidity gate and TIME cutoff retain
precedence. Passed establishes a selected target, not a target hit or execution.

The lifecycle TP stage now shows the recorded rule status. Optional
`Scenario2TakeProfit` presents the native provider/symbol, SELL, reference family
and description, SHA256 identity of the full concrete reference, exact decimal
price strings, original selection/availability and both level/selection sources.
Full original reference/support remain in canonical evidence; the digest does
not authenticate a source. Native price coordinates do not claim monetary
units or conversion. The reader validates published ownership/status, native
identity, equality and temporal bounds without rerunning a strategy. Old full
reports retain historical NotImplemented TP stages. The UI separates this
standalone fact from TP in a complete Standard-owned snapshot; entry, risk,
exit and economic outcome are independently represented.

### Synthetic verification versus authentic Replay #2

Synthetic canonical binder/report/API tests demonstrate human-selected session
and important structural lows, including full decimal precision, with risk still
HumanValidationRequired and without entry, exit, PnL or complete trade. Direct
tests cover exact owners/metadata, missing SL/selection/price/reference, source
identity, direction, conflicts/duplicates, unavailable sources, terminal gates,
future targets/stop revisions and an added future candle tail. Standard TP and
WF2/SL/risk regressions remain separate checks of the existing behavior.

The existing opt-in exporter below regenerates a unique authentic report from
the unchanged adapter/four CSVs, independently evaluating 7,596/7,597-frame
prefixes and comparing every common historical rule/evidence and diagnostic
fingerprint. At 13:36Z, WF2-001/002 remain Passed, SL HumanValidationRequired,
risk/TP Waiting and verdict HumanValidationRequired. No authentic native target
selection, stop, exposure, fill, exit or economic result has been supplied by this
feature. S14's historical USTEC record remains distinct from US100.

Generated artifact: `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-a1d8dc2b3c7149ada452e3e106648ee2.json`.
SHA256: `605b994f5e3b1f98e32d970950b80f86449a0a39c6b8fe307b41a40ba55df66d`.
Size: 4,431,521 bytes. The manifest records base commit
`713a88747a6f502fa6ad5bdf747b6eaa3ff406c2` plus 20 exact runtime code hashes;
the pre-feature base is not claimed to contain this increment. All 7,596 common
frames match the independent longer prefix; the exported six frames cover
13:31Z-13:36Z after the full prefix beginning August 31 22:01Z. No acquisition or
normalization is rerun, and prior reports are not overwritten. Adapter, CSVs
and protected prior reports retain their verified original hashes.

Validation: 2,036 Application Nasdaq/workflow/canonical cases, 124 Nasdaq
integration cases, 19 domain replay cases, one regenerated authentic canonical/
API case and one legacy authentic API case: **2,181 .NET passed, zero failed or
skipped**. The earlier focused 158-case risk/SL/TP run overlaps these totals;
the added terminal-TP regression is included in the final 2,036 cases. All 33
frontend tests, full .NET/frontend builds, ESLint, scoped formatting, local
documentation links and `git diff --check` passed. No manual browser test was
performed; API integration and rendered-component tests cover the feature.

### Nasdaq Demo v0 critical path

| Area                                                                   | Actual state                     | Evidence and remaining requirement                                                                                                                                                                       |
| ---------------------------------------------------------------------- | -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| WF2 applicability, reversal, pre-entry, SL, risk and TP rule consumers | Implemented                      | Canonical selectors/evaluators, prior owner binding and synthetic integration tests; positive SL/TP still need human inputs.                                                                             |
| Native original Replay #2 SL and TP decisions                          | HumanOnly                        | Exact US100 initial StopPrice, native protective reference and original availability; source-backed important Low, exact TP/reference price and original selection/availability. S14 USTEC is not US100. |
| Original exposure                                                      | HumanOnly                        | Documented positive named account basis/currency, same-currency planned maximum loss, planned entry, costs/calculation provenance and original assessment/availability; optional supplied quantity/unit. |
| WF2 observed entry and coherent historical snapshot                    | NotImplemented                   | `NasdaqHistoricalObservedEntry` and `NasdaqHistoricalTradeSnapshot` accept Standard eligibility/facts only. A planned entry or TP Passed is not a recorded execution.                                    |
| Authentic entry chronology/native identity                             | HumanOnly                        | Reviewed execution ID/price/time, native applicability and authentic availability; recorded 08:32 entry cannot be retroactively authorized by later canonical WF2 recognition.                           |
| WF2 supervised order/fill state                                        | NotImplemented                   | No WF2 order sender, accepted-order/fill state or broker execution is delivered by these replay facts.                                                                                                   |
| WF2 exit and factual composition                                       | NotImplemented                   | Existing documented-exit/contact/factual infrastructure is Standard-snapshot-owned; requires original execution/management records and causal ordering before reuse.                                     |
| Economic result and definitive loss/violation classification           | BlockedByUnresolvedSpecification | Factual price contact is not a fill. Execution/management policy, quantity/units/valuation/costs and outcome classification must be supported; summary remains inconclusive.                             |
| Trade count, session completion and no-reentry                         | BlockedByUnresolvedSpecification | NQ-RISK-003/NQ-REENTRY-001 remain unresolved. Existing terminal/cutoff guards work; no universal one-trade or reentry rule is invented.                                                                  |
| Persisted diagnostics API and historical UI                            | Implemented                      | Read-only frame navigation, risk/SL/TP evidence and explicit gaps; no automatic trading or complete-session claim.                                                                                       |
| Autonomous importance/ranking                                          | BlockedByUnresolvedSpecification | Human-reviewed targets are supported; no general target-choice algorithm is approved.                                                                                                                    |

**One next functional feature:** WF2 human-observed historical entry evidence and
causal selector/report integration, reusing existing execution fields and the
actual WF2 eligibility owner without a fill simulator. Preserve the existing
constraint that execution cannot precede eligibility; expose unsupported earlier
execution as unresolved evidence rather than silently backdating eligibility or
changing strategy rules. Authentic entry/native chronology must be resolved
before it can produce a coherent original Replay #2 snapshot. After that remain
WF2 snapshot/exit/outcome composition and unresolved session controls. The
current delivery supports supervised historical analysis, not complete supervised
operation or autonomous order sending.

## WF2-SL-to-NQ-RISK-001 functional convergence (2026-11-17)

This increment supersedes only the older Standard-only risk-consumer limitation
below. Initial checkpoint: clean synchronized `main` at
`475c01849ca958ae3a3956f1ab29066f62e1ca4e`. The strategy remains
`nasdaq-0.1.0-draft`; no definition, activation or workflow edge changes.

### A. Functional delivery and audited 1% semantics

The existing risk evaluator accepts the original prior eligible Scenario 2 SELL
SL fact through `NasdaqWickfillScenario2RiskExposureObservation` and its stateless
selector. The mentor binder checks exact session, actual Start and prior SL owner.
The selector revalidates current applicability/reversal/SL sources; it cannot
revive a superseded or foreign stop. Original assessment and availability times
remain separate from canonical emission time. The original native provider and
instrument must match; no USTEC/US100 substitution or conversion is permitted.

The audited contract in [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md)
and [rule catalog](../strategies/nasdaq/rule-catalog.md) means:

- Maximum **planned risk per trade**, not a confirmed daily limit or realized PnL.
- Compare source-reported maximum loss / source-documented positive account basis
  **<= 0.01**, inclusive. Both money amounts use the one explicit currency.
- Required human/account inputs: actual amount, named basis, reported planned
  loss, native planned entry parameter, exact selected SL, cost treatment,
  calculation provenance and original assessment/availability. Quantity is
  optional documented input with an explicit unit, never calculated position size.
- The engine chooses neither balance nor equity. Unsupported basis/calculation
  or missing evidence cannot pass. No leverage, tick value, distance formula,
  currency exchange or implicit costs are introduced. Zero loss is permitted by
  the existing invariant; positive basis and nonnegative loss are validated.
- Both paths use the same existing decimal-to-integer exact comparison. A decimal
  ratio is an audit projection; overflow yields null with an explicit flag while
  exact operands still decide the result. There is no rounding tolerance.
- The rule catalog applies to both directions and contains no Scenario 2
  exemption; this task extends the existing owner consumer, not strategy semantics.
  Standard's input, selector, statuses and comparison results remain unchanged.

After an eligible SL, missing exposure returns HumanValidationRequired, conflicting
financial assertions require review, unavailable current SL sources return
DataUnavailable, and complete compatible exposure returns Passed/Failed from
the exact comparison. Missing eligible SL remains Waiting. Canonical same-frame
terminal liquidity invalidation also blocks WF2 risk. No successful risk result
approves TP or creates an observed fill, trade, exit or economic result.

The lifecycle Risk row now reflects the recorded rule instead of saying WF2 risk
is unimplemented. Optional `Scenario2RiskExposure` retains operands, currency,
named basis, planned entry, cost treatment, supplied quantity/unit, exact SL,
ratio/overflow/limit/result, original times and source. The API preserves exact
decimal strings for browser display without client-side risk calculation. The
reader validates the published projection against recorded ownership/status and
temporal bounds; it does not rerun a strategy. Legacy reports retain their
historical NotImplemented risk rows. Complete supervised trade remains false.

### B. Synthetic evaluable path

Explicitly synthetic canonical binder/coordinator/report/API tests cover missing
exposure, loss 100 / basis 10000 => Passed and loss 101 / basis 10000 => Failed.
The direct native path additionally checks below/exact/above boundaries at full
decimal precision, tiny operands, ratio overflow, zero loss, identity/units,
provenance, unavailable sources, foreign owners, conflicts, same-frame ancestry,
terminal gates and future human/candle/stop revisions. No observed execution,
TP, exit or economic result is produced. UI tests show the server decision and
exact source amount even beyond JavaScript numeric precision.

### C. Authentic Replay #2

The existing opt-in command below regenerates a unique canonical report from the
unchanged reviewed adapter/CSVs and independently verifies the common
7,596/7,597-frame prefixes. No acquisition or normalization is rerun.
At 13:36Z, WF2-001/002 remain Passed, SL HumanValidationRequired, risk/TP Waiting
and the canonical verdict HumanValidationRequired. Authentic exposure is absent;
no native initial stop, entry, target, execution, PnL or risk approval is invented.
S14's USTEC evidence is retained separately and does not authorize US100 inputs.

Regenerated artifact: `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0b29e8617c9498298b4420ab854cae0.json`.
SHA256: `4fec5c8d3f409dc0b763ac009a2ba91a350e7bf0643b65b2905f24ca93bcd0a7`.
Size: 4,430,954 bytes. The independent prefixes have identical common historical
rule/evidence and diagnostic fingerprints; the selected six frames span
13:31Z-13:36Z after the complete 7,596-frame prefix beginning August 31 22:01Z.
The runtime manifest records base commit `475c01849ca958ae3a3956f1ab29066f62e1ca4e`
plus exact working-tree code hashes; the base is not falsely claimed to contain
this increment. The adapter, four CSVs and both protected prior reports retain
their seven verified hashes. Existing report configuration can select this new
file/hash; no old report was overwritten and no source adapter was edited.

Validation: 2,021 Application Nasdaq/workflow/canonical cases, 117 Nasdaq API/
integration cases, 19 domain replay cases, one regenerated authentic canonical/API
case and one legacy authentic API case: **2,159 .NET passed, zero failed/skipped**.
The focused 105-case risk/SL selection overlaps those totals. All 32 frontend
tests passed. Complete .NET/frontend builds, ESLint, scoped formatting, local
documentation links and `git diff --check` passed. No manual browser test was
performed; API integration and rendered-component tests exercise the delivery.

Remaining original evidence: one complete native SL record (uniquely reviewed
protective reference, exact original StopPrice, native applicability and authentic
availability), then source-documented account basis/currency/planned loss,
planned-entry/cost/calculation evidence and original risk times. TP selection,
observed entry/exit and economic composition remain independent blockers.
The next smallest functional feature is scoped WF2-to-NQ-TP-001 consumption of
human-selected native target evidence, preserving the existing target contract;
it must not select a target automatically or claim authentic inputs are present.

## WF2-to-NQ-SL-001 canonical convergence (2026-11-17)

This functional increment supersedes the earlier delivery's **NotImplemented
WF2-to-SL convergence** row below. Preflight verified clean synchronized `main`
at `0b328067b4d145d8c7cd8b4ae984033e7364f18b`. The audited definition remains
`nasdaq-0.1.0-draft`; no ACTIVE version, activation, Required flag or Sequence
is modified.

The existing NQ-SL-001 evaluator now routes actual surviving Scenario 2 SELL
ancestry through `NasdaqHumanWickfillScenario2StopLossObservationSelector`.
Missing native reference/price, independently documented initial StopPrice and
authentic temporal/provenance fields require human validation. Complete compatible
evidence publishes `NasdaqHumanWickfillScenario2StopLossRuleFact`; no Standard
5M LH or M1 fact is manufactured. Conflicts remain human-review cases; unavailable
exact sources, including competing unavailable assertions, prevent Passed.

The draft workflow retains Standard's original AND route and adds explicit
prior WF2-002 as a latest-pass OR alternative. Its satisfied route is recorded
in `RuleEligibility.PrerequisiteRuleIds`. Same-frame WF2 completion, an older
favorable pass after current failure, unknown/cyclic metadata and foreign owners
cannot establish the new SL progression. The canonical dispatcher's terminal
liquidity gate now also covers SL **only for actual WF2 ancestry**. Direct source
selection is not a canonical terminal evaluation or execution authorization.
Standard's route, selector, fact type and terminal predicate remain unchanged.
See [ADR 0003](../decisions/0003-define-canonical-replay-prerequisite-gating.md)
and [ADR 0026](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md).

The mentor binder accepts the existing SL observation with exact session,
actual Start and prior eligibility references. It never clamps the original
selection/availability time to binding or emission time. A bound observation is
not automatically usable or Passed. The lifecycle summary/API/UI preserve an
optional `Scenario2StopLoss` selection, including protective-reference identity,
separate reference/stop prices, original times and both source references.
This standalone SL evidence is distinct from the prices of a complete historical
trade snapshot. Risk/TP consumers remain Standard-owned and cannot consume this
new fact as an approved exposure/target. On the following eligible frame they
explicitly acknowledge that the canonical WF2 SL exists and return
HumanValidationRequired for their own unimplemented positive contracts/inputs,
rather than claiming the SL prerequisite is absent. They never inherit a success
from SL. Authentic Replay #2, which still has no approved SL, keeps both Waiting.

### Authentic regenerated Replay #2

The protected original report retains SHA256
`41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2`.
It remains an honest record of the older runtime's Wait result; the report
reader does not reevaluate old artifacts. Configure the **new report** to view
the new canonical result.

The existing opt-in exporter imports the unchanged reviewed native CSVs through
`GenerateCanonicalMultiTimeframeBacktestUseCase.ExecuteMentorSession`, evaluates
the complete 7,596-frame prefix and independently compares the 7,597-frame prefix.
All common historical rule/evidence and native diagnostic fingerprints match;
future 13:37Z evidence does not alter 13:36Z or earlier. The selected six-frame
window is exported after evaluation, not used as a truncated source history.
The newly generated artifact is then read through the configured HTTP API with
its expected SHA256. The existing adapter retains SHA256
`8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2`.

| UTC frame | WF2-001 | WF2-002 | NQ-SL-001               | Canonical verdict       |
| --------- | ------- | ------- | ----------------------- | ----------------------- |
| 13:34     | Passed  | Waiting | Waiting                 | Wait                    |
| 13:35     | Passed  | Passed  | Waiting                 | Wait                    |
| 13:36     | Passed  | Passed  | HumanValidationRequired | HumanValidationRequired |

At 13:36Z the actual SL selector has been reached. Native SL evidence remains
missing; no reference/StopPrice, entry, target, exposure, exit, PnL or operation
is invented. Risk and TP remain Waiting, and failure classification stays
`inconclusive`. The diagrammatic M30 "second candle", sweep extreme and S14
USTEC scalars are not selectors or numeric US100 substitutes.

Published artifact: `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-68e61670d99141f7b6025983d3f033be.json`.
SHA256: `c60d0caa3615c3e944df8aa519c16942ef030f44e0fb57a982a65d8d09c7bd5e`.
The envelope records its runtime base commit plus hashes of changed convergence
code, source CSV identities/hashes, original evidence, complete prefix bounds,
capabilities and actual canonical observations. Its base commit is explicitly
the pre-feature checkpoint, not a claim that the old code generated the new
result. Native data is not reacquired/renormalized and old reports are not
overwritten. The initial exploratory export is retained separately.

Use the existing report configuration and
`http://localhost:5173/#nasdaq-historical-demo` with this new file/hash. To
regenerate with the reviewed external adapter from repository root:

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
try {
    dotnet test tests/integration/MoneyWay.IntegrationTests --no-restore "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter FullyQualifiedName~ExportReplay2CanonicalPreEntryDiagnosticFromRealHistoricalOutcomes --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
}
```

Controlled synthetic tests exercise the real synchronized canonical coordinator,
mentor binder, dedicated SL fact, workflow eligibility and serialized lifecycle
projection, preserving independent risk/TP gates. Synthetic prices are never
registered as authentic Replay #2 evidence. The selector suite also covers
missing stop/reference, invalid provenance, mismatched feed, source availability,
conflict, current terminal invalidation and unchanged earlier/future-prefix
results. Old Waiting assertions specific to WF2's former Standard-only SL
blocker are updated to HumanValidationRequired; their Standard exclusion and
no-trade protections remain asserted.

Final validation: 1,990 Application Nasdaq/workflow/canonical cases, 108 Nasdaq integration cases, 19 domain replay cases, one authentic regenerated canonical/API case and one legacy authentic API case: **2,119 .NET passed, zero failed, zero skipped**. The 47-case SL selector/convergence selection overlaps those totals. All **30 frontend tests** passed; complete .NET/frontend builds, ESLint, formatting, links and `git diff --check` passed. Synthetic integration success is separate from authentic SL completion. The published report is 4,429,846 bytes. Independent 7,596/7,597-frame generations have identical common-prefix rule/evidence and diagnostic fingerprints; no byte-equality claim is made between different runtime manifests or different prefix scopes.

The remaining authentic human input is one complete native SL record: uniquely
identified protective reference and price, independently approved initial native
StopPrice, original effective/available times, source/human provenance and any
explicitly validated USTEC-to-US100 numeric applicability. S14's USTEC initial
price remains historically recorded, but does not supply those native fields or
SL timestamps. No universal M30 geometry, buffer or historical timestamp is
inferred.

The next smallest functional feature is WF2-compatible documented risk exposure
under NQ-RISK-001, reusing the existing exact 1% comparison and requiring a prior
canonical WF2 SL plus independently documented account basis/maximum loss. No
sizing, TP, execution or complete Nasdaq Demo v0 claim follows from this delivery.

## Historical trade lifecycle panel (Demo recovery)

The existing historical-demo page/API now expose a **trade lifecycle for the
selected canonical frame**, alongside its unchanged rule evaluations and verdict.
This is product reporting over the canonical multi-timeframe run, not a second
replay, strategy branch, order sender or economic simulator.

`NasdaqHistoricalTradeLifecycleSummary` groups pre-entry, observed entry, SL,
risk, TP, documented exit, economic outcome and session attempts. New full
reports preserve the projection per frame through
`NasdaqMentorSessionReportJson.Write`; the bounded Replay #2 format exposes its
actual rule results and visible input provenance through the same API.
The UI shows exact entry/SL/TP/risk/exit values **only from coherent existing
canonical artifacts**. Documented exit scope/reason remain literal; a partial
close is not silently presented as a complete trade. A missing/conflicting
execution is not replaced by an OHLC contact or inferred fill.

Availability labels such as `RecordedArtifact`, `MissingInput`, `NotImplemented`
and `BlockedByUnresolvedSpecification` describe this presentation's artifacts
and dependencies. They do not replace `RuleEvaluationResult` or `StrategyVerdict`.
`RecordedEvaluation` means only that the original rule result was recorded.
Failure classification remains `inconclusive`: Failed rule IDs are shown
separately, and neither a losing price move nor a failed pre-entry rule is
automatically classified `valid_strategy_loss` or `rule_violation`.
`CompleteSupervisedTrade` remains false because the existing factual composition
does not establish an economic outcome, execution compliance or session-attempt
policy. Nasdaq Demo v0 is not declared complete.

### Verified implementation checkpoint and remaining contracts

Preflight verified clean `main` at
`84bad774bd32d4c41b0f59ea046580751e2d45db`, synchronized `origin/main` (0/0).
The following distinctions were checked against code and tests, not inferred
from earlier prompts. HumanOnly below describes the required source evidence,
not a change to the capability catalog's registration status.

| Capability                                                                  | Actual state                                 | Existing implementation / remaining boundary                                                                                                                                                                                       |
| --------------------------------------------------------------------------- | -------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Applicability, WF2-001 reversal and WF2-002 eligibility                     | DONE with HumanOnly evidence                 | `NasdaqHumanBranchApplicabilityObservationSelector`, `MoneyWayNasdaqHumanWickfillScenario2ReversalEvaluator`, `MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator`; exact prior owners and causal sources remain required |
| Scenario 2 pre-entry lifecycle completion                                   | DONE                                         | `NasdaqScenario2LifecycleCompletionEvidence` / `MoneyWayNasdaqReplayLifecyclePolicy`; no Standard M1 facts are manufactured                                                                                                        |
| Scenario 2 SL input and selection                                           | DONE with HumanOnly evidence                 | `NasdaqHumanWickfillScenario2StopLossObservation` / selector preserve native price domain, independently selected stop, original times and exact WF2 ancestry                                                                      |
| Scenario 2 convergence into NQ-SL-001                                       | NotImplemented                               | Current workflow/evaluator still require Standard `NQ-M1-003`; the primitive/selector alone cannot publish SL Passed                                                                                                               |
| NQ-RISK-001 / NQ-TP-001                                                     | DONE for Standard; Scenario 2 NotImplemented | Existing evaluators consume `NasdaqHumanStructuralStopLossRuleFact`; no WF2-compatible downstream fact integration exists                                                                                                          |
| Observed entry, snapshot, documented exit and factual composition           | DONE for Standard; Scenario 2 NotImplemented | `NasdaqHistoricalObservedEntry`, snapshot assembler, exit selector and factual composer retain Standard eligibility/SL ancestry                                                                                                    |
| Safe canonical outcomes                                                     | DONE                                         | `EvaluateStrategyReplayContextOutcomeUseCase` checks coverage/metadata and produces strategy verdicts; it is not a PnL or paper execution engine                                                                                   |
| Paper order execution / economic outcome and loss-compliance classification | NotImplemented                               | No order sender is introduced; existing factual trade evaluation explicitly excludes economic classification                                                                                                                       |
| Universal trade-count/no-reentry policy                                     | BlockedByUnresolvedSpecification             | `NQ-RISK-003` and `NQ-REENTRY-001` remain unresolved; no one-trade cap or retry allowance is invented. Existing terminal session gates are unchanged                                                                               |
| Selected-frame trade lifecycle in demo/API/report                           | DONE in this feature                         | New lifecycle summary, persisted full-report field, bounded-window provenance and UI panel, with deterministic regression coverage                                                                                                 |

The approved human S14 entry `29604.00` and initial stop `29662.25` belong to
the reviewed **USTEC** position. They are not native US100 runtime inputs.
[ADR 0026 section 2.14](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md#214-human-confirmed-replay-2-initial-ustec-sl-price-2026-11-05-s14)
still requires source reconciliation, exact protective-reference identity/price
and original SL selection/availability times. The panel preserves missing native
values rather than importing those scalars. Its availability reasons identify
the architecture blocker separately from missing human evidence.

### Authentic Replay #2 API artifact

The opt-in
[HTTP integration test](../../tests/integration/MoneyWay.IntegrationTests/Replay2TradeLifecycleDemoTests.cs)
reads the already completed canonical Replay #2 window. It does **not** rerun
market acquisition, normalization or strategy evaluation. Source report:
`C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0d69f182a0b456ba4662896192dc242.json`,
SHA256 `41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2`.
The API validates the pinned artifact's schema/chronology/hash; nested historical
provenance remains `EvidenceValidation = NotRevalidated`.

At step 7596 / `2026-09-08T13:36:00Z`, the actual API response retains:

- Native identity US100 / `oanda-global-markets-mt5-demo`, September 8 session.
- WF2-001/002 Passed; canonical verdict Wait; blocker NQ-SL-001 / Waiting.
- Scenario 2 SL/risk/TP integration unavailable; entry, native stop, target,
  exposure and documented exit values remain absent.
- S9/S11/S13 input provenance visible only at its original source availability;
  an independent request for step 7592 / 13:32Z does not transport S13 backwards.
- `inconclusive` classification and no economic, fill or complete-trade claim.

Two independent final executions produced byte-identical 18,306-byte artifacts,
SHA256 `abca72bff222cbbce398277c08de30bb1597fa4fd4a539ed73d0ee698a0186cf`.
The retained example is
`C:\MoneyWay\session\reports\2026-09-08\replay2-trade-lifecycle-9fd89b07fc264fceb55785f6586a10bb.json`.
These are new create-only diagnostic view artifacts outside Git; the original
canonical report's hash was rechecked unchanged. No original adapter, CSV,
evidence, strategy definition, metadata or canonical pipeline was modified.

Reproduce the API result using the existing configuration instructions below,
with that original canonical report path/hash, then open
`http://localhost:5173/#nasdaq-historical-demo` or request:

```text
GET /api/nasdaq/historical-demo?frameStep=7596&reportSha256=41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2
```

Reproduce the protected artifact test from repository root:

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
try {
    dotnet test tests/integration/MoneyWay.IntegrationTests --no-restore --filter FullyQualifiedName~Replay2TradeLifecycleDemoTests --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
}
```

The existing variable opts into local historical evidence tests; this test reads
the pinned report, not the adapter's strategy logic. No browser session or
interactive manual test was claimed: real HTTP integration and React component
tests validate this delivery. Optional manual review uses the URL above.

### Bounds, compatibility and validation

Full-report `TradeLifecycle` is an additive, flat schema-v2 property: no graph
deserialization or new checkpoint is required. Legacy full reports stay readable;
an existing snapshot without the new projection is explicitly marked
`PublishedArtifactsWithoutDetails`, rather than falsely claimed missing. Rebuild
such a report through the existing harness to expose its numeric details. Legacy
bounded windows keep their original source format and missing trade artifacts.

Only the selected frame's sources are returned, at most 16 references with
512-character previews and an exact SHA256 of each full reference. Omitted
counts/truncation are explicit and original text remains in the original Inputs.
Sources are input provenance, not fresh cryptographic source authentication.
Future provenance and execution times, fabricated completion, and prices without
an available snapshot are rejected. Original report hash navigation and
Development-only/read-only API behavior remain unchanged.

The full .NET build passed with zero warnings/errors. Final non-overlapping
selections passed 1,916 Nasdaq/application canonical tests, 107 Nasdaq integration
tests (including eleven new lifecycle tests), 19 domain replay tests and one real
Replay #2 HTTP artifact test: **2,043 .NET passed, zero failed, zero skipped**.
All 29 frontend tests passed, including two new panel tests; frontend production
build and ESLint passed. C#/Markdown/TypeScript formatting, links and
`git diff --check` are checked before publication. Focused runs overlap these
totals and are not added twice. Authentic API execution was repeated with the
same output hash; synthetic complete Standard artifacts are never reported as
an authentic Replay #2 trade.

The next smallest runtime feature is scoped WF2-to-NQ-SL-001 convergence with
exact canonical ancestry and terminal protection, leaving Standard behavior
intact. Real population first needs one complete human source record: native
US100 protective-reference identity/price, independently approved initial
StopPrice, original effective/available times and validated native applicability
of any USTEC source. No universal M30 selector, buffer, altered timing, TP,
quantity or outcome is inferred. This delivery authorizes no active-version
change or automatic trade.

## Generate or reuse a report

For a full historical report, reuse a legitimate SchemaVersion 2 report written by `NasdaqMentorSessionReportJson.Write` through the [official local harness](nasdaq-mentor-session-local-run.md). Set `MONEYWAY_MENTOR_REPORT_PATH` when generating it. This view reads an existing report; it does not run replay, accept uploads or execute orders.

The reviewed 2026-06-29 report contains 9,660 frames, 178,140,291 bytes and final canonical `NoTrade`. Its verified SHA-256 is `B0D5C3243206276C135960F090DADBA069450D6AB1C1590A1AF7116DE49A56F7`. It demonstrates historical review infrastructure, not an executed Standard trade, profitability or autonomous readiness. Setup/branch classification is not inferred.

## Start locally

From the repository root, in the API terminal, replace the placeholder with your existing report file:

```powershell
$env:NasdaqHistoricalDemo__ReportPath = (Resolve-Path -LiteralPath '<your-existing-report.json>').Path
# Optional but recommended: pin an independently verified source digest.
$env:NasdaqHistoricalDemo__ExpectedSha256 = '<verified-64-character-SHA256>'
dotnet run --project src/backend/MoneyWay.Api --launch-profile http
```

For the reviewed June report, use the verified digest above. For a different report, use that report's independently verified digest. Omit/remove `NasdaqHistoricalDemo__ExpectedSha256` if unavailable: the API then calculates an identity hash but cannot claim an expected-hash match. Neither variable is a secret, but local paths and report files must not be committed.

In a separate terminal:

```powershell
cd src/frontend/moneyway-web
npm ci
npm run dev
```

Open **http://localhost:5173/#nasdaq-historical-demo**. The existing `VITE_API_BASE_URL` convention defaults to `http://localhost:5080`. API configuration keys are `NasdaqHistoricalDemo:ReportPath` and `NasdaqHistoricalDemo:ExpectedSha256` (double underscores in environment variables). Configuration is server-owned; the browser cannot choose a path.

This endpoint is mapped only in Development, using the existing localhost frontend CORS policy. The existing local application has no authentication mechanism; this does not add a remotely exposed file service. Use the local launch profile. Production has no historical-demo route.

## What is displayed

`GET /api/nasdaq/historical-demo` returns `NasdaqHistoricalReportSummary` with source session ID, explicit strategy/version/provider/instrument/day, report frame count/time bounds/final verdict, content SHA-256/size, validation scope, diagnostic codes, `SelectedFrame` and bounded `Navigation` metadata.

Without a selector, the selected frame remains the last persisted frame on the report's declared trading day in America/Bogota, which can differ from the complete report's last frame. For compatibility, `SelectedSessionLastFrame` retains that same last-frame value only on requests without `frameStep`. Explicit selections return that legacy field as null, preventing later rule evidence from accompanying an earlier selected frame. Both complete-report and selected-frame results are labeled separately. Rule IDs, definition statuses, evaluation results, ordering, required flags, reasons, timestamps and evidence IDs are copied from the selected frame. Required non-Passed/non-NotApplicable decisions are shown as outstanding conditions; the canonical verdict and blocker are never recalculated from them. Optional decisions remain inspectable.

### Select a canonical Step

```text
GET /api/nasdaq/historical-demo?frameStep=<Step>
GET /api/nasdaq/historical-demo?frameStep=<Step>&reportSha256=<current-report-hash>
```

`frameStep` is the persisted canonical `Step`: a positive, **one-based identifier within the complete replay report**, not an array index or an ordinal within the selected session. It has no inferred timestamp relationship. Only Steps actually found on the report's declared America/Bogota session day are selectable. The existing cursor groups simultaneous timeframe closes, and `StrategyReplayContext` requires strictly chronological prior observations; the reader preserves its existing rejection of duplicate/out-of-order timestamps. Selection uses Step, never timestamp matching or deduplication.

`Navigation` contains `FirstStep`, `LastStep`, `SelectedStep`, `PreviousStep`, `NextStep` and `SessionFrameCount`. Neighbors are actual encountered session frames; null means no neighbor or unavailable selection/session. The UI uses these returned identifiers for Previous/Next, allows direct integer Step selection, and returns to the default last frame with **Último frame**. It displays canonical UTC plus local America/Bogota time. No frame list is returned.

The optional `reportSha256` pins navigation to the report already loaded by the browser. It never overrides the server's configured expected hash. A changed report returns 409 before a missing-frame result; the UI discards incompatible selection and loads the new report's last session frame. Changing API base URL remounts the view with default selection. Unknown/duplicate query parameters, malformed IDs or malformed hashes are rejected. Loading hides previous evidence; cancellation and request generations prevent late responses from replacing the current selection. Returning to last can supersede a pending navigation request.

Snapshot state and `NasdaqHistoricalFactualTradeEvaluation` availability/human-review requirement are independent of the strategy verdict. Missing/unsupported optional historical fields stay unavailable. No entry, SL, TP, economic outcome or setup classification is synthesized. Input/binding diagnostic codes are shown without exposing their local file paths.

## Bounded access and validation

For harness SchemaVersion 2, Infrastructure reads one JSON property/frame at a time with an incrementally filled buffer; it never deserializes all frames into a collection. Each selection uses one complete incremental pass, including integrity validation, and retains only bounded header fields, the selected frame and small current-frame/navigation state. The default response duplicates the same last-frame projection under its legacy and new property names; explicit selection contains only the selected frame's decisions. Limits remain 1 GiB source, 8 MiB per property/frame, depth 64, 32 root properties, 128 rule decisions/missing rule IDs/distinct diagnostic codes, and 4,096 characters per projected string. Exceeding supported limits returns unavailable, not a fabricated result.

Reads are serialized per API instance, cancel with the request and use no projection cache. File access is read-only with `FileShare.Read`. Only a configured absolute local path is accepted; UNC paths, URL paths, browsing and caller-selected paths are rejected. Only `frameStep` and `reportSha256` query parameters are supported. No source paths, raw evidence bodies or per-frame lists enter the response. `Cache-Control: no-store` applies to success and errors.

Validation checks supported schema, required projection fields, canonical enum values, chronological frame steps/timestamps, rule ordering/uniqueness, declared counts/time bounds and final frame/verdict agreement. SHA-256 is computed over the complete source and compared with the configured expected digest when supplied. This is **not** strategy re-evaluation or full provenance certification: compressed evidence hashes/references and every nested canonical fact are **not revalidated** (`EvidenceValidation = NotRevalidated`). The original harness/writer and its validations remain authoritative.

| Response | Meaning                                                                                                       |
| -------- | ------------------------------------------------------------------------------------------------------------- |
| 200      | Supported report read; canonical/factual availability still comes from source                                 |
| 400      | Invalid/duplicate/unknown selection parameters or malformed report identity; no caller-selected paths         |
| 404      | No configured report, missing file, Step outside the selected session, or endpoint absent outside Development |
| 409      | Report identity changed; discard selection and reload the default frame                                       |
| 422      | Invalid/incomplete/unsupported report, exceeded read limits or expected-hash mismatch                         |
| 503      | Invalid location configuration or inaccessible report                                                         |

Errors contain a stable code and generic message, without exception text or filesystem details. Generate a complete report before configuring it; an actively written or truncated file is unavailable. Refresh the page after replacing/configuring the report. No Run button, replay queue or persistent run manager is provided.

## Functional verification

For an existing source, record its SHA-256 before/after requesting the endpoint and opening the view. Confirm the session, report identity, exact canonical result and unavailable evidence match the source. Temporarily unconfigure the source or configure a nonexistent path to check unavailable handling, then restore your configuration. The real report stays outside Git; automated tests use synthetic canonical fixtures and the existing 9,660-frame report-shape fixture.

### Frame-navigation check (2026-10-22)

The existing 178,140,291-byte June report was reused without replay execution. An independent incremental JSON scan supplied the reference Steps and decisions; eight API reads were compared against those source values, including neighbors, rule statuses/reasons/evidence IDs, canonical UTC and session ownership. The session contained **1,380 available frames**, Steps **7320–8699**; this count was observed rather than inferred from elapsed minutes.

| Selection       | Step | Bogotá timestamp | Selected verdict        | Response bytes | Query seconds |
| --------------- | ---: | ---------------- | ----------------------- | -------------: | ------------: |
| Default last    | 8699 | 2026-06-29 23:59 | NoTrade                 |         12,789 |         2.621 |
| First           | 7320 | 2026-06-29 00:00 | HumanValidationRequired |          5,887 |         1.130 |
| Before deadline | 7829 | 2026-06-29 08:29 | Wait                    |          5,889 |         1.078 |
| Opening         | 7830 | 2026-06-29 08:30 | NoTrade                 |          5,896 |         1.072 |
| Cutoff          | 7980 | 2026-06-29 11:00 | NoTrade                 |          6,791 |         1.099 |
| Explicit last   | 8699 | 2026-06-29 23:59 | NoTrade                 |          6,791 |         1.124 |

Actual Next/Previous queries between Steps 7829 and 7830 also matched source (5,896 / 5,889 bytes; 1.074 / 1.131 seconds). `NQ-TIME-003` was Waiting at 08:29 and Failed at 08:30; `NQ-TIME-002` was Passed before 11:00 and Failed at/after 11:00. These are persisted decisions, not new trading rules or a retrospective session-wide cutoff. Complete-report `FinalVerdict` remained NoTrade throughout.

The standalone API process peaked at **74,620,928 bytes (71.17 MiB) working set** over these eight reads, using the OS peak-working-set counter; final observed working set was 73,138,176 bytes. These are single-machine observations, not allocation measurements or performance guarantees. The source SHA-256 remained the verified value above. Installed Chrome was exercised against the real API/UI for default, Previous/Next, direct selection, invalid Step, first/last boundaries, Bogotá/UTC display and return to last. An early-frame screenshot showed complete-report NoTrade alongside selected-frame Wait. No browser/test artifacts or report data are committed.

## Real Replay #2 bounded canonical diagnostic (2026-11-06)

The same Development-only endpoint and React screen also accept the explicit `moneyway.canonical-preentry-window` schema, version `1`. `ReadNasdaqHistoricalReport` dispatches the named schema to the isolated `ReadNasdaqCanonicalPreEntryWindow` projection; it does not rename it to harness v2, invoke replay or change any evaluator. The server still selects exactly one source via the existing configuration keys. Stop/restart the API with the desired path/hash to switch reports; the browser does not select files.

Verified protected source: `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0d69f182a0b456ba4662896192dc242.json`, 4,493,832 bytes, SHA256 `41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2`. Its source and [canonical generation record](nasdaq-replay2-canonical-preentry-diagnostic.md) remain unchanged. No raw CSV, adapter or prior report is regenerated, overwritten or renamed.

### Configure Replay #2

From the repository root, **Git Bash (MINGW64)**:

```bash
export NasdaqHistoricalDemo__ReportPath='C:/MoneyWay/session/reports/2026-09-08/canonical-preentry-d0d69f182a0b456ba4662896192dc242.json'
export NasdaqHistoricalDemo__ExpectedSha256='41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2'
dotnet run --project src/backend/MoneyWay.Api --launch-profile http
```

Equivalent **PowerShell** commands:

```powershell
$env:NasdaqHistoricalDemo__ReportPath = 'C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0d69f182a0b456ba4662896192dc242.json'
$env:NasdaqHistoricalDemo__ExpectedSha256 = '41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2'
dotnet run --project src/backend/MoneyWay.Api --launch-profile http
```

In a second terminal, from the repository root (same commands in either shell):

```text
cd src/frontend/moneyway-web
npm run dev
```

Open **http://localhost:5173/#nasdaq-historical-demo**. No Worker is needed. The screen must identify September 8, US100, `oanda-global-markets-mt5-demo`, `moneyway-nasdaq` / `nasdaq-0.1.0-draft`, and **bounded canonical diagnostic** scope.

| Scope field                 | Authentic source / presentation                                                                                                                                                                                                              |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Exported window             | 6 frames, Steps 7591-7596, 13:31Z-13:36Z / 08:31-08:36 America/Bogota                                                                                                                                                                        |
| Evaluated historical prefix | 7,596 frames, 2026-08-31T22:01Z through 2026-09-08T13:36Z; distinct metadata, not 7,596 exported/selectable frames                                                                                                                           |
| Final session verdict       | Unavailable: the envelope does not supply one. API FinalVerdict and SessionId remain null; no session identifier is invented                                                                                                                 |
| Selected canonical verdict  | Copied from that frame's CanonicalDiagnostic; never recalculated from rule rows                                                                                                                                                              |
| Rule states                 | Copied from that frame's StrategyObservation, retaining exact Step, UTC evaluation time, status, definition, required flag and reason                                                                                                        |
| Evidence identifiers        | Persisted RecordIds from BindingDiagnostics through the selected frame's time, plus actual runtime commit/report hash. Native EvidenceReference bodies/local paths are not exposed or renamed as EvidenceIds; absent EvidenceIds remain null |
| Historical trade            | Native snapshot/factual absence stays unavailable; no SL/TP/entry/fill/PnL is synthesized                                                                                                                                                    |

The default is the last **exported** frame. Previous/Next use actual available Step neighbors, not indices or inferred timestamps. Explicit selection cannot return later-frame decisions; binding identifiers/codes are filtered by the selected AsOfUtc. Existing report-hash navigation, no-store, unavailable-frame handling and Development-only exposure remain unchanged. Full-report final-verdict labels are omitted for bounded data.

### Bounded v1 reader limits

This distinct small format is limited to **8 MiB for the entire source, 128 exported frames, JSON depth 64, 4,096 preserved-reference identities, 128 rules/diagnostic codes/record IDs, 32 root properties and 4,096 characters per projected string**. The complete bounded document is parsed within that limit to resolve native `$id`/`$ref` sharing. Duplicate properties/IDs, forward or unresolved references, mismatching identity, non-UTC or nonchronological frames/evaluations, invalid enums, incompatible window/prefix bounds and expected-hash mismatch fail safely. No forward-reference lookup can import later frame objects into an earlier selected projection.

Only the current source objects and authentic scalar fields are projected. The envelope's prefix count and scope metadata are validated for structural consistency, not recomputed by replay. Neither path certifies all nested strategy provenance: EvidenceValidation remains `NotRevalidated`. One concurrent read and read-only FileShare.Read remain shared with the v2 reader. Full v2 retains its existing incremental processing and limits; it is not loaded as an 8 MiB document.

### Verification and June compatibility

Real local HTTP verification used the protected v1 source and its expected digest: default plus Steps 7591, 7594, 7595 and 7596. At 13:34Z WF2-001 is Passed while WF2-002 is Waiting; at 13:35Z both are Passed; at 13:36Z SL/risk remain Waiting, blocker NQ-SL-001, verdict Wait. All values came from the unchanged artifact. This is pre-entry diagnostic presentation, not a reproduced trade or authorization of the earlier mentor entry. USTEC/US100 reconciliation and S14 temporal/reference gaps remain unresolved.

The existing local June file `C:\MoneyWay\session\report.json` was also read through the API: 9,660 frames, NoTrade final; Step 7829 remains 13:29Z / 08:29 Bogota, Wait, NQ-TIME-003 Waiting. This currently available file is 187,142,873 bytes with SHA256 `6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8`, distinct from the earlier 178,140,291-byte source recorded above. Both historical records are retained; do not pin one file to another file's hash. To return to the current June source in Git Bash, stop the API and set:

```bash
export NasdaqHistoricalDemo__ReportPath='C:/MoneyWay/session/report.json'
export NasdaqHistoricalDemo__ExpectedSha256='6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8'
dotnet run --project src/backend/MoneyWay.Api --launch-profile http
```

Automated endpoint/component verification uses explicitly synthetic transport fixtures. Real API reads are separate from browser interaction; no browser inspection is claimed for this feature. The user's previously verified June browser workflow remains the regression baseline.

Automated validation for this feature: 61 endpoint integration cases passed (legacy v2 plus 22 new bounded-v1 cases), failed 0, skipped 0; 27 React/component cases passed (including two new bounded cases), failed 0, skipped 0. The solution build, frontend production build, lint, formatting and documentation/diff checks complete the validation gates; real API verification does not constitute browser verification.
