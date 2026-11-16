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

Receipt integrity, source identity, acknowledgment eligibility and complete native
sample observation are now [separate properties](mt5-diagnostic-event-receipts.md).
A verified disconnected `source_unavailable` no-sample event can be acknowledged
without changing its `Unresolved` acquisition status or filling any candles.
This exception does not make other unresolved samples CLI-eligible.

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

## Native candle lifecycle and observed availability audit (2026-11-16)

The offline [lifecycle analyzer](../../src/backend/MoneyWay.Infrastructure/MarketData/Mt5/Mt5NativeLifecycleAudit.cs)
consumes `Mt5ReceiptBatch` from the existing validated receipt boundary. The
[one-shot command](../../src/backend/MoneyWay.Worker/Mt5LifecycleCommand.cs) calls
`LocalMt5CollectorReceiptReader.Read(source)` without a checkpoint, checkpoint
store, acknowledgment, terminal call or hosted service. It accepts no direct probe
JSON. Blocked receipts contribute diagnostics, never bar facts. This is a native
evidence diagnostic with no canonical replay or strategy integration.

### Fields, comparison and continuity

The original collector/probe v1 schema actually stores **OHLC, not volume**.
`volume_evidence=data_unavailable`; no collector/schema change was made. Source,
run, publication sequence, poll sequence, event, native raw/display timestamps,
series positions/roles, OHLC literals, sample completeness, source stability,
frame acquisition status/synchronization/head consistency, record predecessor and
probe hashes are available. Samples contain a bounded trailing window, not a
complete native price history. The authentic captures inspected here request 16
bars each for M1, M5, H1 and H4. Acquisition across frames remains non-atomic.

Candle identity inside one source-directory scope is native symbol + timeframe +
native raw open coordinate. The original display string is retained. Comparing
that coordinate across runs establishes matching observed native identities,
not continuous collection. First/last references include sequence, run, poll,
event, record SHA256 and probe SHA256. Positions, observation/revision/identical
comparison counts, active-then-historical facts and interruption flags are
separate. The existing wire parser's normalized sign/digits/arbitrary-precision
exponent representation also supplies exact numerical equality: `101.25`,
`101.2500` and `1.0125e2` compare equal, while differences below decimal/double
precision remain differences. Original literals survive in before/after details.
No tolerance or rounding is applied.

Only connected, stable, head-consistent, synchronized, error-free frames with
valid OHLC supply bar facts. A validated partial acquisition may describe its
usable frames/bars; its incompleteness is retained and it breaks continuity.
Empty/unusable frames are counted separately. Validated diagnostic
`SourceUnavailableWithoutSample` receipts remain evidence without price bars.
Other `source_unavailable` acquisitions retain their own validated quality.
Unavailable/blocked records, incomplete samples, shutdown, sequence/run changes
and regressed heads break continuity conservatively. Head advancement counts
require uninterrupted same-run observations; active-then-historical positions
can still be observed across an explicitly flagged interruption.

Non-period native coordinate differences are counted in adjacent sampled bars
and head advancements. Neither those differences nor local publication sequence
gaps establish missing market candles, a trading schedule or a collection-time
interval. Missing dependencies remain blocked. Duplicate receipts are excluded;
duplicate acquisition identities are already rejected by the reader. No gaps are
interpolated and separate runs are never assumed continuous.

### Command and report

Build the solution, then run from Git Bash in the repository root. This exact
invocation was executed successfully against the authentic directory:

```bash
dotnet ./src/backend/MoneyWay.Worker/bin/Debug/net10.0/MoneyWay.Worker.dll mt5-lifecycle --source "$(cygpath -m "$APPDATA")/MetaQuotes/Terminal/Common/Files/MoneyWayAutomaticReadOnly" --through-sequence 643 --details 8
```

