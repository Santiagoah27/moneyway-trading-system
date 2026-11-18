# Nasdaq supervised execution-evidence capture

The capture form also supports [reported monetary outcomes](nasdaq-reported-monetary-outcome.md) through the same immutable revision workflow, separately from calculated or broker-verified results.

Implementation checkpoint: 2026-11-18. Initial clean synchronized `main`:
`1a275c49a987eca838f4ce1b94f39415d55f6428`.

## Product workflow and source boundaries

The existing Nasdaq historical area now includes a functional capture form,
independent of MT5 or report availability. The operator can create, retrieve,
list and correct documentary records. Entry-only and exit-only partial records
are valid; price, deal, position, provider, clock interpretation and closing
instrument can remain absent. USTEC and US100 stay distinct. BUY/unknown direction
can be recorded without generalizing the SELL-only WF2 strategy.

`NasdaqSupervisedExecutionEvidenceDraft` retains original event classifications,
source platform/provider, original instrument/direction, reviewer and references,
original time/clock/precision, optional exact price strings/unit/currency, native
IDs, mechanism and explicit association. `NasdaqSupervisedExecutionEvidenceRecord`
adds server-generated MoneyWay ID, submission identity, revision, registration
UTC and previous-revision hash. MoneyWay IDs are not broker IDs. Reviewer text is
an operator assertion, not an authenticated identity. Registration time is not
historical availability or authenticated Windows/MT5 time.

Only `note:` statements and HTTPS references are accepted. No URL or local file
is opened or inspected. OriginalArtifactSha256 remains null; ArtifactAvailability
is NotInspected_ReferenceOnly. Revision SHA256 covers the registered payload,
not original artifact bytes, broker authenticity or a trusted timestamp.
Caller-controlled trust, artifact hashes, verification and availability fields
are rejected. Source references are preserved, not independently validated.

None / HumanReported / DocumentarySupported preserve the operator's association
assertion and source. DocumentarySupported is not independent verification.
Instrument/direction/position conflicts, missing IDs, approximate ordering and
reversed exact chronology remain visible. Matching supplied native position IDs
remain unverified. No checkbox grants native trust. No SL, TP, sizing, costs,
PnL, valid-strategy loss or rule violation is derived; trade completion is false.

For declared WF2 SELL, the application consumer reuses existing entry/exit causal
selectors and `NasdaqHistoricalTradeLifecycleSummary`, with an optional stored
record ID/revision/hash reference. This is a separate documentary lifecycle,
without rule results, original entry-frame authorization or invented replay Step.
A revision is not relabeled as a canonical frame. Original precision remains in
the record; where an existing exact-exit contract needs an unavailable UTC
mapping, its projection remains unknown with an explicit missing requirement.
No record is inserted into a historical report or StrategyReplayContext.
Strategy definitions, active versions, snapshot assembler, WF2/SL/risk/TP,
market-data admission, MT5 receipts/checkpoints and broker adapters are unchanged.

## Durable local persistence and API

`LocalNasdaqExecutionEvidenceStore` follows the existing checkpoint pattern:
exclusive file lock, same-directory temporary write, Flush(true), atomic rename
and read-back. It uses a separate directory and never invokes the MT5 store.
Default: `%LOCALAPPDATA%\MoneyWay\supervised-execution-evidence`.
An administrator/test can configure `NasdaqExecutionEvidence:Directory` to a
dedicated absolute local directory; HTTP callers cannot choose paths.

Every revision is a new version-1 `<MoneyWayId>.<revision>.json` envelope. Originals
are never replaced. The hash checks the original serialized record bytes, not a
later reserialization that might add optional fields. Reads validate payload
hash, identity and previous-revision chains;
linked, malformed, missing or tampered files fail closed. Exclusive locking and
expected hashes reject concurrent corrections. Retrying an identical submission
returns its original result; changed content under the same submission ID
conflicts. Registration-clock regression prevents publication. Interrupted
`.pending-*` files are not committed revisions and remain for inspection.
Limits: 64 KiB per revision, 100 revisions per record, 1,000 committed revision
files. This is bounded local persistence, not general event sourcing or a
PostgreSQL migration. Existing filesystem permissions remain the trust boundary;
hashes do not authenticate a malicious local writer.

Development-only and loopback-only routes:

