# MT5 durable local receipt checkpoint

## Scope and architecture

The [local receipt reader](mt5-local-dotnet-receipt-adapter.md) still performs no
writes. Its returned checkpoint is validated **in-memory receipt progress**, not
proof that a consumer processed an acquisition. This feature adds explicit
durable receipt acknowledgment through the Application
[IMt5ReceiptCheckpointStore](../../src/backend/MoneyWay.Application/MarketData/Receipt/IMt5ReceiptCheckpointStore.cs)
and Infrastructure
[LocalMt5ReceiptCheckpointStore](../../src/backend/MoneyWay.Infrastructure/MarketData/Mt5/LocalMt5ReceiptCheckpointStore.cs).
It preserves the boundaries of [ADR 0001](../decisions/0001-select-application-stack.md)
and [ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md).

There is no DI registration, database, API, Worker, polling loop, order mechanism
or strategy/replay integration. The caller supplies **absolute** source and
checkpoint paths. The checkpoint parent must already exist and be outside the
source evidence directory; no default location or directory creation is hidden.
The supported publication target is Windows on a fixed local NTFS volume.
Other targets fail with `local_fixed_ntfs_required`.

## Explicit acknowledgment

```csharp
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

// Explicit existing directories; checkpointPath must be outside sourceDirectory.
IMt5ReceiptCheckpointStore store =
    new LocalMt5ReceiptCheckpointStore(checkpointPath, sourceDirectory);
var state = store.Load();
if (state.Status is not (Mt5CheckpointStatus.Missing or Mt5CheckpointStatus.Loaded))
    throw new InvalidOperationException("Inspect checkpoint diagnostics before continuing.");

// Load revalidates source evidence; its Verification can contain new, pending receipts.
var batch = state.Verification ?? new LocalMt5CollectorReceiptReader().Read(sourceDirectory);
// Inspect receipt diagnostics. This call explicitly acknowledges receipt only.
var commit = store.Commit(batch.Checkpoint, state.Revision);
// Only Committed / Unchanged confirm durable acknowledgment. Handle all other
// statuses explicitly; PublicationUncertain requires reload and verification.
```

The meanings remain separate:

1. Discovery observes local filenames.
2. Reader validation checks record/probe bytes, hashes, structure and chain.
3. `Read()` / `Load().Verification` propose receipt progress without committing it.
4. `Commit()` explicitly persists a selected validated checkpoint.
5. No step proves a downstream effect, canonical readiness or trading eligibility.

Commit does not silently add other newly discovered records. It validates every
proposed identity against current source bytes using the existing reader and
compares full filename/hash/sequence/run/poll/event metadata. Missing, corrupt or
conflicting acknowledged evidence prevents a new commit. Source inventory errors
also block commit. Structurally valid incomplete diagnostic acquisitions may be
acknowledged, preserving the existing `Unresolved` receipt semantics.

No external side effect is included in this transaction. An effect performed
before commit may be repeated after interruption; a commit before the effect may
suppress redelivery even though the effect did not happen. **Persistent receipt
storage does not establish exactly-once downstream processing.**

## Versioned persisted contract and provenance

The JSON envelope has schema `moneyway.mt5-receipt-checkpoint`, version `1`,
`payload` and `payload_sha256`. The payload includes:

- Exact company `OANDA Global Markets Limited`, server `OANDA_Global-Demo-1`,
  native symbol `US100`, demo and diagnostic-only true, canonical-ready false.
- The existing normalized source-directory scope digest, not a global terminal ID.
- The full bounded acknowledged identity ledger: filename, actual record SHA256,
  sequence, local run ID, poll sequence and event.
- Last acknowledged sequence and record SHA256, checked against the ledger anchor.

SHA256 covers the typed payload serialized in its declared property order with
snake-case names and no indentation. The revision returned to callers is SHA256
of the **actual entire checkpoint file bytes**. Validation rejects missing or
unknown fields, duplicate JSON properties, unsupported schema/version, wrong
source, invalid/duplicate identities, anchor inconsistency or payload corruption.
Load also compares retained identity metadata with any successfully revalidated
source observation; disagreement returns `Conflict` without a usable checkpoint.
The bound is 2 MiB / 4,096 identities, retaining existing receipt bounds.

Record digests anchor the original manifests and their predecessor/probe references;
the store does not duplicate payload candles or replace the original chain.
There are no native timestamp fields in the existing checkpoint model, so none
are invented for storage. Native timestamps and exact prices remain unchanged in
the observations reconstructed by the reader. No credential, account identifier,
order, OHLC dataset or execution state is persisted.

The checksum detects accidental modification, **not authenticated authorship**.
An actor rewriting a payload and checksum can change stored receipt claims. Load
still revalidates retained source evidence: a forged record digest produces a
receipt conflict and no validated observation. Independent retained authentic
hash pins remain useful. A valid persisted ledger is historical acknowledgment,
not proof of the current availability of its source files.

## Publication, concurrency and recovery

