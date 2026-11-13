# MT5 bounded supervised receipt observation

## Start a session in Git Bash

Build from the repository root and choose the source and checkpoint explicitly.
This example uses Windows absolute paths produced by Git Bash's `cygpath`.

```bash
dotnet build MoneyWay.sln
observation_cli='./src/backend/MoneyWay.Worker/bin/Debug/net10.0/MoneyWay.Worker.dll'
observation_source="$(cygpath -m "$APPDATA")/MetaQuotes/Terminal/Common/Files/MoneyWayAutomaticReadOnly"
observation_state="$(cygpath -m "$LOCALAPPDATA")/MoneyWay/ReceiptState"
mkdir -p "$observation_state"
observation_checkpoint="$observation_state/us100-receipt.json"

dotnet "$observation_cli" mt5-receipt observe \
  --source "$observation_source" \
  --checkpoint "$observation_checkpoint" \
  --max-passes 5 \
  --interval-ms 1000
```

Quoted `C:/...` paths also work. The existing checkpoint store requires absolute
local Windows paths, an existing NTFS parent directory and a checkpoint location
outside the immutable collector directory. The CLI retains the store's existing
path validation and lock-marker initialization behavior. Each pass calls `Load()`
and the established reader validation. Durable acknowledgment belongs to the
separate explicit `commit` operation.

Both numeric limits are required positive Int32 integers. `--max-passes` bounds
the number of reads. `--interval-ms` controls the cancellable wait after a completed
pass and before the next read. Validation time contributes to session duration.
The interval is an operational setting. Temporal market-data validation retains
its existing evidence requirements.

Press **Ctrl+C** to request cancellation. The command finishes any current
synchronous reader operation, interrupts the wait and writes a final `session_end`
line with exit 130. Completion by pass limit returns the last pass's exit code.
`failedPasses` counts completed passes with a nonzero exit, including transient
availability failures that later recover. The final line retains the last summary.

For a retained NDJSON log, redirect stdout to a chosen file in the state directory.

```bash
dotnet "$observation_cli" mt5-receipt observe \
  --source "$observation_source" --checkpoint "$observation_checkpoint" \
  --max-passes 5 --interval-ms 1000 > "$observation_state/observation.ndjson"

dotnet "$observation_cli" mt5-receipt observe --help
```

## Output and changes

The output schema is `moneyway.mt5-receipt-observation`, version 1. Each completed
pass emits one compact JSON line with `type="observation"`, followed by one
`type="session_end"` line. Detailed per-record review remains available through
the [inspect command](mt5-on-demand-receipt-command.md). Observation emits summaries
and selected change diagnostics. Review tokens are issued by fresh `inspect` calls.

An abbreviated shape for a quiet second pass follows. Actual lines also include
paths, source identity, receipt counters and validation properties.

```json
{
  "type": "observation",
  "pass": 2,
  "passLimit": 5,
  "intervalMs": 1000,
  "integrityStatus": "Validated",
  "acknowledgmentEligible": true,
  "changes": {
    "firstObservation": false,
    "noChange": true,
    "newValidReceipts": 0,
    "checkpointRevisionChanged": false
  },
  "exitCode": 0
}
```

| Fields                                                                                                   | Meaning                                                                    |
| -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `pass`, `passLimit`, `intervalMs`                                                                        | Operational session progress and supplied limits.                          |
| `expectedSource`, `sourceIdentityValidated`                                                              | Approved OANDA demo/US100 identity and its verification result.            |
| `checkpointStatus`, `checkpointRevision`                                                                 | Current durable state loaded on this pass.                                 |
| `discoveredRecords`, `durablyAcknowledgedRecords`, `verifiedAcknowledgedRecords`, `pendingValidReceipts` | Current evidence inventory and receipt progress.                           |
| `blockedReceipts`, `missingPredecessors`, `unavailableProbeDependencies`, `sequenceGaps`                 | Existing validation/dependency diagnostics.                                |
| `receiptValidDiagnosticEvents`                                                                           | Verified source-unavailable and shutdown evidence in this snapshot.        |
| `sourceUnavailableDiagnosticEvents`, `shutdownEvents`                                                    | Diagnostic and lifecycle event counts separately.                          |
| `incompleteOrUnavailableAcquisitions`, `completeNativeSamples`                                           | Capture-level availability and completeness.                               |
| `receiptIntegrityValidated`, `integrityStatus`, `acquisitionStatus`, `acknowledgmentEligible`            | Separate integrity, quality and receipt eligibility results.               |
| `diagnostics`, `additionalDiagnosticCodes`                                                               | Up to 12 distinct ordered codes and the remaining count.                   |
| `publicationContinuity`                                                                                  | `unresolved`, preserving uncertainty about unobserved publication periods. |

The first pass establishes a baseline, with change counts zero and
`firstObservation=true`. Later changes derive from verified filename/SHA256
identities and validation outcomes. Filesystem modification times play no role.

- `noChange` compares the current validated identity/status/diagnostic snapshot
  and checkpoint state with the preceding pass.
- `newValidReceipts` counts newly verified eligible identities first seen during
  this session. `newReceiptValidDiagnosticEvents`, `newShutdownEvents` and
  `newCompleteNativeSamples` identify their kinds.
