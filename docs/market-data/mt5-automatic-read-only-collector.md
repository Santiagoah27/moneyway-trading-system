# MT5 automatic read-only US100 collector

## Scope and verification status

The [Expert Advisor](../../tools/market-data/mt5/MoneyWayReadOnlyCollector.mq5)
automatically observes the existing OANDA Global Markets demo terminal. It has
OnInit/OnTimer/OnDeinit handlers, no OnTick/trade handlers, order APIs, external
dependencies, credentials or strategy evaluation. It refuses initialization unless
the configured company is OANDA Global Markets Limited, server is
`OANDA_Global-Demo-1`, account mode is demo, tester mode is off and global Algo
Trading is **disabled**. Each poll rechecks those conditions; an unsafe change
stops the timer and removes this EA rather than changing terminal permissions.

**Compilation:** MetaEditor reported 0 errors and 0 warnings. Subsequent
[authentic local receipt verification](mt5-local-dotnet-receipt-adapter.md#authentic-local-verification--november-12-2026)
checked real initial/M1-triggered artifacts and their hashes. Long-term continuity
and real reconnect handling remain unverified; synthetic tests do not establish them.

Automatic source collection is **not canonical market-data readiness**. No
runtime UTC converter or canonical ingestion adapter is implemented. The
[local .NET receipt adapter](mt5-local-dotnet-receipt-adapter.md) remains outside
the engine. Preserve the
[server-time audit](mt5-server-time-policy-audit.md),
[ADR 0001](../decisions/0001-select-application-stack.md),
[ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md)
and [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md).

## Install once, then collect automatically

1. In MT5 verify the exact demo source above. Keep global **Algo Trading off**;
   leave the EA's algorithmic order-execution permission disabled too. Do not
   enable DLLs, WebRequest or other permissions for this collector.
2. File > Open Data Folder > `MQL5\Experts`: create a `MoneyWay` folder. Copy
   these three source files together into it:
   - `MoneyWayReadOnlyCollector.mq5`
   - `MoneyWayCollectorDecision.mqh`
   - `MoneyWayCollectorSnapshot.mqh`
3. Open the `.mq5` in MetaEditor and compile with F7. Refresh Navigator > Expert
   Advisors, attach the collector to one chart and retain the defaults initially.
   The chart's own symbol/timeframe does not select the source: the collector
   always requests native **US100 M1/M5/H1/H4**.
4. Check Experts for record filenames/digests and any errors. Subsequent native
   M1 identity changes are captured automatically while the terminal/EA runs.
   If MT5 refuses execution with existing security settings, stop and report the
   blocker; do not enable trading to work around it.
5. Output is isolated under Common Data Folder >
   `Files\MoneyWayAutomaticReadOnly`. The original `MoneyWayReadOnlyProbe`
   directory, authentic inputs and reports are untouched.
6. To stop, remove this EA from the chart. OnDeinit kills its timer first, attempts
   a shutdown record without acquiring market data, and closes the writer lock.
   Closing/restarting MT5 or changing EA inputs can deinitialize/reinitialize it.
   Inspect actual records after restart; a hard process kill can omit shutdown.

The manual probe and its source remain unchanged. Snapshot primitive JSON/OHLC
logic is reused in the collector's isolated snapshot helper, with additional
per-timeframe source checks and internal quality flags. This preserves existing
probe validators/comparator behavior without altering the old script or schema.

## Technical parameters and triggers

| Parameter         | Default / allowed        | Meaning                                                                                                                         |
| ----------------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------- |
| `TimerSeconds`    | 5 / 1–60 seconds         | Terminal observation cadence, not a bar-close or staleness threshold                                                            |
| `SampleBars`      | 16 / 2–128 per timeframe | Bounded native sample including position zero; not an approved strategy warmup                                                  |
| `RetryEveryPolls` | 12 / 1–120 polls         | Retry/backoff for unavailable, incomplete or failed publication attempts; default nominally 60 seconds, not a latency guarantee |

Timers can be coalesced while a handler runs, and CopyRates can wait for history.
The counters measure local processing order, not guaranteed elapsed wall time.

- Initial poll records an initial diagnostic, even when history is unavailable.
- A strictly greater usable native M1 head triggers `new_native_m1`. Timer ticks
  alone do not produce that event. The same healthy head during a market pause
  produces no new-bar record and no disconnection/staleness inference.
- Reconnection with unchanged identity produces `source_recovery`; a genuinely
  newer head remains a native new-M1 event. Disconnected/unsynchronized states
  produce explicit diagnostics and bounded retries. Recovery is automatic only
  while the configured demo identity remains independently readable; if identity
  becomes unverifiable the collector stops and requires safe manual reattachment.
- A regressed head is `native_head_regressed`, not a new bar. No clock repair or
  backward state advancement occurs.
- Incomplete M5/H1/H4 samples, invalid OHLC or a raced M1 head remain diagnostic.
  `incomplete_snapshot_retry` can acquire the same head again without pretending
  it is new. Payload completeness is distinct from trigger/head agreement and
  `source_stable`; all must be inspected. The payload's `m1_usable` flag concerns
  its local M1 data, not permission for canonical ingestion.
- Failed publication does not advance the committed M1 identity. Repeated attempts
  for that head are throttled; a different newly observed head remains eligible.
  Missed intervening heads are not reconstructed as individually observed events.

Each capture independently brackets the four sequential timeframe queries. An
M1 sample is usable internally only when its head bracket, synchronization,
chronology, OHLC and CopyRates result agree. Full sample completeness additionally
requires all requested rows in all four timeframes. Neither criterion is provider
finality, atomic synchronization, source authenticity or canonical readiness.

## Append-only artifacts and integrity

The collector holds `collector.lock` exclusively for its lifetime. Another
collector sharing the same Common folder cannot acquire that lock. It is a local
file lock, not account metadata, and remains as an empty reusable lock file after
shutdown. There is no remote coordination service or database.

| File                       | Contract                                                                                                     |
| -------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `probe-000000000001.json`  | Unchanged `moneyway.mt5-read-only-probe` v1 payload; use the original probe validator and offline comparator |
| `record-000000000001.json` | `moneyway.mt5-read-only-collector` v1 manifest binding sequence/event/source to the exact probe SHA256       |
| `pending-*.tmp`            | In-progress publication; not an admitted capture or completed manifest                                       |

Twelve-digit sequence numbers increase across restarts by inspecting completed
record filenames. Each record links the previous completed record filename and
SHA256; run identity is `run-<first sequence of this run>`, and poll sequence resets
per run. This is local single-writer publication order, **not UTC, broker event
order or proof of first source availability**. The metadata retains trigger and
captured M1 identities separately, plus the previous committed M1 identity.

Snapshot/manifest bytes are UTF-8 without BOM, followed by a newline. SHA256 is
computed with the supported MQL5 crypto API, then checked against the published
file. Staging files are moved without FILE_REWRITE: an existing final file cannot
be overwritten. A manifest is published only after its snapshot. A crash/failure
between these steps can leave an orphan probe or `.tmp`; those files are retained,
not consumed as complete pairs or silently reused. Sequence gaps are permitted.

Technical storage bounds: startup scans at most 100,000 completed manifests;
reservation skips at most 1,024 collisions, with maximum sequence 999999999999.
Exceeding a bound or encountering invalid record filenames fails safely. These
are local tooling limits, not trading rules. No retention/deletion is automated;
disk/write failures are logged and existing files remain intact. Hash links detect
byte changes against retained references; they are not signatures or protection
against an actor rewriting an entire chain. Retain independent hashes as needed.

### Manifest v1 fields

The manifest contains schema/version, fixed `diagnostic_only=true`,
`canonical_ready=false`, `utc_conversion_authenticated=false`, sequence/run/poll,
event/condition, technical settings, configured non-sensitive source, native M1
trigger/previous/captured heads, `m1_usable`, `sample_complete`, `source_stable`,
previous-record reference/digest, probe filename/digest, shutdown reason and
`sequence_basis=local_single_writer_publication_not_utc`.

Event names are `initial_snapshot`, `new_native_m1`, `source_recovery`,
`source_unavailable`, `native_head_regressed`, `incomplete_snapshot_retry`, and
`shutdown`. Conditions 0–4 mean healthy head, disconnected, unavailable M1,
regressed native head, and unsafe source respectively. Shutdown has no probe and
does not claim a fresh source observation. The fixed source describes the approved
collector scope, not a new source query made during shutdown.

The probe preserves native integers/displays and exact OHLC serialization. Its
UTC opening, provider-confirmed closure and historical-availability fields stay
null. Computer-derived GMT and server quote samples retain their original
limitations. Source checks before/after and before individual queries reduce
identity races but do not make acquisition transactional. No timezone conversion,
USTEC alias, gap filling, order/fill/PnL or strategy status is produced.

## Offline checks and manual acceptance

```powershell
& ./tools/market-data/mt5/Test-Mt5CollectorRecord.ps1 -Path '<record-N.json>'
& ./tests/tools/Mt5ReadOnlyCollector.Tests.ps1
& ./tests/tools/Mt5ReadOnlyProbe.Tests.ps1
& ./tests/tools/Mt5ProbeComparator.Tests.ps1
```

The new receipt checker verifies fixed fields, safe sibling references, immediate
previous-record and probe hashes, native-new-M1 claims and payload-quality flags.
It reuses the original probe validator, holds files against concurrent writes,
and returns `CanonicalReady=false` / `AuthenticityAndUtcVerified=false`. It does
not authenticate a whole historical chain or admit candles to the engine.

Tests execute the **actual pure MQL decision function bodies** inside an in-memory
C# class, adding only the class wrapper and public/static method modifiers. There
is no copied state-machine implementation. MQL-specific API behavior is checked
by compilation, explicit source/API allowlists and synthetic artifact tests; it
still needs real-terminal validation. Neither C# test hosting nor the PowerShell
checker is a production .NET receipt adapter.

For manual acceptance, retain an initial record/probe pair, a same-head timer
interval with no new-bar capture, and a genuinely later M1 pair; validate their
hashes and compare the two probe payloads with the existing comparator. Then
remove/reattach the EA and inspect shutdown/run/sequence continuity. Do not induce
account switches, enable trading or deliberately disconnect merely to test this
feature. Capture unavailable/recovery evidence if naturally observed. Record what
actually happened; no real automatic run or reconnect is claimed by these tests.

The [local .NET receipt/validation adapter](mt5-local-dotnet-receipt-adapter.md)
now verifies manifests/probes, identity, sequence and quality with caller-owned
idempotency checkpoints. Durable receipt storage is a separate next feature.
Native data remains outside the canonical engine until the independent
UTC/closure/availability contract is met.

## Official references

- [OANDA Global Markets MT5 guide](https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm).
- [Terminal events and disabled automated trading](https://www.mql5.com/en/docs/runtime/event_fire),
  [OnTimer](https://www.mql5.com/en/docs/event_handlers/ontimer),
  [EventSetTimer](https://www.mql5.com/en/docs/eventfunctions/eventsettimer),
  [EventKillTimer](https://www.mql5.com/en/docs/eventfunctions/eventkilltimer),
  [OnDeinit](https://www.mql5.com/en/docs/event_handlers/ondeinit).
- [CopyRates](https://www.mql5.com/en/docs/series/copyrates),
  [FileOpen](https://www.mql5.com/en/docs/files/fileopen),
  [FileMove without overwrite](https://www.mql5.com/en/docs/files/filemove),
  [FileFindFirst](https://www.mql5.com/en/docs/files/filefindfirst),
  [CryptEncode](https://www.mql5.com/en/docs/common/cryptencode).
