# MT5 local .NET read-only receipt adapter

## Boundary and invocation

The .NET 10 adapter consumes the existing [collector v1](mt5-automatic-read-only-collector.md)
and probe v1 artifacts on demand. It does not integrate current data into the
canonical engine. The existing market-data separation in
[ADR 0001](../decisions/0001-select-application-stack.md) and
[ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md)
is preserved; no service, endpoint, Worker registration or background polling is added.

- Application port: [IMt5CollectorReceiptReader](../../src/backend/MoneyWay.Application/MarketData/Receipt/IMt5CollectorReceiptReader.cs).
- Native receipt contracts: [Mt5ReceiptModels](../../src/backend/MoneyWay.Application/MarketData/Receipt/Mt5ReceiptModels.cs).
- Filesystem implementation: [LocalMt5CollectorReceiptReader](../../src/backend/MoneyWay.Infrastructure/MarketData/Mt5/LocalMt5CollectorReceiptReader.cs).
- Internal v1 wire validation: [Mt5WireContract](../../src/backend/MoneyWay.Infrastructure/MarketData/Mt5/Mt5WireContract.cs).

Example for an explicitly chosen local directory, from a .NET caller referencing
Application and Infrastructure:

```csharp
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

IMt5CollectorReceiptReader reader = new LocalMt5CollectorReceiptReader();
var sourceDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "MetaQuotes", "Terminal", "Common", "Files", "MoneyWayAutomaticReadOnly");
var pins = new Dictionary<string, string>
{
    ["record-000000000001.json"] =
        "f3466a601a1723f5222984b279cdd9bd5e1cbe377e52569b0d4a462f166673a5"
};
var first = reader.Read(sourceDirectory, expectedRecordHashes: pins);
var next = reader.Read(sourceDirectory, first.Checkpoint, pins);
// Consume receipt diagnostics explicitly. NewObservations may include incomplete
// diagnostic acquisitions or shutdown metadata, never canonical candles.
```

There is no implicit default directory or hidden persistence in the reader. The caller
owns the checkpoint; [explicit durable storage](mt5-durable-receipt-checkpoint.md)
can retain it for idempotency between processes.
The port is an application-facing architectural contract, not an HTTP API.

## Integrity, discovery and receipt semantics

The adapter freezes a top-level filename inventory, sorts canonical record names
ordinally and processes their sequence order. Collector `pending-*.tmp` staging
files and unreferenced probes are not receipts. Unexpected record-like names
produce a sanitized diagnostic. Files are read only, with SHA256 computed from
the same bytes parsed. Optional externally retained record hash pins are checked;
the manifest's probe digest and predecessor digest are also checked. Hash links
are integrity evidence, not signatures or proof against a rewritten entire chain.

Strict .NET validation implements the existing collector/probe v1 wire contract:
no new payload schema and no production PowerShell invocation. It checks exact
company `OANDA Global Markets Limited`, server `OANDA_Global-Demo-1`, demo mode,
native `US100`, schema/version, bounded settings, allowed event names, native
raw/display agreement, sequence/run/poll relationships, sample counts, quality
claims, timeframe identities, bar roles and chronology. Duplicate JSON properties,
unknown fields, invalid readiness assertions and malformed/incomplete documents
fail safely. OHLC tokens are retained literally and inequalities use exact decimal
significands/exponents, with no floating-point tolerance or rounding.

Receipt statuses are separate from strategy results:

| Status            | Meaning                                                                                                     |
| ----------------- | ----------------------------------------------------------------------------------------------------------- |
| `Validated`       | Local wire/integrity checks passed; no canonical readiness implied.                                         |
| `Unresolved`      | Structurally valid diagnostic acquisition with incomplete, unstable, disconnected or invalid-OHLC evidence. |
| `DataUnavailable` | Missing/unreadable dependency, unsafe filesystem entry or technical bound.                                  |
| `Conflict`        | Hash/content, acquisition identity or chain inconsistency.                                                  |
| `Invalid`         | Unsupported or inconsistent payload.                                                                        |

An accepted observation has disposition `New` or `AlreadyReceived`. Invalid,
conflicting or inaccessible artifacts have `NotReceived`, no observation and are
not newly acknowledged. A later call can retry missing/incomplete publication.
Incomplete but structurally valid diagnostic acquisitions are recognized once per
caller-owned checkpoint;
that acknowledgment is not acceptance of usable trading data. Shutdown is retained
as metadata with no fabricated candles.

The returned checkpoint defensively copies immutable filename/hash/sequence/run/
poll/event identities. Identical files plus the same checkpoint produce identical
results. Reprocessing returns no new observations; changes under an acknowledged
filename conflict. There is no automatic exactly-once delivery across crashes or
concurrent callers. The [durable checkpoint store](mt5-durable-receipt-checkpoint.md)
now provides explicit receipt commits; consumer transaction handling remains
outside the receipt boundary. No database is introduced.

Directory scope is a digest of the normalized absolute local path (case-normalized
on Windows). It prevents accidental checkpoint reuse for another configured path;
it is **not** a globally authenticated terminal-installation identity. Run IDs are
scoped locally, not assumed globally unique. Replacing a directory requires keeping
its checkpoint or an explicit caller decision, not silently clearing history.

