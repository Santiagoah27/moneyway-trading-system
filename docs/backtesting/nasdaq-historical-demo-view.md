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

`GET /api/nasdaq/historical-demo` returns `NasdaqHistoricalReportSummary` with source session ID, explicit strategy/version/provider/instrument/day, report frame count/time bounds/final verdict, content SHA-256/size, validation scope, diagnostic codes and one `SelectedSessionLastFrame`.

The selected frame is the last persisted frame on the report's declared trading day in America/Bogota, which can differ from the complete report's last frame. Both results are labeled separately. Rule IDs, definition statuses, evaluation results, ordering, required flags, reasons, timestamps and evidence IDs are copied from that frame. Required non-Passed/non-NotApplicable decisions are shown as outstanding conditions; the canonical verdict and blocker are never recalculated from them. Optional decisions remain inspectable.

Snapshot state and `NasdaqHistoricalFactualTradeEvaluation` availability/human-review requirement are independent of the strategy verdict. Missing/unsupported optional historical fields stay unavailable. No entry, SL, TP, economic outcome or setup classification is synthesized. Input/binding diagnostic codes are shown without exposing their local file paths.

## Bounded access and validation

Infrastructure reads one JSON property/frame at a time with an incrementally filled buffer; it never deserializes all frames into a collection. It retains only bounded header fields, the selected frame and small current-frame state. Limits are 1 GiB source, 8 MiB per property/frame, depth 64, 32 root properties, 128 rule decisions/missing rule IDs/distinct diagnostic codes, and 4,096 characters per projected string. Exceeding supported limits returns unavailable, not a fabricated result.

Reads are serialized per API instance, cancel with the request and use no projection cache. File access is read-only with `FileShare.Read`. Only a configured absolute local path is accepted; UNC paths, URL paths, browsing and query parameters are rejected. No source paths, raw evidence bodies or per-frame lists enter the response. `Cache-Control: no-store` applies to success and errors.

Validation checks supported schema, required projection fields, canonical enum values, chronological frame steps/timestamps, rule ordering/uniqueness, declared counts/time bounds and final frame/verdict agreement. SHA-256 is computed over the complete source and compared with the configured expected digest when supplied. This is **not** strategy re-evaluation or full provenance certification: compressed evidence hashes/references and every nested canonical fact are **not revalidated** (`EvidenceValidation = NotRevalidated`). The original harness/writer and its validations remain authoritative.

| Response | Meaning |
| --- | --- |
| 200 | Supported report read; canonical/factual availability still comes from source |
| 400 | Query parameters supplied; no caller-selected paths |
| 404 | No configured report, missing file, or endpoint absent outside Development |
| 422 | Invalid/incomplete/unsupported report, exceeded read limits or expected-hash mismatch |
| 503 | Invalid location configuration or inaccessible report |

Errors contain a stable code and generic message, without exception text or filesystem details. Generate a complete report before configuring it; an actively written or truncated file is unavailable. Refresh the page after replacing/configuring the report. No Run button, replay queue or persistent run manager is provided.

## Functional verification

For an existing source, record its SHA-256 before/after requesting the endpoint and opening the view. Confirm the session, report identity, exact canonical result and unavailable evidence match the source. Temporarily unconfigure the source or configure a nonexistent path to check unavailable handling, then restore your configuration. The real report stays outside Git; automated tests use synthetic canonical fixtures and the existing 9,660-frame report-shape fixture.
