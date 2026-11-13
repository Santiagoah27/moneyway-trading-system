# MT5 on-demand local receipt command

## Boundary

The .NET 10 command `mt5-receipt` runs in the existing
[MoneyWay.Worker entry point](../../src/backend/MoneyWay.Worker/Program.cs),
**before host creation**, following the existing one-shot `replay-csv` convention.
It exits after one operation: no Worker scheduling, hosted service, continuous
ingestion or API is activated by this command. Implementation:
[Mt5ReceiptCommand](../../src/backend/MoneyWay.Worker/Mt5ReceiptCommand.cs).

It composes the existing Application receipt ports with the
[local reader](mt5-local-dotnet-receipt-adapter.md) and
[durable checkpoint store](mt5-durable-receipt-checkpoint.md). No market-data wire
schema, checkpoint schema, evaluator, strategy or canonical replay behavior changes.
There are no new external dependencies or production registrations.

## Inspect first

From the repository root, build once and choose explicit absolute paths:

```powershell
dotnet build MoneyWay.sln
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$command = (Resolve-Path 'src/backend/MoneyWay.Worker/bin/Debug/net10.0/MoneyWay.Worker.dll').Path
$source = Join-Path $env:APPDATA 'MetaQuotes\Terminal\Common\Files\MoneyWayAutomaticReadOnly'

# Explicit separate receipt state directory, not inside the collector directory.
$stateDirectory = Join-Path $env:LOCALAPPDATA 'MoneyWay\ReceiptState'
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
$checkpoint = Join-Path $stateDirectory 'us100-receipt.json'

$previewJson = & dotnet $command mt5-receipt inspect --source $source --checkpoint $checkpoint
$previewExit = $LASTEXITCODE
$previewJson
if ($previewExit -ne 0) { throw 'Inspection blocked; inspect diagnostics. Do not commit.' }
$preview = ($previewJson -join [Environment]::NewLine) | ConvertFrom-Json
$preview | Select-Object operation, checkpointStatus, checkpointRevision,
    discoveredRecords, durablyAcknowledgedRecords, newlyValidatedPendingRecords,
    blockedRecords, integrityStatus, safeToAcknowledge, expectedRevision, reviewToken
$preview.records | Format-Table fileName, status, durablyAcknowledged, probeDependencyUnavailable
```

Inspection writes a JSON report to stdout, not a report file or checkpoint.
The existing store may initialize its separate `.lock` marker, which is a harmless
coordination file, not acknowledged progress. A missing checkpoint is `Missing`,
with `expectedRevision="missing"` and zero durably acknowledged records. A corrupt
checkpoint blocks inspection; the command never resets it to hide failure.

Review the full `records` and `diagnostics`, including source-health issues,
sequence gaps and unavailable dependencies. Counts distinguish discovered records
from placeholder rows for unavailable files in the old ledger. `recordsReported`
can exceed `discoveredRecords`. `durablyAcknowledgedRecords` is stored progress;
`verifiedAcknowledgedRecords` counts currently verified acknowledged bytes.
`receiptDisposition` retains reader semantics; the separate `durablyAcknowledged`
flag describes the actual committed identity, not proposed progress.

The report identifies the **expected** OANDA Global Markets demo/US100 source;
`sourceIdentityValidated` describes verified receipt identity independently from
market-data availability. `receiptIntegrityValidated` / `integrityStatus` describe
receipt checks; `acquisitionStatus` retains aggregate sample-quality status.
Only a fully receipt-eligible batch has `safeToAcknowledge=true`, a `reviewToken` and
`candidateCheckpoint` summary (scope/count/last identity). Record rows expose
the actual hashes and local identities underlying that candidate. The token
commits to the entire ledger, not only its last sequence.

This command still blocks ordinary incomplete/unresolved snapshots. The narrow
[verified disconnected/no-sample diagnostic profile](mt5-diagnostic-event-receipts.md)
can now be acknowledged as evidence while remaining `Unresolved` as an acquisition.
New receipt, diagnostic and complete-native-sample counters keep those meanings
separate. This is not a changed strategy rule or blanket acceptance of `Unresolved`.
Explicitly linked publication gaps remain permitted according to the existing
reader: the unresolved cause is reported, without inventing missing provider bars.

## Explicit acknowledgment

After reviewing a successful inspection, invoke this separate operation manually:

```powershell
if (-not $preview.safeToAcknowledge) { throw 'Receipt is not safe to acknowledge.' }
& dotnet $command mt5-receipt commit --source $source --checkpoint $checkpoint `
    --expected-revision $preview.expectedRevision --review-token $preview.reviewToken `
    --acknowledge-receipt
if ($LASTEXITCODE -ne 0) { throw 'Commit was not confirmed. Inspect the outcome and reload.' }
```

There is no automatic commit and no interactive prompt. `commit` requires all
three approval arguments; no reset, force, skip-validation or fallback option exists.
The path pair is required for both operations. Unknown/duplicate options, control
characters, relative paths and unsupported path aliases are rejected. Help:

```powershell
& dotnet $command mt5-receipt --help
```

## Review binding, concurrency and restart

The deterministic SHA256 review token covers a versioned review domain, normalized
checkpoint path, loaded checkpoint revision, source directory scope and the full
ordered candidate filename/hash/sequence/run/poll/event ledger. Record digests bind
the manifests and therefore their referenced probe digests. The token is a
comparison guard, **not a signature, secret, authentication mechanism or proof that
a human read the report**. Operators must explicitly review the inspection.
Only `inspect` issues a token; a rejected commit requires a fresh inspection.

