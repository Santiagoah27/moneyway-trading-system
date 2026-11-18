# Nasdaq Standard prospective preparation

## Scope and contracts

The Standard supervised session now accepts partial human preparation reports in
the historical/demo UI. Direction/context use the existing human H4 vocabulary;
structural and liquidity references preserve human selections, exact supplied
decimal strings, scalar versus zone coordinates and original source identity.
Unknown prices, native identifiers and times remain absent. MoneyWay reference
identifiers are documentary identifiers, never broker-native identities.

This implements the capture boundary in [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md).
The existing [preparation input contract](../decisions/0006-define-nasdaq-preparation-replay-input-contract.md)
still owns canonical completion. `NasdaqPreparationCompletionObservationSeries`
receives zero observations from these unauthenticated reports. The existing
`NasdaqHumanH4ContextObservationSelector` additionally needs admitted closed H4
members and structural facts. It is not invoked against these documentary forms:
the API reports `NotEvaluated_AdmissibleSourceAvailabilityAndCanonicalMarketContextMissing`.
No replacement selector, verdict calculator or strategy rule was added.

Human-complete documentation, including `confirmed`, does not satisfy the typed
canonical completion contract. `AdmissionResult=DataUnavailable`,
`CanonicalAdmissionPermitted=false`, with no preparation rule result or verdict.
Original-source verification, authenticated UTC and canonical availability remain
unestablished. Later corrections never enter earlier canonical frames.

## Receipt and persistence

`ReceivedAtUtc` is the actual infrastructure service-entry wall-clock sample,
after bounded JSON parsing and before validation/locking. It is not socket-arrival
time or source publication time. `RegisteredAtUtc` is another ordinary clock sample
before atomic publication, not the exact disk-commit instant. Their clock source
is recorded. Operator-reported times are independent editable source assertions;
they cannot set these server fields or authoritative `ObservedAtUtc`.

The existing local store/lock/atomic rename writes immutable `.prep` envelopes,
SHA256 record hashes, revision chains, idempotent submission IDs and optimistic
revision hashes. Submission sequence orders successful preparation publications
within this bounded store; it is not a native market sequence or the receipt
order of concurrent requests. Hashes protect local integrity, not source truth.
External source references remain supported. The artifact extension below also
preserves selected original PNG bytes, with actual server-calculated hashes.
Documents are not merged into one canonical completion assertion.

Session identity/version comes from the persisted Standard session. Provider and
symbol, when supplied, must match exactly. US100 and USTEC cannot be substituted.
The operational scope does not support silently aliasing TradingView NAS100USD.
Partial capture may leave original source fields unknown. WF2 is independent.

The Development/localhost endpoints share the existing 64 KiB strict JSON and
`X-MoneyWay-Local-Capture: 1` write guard:

- `GET /api/nasdaq/supervised-sessions/{key}/preparation`
- `POST /api/nasdaq/supervised-sessions/{key}/preparation`
- `GET /api/nasdaq/supervised-sessions/{key}/preparation/{id}?revision=1`
- `POST /api/nasdaq/supervised-sessions/{key}/preparation/{id}/revisions`

Creation uses `{submissionId,evidence}`; correction adds `expectedRevision`.
Public receipt/admission/strategy-version overrides are rejected. Inspection and
documentary corrections remain available after terminal closure; operational
EntryIntent, ActivateSetup and ConfirmExecution still reject with HTTP 409.
See [session ownership/closure](nasdaq-supervised-session-termination.md) and
[execution capture](nasdaq-supervised-execution-capture.md).

## First authentic session runbook

Use the next session independently verified eligible by the operator; this UI
does not establish the market calendar. From the repository root, start the API:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5097'
$env:NasdaqExecutionEvidence__Directory=Join-Path $env:LOCALAPPDATA 'MoneyWay/supervised-execution-evidence'
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Api
```

In another terminal from the repository root:

```powershell
Set-Location src/frontend/moneyway-web
$env:VITE_API_BASE_URL='http://localhost:5097'
npm run dev -- --host localhost --port 5173
```

Open `http://localhost:5173/#nasdaq-historical-demo`. API/frontend are sufficient
for documentary capture; Worker and MT5 are not required. Market observations
still require the operator's original source. These commands neither acquire
market data nor make current-market signals available.