Missing predecessor files block dependent receipt even when an old identity remains
in the checkpoint; the adapter does not pretend unavailable bytes were validated
in this call. Duplicate acquisition run/poll identities conflict, with shutdown
allowed to reuse its final poll. Run restart and previous committed native head
consistency are checked. Explicitly linked sequence gaps retain an unresolved-cause
diagnostic; no bars, deletions or gap cause are invented. Disappeared files remain
in the checkpoint with unavailable diagnostics, not permanent provider deletions.

## Filesystem safety and technical limits

References must be exact sibling `record-NNNNNNNNNNNN.json` or
`probe-NNNNNNNNNNNN.json` names, preventing traversal, absolute paths and alternate
data streams. Reparse roots, ancestors and referenced leaves are rejected. Held
read handles use `FileShare.Read` and size/path checks around reading. This protects
against cooperating concurrent writers on Windows; it is not an adversarial
filesystem sandbox or a transactional snapshot of an actively changing directory.
No atomic whole-directory publication is assumed. New publications can be received
on the next call; source files are never renamed, deleted, repaired or overwritten.

Technical bounds, not strategy thresholds: 8,192 top-level entries, 1,024 discovered
records, 2 MiB per file, 64 MiB total loaded bytes, 4,096 checkpoint identities,
JSON depth 32 and 384 characters per numeric token. Exceeding bounds fails with
diagnostics rather than unbounded scanning. No retention policy is added.
Diagnostics expose fixed codes and safe canonical filenames, not raw exception
messages, arbitrary source descriptions, credentials or account identifiers.

## Temporal blockers retained

Native timestamps remain `Mt5NativeTimestamp(Raw, Display)`, not UTC instants.
Computer-derived GMT and server quote samples are retained with their source
meaning. Native OHLC data does not become `Candle` or `StrategyReplayContext`.
Position zero remains provisional; historical-preceding roles do not authenticate
provider closure or first availability. UTC opening, provider-confirmed closure
and historical availability remain null. `CanonicalReady` and
`UtcConversionAuthenticated` stay false, including for `Validated` receipts.

The batch retains unresolved UTC conversion, provider closure, historical
availability, non-atomic multi-timeframe acquisition and warmup/freshness policy.
The [server-time audit](mt5-server-time-policy-audit.md) is not replaced by a fixed
offset assumption. No strategy verdict, order, fill, risk, PnL or execution is produced.

## Authentic local verification — November 12, 2026

The actual files were available under the user's MT5 Common Files
`MoneyWayAutomaticReadOnly` directory. They were read without modification.

| File                       | Independently verified SHA256                                      |
| -------------------------- | ------------------------------------------------------------------ |
| `record-000000000001.json` | `f3466a601a1723f5222984b279cdd9bd5e1cbe377e52569b0d4a462f166673a5` |
| `record-000000000002.json` | `0f94c86a507b0d27554bc797eec21a049fa84b6cf01b8173bdc075e05fc42a0c` |
| `probe-000000000001.json`  | `aea3ce6dfaba643d43da00ea49f7d7c4f5dc5521f6ea18ebcc7aae32c6d182bd` |
| `probe-000000000002.json`  | `fcea585a4fb4d30a5eea25adc24b02a892764e70a52c86bda0ddd9ff164eee5a` |

The existing PowerShell collector validator independently passed both pairs.
A temporary .NET caller outside the repository invoked this adapter against the
real directory with record 1 and 2 hash pins. All **55** discovered records were
`Validated`; both pinned records matched, each with 16 bars per M1/M5/H1/H4.
Record 1 was `initial_snapshot`, sequence/poll 1/1; record 2 was `new_native_m1`,
sequence/poll 2/10, both `run-000000000001`, with the expected predecessor link.
Their captured native M1 heads were `2026.11.12 19:33:00` (raw `1794511980`) and
`2026.11.12 19:34:00` (raw `1794512040`). A second call with the returned checkpoint
reported **55 already received and zero new**. Every JSON input hashed before the
smoke execution retained its hash afterward. The temporary caller is not a new
production host and authentic payload bytes are not copied into the repository.

This is a bounded observation of real reception and initial/M1-triggered
acquisition, not proof of long-term continuity, reconnect handling in the terminal,
authenticated UTC, provider-confirmed closure or atomic multi-timeframe capture.

## Tests and next boundary

[Integration tests](../../tests/integration/MoneyWay.IntegrationTests/LocalMt5CollectorReceiptReaderTests.cs)
use explicitly [synthetic fixtures](../../tests/integration/MoneyWay.IntegrationTests/Mt5ReceiptSyntheticFixture.cs),
not authentic inputs. They exercise receipt, immutable content, retries, ordering,
restart, native precision/time semantics, unavailable samples, paths and reparse
rejection. Run the focused suite and existing MT5 regressions:

```powershell
dotnet test tests/integration/MoneyWay.IntegrationTests/MoneyWay.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~LocalMt5CollectorReceiptReaderTests
& ./tests/tools/Mt5ReadOnlyCollector.Tests.ps1
& ./tests/tools/Mt5ReadOnlyProbe.Tests.ps1
& ./tests/tools/Mt5ProbeComparator.Tests.ps1
```

No manual API/Worker execution is required: neither is integrated.
[Explicit durable receipt storage and recovery](mt5-durable-receipt-checkpoint.md)
are now implemented, still outside the canonical engine.
Separately, source-backed UTC/closure/availability decisions are required before
any canonical current-data ingestion. Canonical current-data ingestion is **not
operational**.