Commit reloads and revalidates the files, compares the supplied revision and token,
then invokes the existing store's locked `Commit(candidate, expectedRevision)`.
Changed evidence, missing probes/predecessors, incomplete snapshots, source
mismatch or chain conflict blocks acknowledgment, except that a verified no-sample
diagnostic is eligible as receipt evidence rather than a complete sample. New records discovered before
the command's final review comparison change the token and require review again.
Additional publications after that comparison are not implicitly added: the store
commits only the reviewed ledger and revalidates its identities under its lock.
There is no atomic whole-directory capture or protection against a hostile actor
rewriting all files outside the cooperating local application model.

The store's exclusive lock, byte-hash revision fence and monotonic progress checks
remain authoritative. A stale or replayed old review returns exit 5; there is no
regression or recreate-on-conflict. For an identical no-progress commit, inspect
again with the current revision; the store returns `Unchanged` without rewriting.

After process restart, repeat `inspect` with the same paths. Previously committed
identities are revalidated and recognized; new records remain pending until an
explicit commit. Failure before commit leaves pending progress recoverable. Orphan
temporary files remain preserved, unpromoted and unacknowledged. Corrupt checkpoints
block operations. `PublicationUncertain` requires reloading and checking the
published checkpoint before retrying. Missing historical evidence remains unavailable;
the stored hashes never substitute for verifying source bytes.

Receipt acknowledgment does **not** establish exactly-once downstream processing.
No consumer side effect is transactionally coordinated with this file.

## Diagnostics and exit codes

The JSON report schema is `moneyway.mt5-receipt-command-report`, version `1`, with
camel-case names matching the existing console report convention. It contains
paths, operation, expected source, checkpoint status/revision, receipt counts,
candidate summary, per-record status/hash/native head, existing diagnostic codes,
temporal blockers, outcome, exit code and actionable next step. Native head raw
integers/displays are not labeled as authenticated Unix UTC. No arbitrary payload
descriptions, account identifiers, credentials or raw exception messages are echoed.
Syntax/path errors use fixed stderr messages; help uses stdout. Reports are not
written to disk by the command; do not redirect shell output over source artifacts.

| Exit | Meaning                                                                                                   |
| ---- | --------------------------------------------------------------------------------------------------------- |
| `0`  | Successful inspection, committed receipt, unchanged commit or help.                                       |
| `2`  | Invalid arguments or path configuration; missing approval/revision/token.                                 |
| `3`  | No eligible source records, missing/unsafe source directory or invalid provider/symbol identity.          |
| `4`  | Receipt integrity/validation failure, unresolved/incomplete evidence or invalid checkpoint.               |
| `5`  | Stale revision, changed reviewed ledger, checkpoint conflict or concurrent-store lock. Inspect again.     |
| `6`  | Checkpoint storage/filesystem/publication failure, including `PublicationUncertain`. Reload before retry. |

`MissingPredecessors`, `unavailableProbeDependencies` and `sequenceGaps` counters
summarize existing reader diagnostics (serialized in camel case). An unavailable
probe is not automatically diagnosed as provider deletion; a corrupt/hash-mismatched
probe is an integrity failure, not a missing-probe count. Per-record diagnostics
retain the precise existing classification. Inspect the exit code even when JSON
was returned; `Loaded` alone describes checkpoint structure, not source readiness.

Checkpoint storage requires an existing parent outside the collector directory,
Windows and fixed local NTFS. Unsafe reparse paths, device/UNC/short-name aliases
and alternate streams are rejected by the existing store. Its temporary write,
flush and same-directory publication do not promise power-loss durability or an
atomic multi-timeframe snapshot; see [storage limitations](mt5-durable-receipt-checkpoint.md#publication-concurrency-and-recovery).

## Verification evidence and next step

[Command integration tests](../../tests/integration/MoneyWay.IntegrationTests/Mt5ReceiptCommandTests.cs)
use synthetic files and the actual reader/store, including blocked publication,
corrupt/missing evidence, preview binding, concurrency, idempotency and no input
mutation. A subprocess test invokes the actual executable for inspect, commit,
restart and new pending discovery, proving the command exits without starting
the hosted Worker. No API/Worker daemon manual execution is required.
Validation passed: 30 command tests, 48 receipt reader tests, 43 checkpoint tests
and 98 PowerShell MT5 regressions: 219 passed, zero failed/skipped. The complete
.NET solution built with zero warnings/errors; formatting and links were checked.

On November 12, 2026 a separate authentic **inspect-only** smoke execution against
the available MT5 directory returned exit 0: **88** discovered/validated pending
records, zero acknowledged, zero blocked, `checkpointStatus=Missing`. An explicitly
isolated temporary state directory was used. No checkpoint was created and no real
commit was performed. All **176** original JSON artifacts hashed before inspection
retained their hashes afterward. These counts describe that snapshot only and
are not runtime expectations. No authentic payload is committed to the repository.

`diagnosticOnly=true`, `canonicalReady=false` and
`utcConversionAuthenticated=false` remain fixed. UTC mapping is not authenticated;
position zero remains provisional; provider closure and historical availability
are not verified. No freshness threshold is invented, no strategy verdict is
derived, no canonical ingestion is operational and no trading is executed.

The smallest next feature toward supervised continuous receipt is a separately
invoked **bounded receipt watch session with inspection-only output and explicit
stop/iteration limits**, reusing these ports and preserving manual commit. It must
not add automatic acknowledgment or canonical ingestion.