1. At 08:00 America/Bogota, open/select Standard with the actual Bogota date,
   original provider and supported exact instrument. Do not borrow another
   instrument's prices or invent a source to fit the form.
2. In preparation, report only observed H4 directional/context interpretation,
   human-selected HH/LL or other supported structural references and relevant
   liquidity references. Leave missing levels/IDs/times unknown. Mark human
   validation explicitly. No automated geometry or session boundaries are inferred.
3. Preserve original screenshots/exports separately. Supply a meaningful `note:`
   reference or HTTPS source reference, original source clock/time and precision
   if known. Save partial preparation, then select an original supported PNG in
   **Artefactos originales**. Confirm its actual provider/instrument, describe the
   source, select H4 or an existing structural/liquidity reference where appropriate,
   and give a revision reason. Leave unknown original times unknown. Click
   **Preservar adjunto en nueva revisión**; no manual JSON editing is required.
4. Save partial preparation. Record MoneyWay ID, revision/hash, receipt clock and
   missing-documentation/admission messages. Reload the record, then use a new
   revision for corrections with an explicit reason. Never rewrite the original.
5. Click **Inspeccionar original**, check SHA256/size and preview, then download
   the original bytes. Restart the API using the same directory; recover the
   session and preparation. Add a second artifact through another revision.
   Use **Número de revisión de preparación** / **Ver revisión de preparación**
   to inspect an intermediate revision; original submissions and attachments must
   remain unchanged. Verify exact values and distinct receipt times.
6. Continue in the existing Standard canonical diagnostic panel. A configured
   admissible run/frame is required; unavailable data must remain unavailable.
   Preparation capture alone supplies neither a canonical signal nor entry approval.
7. Perform a supervised paper/demo operation only when permitted by the approved
   workflow and separately validated requirements. No operation is required when
   no valid setup exists. Use existing execution evidence only to document an
   actual human-observed operation, preserving its separate source and unresolved
   requirements. No form sends an order. After an evidenced own execution,
   explicitly confirm ownership and the subsequent actual closure/type/reason/source
   in the session panel.
8. Recover the terminal session after restart. Inspect the lock and rejection
   check; new operational setups/attempts are prohibited. Documentary corrections
   remain available without reopening the session.

Preparation remains `[08:00,08:30)` Bogota in the existing evaluator. Documentary
saving is not restricted to that window; different dates and terminal submissions
are labelled report-only. Human trading guidance 08:30–11:30 conflicts with runtime
`[08:30,11:00)` (historical implementation `0d1ffe7`, Video 3 approximately
04:15–04:30). The UI exposes both. Runtime entry-intent guards remain unchanged;
no late-window eligibility is asserted. Correction requires a candidate version,
independent backtest, human approval and activation, never editing ACTIVE metadata.

## Original artifact capture

This extension reuses the same local store, process lock, atomic publication,
immutable preparation revision hashes and session guard. No suitable screenshot
upload store existed; no generic storage framework or database was introduced.
`NasdaqPreparationArtifact` records server-owned ID, exact length/SHA256, related
session/record/origin revision, source annotations and ordinary service receipt.
The ID is internal, never broker-native. The original content type is retained
and restricted to `image/png`.

Policy: at most 4 MiB/file, 16,777,216 pixels, dimension at most 16,384, eight
artifacts/record and 128 blobs/store including unreferenced interrupted uploads.
Only noninterlaced 8-bit RGB/RGBA PNG screenshots are supported. The validator
checks signature, bounded chunk structure, CRCs, end marker/no appended bytes,
allowed chunks and the bounded decompressed scanline length/filter values.
SVG/HTML/PDF/JPEG/executables are unsupported. The original bytes are not
reencoded. Display filenames are bounded safe ASCII `.png` names; paths,
reserved names and control characters are rejected. Internal storage paths never
come from filenames supplied by callers.

New Development/localhost operations:

- `POST /api/nasdaq/supervised-sessions/{key}/preparation/{id}/artifacts`
  accepts strict JSON `{submissionId,preparationRevision,expectedRevision,source,bytes}`,
  where `bytes` is base64. Wire limit 6 MiB; decoded file limit 4 MiB. Existing
  non-upload JSON endpoints retain their 64 KiB limit and local-capture header.
