# Nasdaq historical report in the supervised local demo

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
