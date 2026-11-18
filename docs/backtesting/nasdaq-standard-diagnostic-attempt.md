# Standard canonical diagnostic to supervised attempt

2026-11-18 discovery update: the [historical package feature](nasdaq-standard-historical-package.md)
located the authentic June 29 exports and prior 9,660-frame NoTrade research
report. The earlier availability search below was incomplete. This candidate
is now inspectable, but causal availability is not established and no newly
admitted Standard replay or diagnostic-attempt authority is claimed.

2026-11-18. Initial clean synchronized main:
`a58fcb62932a681365d4fec8e45c01392d24e5e9`.

## Reused implementation and boundaries

`RunLocalNasdaqMentorSessionUseCase` imports four CandleSeries and uses the
existing synchronized replay, StrategyReplayContext, IReplayRuleEvaluator,
safe outcomes and GenerateCanonicalMultiTimeframeBacktestUseCase. This feature
consumes its saved output with ReadNasdaqHistoricalReport. There is no new
engine, verdict calculator, single-timeframe replay or evaluator side effect.

The catalog has 34 RuleIds and 18 registered evaluators: preparation, H4 context,
session/structural liquidity, TIME start/end, initiating take, Standard M5,
optional FVG/quality, M1 retracement/realignment/derived eligibility, SL, risk, TP
and both WF2 stages. Registration does not prove adequate runtime evidence or
autonomous geometry. Version nasdaq-0.1.0-draft, evaluators, workflow definitions,
Forex, current-data admission and ACTIVE metadata remain unchanged.

Reuse [historical navigation](nasdaq-historical-demo-view.md),
[capture](nasdaq-supervised-execution-capture.md),
[reported monetary outcome](nasdaq-reported-monetary-outcome.md) and
[session termination](nasdaq-supervised-session-termination.md). Applicable
contracts: [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md),
[ADR 0026](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md)
and [canonical prerequisites](../decisions/0003-define-canonical-replay-prerequisite-gating.md).

## 11:00 versus 11:30 conflict

The current human instruction establishes 08:30–11:30 America/Bogota. Repository
history separately records a human re-review in commit `0d1ffe7`
(docs: correct Nasdaq trading cutoff to 11:00). The
[specification](../strategies/nasdaq/strategy-specification.md) introduction and
section 6.0 and [changelog](../strategies/nasdaq/changelog.md) cite Video 3
approximately 04:15–04:30 and the "zona muerta" rationale, superseding an older
11:30 interpretation. These are retained human-review texts; original media or
transcript was not independently reverified in this task.

Runtime 11:00 is in NasdaqTradingWindowFact.EndUtc,
MoneyWayNasdaqTradingWindowEndEvaluator.TradingWindowEnd, versioned TIME-002
metadata and the TIME-owned lifecycle. The supervised guard reuses that fact;
there is no independent runtime configuration fixing the mismatch.

API metadata and UI expose both claims, retaining actual results without claiming
fully verified timing or entry authorization. No cutoff or 11:30 inclusivity is
inferred. Correction requires a separate candidate version, source reconciliation,
independent boundary/Bogota/DST/replay/workflow/lifecycle validation, human
approval, then explicit activation. No candidate is created or activated here;
existing ACTIVE definitions and earlier observations must remain immutable.

## API and immutable context

Development/loopback only:

`GET /api/nasdaq/supervised-sessions/{key}/diagnostic?frameStep=36&reportSha256=...`

Reads only server-configured NasdaqHistoricalDemo:ReportPath and optional
ExpectedSha256. Callers cannot choose paths or authority flags. Omitted frame
uses the existing reader's last available session-day frame. The returned digest
pins the content; later reads/writes verify it. Changed, missing, invalid,
noncanonical or conflicting reports fail closed, without substitution.