All cooperating load/commit operations hold an exclusive `checkpointPath.lock`
handle with a versioned marker. A competing operation returns `Busy`; the marker
file remains after handle release and process termination. An unrecognized or
partially initialized lock file is preserved and blocks operations with a
diagnostic; inspect it independently before manual recovery. No unrelated file
is silently truncated or overwritten.

Commit uses optimistic concurrency as well: null expected revision means
**create only**; subsequent calls must supply the exact loaded revision. A stale
revision or any removal/change of previously acknowledged identities returns
`Conflict`. An identical save with the current revision returns `Unchanged`
without rewriting the checkpoint, after source revalidation.

Each write creates a unique sibling `checkpointPath.pending-<guid>.tmp` using
`CreateNew`, `WriteThrough` and `Flush(true)`, then closes it. Publication uses
same-directory `File.Move` without overwrite for initial creation or `File.Replace`
for an existing validated checkpoint. There is no truncate/copy/delete fallback.
The committed bytes are reread and checked before returning `Committed`.
The implementation follows the documented [.NET replacement](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0)
and [flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
APIs. Windows documents [replacement error states and an unsupported write-through replacement flag](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew);
the adapter therefore does not promise power-loss durability or transactional
hardware/directory flushing. Process-interruption states and local replacement
behavior are tested; machine power failure is not tested.

| Restart state                                   | Behavior                                                                                                                             |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| No published file                               | `Missing`; no records durably acknowledged.                                                                                          |
| Partial or complete temporary file only         | `Missing` with orphan diagnostic; never promote it. Records can be reprocessed and explicitly committed.                             |
| Valid published file plus orphan temporary file | Load the published checkpoint and preserve the orphan; pending progress remains unacknowledged.                                      |
| Successful publication before process exit      | Load committed identities and revalidate source bytes; identical receipts are recognized.                                            |
| Corrupt/unsupported published file              | `Invalid`; preserve it and stop. Never fall back to zero or a temporary file.                                                        |
| Publication error/uncertainty                   | `PublicationUncertain`; reload and inspect the committed file and receipt diagnostics before retrying.                               |
| Missing historical source evidence              | Preserve the committed ledger; verification reports `DataUnavailable`/chain diagnostics. No automatic retention/deletion assumption. |

Temporary files are never trusted, promoted, overwritten or deleted automatically;
a later explicit commit uses another unique temporary name. They are retained for
inspection; manual cleanup/retention is outside this feature. Reparse ancestors,
leaf paths, source redirection and alternate data streams are rejected. Explicit
Windows drive paths exclude device/UNC namespaces, short-name (`~`) aliases and
trailing-dot/space components to prevent ambiguous path comparisons. Path and
file checks protect a cooperating local application, not a hostile filesystem
actor bypassing the lock. No global run-ID uniqueness is assumed.

`Loaded` means the **checkpoint file** is structurally valid; inspect its separate
`Verification.Status` and per-record diagnostics for present source health.
Verification may propose a larger checkpoint; `result.Checkpoint` remains the
previously committed ledger. When historical files are absent, their stored hashes
do not substitute for source validation or authorize any new receipt observation.
There is no new retention contract.

## Validation evidence

[Synthetic filesystem integration tests](../../tests/integration/MoneyWay.IntegrationTests/LocalMt5ReceiptCheckpointStoreTests.cs)
cover explicit save/load, new instances, pending progress, identical saves,
pre-publication/partial temporary states, actual blocked replacement, successful
replacement, checksum/schema/source/identity/anchor failures, regression,
source revalidation, missing evidence, concurrent writers, unsafe junctions,
unrelated files, unchanged native timestamps and no input mutation. Interruption
fixtures reproduce filesystem states; they do not claim a machine power-cut test.
Validation passed: 43 checkpoint tests, 48 existing receipt reader tests and 98
PowerShell MT5 regressions (189 passed, zero failed/skipped). The complete .NET
solution built with zero errors/warnings; formatting and local links were checked.

On November 12, 2026 an isolated temporary .NET console outside the repository
read the authentic MT5 directory and committed **70** validated records to its
own separate local checkpoint. The first two record hashes were independently
pinned to the [previously recorded authentic hashes](mt5-local-dotnet-receipt-adapter.md#authentic-local-verification--november-12-2026).
The process exited. A second process loaded the checkpoint, recognized **70**
already received records and discovered **1** additional uncommitted receipt.
Verification was `Validated`, the checkpoint revision remained
`5eda02c7a10596f54433861cbc6fa5c96fa63899847d467832b0debf49edffb1`,
and all **140** original JSON inputs hashed before the check retained their hashes.
No authentic input was copied into repository fixtures or modified. The growing
directory's later additions are not mutations of those original bytes.

UTC mapping remains [unresolved for canonical use](mt5-server-time-policy-audit.md).
Candle closure and historical availability remain unverified, position zero stays
provisional and `CanonicalReady=false`. Current-data ingestion into the canonical
engine is **not operational**. No manual API/Worker testing is necessary.

The smallest next feature is a bounded **on-demand local receipt command** that
uses these ports, exposes diagnostics and makes durable acknowledgment explicit,
without adding background ingestion or passing native data to the engine.