| Method | Endpoint                                         | Behavior                                                |
| ------ | ------------------------------------------------ | ------------------------------------------------------- |
| POST   | `/api/nasdaq/execution-evidence`                 | Create with submissionId and evidence                   |
| GET    | `/api/nasdaq/execution-evidence?sessionId=...`   | Latest records in an explicit session                   |
| GET    | `/api/nasdaq/execution-evidence/{id}?revision=1` | Latest or original revision                             |
| POST   | `/api/nasdaq/execution-evidence/{id}/revisions`  | Append with submissionId, expectedRevision and evidence |

Writes require JSON and `X-MoneyWay-Local-Capture: 1`. Unknown/duplicate fields,
arbitrary paths and nonlocal origins/hosts are rejected. Existing development
CORS permits localhost:5173. Production exposes none of these routes. Structured
errors: 400 validation, 404 missing, 409 conflict, 422 stored integrity, 503
unavailable. An uncertain save must be retried with the same submission ID; the
form retains that pending request and prevents edits during an uncertain retry.

## Actual smoke test and manual visual acceptance

The real application ran on localhost:5097 using the isolated synthetic folder
`C:\Users\Santi\AppData\Local\Temp\moneyway-capture-smoke-20261117-65e11190`.
Actual HTTP create, duplicate retry, API process stop/restart, retrieval, revision,
old-revision retrieval and session listing passed. Synthetic MoneyWay record:
`7029fee6-f91f-4af0-bd88-bad8a507ffc6`.
Revision 1 SHA256: `e6dd790ea519df365ab9de89a22455aec2551a2bcb750a58e01de2ec4068e4f3`.
Revision 2 SHA256: `7182c3f2cc52f350bde80fef2094ebb3b6541a1ba5aa90e6474e0b024d7ae035`.
The original entry-only record retains 29604.00 as exact source-display text;
revision 2 adds approximate reported closure, unknown exit instrument and human
association. These are synthetic application-test assertions, not new personal
validation of S14, authentic native fills or Replay #2 conclusions.

Browser automation was attempted but could not initialize: missing
`@oai/cua/tinyskyAlt`; Windows fallback lacked `@oai/sky`. No browser visual
acceptance is claimed. Manual acceptance remains:

1. Run the Development API/frontend and open `/#nasdaq-historical-demo`.
2. Select Nuevo registro; enter session/date, original platform/instrument,
   reviewer, `note:` sources and registration reason. Save an entry-only record
   with absent price/IDs, or explicitly documented values and units.
3. Check ID, original values, missing evidence, unverified status and incomplete
   trade. Restart the API with the same storage directory.
4. Retrieve by ID or session list; add a reported exit, explicit association
   source and correction reason, then save a new revision.
5. Retrieve revision 1 and confirm it is unchanged. Check narrow-screen layout,
   keyboard navigation, validation messages and uncertain-save retry behavior.

The original authentic report stays byte-identical: USTEC 29604.00, no original
13:32Z eligibility, approximate/unverified exit, SL HumanValidationRequired,
risk/TP Waiting and incomplete trade. No acquisition, normalization or clock
audit is repeated.

## Final validation

Passed: 2,086 relevant Application tests; 172 Nasdaq API/integration regressions
(27 nonlocal capture cases included); 28 related backtesting scenarios; 19
domain replay tests; one authentic Replay #2 preservation test. Total: 2,306
.NET tests, zero failed/skipped in these final runs. The focused capture run
passed all 28 cases including the opt-in authentic preservation check; it
overlaps the totals above. All 44 frontend tests, full backend/frontend builds,
ESLint, full frontend Prettier check, scoped C# formatting, 52 local documentation
links and git diff whitespace checks passed. Final backend build had zero
warnings/errors. Actual HTTP persistence/restart checks passed; browser smoke
remains unavailable for the exact tool errors stated above.

The protected authentic report retains SHA256
`48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db`.
The preservation test compares the earlier selected frame before/after capture,
original USTEC display price, lack of original eligibility, and final SL/risk/TP
statuses. No record was saved as a new personal verification by the user.

## Smallest next functional feature

Register a source-reported monetary outcome and its valuation/fee provenance
against a selected supervised record revision, separately from calculated PnL
and strategy-compliance classification. Minimum human decision: whether to admit
a documentary reported net result with explicit currency and cost treatment;
no valuation formula is inferred. Session completion and NQ-RISK-003 /
NQ-REENTRY-001 remain unresolved. The UI supports documentary capture and history,
not a complete demo session or orders. Existing market-data temporal admission
still blocks causal current-data use and is not weakened by manual capture.