ReadNasdaqSupervisedDiagnostic requires exact strategy/version, provider,
original instrument and Bogota date. It returns actual rule statuses, sequence,
reasons/EvidenceId, missing-required evaluations, available source provenance,
actual verdict, operational session state and exact-version workflow dependencies.
Dependency declarations do not recalculate prior-frame progression. Explicit
earlier selections do not carry a later SelectedSessionLastFrame.

The session's Standard label is operator-selected. CanonicalPath is separate,
including Unresolved before sufficient branch evidence. A WF2 path, passed
WF2-002 or Standard M5 NotApplicable cannot be relabeled Standard. A missing
admissible Standard run remains unavailable.

NasdaqCanonicalDiagnosticReference holds SessionKey, ReportSha256, FrameStep
and AsOfUtc only: no passed/ready/eligibility field. Existing capture creation
validates the reference and exact documentary identity. Immutable revisions
cannot drop, replace or move a stored association; use a separate record for
another context. Corrections with an unchanged stored reference remain possible
if the report disappears, without claiming current diagnostic availability.

Existing EntryIntent can carry the verified reference. It is local supervised
intent, never canonical permission or execution. Existing date/window and terminal
guards still apply. Terminal rejection dominates new attempts even if the report
disappears; idempotent same-submission behavior is retained. Documents/history
remain readable and correctable after terminal closure.

Report hashes identify bytes, not authentic fills or historical availability.
EvidenceValidation retains the reader's existing value, including NotRevalidated.
USTEC/US100 are not mapped. Unadmitted MT5 receipt JSON cannot become a canonical
report. MT5 temporal admission, AsOfUtc, clocks and broker adapters are unchanged.

## Frontend and manual visual acceptance

On /#nasdaq-historical-demo, open/recover Standard and load its configured frame.
The table displays actual statuses/reasons, required flags, evidence, dependencies,
missing evaluations and provenance limitations. Canonical verdict, operational
lock and specification conflict are separate.

Vincular frame a evidencia documental copies the reference and only its
session/date/provider/instrument/Standard label into the existing draft. It fills
no direction, price, SL, TP, risk or permission. The form link retains context.
After restart, recover the record, use its scope and Recuperar frame vinculado
al registro. Exact hash/Step/time reloads or becomes explicitly unavailable.
Digest remains pinned across failed reloads/retries. Documentary access remains
possible for terminal sessions; operational attempts do not.

No browser-control tool was available in this turn; visual acceptance is not
claimed. Manual acceptance:

1. Start Development API/frontend using the same report/capture directory.
   Open matching Standard scope, load a frame and inspect its reasons/conflict.
2. Link to a documentary draft, supply original human sources/reviewer and save.
   Do not invent missing execution parameters or treat saving as authorization.
3. Restart API, retrieve record and linked frame. Change/move report and verify
   explicit error, not a replacement frame.
4. Using synthetic own-operation evidence only, confirm ownership and actual
   closure. Verify same/new setup and second entry rejection while documents
   remain readable/correctable.
5. Check keyboard, narrow layout and uncertain-save retries.

## Real HTTP evidence and authentic-data limitation

The available authentic local report is September 8 Replay #2, which is WF2.
No admissible authentic Standard example was established. Actual HTTP loading
Replay #2 into a synthetic Standard scope returned 409; the historical report
still returned HumanValidationRequired and the own session remained nonterminal.

Positive HTTP acceptance used an explicitly synthetic canonical run, generated
from existing fixture CSVs by the real multi-timeframe use case. The fixture
NASDAQ label was consistently renamed to synthetic US100 for the supported
supervised scope; human inputs were deliberately absent. This is not authentic
US100 data, a mentor review or live analysis.

Artifact: `%TEMP%\moneyway-standard-diag-http-20261118-1\synthetic-standard-canonical.json`.
SHA256: `205f1b0f955549b9272d9ce1f204d3f558374a099d75d33b1d42e92544e418c5`.
Run synthetic-us100-canonical-no-human-evidence; frame 36 at
2026-10-03T15:10:00Z; CanonicalPath Unresolved; actual verdict NoTrade.
Preparation Failed, H4 HumanValidationRequired, liquidity/time Passed and
M5/M1/SL/risk/TP Waiting are actual evaluator results, not filled approvals.