- `GET /api/nasdaq/supervised-sessions/{key}/preparation/{id}/revisions/{revision}/artifacts/{artifactId}`
  verifies the original blob before returning its metadata.
- The same route plus `/content` returns exact bytes as an attachment, with
  `image/png`, `nosniff`, sandbox CSP and no-store. No arbitrary path/query is accepted.

Attachment consumes an exact persisted base revision and atomically publishes a
new preparation revision inheriting all previous artifacts. Source/provider and
instrument must match the session exactly; an association refers to H4, an existing
local documentary reference or general preparation. New attachments do not save
unsaved form edits. Ordinary annotation corrections inherit the artifact list.
Committed attachments cannot be removed or silently replaced; contradictory
annotations can be explained in a later documentary correction while retaining
the original claim and bytes.

The immutable blob is published before its referencing preparation revision. If
the second publication is interrupted, earlier revisions remain intact; an orphan
blob has no retrieval route. Retrying the identical submission verifies/reuses it
before committing the new revision. Partial temporary files are never exposed.
Conflicting content is rejected. Reads recheck exact byte length and SHA256.
Old `.prep` envelopes without an `artifacts` field remain readable with their
original raw-record hash. No original market data/export/checkpoint was modified.

The frontend sends user-selected bytes and assertions, renders only a verified
PNG blob URL, and independently checks downloaded length and SHA256 before preview.
It preserves identical uncertain retries, rejects stale revision conflicts and
provides the original-byte download. No uploaded text/HTML or image metadata is
executed or interpreted. No OCR, AI analysis, OHLC extraction or signal derivation
is performed.

Artifact receipt is an ordinary service-entry clock sample after bounded JSON
parsing, before image validation and publication. Source times remain human
annotations; no filesystem timestamps or upload times establish market time.
Preserving bytes and their hash establishes neither depicted-market truth,
independently verified instrument/provider nor historical availability/admission.
Attachment satisfies only the missing documentary artifact reference; other
preparation requirements remain independent. There is no single inferred original
source hash: actual individual artifact hashes are in `record.artifacts`.
The selector stays NotEvaluated, admissible completions zero and admission
DataUnavailable; no Passed/Ready/verdict/entry is generated. The 11:00/11:30
conflict and ACTIVE strategy definitions remain unchanged.

Actual synthetic HTTP acceptance used isolated
`%TEMP%/moneyway-artifact-http-d34e62fa`; proof is
`%TEMP%/moneyway-artifact-http-proof-d34e62fa.json`.
Preparation `9302193f-38ba-4175-adad-cdfa6fe17135`, first artifact
`8fd0b3f0-f747-4a8e-bc9a-c70df5dc5d29`, has 71 exact bytes and SHA256
`6fe8b897998e41db82f635c2bc1ee3c552369debf7fe862f043f6c6feb2bb60b`.
It is a two-pixel synthetic red/blue test image, not market evidence. Downloaded
bytes were independently checked before and after an actual API process restart.
Revision 3 added another synthetic artifact without changing revisions 1/2 or
their hashes. A post-terminal attachment created revision 4; EntryIntent,
ActivateSetup and ConfirmExecution each remained HTTP 409. No authentic session
or authentic market preparation was created on behalf of the operator.

Manual browser acceptance: follow steps 1–8 above, also checking safe preview,
download/hash, intermediate revision recovery, unknown original times, rejected
wrong-symbol/non-PNG uploads, and terminal documentary attachment without reopening.
An external market-data platform is needed to observe actual H4/liquidity and
capture original screenshots. API/frontend suffice for upload/inspection/restart;
neither MT5 nor Worker is required for these documentary actions. Browser tooling
was not available and the unsupported integrations were not retried; visual/manual
acceptance is pending.

