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
Original artifacts remain external references; no artifact bytes or hashes are
invented. Documents are not merged into one canonical completion assertion.

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
   reference or HTTPS artifact reference, original source clock/time and precision
   if known. A reference alone does not establish verified artifact bytes.
4. Save partial preparation. Record MoneyWay ID, revision/hash, receipt clock and
   missing-documentation/admission messages. Reload the record, then use a new
   revision for corrections with an explicit reason. Never rewrite the original.
5. Restart the API using the same directory; recover the session and both the
   original and latest preparation. Verify exact values and distinct receipt times.
6. Continue in the existing Standard canonical diagnostic panel. A configured
   admissible run/frame is required; unavailable data must remain unavailable.
   Preparation capture alone supplies neither a canonical signal nor entry approval.
7. Use existing execution evidence only to document an actual human-observed
   operation, preserving its separate source and unresolved requirements. No form
   sends an order. After an evidenced own execution, explicitly confirm ownership
   and the subsequent actual closure/type/reason/source in the session panel.
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

## Actual acceptance and limits

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

The next functional feature should attach immutable original artifact bytes and
their SHA256 to a preparation revision during the first operator-led Standard
session, preserving source identity and admission limitations. This makes that
real session reviewable without pretending hashes establish historical UTC or
automatically selecting structural references.