HTTP localhost:5097 opened Standard key
`7e0585ddbe39cb14954e78156a13d32f817cae076e0c829dfa52657dff049bfe`,
saved synthetic record `07347060-fe1b-4261-9e66-6abab5880397` with the reference,
and confirmed its canonical frame was unchanged. Explicit synthetic own
execution/closure then terminated that session. Actual API stop/restart recovered
the reference/state; EntryIntent with diagnostic and ActivateSetup returned 409.
A documentary correction created revision 2 retaining the reference and original
revision 1. Canonical verdict stayed NoTrade. Terminal hash:
`970d3aa9db557596724af6d7b287f6c575008ed0ea51ab2b4e90d8ca8c52b951`.
Detailed local responses are in the same directory, `http-proof.json`.

Reproduce a synthetic artifact into a new file in an existing absolute directory:

```powershell
$env:MONEYWAY_STANDARD_DIAGNOSTIC_SMOKE_EXPORT='C:\absolute\existing-directory\new-synthetic-report.json'
dotnet test tests/integration/MoneyWay.IntegrationTests --filter FullyQualifiedName~FocusedSyntheticCanonicalArtifactCanBeExportedForRealHttpAcceptance
Remove-Item Env:MONEYWAY_STANDARD_DIAGNOSTIC_SMOKE_EXPORT
```

## Validation

Passed nonoverlapping .NET sets: 2,124 related Application tests, 256 nonlocal
Nasdaq integrations (21 new focused cases included), 35 related backtesting
cases, 19 Domain replay cases and four opt-in authentic report regressions.
Total 2,438; zero failed/skipped in these final sets. Focused/repeated runs overlap
these totals. All 55 frontend tests, full .NET/frontend builds, ESLint, frontend
Prettier, scoped C# whitespace checks and git diff whitespace validation passed.
The full backend build had zero warnings/errors. Local documentation links and
anchors were checked.

The new shorter/extended actual multi-timeframe synthetic replay test proves
earlier selected frame rules/verdict remain identical after appending future
frames; earlier selection carries no later session frame. Four authentic-report
regressions verify documentary/monetary/operational closure cannot rewrite the
original entry or final report, and WF2 cannot be relabeled Standard. Replay #2
still has USTEC 29604.00, no original 13:32Z eligibility, SL HumanValidationRequired,
risk/TP Waiting, approximate unverified exit and CompleteSupervisedTrade=false.
Authentic report SHA256 remains
`48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db`;
adapter SHA256 remains
`8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2`.
No raw acquisition, normalization or full native report regeneration was performed
in this feature. Existing authentic artifacts were preserved, read and tested.

## December 18, 2026 delivery review

| Area | Demonstrated / remaining |
| --- | --- |
| Standard coverage | Existing human-assisted gates/diagnostics; runtime evidence and subjective selections still required |
| Admissible data | Reproducible synthetic positive integration; authentic local Replay #2 is WF2 |
| Diagnostic-to-attempt | Functional read, pinned reference, documentary form and existing local intent association |
| Operational entry | Explicit own execution recording; no native verification or automatic orders |
| Lifecycle/no-reentry | Durable own closure and API-level terminal lock retained |
| Economic outcome | Reported-only; no calculated PnL or invented compliance classification |
| Human/specification | Exact sources/availability required; timing conflict needs separate candidate lifecycle |
| UI acceptance | Component/HTTP/restart evidence; manual visual acceptance pending |

ONE next feature: bind one human-approved source-backed Standard historical
session input pack to the existing canonical harness and expose that run here,
preserving exact availability and unresolved parameters. This supplies authentic
Standard evidence for the supervised flow. Manual record saving alone does not
complete Nasdaq Demo v0.
