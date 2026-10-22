# Nasdaq historical report in the supervised local demo

## Generate or reuse a report

Reuse a legitimate SchemaVersion 2 report written by `NasdaqMentorSessionReportJson.Write` through the [official local harness](nasdaq-mentor-session-local-run.md). Set `MONEYWAY_MENTOR_REPORT_PATH` when generating it. This view reads an existing report; it does not run replay, accept uploads or execute orders.

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

Infrastructure reads one JSON property/frame at a time with an incrementally filled buffer; it never deserializes all frames into a collection. Each selection uses one complete incremental pass, including integrity validation, and retains only bounded header fields, the selected frame and small current-frame/navigation state. The default response duplicates the same last-frame projection under its legacy and new property names; explicit selection contains only the selected frame's decisions. Limits remain 1 GiB source, 8 MiB per property/frame, depth 64, 32 root properties, 128 rule decisions/missing rule IDs/distinct diagnostic codes, and 4,096 characters per projected string. Exceeding supported limits returns unavailable, not a fabricated result.

Reads are serialized per API instance, cancel with the request and use no projection cache. File access is read-only with `FileShare.Read`. Only a configured absolute local path is accepted; UNC paths, URL paths, browsing and caller-selected paths are rejected. Only `frameStep` and `reportSha256` query parameters are supported. No source paths, raw evidence bodies or per-frame lists enter the response. `Cache-Control: no-store` applies to success and errors.

Validation checks supported schema, required projection fields, canonical enum values, chronological frame steps/timestamps, rule ordering/uniqueness, declared counts/time bounds and final frame/verdict agreement. SHA-256 is computed over the complete source and compared with the configured expected digest when supplied. This is **not** strategy re-evaluation or full provenance certification: compressed evidence hashes/references and every nested canonical fact are **not revalidated** (`EvidenceValidation = NotRevalidated`). The original harness/writer and its validations remain authoritative.

| Response | Meaning |
| --- | --- |
| 200 | Supported report read; canonical/factual availability still comes from source |
| 400 | Invalid/duplicate/unknown selection parameters or malformed report identity; no caller-selected paths |
| 404 | No configured report, missing file, Step outside the selected session, or endpoint absent outside Development |
| 409 | Report identity changed; discard selection and reload the default frame |
| 422 | Invalid/incomplete/unsupported report, exceeded read limits or expected-hash mismatch |
| 503 | Invalid location configuration or inaccessible report |

Errors contain a stable code and generic message, without exception text or filesystem details. Generate a complete report before configuring it; an actively written or truncated file is unavailable. Refresh the page after replacing/configuring the report. No Run button, replay queue or persistent run manager is provided.

## Functional verification

For an existing source, record its SHA-256 before/after requesting the endpoint and opening the view. Confirm the session, report identity, exact canonical result and unavailable evidence match the source. Temporarily unconfigure the source or configure a nonexistent path to check unavailable handling, then restore your configuration. The real report stays outside Git; automated tests use synthetic canonical fixtures and the existing 9,660-frame report-shape fixture.

### Frame-navigation check (2026-10-22)

The existing 178,140,291-byte June report was reused without replay execution. An independent incremental JSON scan supplied the reference Steps and decisions; eight API reads were compared against those source values, including neighbors, rule statuses/reasons/evidence IDs, canonical UTC and session ownership. The session contained **1,380 available frames**, Steps **7320–8699**; this count was observed rather than inferred from elapsed minutes.

| Selection | Step | Bogotá timestamp | Selected verdict | Response bytes | Query seconds |
| --- | ---: | --- | --- | ---: | ---: |
| Default last | 8699 | 2026-06-29 23:59 | NoTrade | 12,789 | 2.621 |
| First | 7320 | 2026-06-29 00:00 | HumanValidationRequired | 5,887 | 1.130 |
| Before deadline | 7829 | 2026-06-29 08:29 | Wait | 5,889 | 1.078 |
| Opening | 7830 | 2026-06-29 08:30 | NoTrade | 5,896 | 1.072 |
| Cutoff | 7980 | 2026-06-29 11:00 | NoTrade | 6,791 | 1.099 |
| Explicit last | 8699 | 2026-06-29 23:59 | NoTrade | 6,791 | 1.124 |

Actual Next/Previous queries between Steps 7829 and 7830 also matched source (5,896 / 5,889 bytes; 1.074 / 1.131 seconds). `NQ-TIME-003` was Waiting at 08:29 and Failed at 08:30; `NQ-TIME-002` was Passed before 11:00 and Failed at/after 11:00. These are persisted decisions, not new trading rules or a retrospective session-wide cutoff. Complete-report `FinalVerdict` remained NoTrade throughout.

The standalone API process peaked at **74,620,928 bytes (71.17 MiB) working set** over these eight reads, using the OS peak-working-set counter; final observed working set was 73,138,176 bytes. These are single-machine observations, not allocation measurements or performance guarantees. The source SHA-256 remained the verified value above. Installed Chrome was exercised against the real API/UI for default, Previous/Next, direct selection, invalid Step, first/last boundaries, Bogotá/UTC display and return to last. An early-frame screenshot showed complete-report NoTrade alongside selected-frame Wait. No browser/test artifacts or report data are committed.