Artifact feature validation: 25 new backend artifact cases and six new frontend
cases. Final nonoverlapping runs passed 2,124 Application Nasdaq/replay tests,
325 nonlocal Nasdaq integration tests, six local authentic-report regressions,
35 backtesting/replay cases and 19 Domain replay cases: 2,509 .NET passed,
zero failed/skipped. All 70 frontend tests passed with an invocation-only
15-second timeout. The earlier focused run had 45 passed and one intentionally
environment-gated authentic test skipped; that authentic test subsequently ran
and passed in the six-case local run. No test assertions or repository timeouts
were relaxed. Original June/report #2/adapters/manifest hashes listed below were
independently rechecked and remain unchanged.
Full .NET/frontend builds, ESLint, Prettier and scoped C# whitespace verification
passed; .NET reported zero warnings/errors. Four local documentation links and
`git diff --check` were validated. No browser/visual acceptance is claimed.

## Initial prospective-capture acceptance (af50381)

Only synthetic documents were created by development acceptance, in isolated
`%TEMP%/moneyway-prospective-http-20261118-83c5`; no real preparation was submitted
on behalf of the operator. Local HTTP proof is
`%TEMP%/moneyway-prospective-http-proof-83c5.json`.
Session `3a67bcbba8c132d3fa2d91969be17e665660c21a8dc1f20ce3afba6760aa9c12`,
preparation `b5e86224-27a6-4345-b877-9546b74f5107`: partial revision 1 and corrected
revision 2 survived an actual API process restart, with original hashes intact.
First ordinary receipt was `2026-11-18T19:23:03.7578556+00:00`.
Both returned zero admissible completion observations and no rule/verdict.
After explicitly synthetic owned execution/closure, revision 3 was accepted as
`TerminalSession_DocumentaryOnly`; valid EntryIntent, ActivateSetup and
ConfirmExecution requests each returned 409. Another process restart preserved
terminal state and revision 3.

Browser tooling is unavailable; no visual acceptance is claimed and unsupported
browser integrations were not retried. Execute the manual runbook, including
keyboard/mobile inspection and save/reload/revision interactions.

Validation: 300 nonlocal Nasdaq integration tests passed, including 23 new
preparation cases; six local authentic-report regressions passed, including the
new later-preparation correction check. Backtesting/replay: 35 passed; Domain
replay: 19 passed. Frontend: all 64 passed with an invocation-only 15-second
timeout. Two earlier full frontend runs each had 63 passed and the same existing
monetary-outcome interaction exceed its default 5-second timeout; assertions and
repository timeout configuration were not weakened. Full .NET build passed with
zero warnings/errors. Frontend build, ESLint, Prettier and scoped C# whitespace
verification passed. The Application Nasdaq/replay suite passed all 2,124 tests.
The final nonoverlapping .NET total is 2,484 passed, zero failed/skipped; frontend
adds 64 passed. Fifteen local documentation link targets and `git diff --check`
were also validated. Earlier focused runs overlap these totals.

Independently rechecked protected SHA256 values:

| Source | SHA256 |
| --- | --- |
| June research report | `6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8` |
| June validated adapter | `944bd132b73ad061572a752ddcefc187582812c60a88dccf056bdc4e18806eba` |
| June registered documentary manifest | `12684556c3a095ead00d1138b41902b9aba1844cf315383ec5ed87c966665155` |
| Replay #2 original report | `48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db` |
| Replay #2 validated adapter | `8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2` |

June remains the original 9,660-frame NoTrade research report with no package
admission. Replay #2 remains independent and incomplete; no raw acquisition,
normalization or historical source modification was performed. Authentic report
tests compared pinned earlier frames before and after later synthetic preparation
corrections. Existing replay prefix tests cover extension with future frames;
documentary storage is never queried by a deterministic evaluator.

Usable prospective capture is delivered. Authentic human preparation has not yet
been demonstrated. Canonical admission, an authentic causal Standard run, current
market signals and full demo readiness remain blocked by missing admitted source
evidence and human requirements. Existing documentary execution, reported outcomes,
closure and no-reentry remain separate working capabilities. December 18 readiness
cannot be claimed from capture alone.

The artifact extension now makes the next operator-led Standard session reviewable
using preserved original bytes. Next priority: conduct the first authentic
supervised Standard session using this runbook and correct concrete product
defects found during that use. No further generic storage/timestamp project is
required to perform documentary preparation. Real Standard canonical readiness
and current-market signals still require independently admissible source inputs;
neither an authentic causal run, manual browser acceptance nor a complete
supervised session has been demonstrated by these synthetic checks.