`--source` is mandatory and absolute. Optional `--report` must be a new absolute
file outside Git, the source directory and reparse ancestors. Default output is
`%LOCALAPPDATA%/MoneyWay/audits/mt5-lifecycle-<guid>.json`, opened with `CreateNew`.
`--details` defaults to 16 and accepts 0–128. It bounds selected bars, selected
events and revisions per selected bar; all aggregate counts remain complete and
omitted counts are explicit. Selection prefers a representative from each frame,
then bars with more revisions. Stdout contains a concise count summary and report
path. There is no overwrite, automatic acknowledgment or durable receipt change.
Exit codes: 0 diagnostic completed, 2 invalid arguments/path, 3 blocked/empty
receipt inventory or inventory diagnostics, 4 report I/O failure. Exit 0 does not
mean canonical readiness.

`--through-sequence` is an optional positive local publication-sequence cutoff.
It is **not historical UTC T**. The analyzer filters before folding snapshots;
later validated observations cannot rewrite returned objects or facts for the
same validated prefix. The reader still inspects the bounded source inventory and
revalidates its integrity; later integrity conflicts can invalidate trust in an
earlier receipt. This does not make later price contents visible in an earlier
prefix. The diagnostic supplies no `ObservedAtUtc`, `AvailableAtUtc` or
`StrategyReplayContext`.

Report schema is `moneyway.mt5-native-candle-lifecycle`, version 1. Source identity,
directory scope, inspected/validated/blocked counts, event/evidence-kind/diagnostic
histograms, run boundary references, native coordinate coverage, lifecycle counts,
bounded provenance and omitted counts are included. The input manifest digest is
SHA256 of UTF-8 `filename:actualSha256:receiptStatus` lines ordered by local
sequence then ordinal filename, joined with LF without a final LF. It identifies
the inspected record inventory/validation result; each record transitively pins
its probe. It is not provider authentication or a full report retention protocol.

### Authentic findings

The explicit source was
`%APPDATA%/MetaQuotes/Terminal/Common/Files/MoneyWayAutomaticReadOnly`, with source
OANDA Global Markets Limited / `OANDA_Global-Demo-1` / demo `US100`, MT5 build 6246.
The fixed analyzed prefix is records 1–643: **643 validated receipts, 0 blocked**,
642 hash-verified probe dependencies and 1,027 distinct sampled native bars.
Validated includes structurally valid unresolved acquisition evidence.

| Timeframe | Distinct bars | Only active head | Active then historical | Revised bars | Identical across all sampled observations | Single observation | Active-to-historical revisions | Historical-to-historical revisions |
| --------- | ------------: | ---------------: | ---------------------: | -----------: | ----------------------------------------: | -----------------: | -----------------------------: | ---------------------------------: |
| M1        |           723 |                6 |                    623 |          624 |                                        89 |                 10 |                            623 |                                  0 |
| M5        |           213 |                4 |                    126 |          130 |                                        83 |                  0 |                            126 |                                  0 |
| H1        |            62 |                2 |                     13 |           15 |                                        47 |                  0 |                             12 |                                  0 |
| H4        |            29 |                1 |                      6 |            7 |                                        22 |                  0 |                              5 |                                  0 |

Native open-coordinate coverage, without UTC conversion or a continuity claim:
M1 `2026.11.12 19:18:00`–`2026.11.16 21:06:00`; M5
`2026.11.12 18:15:00`–`2026.11.16 21:05:00`; H1
`2026.11.12 04:00:00`–`2026.11.16 21:00:00`; H4
`2026.11.10 04:00:00`–`2026.11.16 20:00:00`.

Events: 2 initial snapshots, 627 new-native-M1 events, 4 source recoveries,
9 source-unavailable events and 1 shutdown. Evidence kinds: 635 acquisition
snapshots, 7 valid unavailable-without-sample diagnostics and 1 shutdown.
Nine acquisitions have incomplete-sample/M1-unusable diagnostics; seven also have
unavailable/disconnected acquisition diagnostics. Two runs are observed:
`run-000000000001` (records 1–216, ending in shutdown) and
`run-000000000217` (217–643). No linked publication-sequence gap or blocked
dependency was found in this prefix. M1/M5 have one non-period head advancement
each; H1/H4 have 28/147 non-period adjacent-coordinate observations respectively.
These are repeated sampled coordinate differences, not counts of missing bars.

