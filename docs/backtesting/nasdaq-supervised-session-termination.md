# Nasdaq supervised session termination and no reentry

Current extension: [Standard diagnostic-to-attempt integration](nasdaq-standard-diagnostic-attempt.md).
The current human 11:30 instruction conflicts with the repository's earlier
11:00 re-review/runtime. That extension exposes the conflict and preserves runtime
behavior; the implementation-window descriptions below are not new human approval.

Implementation checkpoint: 2026-11-18, initial clean synchronized `main`
`268d39976ad9aa1918f90ff66dbab9039873cc07`.

## Approved scope and boundaries

Current explicit human decision: one actual MoneyWay trade per Nasdaq session;
after its evidenced closure, no reentry, new setup or second trade in that
session. Closure may be TP, SL, BE, protected or another evidenced exit. Protected
1:1 is a declared closure category, never a calculated realized return.
See [specification section 20](../strategies/nasdaq/strategy-specification.md#20-reentries)
and [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md).

`NasdaqSupervisedSessionPolicy` is an application-level operational projection,
separate from canonical `MoneyWayNasdaqReplayLifecyclePolicy` setup expiry and
invalidation. It does not implement a new replay evaluator. Original canonical
observations, AsOfUtc, Standard M5/M1, WF2-001/002, SL/risk/TP and ACTIVE strategy
versions remain unchanged. No MT5, clock, broker or order sender is involved.
Native execution verification remains false, calculated PnL null and complete
supervised trade false. Existing temporal admission still prevents unauthenticated
current market observations from becoming canonical live signals.

## Identity, time and lifecycle

The existing `NasdaqDemoSessionIdentity` supplies strategy/version, explicit
known provider, original US100 or USTEC symbol and America/Bogota trading date.
The server hashes these exact fields. Labels and Standard/WF2 path cannot create
an alias to bypass the same scope. There is no US100/USTEC mapping. Account scope
remains unknown: conservative local workflow enforcement, no cross-account or
cross-provider enforcement claim. A changed scope is not proof of eligibility.

Open records a scope. ActivateSetup/InvalidateSetup track only local supervised
intent, not canonical setup activation. EntryIntent creates no order or Ready
verdict. New setup/intent requires the current Bogota date and reuses the exact
existing `NasdaqTradingWindowFact` [08:30, 11:00) guard. No weekday, holiday or
new cutoff is inferred. A subsequent date has its own identity and no inherited
terminal flag; preparation, calendar, risk and strategy gates remain independent.

ConfirmExecution records an already performed own supervised trade; it is not
authorization for a new entry and can be registered retrospectively outside the
entry window. It requires explicit operator ownership and a saved exact evidence
record/revision/hash with matching label/date/provider/instrument/path, direction
and opening event. Once registered, a second own trade is refused even before
closure. Standard requires no WF2 facts. WF2 remains SELL only.

ConfirmClosure requires that prior owned record, the original frozen opening,
an actual closing event with matching original instrument, explicit same-trade
association/source, operator closed confirmation, closure category/reason and
provenance. Missing event, stop-protection commentary, known position/direction
conflict or reversed exact chronology cannot establish closure. Approximate
original close times retain their precision. TerminalRegisteredAtUtc is solely
the operational registration time, never a backdated exact historical fill.

Terminal rejects operational mutations; exact submission retries are idempotent.
There is no reopen action. Original evidence can still be read/corrected through
immutable documentary revisions. A mentor entry/exit, reported PnL, SL/TP selection
or failed setup alone never registers MoneyWay ownership or terminality.

## Storage and API

`LocalNasdaqExecutionEvidenceStore` reuses its approved directory, exclusive
capture.lock, same-directory pending file, Flush(true) and atomic non-overwriting
publication. Bounded `session-{key}.{revision}.session` envelopes preserve commands,
raw payload SHA256, previous hash, references and registration times. They are
separate from existing documentary `.json` revisions. Limits: 100 events/session,
1,000 total operational events, 64 KiB/event. Concurrent writers conflict safely;
retry the same submission ID. Tampered/incomplete chains fail closed. Hashes
prove payload integrity, not broker authenticity or a malicious local writer.

Development/loopback only, same strict JSON, origin and
`X-MoneyWay-Local-Capture: 1` write guard as [capture](nasdaq-supervised-execution-capture.md):

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/nasdaq/supervised-sessions` | `{submissionId,session:{sessionId,tradingDay,providerId,symbol,path,reviewer,sourceReference}}` |
| GET | `/api/nasdaq/supervised-sessions?tradingDay=2026-11-18` | List exact date |
| GET | `/api/nasdaq/supervised-sessions/{key}` | Recover state and original events |
| POST | `/api/nasdaq/supervised-sessions/{key}/actions` | Explicit command with submissionId/action/reviewer/sourceReference |

Confirmation commands reference `execution:{recordId,revision,revisionHash}`.
ConfirmExecution additionally requires `moneyWayOwnedExecutionConfirmed:true`;
ConfirmClosure requires `moneyWayTradeClosedConfirmed:true`, closureKind and reason.
Accepted kinds: TakeProfit, StopLoss, BreakEven, ProtectedOneToOne, OtherEvidencedExit.
Setup/intent commands carry setupReference. Unknown client status/trust fields
are rejected; clients cannot overwrite state. Terminal rejection is HTTP 409
EvidenceConflict with detail TerminalSession_NoReentryNoNewSetupNoSecondTrade.
All operational entry/setup service calls enforce the guard, independently of UI.

## Product/manual acceptance

In `/#nasdaq-historical-demo`, the session panel works without MT5 or a report.
It opens/lists/recovers sessions, shows date/route/source/status, original events,
owned confirmation and closure provenance, and disables terminal entry/setup.
The explicit rejection check still calls the API guard. Documentary history
remains usable. Pending writes freeze editing and retry the same submission.

1. Run Development API/frontend with an isolated configured evidence directory.
2. Open a Standard session with explicit date, label, provider and original symbol.
   During the established window, register/invalidate a synthetic setup; no own
   trade means no terminal state. Outside the window, verify safe intent rejection.
3. Save a clearly synthetic execution record for the same scope, with entry and
   actual closing event, known original symbol and explicit association/source.
   Select it; the document alone must not terminate the session.
4. Explicitly confirm ownership, then actual closure/type/reason/source. Check
   TERMINAL, no native verification, original approximate time and registration time.
5. Restart API with the same directory; list/recover and check the same terminal
   event/reference/hash. Attempt same/new setup or second trade: HTTP 409.
6. Add a documentary correction and read original revision; view historical report.
7. Open another date: terminal=false, but no entry authorization is implied.
   Verify next valid date/time and all independent prerequisites separately.
8. Check keyboard navigation, 320 px layout and uncertain-save retry.

Browser acceptance is not claimed unless actual browser verification succeeds.

## Validation results

Actual HTTP acceptance used localhost:5097 and the dedicated synthetic store
`C:\Users\Santi\AppData\Local\Temp\moneyway-session-smoke-20261118-afd57738`.
Standard scope key:
`e2b4e5c513c2e5ed82105a90a24d782e6d6da9027cc4413d2c98ce8d4e8b6d3d`.
Synthetic evidence record `816288db-e2a3-4a6b-8c90-15e7f6d17ed0`, revision 1:
`f3273741e4c9b8c59555a9b44caf00bb771b87e27d1b6e38a2abc9840dfbbf13`.
After explicit own execution and closure, terminal revision 3 hash:
`07632f4eee29686907665dc9634ab2534905d336f9290244748acc587db02f50`.
The API process was stopped and restarted; the terminal hash matched. EntryIntent,
ActivateSetup and ConfirmExecution each returned 409 afterward. A 2026-11-19
Standard session returned terminal=false, without granting calendar eligibility.
Original documentary revision remained readable and historical report still
returned HumanValidationRequired. These are synthetic attestations, not broker
fills. Local detailed responses are in `%TEMP%\moneyway-session-http-proof-afd57738.json`;
they are separate from immutable source/report evidence.

All 50 frontend tests passed, including terminal display, explicit ownership,
blocked API attempt, no automatic request/ownership from a selected record and
confirmation reset when selecting a different record revision or session.
Frontend build, ESLint and full Prettier check passed. Browser smoke was attempted:
the browser runtime lacked `@oai/cua/tinyskyAlt`, and Windows fallback lacked
`@oai/sky`. No visual acceptance is claimed; execute the manual steps above.

Final nonoverlapping .NET validations passed: 2,124 Nasdaq/replay Application
tests, 235 nonlocal Nasdaq integration cases (27 operational cases included),
35 related backtesting cases, 19 Domain replay cases, three opt-in authentic
report preservation cases and one full authentic regeneration. Total 2,417,
zero failed/skipped in these runs. Earlier focused runs overlap these totals.
The new local opt-in test confirms an explicitly recorded later own closure
cannot change the authentic earlier canonical frame or final report. The full
.NET build passed with zero warnings/errors; scoped C# formatting, 87 local
documentation links/anchors and git diff whitespace validation passed.

Authentic regeneration produced two byte-identical 5,463,844-byte reports:

- `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-2b3105e3b685401cbc50ae7dd1b9640b.json`
- `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d5c771aa85ce43688bee855506db31db.json`

SHA256 for both: `3fcc6d9155978cb705609140cc6d5901b945f63bd4de41c8dfb8bfc531f2d433`.
All embedded working-tree runtime hashes were independently checked against the
actual files. Original report SHA256 remains
`48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db`.
Original adapter and normalized 1M/5M/1H/4H source hashes match their prior pins;
no raw acquisition or normalization was rerun.

The native canonical replay ran 7,597 frames through 13:37Z and independently
through 13:38Z, asserting identical common strategy/diagnostic/entry/exit/snapshot
prefixes. USTEC 29604.00 remains documentary, no 13:32Z entry eligibility is
established, WF2 later progresses, SL remains HumanValidationRequired and risk/TP
Waiting. Approximate exit has no independent native verification or authentic
realized monetary outcome; CompleteSupervisedTrade=false and final verdict stays
HumanValidationRequired. Reading this report leaves a separate synthetic own
Standard session Open_NoTrade with one open event and no owner/closure.

Reproduce the native regression locally with the validated adapter:

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE='C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
dotnet test tests/integration/MoneyWay.IntegrationTests "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter FullyQualifiedName~ExportReplay2CanonicalPreEntryDiagnosticFromRealHistoricalOutcomes --logger 'console;verbosity=detailed'
Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
```

## Next functional feature

Bind the existing canonical Standard pre-entry diagnostic and its evidence to
the supervised EntryIntent screen, showing every missing prerequisite before an
operator decision. Preserve the human boundary and unavailable live temporal
admission. This operational session feature alone is not Nasdaq Demo v0 completion.