- `newIntegrityBlockers` reports newly observed validation failures/unavailable
  dependencies. A valid acquisition diagnostic retains its separate quality state.
- `newSampleAfterUnavailableEvidence` identifies a new complete native sample
  published after previously observed acquisition-unavailable evidence in local
  sequence order. Interpretation of the interruption's cause or duration remains
  unresolved.
- `checkpointRevisionChanged` detects external commits or removal. Busy/unknown
  checkpoint reads preserve the last known revision for the next comparison.
- `sourceDirectoryUnavailable` and `sourceDirectoryReturned` describe local access.
  In-memory verified identities survive a temporary directory loss, so rediscovery
  of old bytes preserves their already-observed classification.
- `changedReceiptFiles` detects changes to a previously verified manifest under
  the same filename. The session stops with a conflict and includes up to three
  `changedReceipts` entries containing the prior/current digests.

The existing [diagnostic-event contract](mt5-diagnostic-event-receipts.md) remains
authoritative. Record 136's verified no-sample event is receivable evidence while
its acquisition remains `Unresolved`, with zero bars. Later samples retain their
own source evidence. Native sample completeness and receipt acknowledgment leave
UTC mapping, provider closure, freshness, historical availability and multi-timeframe
atomicity pending independent validation. Canonical current-data ingestion remains
pending. Trading execution is outside the observation operation.

## Failure handling and restart

Missing or empty source directories, unavailable dependencies, busy checkpoint
locks and malformed/incomplete publications can be inspected again on the next
configured pass. Genuine hash/identity/chain conflicts, unsafe paths, unsupported
payloads and invalid checkpoints stop the session. A predecessor awaiting validation
can be retried when its own failure is transient. Storage/configuration failures
also stop. The final reason is `pass_limit`, `cancelled`,
`safety_or_storage_failure`, `configuration_failure` or `io_failure`.

Exit codes preserve the [existing CLI meanings](mt5-on-demand-receipt-command.md#diagnostics-and-exit-codes)
0 and 2–6, with 130 for cancellation. If stdout becomes unavailable, the command
returns 6 and attempts a fixed stderr diagnostic. Syntax errors use stderr and
exit 2. Check the process exit code and final reason alongside pass diagnostics.

Restarting observation establishes a new in-memory baseline and reloads the durable
checkpoint. External confirmations are visible on subsequent passes. The command
calls the existing inspection path on every pass and performs zero `Commit()` calls.
The store may initialize its harmless `.lock` marker in the configured state
directory. Existing checkpoint and source bytes receive read-only access.

Durable acknowledgment remains the operator's separate workflow.

```bash
dotnet "$observation_cli" mt5-receipt inspect \
  --source "$observation_source" --checkpoint "$observation_checkpoint"
```

Review that current report, then use its current revision/token with the explicit
commit command in the guide. Observation's earlier eligibility summary remains
specific to its pass. The external consumer's processing transaction has its own
delivery semantics. Receipt acknowledgment provides no downstream exactly-once
guarantee.

## Architecture and validation evidence

[Mt5ReceiptObservationCommand](../../src/backend/MoneyWay.Worker/Mt5ReceiptObservationCommand.cs)
uses the shared inspection path in
[Mt5ReceiptCommand](../../src/backend/MoneyWay.Worker/Mt5ReceiptCommand.cs).
The [entry point](../../src/backend/MoneyWay.Worker/Program.cs) handles observe and
Ctrl+C before host creation. Scheduling is a cancellable `Task.Delay`, with an
injected wait delegate for controlled tests. Session identity/change state remains
in memory for the configured pass count. Receipt validation and checkpoint reads
use their established ports.

[Observation tests](../../tests/integration/MoneyWay.IntegrationTests/Mt5ReceiptObservationTests.cs)
exercise real synthetic files, reader/store validation, controlled waits,
cancellation, external commits, partial publication, integrity failures and an
actual bounded executable invocation. The native cancellation signal is wired to
the same token exercised by those cancellation tests.
Validation passed with 35 observation tests, 149 existing receipt/CLI/checkpoint
tests and 98 PowerShell MT5 regressions. The total was 282 passed, zero failed and
zero skipped. The complete solution build reported zero warnings and errors.

On November 13, 2026 an authentic read-only smoke session ran two passes at
200 ms against the available MT5 folder, using an isolated absent checkpoint.
Both passes reported 239 discovered/pending receipts, zero acknowledged/blocked,
237 complete native samples, one source-unavailable diagnostic and one shutdown.
Receipt integrity was `Validated`; the one unavailable acquisition remained
`Unresolved`. The second pass reported `noChange=true`. The command returned 0
with `reason="pass_limit"`, emitted three NDJSON lines and left the checkpoint
absent. All 477 JSON artifacts hashed before the run retained their hashes.
The largest line was 2,359 characters. These counts describe the observed snapshot.
The earlier human-confirmed 216-record checkpoint remains separate historical
evidence. The documented default checkpoint path was absent locally, so this smoke
test used isolated state rather than claiming to load that historical checkpoint.
A separate real Git Bash invocation accepted `cygpath -m` absolute paths, emitted
an observation and final line and exited 0.

The next smallest temporal-validation action is to retain a dated clarification
for `OANDA_Global-Demo-1` together with an independently UTC-anchored native
example, following the [existing server-time audit](mt5-server-time-policy-audit.md).