For example, M1 native `2026.11.16 19:10:00`, raw `1794856200`, is active in
record 525, remains active at recovery 527 across an interruption, and is at
position 1 in record 528. Close changes exactly from
`27441.7999999999992725` to `27439.0999999999985448`, then
`27438.2000000000007276`. The 527→528 change also raises High from
`27444.2999999999992724` to `27446.2000000000007276`.
The report retains all before/after OHLC and record/probe hashes. This is
active-bar evolution and subsequent observed historical position. It is not an
observed historical-to-historical correction or a provider closure guarantee.

The final report is external:
`%LOCALAPPDATA%/MoneyWay/audits/mt5-lifecycle-260286bcdd214818a8930dc54e97774c.json`,
122,940 bytes, SHA256
`e725e81781f8ca4cb23281e91199ff9faf8b148b040a440d9ff4321fc1a01d08`.
Its input manifest digest is
`a84bf6bb39f87128a14d4e6937197097e819bec6c636e39687f30850fa20e3fb`.
The last record 643 SHA256 is
`984a8a26ca1f93182eec60e3a643e432494294b08a3a4b7319c2305608917f26`;
its probe SHA256 is
`483b32c975860b8a743f7e98c877c794d24b5830bf7b338a309a35e06b3c95fc`.

An independent read-only raw-file smoke check, retained outside Git with the
report working artifacts, rechecked all 642 probe hashes and matched the four
frames' bar/revised/stable/active-then-historical and revision-category totals.
It compared literal prices only after checking the actual source's uniform
positive fixed-16-decimal format; it did not parse prices through floating point.
All 1,283 original files pinned before inspection remained byte-identical. The
collector was active and appended evidence during inspection; record 643 and its
probe were validated/pinned by the report rather than claimed as part of that
earlier hash inventory. `collector.lock` was held by the running collector and
was not hashed or modified. The existing
`%LOCALAPPDATA%/MoneyWay/mt5-receipt-observation/checkpoint.json` retained its
before/after hash; no checkpoint store or acknowledgment was invoked.

### Temporal conclusions and validation

Observed head supersession, historical position and identical sampled content
are supported. Post-supersession stability helps measure the observed revision
profile and detect later sampled conflicts. It supplies no promise that an
unobserved revision cannot occur. The absence of historical-to-historical
changes in this bounded dataset is not proof of immutable finality.
`provider_confirmed_closed`, `open_time_utc` and
`historical_available_at_utc` remain null; closure and availability are
`unresolved`. First collector observation establishes a local evidence reference,
not broker publication time or availability to a historical strategy decision.

The [canonical replay contract](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md)
keeps closed canonical candles at or before `AsOfUtc`, immutable synchronized
frames and provider/symbol-bound chronological observations. This diagnostic
does not supply the missing authenticated native-to-UTC mapping, provider
finalization/availability evidence, warmup/freshness policy or atomic capture.
The [UTC capability result](mt5-server-time-policy-audit.md) remains
`REQUIRE_ENVIRONMENT_PREREQUISITE`. No usable authenticated UTC reference or NTS
experiment was established by this feature. All three flags remain false:
`canonical_utc_conversion_authorized`, `utc_conversion_authenticated`,
`canonical_ready`.

Full .NET build passed with zero warnings/errors. The
[focused lifecycle suite](../../tests/integration/MoneyWay.IntegrationTests/Mt5NativeLifecycleTests.cs)
passed 31 cases; existing receipt/checkpoint/observation suites passed 184;
canonical application visibility/prefix suites passed 15, domain multi-timeframe
replay passed 7 and canonical replay/backtest boundary scenarios passed 4.
Total: **241 passed, 0 failed, 0 skipped**. Focused cases include missing/corrupt
dependencies, invalid source/chain, duplicate evidence, partial/unusable frames,
recovery/shutdown/runs, coordinate/sequence gaps, head regression, precise
sub-decimal revisions, immutable prefix/results, source/checkpoint preservation,
bounded output and no overwrite. Markdown/local links, C# whitespace formatting,
report provenance and `git diff --check` are checked before publication.

The single next smallest feature remains a separately authorized supported NTS
client environment prerequisite, followed by a bounded authenticated-reference
experiment. It advances the independent UTC blocker without treating observed
bar stability as a new provider finalization rule.
