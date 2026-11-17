# OANDA Global Markets MT5 server-time policy audit

Audit date: 2026-11-11. Initial Git checkpoint: `main`,
`93dff96a3e2ca86d8e10cb7fdeafc35db850983d`, clean and synchronized.
The initial November 11 audit was documentation-only. The November 13 diagnostic
extension is recorded below.

## Provider statement and exact applicability

The first-party [Spanish BVI MT5 guide](https://help.oanda.com/bvi/es/faqs/mt5-user-guide-bvi.htm)
identifies its division as OANDA Global Markets, names OANDA Global Markets Limited
and explicitly assigns `OANDA_Global-Demo-1` to demo accounts. This independently
matches the authentic source identity; it is not evidence borrowed from MT4 or
another division.

Its daily-candle section states:

> Las velas diarias de MT5 se cierran a las 17:00 EST (GMT+2 con horario estándar o GMT+3 con horario de verano).

The [English BVI guide](https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm)
was cross-checked. Both retain the literal abbreviation **EST**. This audit does
not silently replace it with ET/EDT, select a named timezone, or infer transition
rules. The published offset pair is supported; a complete historical mapping is
not. The daily-candle statement does not authenticate US100 bar availability.

Sources were accessed on the audit date. No publication/effective date or policy
revision history was established. Out-of-scope OANDA divisions, mobile-only chart
policies, MT4 guides and generic broker conventions are not used to fill gaps.

## Four separate milestones

| Milestone                                           | Status                                             | Meaning                                                                                                           |
| --------------------------------------------------- | -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Provider policy documented                          | `confirmed`, scoped to the published product guide | Correct entity, MT5 and demo server identified; standard/DST offset pair published                                |
| Complete deterministic conversion algorithm defined | `unresolved`                                       | Transition authority/instants, historical validity and source timestamp treatment remain insufficiently supported |
| MoneyWay runtime server-time converter implemented  | No                                                 | CSV import expects already explicit UTC; the probe/comparator do not normalize native time                        |
| Canonical current-data ingestion operational        | No                                                 | Mapping, closure, availability and other acquisition safeguards remain separate prerequisites                     |

The source clock policy is not a trading rule or a new strategy evidence label.
There is no promotion of `utc_conversion_authenticated` or `canonical_ready`.

## MetaQuotes semantics and proof limits

| Source/API                                                                  | Documented semantics                                                                                   | What it does not establish here                                                                       |
| --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| [TimeCurrent](https://www.mql5.com/en/docs/dateandtime/timecurrent)         | Last known server quote time; in this script context it can come from any selected Market Watch symbol | A fresh US100 quote, acquisition completion time, authenticated UTC or historical source availability |
| [TimeGMT](https://www.mql5.com/en/docs/dateandtime/timegmt)                 | GMT calculation based on the computer's time settings, including DST                                   | Independently trusted UTC or a broker offset obtained merely by subtraction                           |
| [TimeTradeServer](https://www.mql5.com/en/docs/dateandtime/timetradeserver) | Client-calculated estimate of server time dependent on computer time settings                          | Independent clock authentication; the current probe does not call this function                       |
| [CopyRates](https://www.mql5.com/en/docs/series/copyrates)                  | Position zero is the current bar; returned history may depend on download/build readiness              | Provider-confirmed closure, first exposure time or exact historical availability                      |
| [MqlRates](https://www.mql5.com/en/docs/constants/structures/mqlrates)      | The time member identifies a bar's opening                                                             | A UTC interpretation of that native coordinate or the receipt time of its OHLC                        |

No assumed trusted UTC clock, latency estimate or first-availability timestamp is
introduced. A timezone correspondence, even when independently established, would
not by itself settle the two finality/availability questions.

## Authentic evidence rechecked locally

The files remained accessible in the existing external `MoneyWayReadOnlyProbe`
folder. Each was independently hashed and passed the existing v1 validator:
64 bars, zero invalid OHLC per file. Inputs were not modified or converted.

| File                           | SHA256                                                             | Server quote display | Computer-derived GMT candidate | Observed difference |
| ------------------------------ | ------------------------------------------------------------------ | -------------------- | ------------------------------ | ------------------: |
| `probe-1794315975-132125.json` | `d2dadabb3586bb2378c4b9287cfaa2d1ff3467d70e6f73220d56baebcf7a8f03` | 2026.11.10 20:06:14  | 2026.11.10 18:06:14            |              +7200s |
| `probe-1794333771-28930.json`  | `b59af8c40497ab7be2f666775db0ab516d795efbb1a0acc5caa82fb13ed964f0` | 2026.11.11 01:02:51  | 2026.11.10 23:02:51            |              +7200s |
| `probe-1794333851-3574.json`   | `76ed94be68ec2b582abf35ffa6f92a35245dc5e91edac6b8f7b80569314ca991` | 2026.11.11 01:04:11  | 2026.11.10 23:04:11            |              +7200s |

The new audit prompt repeated the earlier malformed 66-character digest. The
64-character Earlier hash above is the one explicitly confirmed by the human and
retained in the [authentic comparison record](mt5-first-authentic-two-sample-comparison.md#inputs-provenance-and-hash-correction).
That correction remains authoritative; no new correction or capture-order
attestation is inferred.

The [first acquisition](mt5-first-real-acquisition.md) and actual
[two-sample comparison](mt5-first-authentic-two-sample-comparison.md) remain the
sources for detailed OHLC evidence. The latter observed M1 overlap 14, unchanged
13, one former-current-to-historical change, two newly present and two absent from
the bounded sample. M5/H1/H4 each had overlap 16, unchanged 15 and one current-bar
change. Historical-to-historical discrepancies were zero. Capture order remains
`unverified_caller_labels_only`; this audit does not upgrade it or rerun acquisition.

## Evidence matrix

| Evidence and scope/date                                        | Classification                                                     | Remaining uncertainty                                                                  | Conversion impact                                                                 |
| -------------------------------------------------------------- | ------------------------------------------------------------------ | -------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| Current BVI provider guide; same company/MT5/demo server       | `confirmed` published source applicability                         | No retrospective policy-validity interval established                                  | Supports a scoped policy candidate, not historical activation                     |
| Published standard/DST offset pair                             | `confirmed` statement; `candidate` for future conversion selection | Authority, change instants, historical timestamp behavior and literal EST wording      | Cannot select an offset for an arbitrary native datetime                          |
| Three authentic observations, November 10–11, 2026             | `context_specific` observed +7200s differences                     | Computer-derived reference, non-identical clock sampling and no independent UTC anchor | Consistent with the published standard offset, not independent UTC authentication |
| Position-zero M1 progresses in the compared argument order     | `context_specific`                                                 | Capture order, finality and exact first availability remain unauthenticated            | Does not define timezone transition rules or a receipt timestamp                  |
| Native 23:59→01:00 M1 opening interval                         | `context_specific`; cause `unresolved`                             | Pause, calendar interpretation, history coverage or other explanation not established  | Neither a DST transition observation nor a gap-repair instruction                 |
| Reusing September's separately audited offset as a global rule | `rejected_ai_inference`                                            | September validation is explicitly session-scoped                                      | Do not overwrite, expand or apply that mapping to these dates                     |

The samples corroborate the guide's plausibility independently of its text, but
do **not** independently authenticate UTC: both timestamp sources in the observed
subtraction retain their original limitations. No observations for other dates or
at a seasonal transition are fabricated.

## Requirements for a future deterministic conversion contract

These are admission requirements, not a completed algorithm or approval to add a
converter. Preserve raw coordinates and provenance under
[ADR 0001](../decisions/0001-select-application-stack.md), the bounded pipeline in
[ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md)
and [ADR 0025 causal availability](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md#identity-and-causal-availability).

| Requirement                                    | Evidence classification                                                  | Necessary independent evidence/decision                                                                                                                                                                                             |
| ---------------------------------------------- | ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A. Provider/server scope                       | `confirmed` guide/source identity; `context_specific` US100 observations | Bind an eventual mapping to exact entity, server, timestamp field and applicable dataset; do not add aliases or cross-feed equivalence                                                                                              |
| B. Offset-regime application                   | `confirmed` published pair; selection rule `candidate`                   | Resolve EST wording and obtain a provider-backed definition selecting standard versus DST for this source; do not choose America/New_York, Europe or a machine-local zone silently                                                  |
| C. Transition authority, dates and instants    | `unresolved`                                                             | Obtain authority/rule version and exact effective UTC instants, including before/after offset and boundary inclusivity; calendar dates without instants are insufficient                                                            |
| D. Historical effective periods/policy changes | `unresolved`                                                             | Establish when each policy applied, migrations/exceptions and revision provenance; today's guide cannot automatically authorize all past/future dates                                                                               |
| E. Historical bar timestamp convention         | `unresolved`                                                             | Confirm whether M1/M5/H1/H4 history retains period-specific server coordinates or is relabeled after clock changes; require exact provider-native/UTC reference examples                                                            |
| F. Ambiguous/nonexistent native times          | `unresolved`                                                             | Determine whether/where such cases occur for this source and how to resolve them using supported event identity/sequence. Until resolved, block ambiguity; no earliest/latest choice or automatic time shifting                     |
| G. Pauses and absent intervals                 | `context_specific` observed discontinuities; interpretation `unresolved` | Separate actual source calendars/coverage from timezone mapping. Preserve absent intervals; do not fill them or infer a schedule from the M1 gap                                                                                    |
| H. Independent UTC anchor                      | `human_validation_required`                                              | Retain a dated broker statement/export or other auditable reference tying the same source event/native field to independently established UTC, with scope and uncertainty. A second computer-derived clock estimate is insufficient |
| I. Quote versus acquisition/receipt time       | Distinction `confirmed`; UTC receipt evidence `unresolved`               | Preserve source-event and locally observed receipt/bracket fields separately; authenticate their clock bases. Native quote time must not become copy completion or retroactive backfill availability                                |
| J. Closure and historical availability         | `unresolved`                                                             | Obtain a separate source-backed completed-bar criterion and availability provenance. Neither daily close policy, changed position nor authenticated timezone establishes first receipt or immutable OHLC                            |

The unsupported substitution of a host timezone, a generic DST calendar, a fixed
two-hour offset or a global September rule is `rejected_ai_inference`. If mapping
scope, validity or disambiguation cannot be established, the future boundary must
retain an unresolved result rather than label native data UTC/canonical-ready.

## Existing code/tests: UTC assumptions reviewed, behavior unchanged

- [Candle](../../src/backend/MoneyWay.Domain/MarketData/Candle.cs) requires zero
  offsets and ordered interval endpoints. This checks the supplied representation;
  it does not authenticate the source's native-to-UTC mapping.
- [CsvMarketDataImporter](../../src/backend/MoneyWay.Infrastructure/MarketData/Csv/CsvMarketDataImporter.cs)
  requires explicit UTC strings, `DateTimeStyles.None` and zero offset. Its
  [existing tests](../../tests/integration/MoneyWay.IntegrationTests/CsvMarketDataImporterTests.cs)
  accept Z/+00:00 and reject offset-free or nonzero-offset inputs. There is no
  implicit server-to-UTC fallback in this importer.
- [Probe validation](../../tools/market-data/mt5/Test-Mt5ProbeArtifact.ps1) uses
  ParseExact and an Unspecified-kind reference only to compare native numeric and
  displayed representations. It does not call ToUniversalTime. The comparator
  retains the native raw identity and unauthenticated candidates.
- [Replay frames](../../src/backend/MoneyWay.Domain/MarketData/Replay/MultiTimeframeReplayFrame.cs)
  and [StrategyReplayContext](../../src/backend/MoneyWay.Application/StrategyReplay/StrategyReplayContext.cs)
  preserve UTC AsOfUtc and bounded observations. Those protections rely on truthful
  input provenance; attaching a zero offset to an unconverted server value does
  not satisfy the source contract.

The November 11 audit changed documentation only. Focused runtime tests were
inspected for that audit. Its validation consisted of three actual artifact checks,
hash verification, primary-reference inspection and documentation/link/diff checks.

## November 11 proposed evidence step

Obtain one retained, dated provider clarification for **OANDA_Global-Demo-1** that
defines the governing seasonal-clock authority, exact changeover instants and
historical bar labeling/validity, resolving the EST wording and supplying an
independently UTC-anchored native example. A support request may be drafted for
human submission; this audit sends no message and requests no credentials. If the
provider cannot establish those facts, keep them unresolved rather than implement
a guessed converter. Closure and historical availability still require their own
evidence after any clock-policy clarification.

## November 13 bounded native-to-UTC diagnostic

Initial checkpoint was `main` at `30aa3945ce1c21494cc0bcc11acbed4bf53567ce`,
clean and synchronized with `origin/main`. This extension adds a locally invoked
PowerShell diagnostic with schema `moneyway.mt5-native-utc-evidence`, version 1.
The [entry point](../../tools/market-data/mt5/New-Mt5NativeUtcEvidence.ps1),
[assessment helpers](../../tools/market-data/mt5/Mt5NativeUtcEvidence.ps1) and
[focused tests](../../tests/tools/Mt5NativeUtcEvidence.Tests.ps1) are diagnostic tooling.
The existing manual probe, automatic collector and their v1 validators supply
native identity, quote coordinates, M1 head, connection state and copy errors.
The receipt reader, checkpoint store and replay pipeline remain isolated.

The workflow reads an existing immutable probe or collector record, validates it,
retains exact input bytes and SHA256, then queries Windows time and an explicitly
selected external reference. Monotonic counters and host UTC candidates bracket
file reads and Windows queries. These brackets describe diagnostic reads of old
files. The native event was captured earlier, outside those brackets.

`TimeCurrent` can identify the last quote of any selected Market Watch symbol in
this script context. The existing probes cannot establish a fresh US100-specific
tick or bind their original capture to the new host measurement. Quote freshness,
symbol binding and independent discrimination between hour-offset hypotheses
remain `unresolved`. [MetaQuotes TimeCurrent](https://www.mql5.com/en/docs/dateandtime/timecurrent)

### Operator procedure and artifact contract

Run from the repository root in Windows PowerShell. Select an existing validated
acquisition record with a probe. The report parent must already exist. Each run
requires a fresh filename and creates a separate `<ReportPath>.inputs` directory.

```powershell
$record = Join-Path $env:APPDATA 'MetaQuotes\Terminal\Common\Files\MoneyWayAutomaticReadOnly\record-000000000310.json'
$report = Join-Path $env:TEMP ('moneyway-native-utc-' + [guid]::NewGuid().ToString('N') + '.json')
& ./tools/market-data/mt5/New-Mt5NativeUtcEvidence.ps1 `
    -RecordPath $record -ReferenceHost time.windows.com -ReportPath $report `
    -ReferenceSamples 3 -QueryTimeoutMilliseconds 10000
Get-FileHash -LiteralPath $report -Algorithm SHA256
```

Alternatively supply `-ProbePath` with an absolute manual-probe path in place of
`-RecordPath`. The reference hostname is mandatory. Reference samples are bounded
to 1–5 and each Windows query to 1–30 seconds. Official policy retrieval uses a
10-second timeout per page. No API, Worker, MT5 recompilation or elevated session
is required for this workflow. Unavailable queries retain their errors.

The report preserves source paths/hashes, immediate predecessor when supplied by
a record, read windows, tool hashes, raw Windows outputs, exact decimal reference
measurements, source identity, policy provenance and assessment. The input bundle
retains original JSON bytes and UTF-8 reencodings of the web client's decoded HTML.
Policy hashes identify that saved representation. Retrieval timestamps use the
unverified host clock and carry no historical policy-effective meaning.

Source handles allow concurrent reads while excluding writes during the audit.
Output uses create-only writes and a final move into a fresh report path. Existing
reports and bundles are rejected. An interrupted run can leave a partial bundle
or pending file for inspection. Retry with a fresh path. Retention of the report
and bundle is an operator responsibility because the example uses temporary
storage. Neither a record's immediate predecessor hash nor schema validation
authenticates the complete historical chain or independent source authenticity.

The bounded Windows calls are `/query /source`, `/query /status /verbose` and
`/stripchart /computer:<explicit-host> /samples:<count> /period:1 /rdtsc /dataonly`.
The CSV parser preserves request FILETIME, counter order, reported roundtrip and
NTP offset. FILETIME remains a local request UTC candidate. Offset corrections
and latency/skew acceptance thresholds are unspecified. An exit-zero query with
valid samples yields `context_specific` reference evidence. Timeout, error or
missing valid samples yields `data_unavailable`. Reference authentication and
host synchronization remain `unverified`. [Windows Time tools](https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/Windows-Time-Service-Tools-and-Settings)

### Authentic November 13 execution

The final diagnostic consumed existing acquisition 310 from the external
`MoneyWayAutomaticReadOnly` directory. All three original files and retained byte
copies were independently rehashed and matched the report, including the hashes
already checked during this audit before the final run.

| Input                      | SHA256                                                             |
| -------------------------- | ------------------------------------------------------------------ |
| `record-000000000310.json` | `c00d4a163fac035207f2dc85ca656761d201735849c6aaad064629acef11f356` |
| `record-000000000309.json` | `013582e9346b21cdae92495204c5e3414cf59fb4ad82aefd332aaac6ec382f99` |
| `probe-000000000310.json`  | `17d5692e4ec081c90bf04f3e8f27d28327d025dd2fb372ac0b3cc6d9223d59a0` |

The external report is retained at
`%LOCALAPPDATA%\Temp\moneyway-native-utc-final-8a40c3421d6c49c99c2a96ba788dd13e\native-utc.json`
with SHA256 `d329d20423c88e03de8907b3244c8261d9c95d16d965602e99a03a062e16de23`.
Its trace is `92d26927ba6849fc9f03dd1ad5094f8f`. Raw artifacts remain external to
Git. This section registers the measurements and hashes in the repository.

Source identity is OANDA Global Markets Limited / `OANDA_Global-Demo-1`, demo
verified, native `US100`. Gate passed, connected before and after, and every
M1/M5/H1/H4 copy returned 16 bars with copy error zero. Native quote before/after
and M1 head are raw `1794599880`, displayed `2026.11.13 19:58:00`. Computer-derived
GMT is raw `1794592681`, displayed `2026.11.13 17:58:01`. Exact subtraction is
**7199 seconds** in both cases. It matches neither published integer offset.
The assessment is `unresolved`. No tolerance, rounding or causal explanation for
the one-second difference is selected. The unchanged quote does not establish age.

The host diagnostic bracket is `2026-11-13T18:09:13.6201687+00:00` through
`2026-11-13T18:09:19.1175924+00:00`, all host-clock candidates. Source query failed
with `0x80070005` access denied. Status query exited zero but reported Leap
Indicator 3 (not synchronized), Stratum 0, State Machine 0 (Unset), source
`time.windows.com,0x9` and Last Sync Error 1 (no time data).

The independent network measurement against `time.windows.com` returned three
samples with exit zero. These are newly measured external reference evidence,
classified `context_specific`. The NTP exchange is unauthenticated and cannot
attest absolute UTC accuracy or authenticate the older native event.

| Local request UTC candidate    | Reported roundtrip seconds | Reported NTP offset seconds |
| ------------------------------ | -------------------------: | --------------------------: |
| `2026-11-13T18:09:16.7057975Z` |                `0.1010499` |                `-0.7293726` |
| `2026-11-13T18:09:17.8175509Z` |                `0.0983213` |                `-0.7277818` |
| `2026-11-13T18:09:18.9307232Z` |                `0.0897412` |                `-0.7350449` |

The broker/reference relationship is
`insufficient_existing_probe_capture_not_bracketed_by_host_measurement`.
`canonical_utc_conversion_authorized`, `utc_conversion_authenticated` and
`canonical_ready` are all `false`. Native raw coordinates retain their original
meaning. No historical availability, candle closure or atomic multi-timeframe
snapshot is established.

### Refetched provider evidence and coverage

Both fixed official pages were retrieved during the final run. The MT5 guide
identified the demo server and the published GMT+2/GMT+3 pair, retaining literal
EST wording. Retrieval candidate was `2026-11-13T18:09:19.1175924+00:00`, saved
HTML SHA256 `688c76b45b424ab658c16159ca8fe25d86a809fb8583a5a08d93b9fadd9e5db9`.
The selection rule, historical effective dates and transition authority remain
`unresolved`. [OANDA Global Markets MT5 guide](https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm)

The mobile guide corroborates the published offset pair within its own product
scope. It explicitly requires a live MT5 account and excludes demo use of that
mobile application. Its evidence therefore cannot independently establish demo
applicability. Retrieval candidate was `2026-11-13T18:09:20.7715557+00:00`, saved
HTML SHA256 `e8bc6752569b1bce414fcf10cbe2dfebbb91b13a22d488f9b7f36a9b112e5275`.
[OANDA mobile guide](https://help.oanda.com/bvi/es/faqs/oanda-mobile-user-guide-bvi.htm)

| Area                                | Actual coverage                                                                        | Evidence still required                                                             |
| ----------------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Standard/winter +2                  | Published policy and earlier November +7200 computer-derived observations, `candidate` | Contemporaneous native event independently related to UTC                           |
| Daylight/summer +3                  | Published policy, synthetic candidate tests                                            | Authentic independently anchored observation within that regime                     |
| Provider transitions                | `unresolved`                                                                           | Source-backed authority, exact before/after instants and observed boundary behavior |
| Historical applicability            | `unresolved`                                                                           | Dated policy validity and native historical labeling evidence                       |
| Ambiguous/nonexistent times         | `unresolved`                                                                           | Provider-specific disambiguation and boundary evidence                              |
| Quote freshness and timing          | `unverified`, insufficient event pairing                                               | Symbol-specific tick and causally bound measurement windows                         |
| Closure and historical availability | `unresolved`                                                                           | Separate source-backed finality and availability contracts                          |

The host timezone, a generic seasonal calendar, current November observations
and the separately scoped September mapping cannot establish missing historical
periods. Nasdaq strategy time remains `America/Bogota`. Canonical integration
continues to require the independent contracts in the matrix above.

### Validation and smallest next feature

Full solution build passed with zero warnings/errors. The five focused .NET MT5
receipt/observation/checkpoint regression classes passed 184 tests, failed 0,
skipped 0. Existing PowerShell collector, probe and comparator suites passed
39 + 27 + 32 tests. The new synthetic diagnostic suite passed 28 tests. Combined
results are 310 passed, 0 failed, 0 skipped. Synthetic +7200/+10800 cases validate
assessment behavior only. They provide no authentic seasonal observations.

The new tests cover policy scope/provenance, identity failures, native timestamp
validation, unknown freshness/synchronization, missing and regressing host
readings, exact external offsets, failed/timed-out/malformed samples, capture
ordering, +2/+3 candidates, exact 7199 mismatch, unresolved historical/DST cases,
partial inputs, immutable outputs, checkpoint/source preservation and the absence
of execution/strategy calls. PowerShell AST parsing, Markdown formatting, local
links and `git diff --check` are checked before publication.

The next smallest feature is one bounded, nonce-bound US100 tick diagnostic
capture, bracketed by host/external measurements. Preserve `MqlTick` raw fields,
provider identity, read ordering and quote uncertainty, then assess whether that
specific event can distinguish the hour-offset candidates. This requires a fresh
local observation and retains all seasonal/historical gaps above. The current
feature performs no broker-support contact. [MetaQuotes MqlTick](https://www.mql5.com/en/docs/constants/structures/mqltick)

## November 13 correlated US100 tick diagnostic

Initial checkpoint for this extension was `main` at
`f8785fad2d7bfd4495431142ca7bd14906f578c8`, clean and synchronized with
`origin/main`. The operational entry point is
[Invoke-Mt5CorrelatedTickCapture.ps1](../../tools/market-data/mt5/Invoke-Mt5CorrelatedTickCapture.ps1).
It reuses the existing bounded W32Time implementation and exact decimal sample
parser through [Mt5CorrelatedTickEvidence.ps1](../../tools/market-data/mt5/Mt5CorrelatedTickEvidence.ps1).
The dedicated [MoneyWayCorrelatedTick EA](../../tools/market-data/mt5/MoneyWayCorrelatedTick.mq5)
and its [scalar decisions](../../tools/market-data/mt5/MoneyWayCorrelatedTickDecision.mqh)
operate in `FILE_COMMON\MoneyWayCorrelatedTick\<nonce>`. Existing production
collector code, receipt contracts/checkpoints and canonical replay retain their
current behavior and isolation under ADR 0001 and ADR 0004.

### Correlation and event semantics

1. The host generates a 32-byte cryptographic nonce, creates a fresh directory and
   atomically publishes `offer.txt` with that nonce, explicit session duration and
   `US100` identity. Protocol text uses UTF-8 and LF with a versioned first line.
2. The operator attaches the EA to a separate US100 chart with `SessionNonce`.
   The EA checks company/server/demo/chart symbol, connection, tester status and
   disabled Algo Trading. It holds an exclusive session lock. Existing ready,
   request, response or cancellation files disqualify a reused nonce.
3. The EA publishes `ready.json` with the offer hash and a per-instance token.
   The host verifies identity, nonce and offer hash. It queries Windows source and
   synchronization status and collects a bounded external measurement.
4. The host atomically publishes `request.txt`, binding nonce, ready token, US100
   and remaining capture duration. A request predating EA readiness is rejected.
   The token prevents an earlier request from arming a different EA instance.
5. The EA accepts that exact request on its timer, records its hash and obtains a
   baseline latest tick. A subsequent `OnTick` callback must advance both callback
   ordinal and native `time_msc` beyond that baseline. Cached or regressing tick
   timestamps remain ineligible. Equal-millisecond updates can remain unavailable.
6. Within `OnTick`, `SymbolInfoTick(_Symbol, captured)` preserves raw seconds and
   milliseconds, bid/ask/last, volume, real volume and flags. Floating-point fields
   use scientific decimal strings with 17 significant digits. The context includes
   `TimeCurrent`, `TimeTradeServer`, `TimeGMT`, `TimeLocal`, optional native M1 head
   and error codes. `context.json` is hashed into the response.
7. The EA flushes and closes pending output before a create-only final move.
   `response.json` binds nonce, ready token, request hash, context hash, source
   identity, callback counters and terminal monotonic readings. The host validates
   these fields, retains receipt boundaries and measures the external reference
   again. Host publication/receipt boundaries use Stopwatch plus host UTC candidates.

The fresh-tick assessment is `context_specific_subsequent_OnTick_with_native_timestamp_progression`.
It describes local handler observation and timestamp progression. The event queue
can coalesce updates, and the latest lookup may reflect an update different from
the queued event that caused the callback. Network quote generation after the
request and exact queued-event identity remain `unresolved`. MetaQuotes documents
that `OnTick` continues when Algo Trading is disabled and that queued NewTick
events can be coalesced. [MetaQuotes OnTick](https://www.mql5.com/en/docs/event_handlers/ontick)

The same nonce identifies one session and its single accepted tick observation.
Its role is correlation. Source provenance remains dependent on the local terminal
and operator, with source identity validation kept separate from UTC authority.
Host-recorded repository source hashes document the tool inputs. They cannot attest
which compiled binary was attached to a chart.

### Evidence bundle and UTC assessment

Each session retains `offer.txt`, `ready.json`, `request.txt`, `context.json`,
`response.json`, a session lock and cancellation marker as those phases occur.
The compact final `evidence.json` uses schema
`moneyway.mt5-correlated-tick-evidence`, version 1. It includes nonce, outcome,
reason, publication/receipt windows, monotonic frequency, native response/context,
raw Windows outputs, external measurement results, tool/input hashes, assessments
and unresolved guarantees. Unobserved request/receipt fields remain null.
Raw files stay external to Git. Partial files and failed sessions remain available
for audit. Retain the entire directory together with the final report hash.

The host performs one external sample immediately before request publication and
one after receipt or session termination. Each uses the existing explicitly named
reference, bounded timeout, raw `/rdtsc` output and exact decimal offset/roundtrip
parser. Missing references are `data_unavailable`. Host synchronization and external
reference authentication remain `unverified`, including when measurements succeed.
Windows query failures are retained. [Windows Time tools](https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/Windows-Time-Service-Tools-and-Settings)

The report preserves the exact native millisecond tick minus computer-derived GMT
difference. For each published +7200/+10800 hypothesis it records a conditional
coordinate and membership in the **uncorrected host observation interval**. That
interval brackets local request publication and response receipt. Its precision
comes from retained host candidates and monotonic counters. NTP measurements and
roundtrips remain separate evidence with their original uncertainty. This test of
compatibility supplies a `candidate` hypothesis and applies no NTP correction or
skew tolerance. Native source-event UTC remains unresolved even if only one
hypothesis fits the local interval. [OANDA MT5 policy](https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm)

| Evidence                | Independent limitation                                                            |
| ----------------------- | --------------------------------------------------------------------------------- |
| `TimeCurrent`           | An event-associated native server clock value retains provider clock semantics    |
| Cached `SymbolInfoTick` | Latest stored data alone cannot establish a subsequent event                      |
| `TimeGMT`               | Computer settings and synchronization determine this clock                        |
| Unsigned NTP sample     | Offset and roundtrip measurements retain authority, network and delay uncertainty |
| Nonce handshake         | Correlates local artifacts while network quote generation time remains unresolved |
| Native M1 head          | Identifies a bar opening with closure and availability assessed separately        |

`canonical_utc_conversion_authorized`, `utc_conversion_authenticated` and
`canonical_ready` remain `false`. Provider historical DST selection, transition
authority/instants, ambiguous times, historical policy validity, authenticated UTC
event mapping, provider candle closure, historical availability, atomic
multi-timeframe acquisition and canonical integration retain their earlier blockers.
Nasdaq strategy time remains `America/Bogota`.

### Supervised operator procedure

Keep Algo Trading disabled throughout. Use MT5 **File > Open Data Folder** to
identify the active terminal's data folder, then set `$terminalData` below to that
exact path. Run installation commands from the repository root. The unique expert
subdirectory keeps each installation separate.

```powershell
$terminalData = '<paste the MT5 data folder here>'
$eaFolder = Join-Path $terminalData ('MQL5\Experts\MoneyWayDiagnostics\CorrelatedTick-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $eaFolder -Force | Out-Null
Copy-Item -LiteralPath ./tools/market-data/mt5/MoneyWayCorrelatedTick.mq5 -Destination $eaFolder
Copy-Item -LiteralPath ./tools/market-data/mt5/MoneyWayCorrelatedTickDecision.mqh -Destination $eaFolder
$ea = Join-Path $eaFolder 'MoneyWayCorrelatedTick.mq5'
$log = Join-Path $eaFolder 'compile.log'
$editor = Start-Process -FilePath 'C:\Program Files\MetaTrader 5\MetaEditor64.exe' `
    -ArgumentList @('/compile:"' + $ea + '"', '/log:"' + $log + '"') `
    -WindowStyle Hidden -PassThru
$editor.WaitForExit()
Get-Content -LiteralPath $log
```

Alternatively open `$ea` in MetaEditor and press F7. Verify the actual compiler
result and generated `.ex5` before attachment. In MT5, open a **new US100 chart**
on the demo account, leaving the production collector on its existing chart.
Refresh Navigator and locate `MoneyWayCorrelatedTick` in the unique expert folder.
Prepare its Inputs dialog with Algo Trading still disabled.

Start the host command, then attach the EA to that separate chart using the
printed `SessionNonce` before the operator-selected session expires.

```powershell
& ./tools/market-data/mt5/Invoke-Mt5CorrelatedTickCapture.ps1 `
    -SessionSeconds 180 -ReferenceHost time.windows.com -QueryTimeoutMilliseconds 5000
```

SessionSeconds is required, between 1 and 900. QueryTimeoutMilliseconds is required,
between 1000 and 30000. Ready/capture waiting uses the same monotonic session
deadline. Four bounded Windows queries can extend total command time by at most
four query timeouts beyond that waiting limit, plus local I/O/scheduling. The EA
also has its own operator-supplied duration and checks cancellation on timer/tick
callbacks. A forced host termination leaves the EA's independent limit in place.

Outputs appear under
`%APPDATA%\MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick\<printed-nonce>`.
For supervised cancellation, create the marker in a second PowerShell window.
Replace the nonce with the printed value. The coordinator then writes a normal
`data_unavailable` cancellation report.

```powershell
$nonce = '<printed nonce>'
$session = Join-Path $env:APPDATA ('MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick\' + $nonce)
New-Item -ItemType File -Path (Join-Path $session 'cancel.txt')
```

The same marker also closes a normally completed session. After completion,
inspect `evidence.json`, compare request/response/report nonce, verify recorded
SHA256 values and retain the report's own SHA256. The host already validates the
ready token, request hash and context hash before accepting a capture.

```powershell
$report = Join-Path $session 'evidence.json'
Get-Content -LiteralPath $report -Raw | ConvertFrom-Json | Select-Object nonce,outcome,reason,assessment
Get-FileHash -LiteralPath $report -Algorithm SHA256
```

The EA removes itself after a terminal outcome. If it remains attached after an
interruption, remove it from the separate chart using **Expert List > Remove**.
Close that diagnostic chart if desired. Keep the evidence directory for audit.
Every subsequent attempt uses a fresh coordinator nonce. MoneyWay API/Worker
startup is outside this local procedure.

### Actual validation and remaining manual observation

MetaEditor was available and compiled the final EA plus shared header with
**0 errors and 0 warnings**. The build is retained externally in
`%LOCALAPPDATA%\Temp\moneyway-correlated-tick-final-269dd8160ba4493f8723708d8057bb75`.
The generated `.ex5` SHA256 is
`4162676c3897575258a55a3b884b9005efdd4d0dc5e8b478f6c20a20317294cb`.
Compilation establishes a successful build. Attaching that binary and observing
an authentic US100 callback remain manual validation steps.

The final host-only run used a 2-second session and 1000 ms query timeout with
`time.windows.com`. The EA was left unattached. The real result was
`data_unavailable` / `ea_unavailable_timeout`, with request/receipt boundaries null
and canonical authorization false. This outcome establishes host timeout behavior.
Market tick delivery was untested.

- Nonce `5553ac5f50515b88484cbe9da832f516664feeb8a0652be582007b51c0df81aa`.
- External report `MoneyWayCorrelatedTick\<nonce>\evidence.json` under the common
  files directory above, SHA256
  `e96466bdd0411c3bca4a68b858f12146468ebe8d0a80f2b52caf53f40f7f1eee`.
- Real post-session reference sample at local request candidate
  `2026-11-13T19:03:44.1649480Z`, roundtrip `0.0868949` seconds and reported NTP
  offset `-0.7479470` seconds. Pre-request reference and Windows status/source
  queries remained unobserved because EA readiness was absent.
- Input and tool hashes were checked against the retained final bundle. Existing
  authentic record 310, predecessor 309 and probe 310 still matched the hashes
  registered in the earlier audit. Production receipt/checkpoint paths were absent
  from the new workflow's writes.

The [new automated suite](../../tests/tools/Mt5CorrelatedTickEvidence.Tests.ps1)
passed 56 synthetic tests. It executes the actual shared MQL scalar decisions in
a C# host, validates protocol/evidence handling and exercises the real coordinator
against a synthetic file peer with mocked Windows queries. These tests cover
success, cache rejection, nonce replay/mismatch, identity, cancellation/timeouts,
atomic publication, partial files, context binding, bounds, conditional +2/+3,
precision, hashes and safety isolation. Terminal APIs and authentic OnTick delivery
remain outside those synthetic tests.

Full solution build passed with 0 warnings/errors. Existing .NET MT5 receipt,
checkpoint and observation regressions passed 184 tests. Existing PowerShell
native-UTC, collector, probe and comparator suites passed 28 + 39 + 27 + 32 tests.
Together with the 56 new tests, totals are **366 passed, 0 failed, 0 skipped**.
PowerShell AST, Markdown format/local links, retained bundle integrity and
`git diff --check` are verified before publication.

The smallest next action is one supervised execution of this compiled diagnostic
on the separate demo US100 chart while source ticks are available. Retain the
nonce-bound bundle and assess its actual native tick against the two reference
measurements. The remaining historical/DST/finality contracts above continue to
govern any later canonical integration decision.

## November 16 first authentic correlated tick admission

Review date is 2026-11-16. Initial checkpoint was `main` at
`8267853f89366f7afec88b0606f88fd63d0e4b1f`, clean and synchronized with
`origin/main`. The human operator reported running the actual diagnostic EA and
coordinator on the demo terminal. Codex independently accessed and revalidated the
original local bundle. The historical terminal execution remains human-attested;
the file integrity, lineage and calculations below were independently checked.

Session nonce is
`d4542012e0528790739f573642ec1271eb0151d59758bb1c53dd29467d491dfa`.
The original directory is
`%APPDATA%\MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick\<nonce>`.
All seven evidentiary files were available. Their exact SHA256 values matched the
original manifest and the two separately supplied human hash pins.

| Original file   | Verified SHA256                                                    |
| --------------- | ------------------------------------------------------------------ |
| `offer.txt`     | `5c5cbb13e2b97468b72986eeacd2a29670efbd26149471df84185c27d457f816` |
| `ready.json`    | `5db48481d2bb339151dc919a401be427383593d5f980ce9095dbbf3cd04f4b67` |
| `request.txt`   | `6e05be2b48fb20f1aea337848a053bb9701482a674e47eabfc8b89c89df737af` |
| `context.json`  | `ed997c95a9fcbcf2de72f923affdb31ee125aea5a14a6a997032122a25af6311` |
| `response.json` | `4b9760ac783abe49ada999f71a4c7df0e3d7a6481a622ae8a4f202e4badd7557` |
| `cancel.txt`    | `ce23e4e0d2320b30199a4293d463fa19794b7768dd1cb74b57024ad4735b9394` |
| `evidence.json` | `e8627f3c80c4bae81dcfd17f3397df525e855fbdc12d368ffc252f5f2230379b` |

### Lineage and provenance verification

| Component                         | Independent review result                    | Scope                                                                                                                                            |
| --------------------------------- | -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Versioned schemas and outcomes    | `confirmed`                                  | Evidence/ready/response v1, `captured`, `subsequent_ontick_timestamp_progressed`, canonical authorization false                                  |
| Recorded source identity          | `confirmed` artifact contents                | OANDA Global Markets Limited, `OANDA_Global-Demo-1`, demo true, native US100, connected true, Algo Trading false in ready and response           |
| Offer and request                 | `confirmed`                                  | Exact nonce, US100, operator limits, ready token and request SHA256 bindings validated                                                           |
| Context and standalone response   | `confirmed`                                  | Context SHA256 and native representations validated; standalone values equal embedded evidence values semantically                               |
| Original evidence manifest        | `confirmed`                                  | All six referenced raw files match their hashes; evidence itself matches the separate supplied hash                                              |
| Recorded diagnostic source hashes | `confirmed` repository correspondence        | All five match repository files at review checkpoint; deployment of a particular binary remains human-attested                                   |
| Reference samples                 | `confirmed` recorded measurement consistency | Both raw CSV outputs were reparsed with the existing exact-decimal parser and compared with embedded samples                                     |
| Assessment                        | `confirmed` reproducibility                  | Recomputed using the existing evaluator from independently validated response/context, host bounds and reparsed reference measurements           |
| Authentic EA operation            | Human-observed historical evidence           | The operator's observation and retained terminal artifacts establish this session's provenance; Codex reviewed the resulting artifacts afterward |

The ready token is `375025968-3303-99695352353280`. Request duration was 272866 ms
within the 300-second session limit. Ready and response source identities match.
The five source files are `Invoke-Mt5CorrelatedTickCapture.ps1`,
`Mt5CorrelatedTickEvidence.ps1`, `Mt5NativeUtcEvidence.ps1`,
`MoneyWayCorrelatedTick.mq5` and `MoneyWayCorrelatedTickDecision.mqh`. Their original
hashes and review-time matches remain in the retained evidence and review manifest.

### Native progression and host observation

| Recorded field                                 | Exact value                             |
| ---------------------------------------------- | --------------------------------------- |
| Baseline native `time_msc`                     | `1794856450359`                         |
| Captured native `time_msc`                     | `1794856450417`                         |
| Native progression                             | `58` milliseconds                       |
| Captured native seconds/display                | `1794856450` / `2026.11.16 19:14:10`    |
| Captured bid decimal string                    | `2.7442200000000001e+04`                |
| Captured ask decimal string                    | `2.7444000000000000e+04`                |
| Captured last decimal string                   | `2.7443099999999999e+04`                |
| Flags / volume / real volume                   | `1030` / `0` / `0.0000000000000000e+00` |
| Tick error                                     | `0`                                     |
| Armed callback count / captured callback count | `23` / `25`                             |
| Cached callbacks rejected                      | `1`                                     |
| Request publication before                     | `2026-11-16T17:14:10.2482979+00:00`     |
| Request publication after                      | `2026-11-16T17:14:10.3057568+00:00`     |
| Host receipt before                            | `2026-11-16T17:14:10.5750488+00:00`     |
| Host receipt after                             | `2026-11-16T17:14:10.6067635+00:00`     |
| Uncorrected host observation interval          | `358.4656` milliseconds                 |
| Native tick minus computer-derived GMT         | `7200.417` seconds                      |

The host boundary order was checked in both the recorded UTC candidates and their
monotonic counters. The raw terminal progression and callback counters passed the
existing capture contract. This is `context_specific` fresh-tick evidence with
`nonce_correlated=true`, `source_identity_contract_valid=true` and
`handler_observation_bounded=true`. It preserves
`queued_event_to_latest_lookup_identity=unresolved`,
`network_quote_generated_after_request=unresolved` and
`source_event_UTC_bounded=false`. A latest-tick lookup can differ from the queued
update that caused a callback, and callback counts cannot enumerate every feed
update. The recorded native M1 head is `2026.11.16 19:14:00`, with its closure and
availability unresolved.

### External reference and candidate policy

| Reference phase | Local request UTC candidate    | Reported NTP offset seconds | Reported roundtrip seconds |
| --------------- | ------------------------------ | --------------------------: | -------------------------: |
| Before request  | `2026-11-16T17:14:09.7849729Z` |                `+0.0151833` |                `0.1176115` |
| After receipt   | `2026-11-16T17:14:11.3093792Z` |                `+0.0002248` |                `0.0892331` |

Both measurements targeted `time.windows.com`, exited zero and were recorded in
the expected before/after order. The Windows source query returned access denied
`0x80070005`. The separate status query exited zero and reported Leap Indicator 3
(not synchronized), Stratum 0, source `time.windows.com,0x9` and Last Sync Error 1.
Host synchronization and reference authentication remain `unverified`. The offset
and roundtrip numbers retain their measured precision and uncertainty.

Under the conditional GMT+2 hypothesis, the native coordinate corresponds to
`2026-11-16T17:14:10.417Z` and falls inside the uncorrected host observation
interval. Under GMT+3 it corresponds to `2026-11-16T16:14:10.417Z`, outside that
interval. The recomputed comparison supports GMT+2 as `candidate` for this
observation. It establishes a reproducible local consistency result. Independently
authenticated UTC event timing, provider-wide policy activation and historical
offset selection remain `unresolved`. No NTP correction or skew tolerance was
applied.

Official sources were retrieved and reviewed on **2026-11-16** with these scopes.

| Official source                                                                                                                                          | Relevant scope and remaining limit                                                                                                                                            |
| -------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [MetaQuotes OnTick](https://www.mql5.com/en/docs/event_handlers/ontick)                                                                                  | Chart-symbol event handling, event coalescing and operation with Algo Trading disabled; broker-specific UTC authority remains outside this API contract                       |
| [MetaQuotes MqlTick](https://www.mql5.com/en/docs/constants/structures/mqltick)                                                                          | Latest-price fields, seconds/milliseconds and tick flags; provider-specific timestamp interpretation requires separate evidence                                               |
| [OANDA Global Markets MT5 guide](https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm)                                                              | Correct division/demo server and published GMT+2/GMT+3 pair with literal EST wording; exact transition authority, instants and historical effective periods remain unresolved |
| [Microsoft Windows Time tools](https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/Windows-Time-Service-Tools-and-Settings) | Query/stripchart semantics, local request FILETIME and reported NTP computations; these outputs retain their synchronization, authentication and delay limitations            |

### Local retention and reproducible admission

Existing tools already provide create-only publication, SHA256 and immutable-input
copies. The repository had no whole-session retention/import command. The new
[Import-Mt5CorrelatedTickEvidence.ps1](../../tools/market-data/mt5/Import-Mt5CorrelatedTickEvidence.ps1)
reuses those primitives and the existing ready/response/native/NTP/assessment
helpers. It adds one offline admission workflow outside the production ledger.

The seven original files were copied byte for byte into
`%LOCALAPPDATA%\MoneyWay\Evidence\Mt5CorrelatedTick\<nonce>`.
The empty operational `session.lock` remains in the source directory and is
excluded from retained evidence. A `retention.json` manifest adds provenance,
the seven hashes, independent check results and the recomputed assessment.
`retention.sha256` binds that review manifest's bytes. Its SHA256 is
`306b5fa501307af9385f9baabe4dd71be8cfd2047add79a6e4c74e7b14ec18fa`.
Review-time host UTC candidate is `2026-11-16T17:42:33.3288008+00:00`, recorded
separately from the earlier native capture and host observation bounds.

Source provenance is `human_attested_local_artifacts`. Input handles exclude
writers throughout validation/copying. Copies and metadata use create-only writes,
hash checks and a final directory move. Existing nonce directories are verified
and return `already_retained` when identical. A conflicting or partial retained
session blocks import without replacing or completing it. Interrupted publication
can leave a uniquely named `.pending-*` directory for inspection. A subsequent
import can publish a new complete copy while preserving that interrupted attempt.
Concurrent publications cannot overwrite the winning directory. A losing caller
can retry to verify it as `already_retained`.

Retention is local, append-only by workflow convention, with no automatic expiry
or deletion. SHA256 supplies content-integrity checks. Local administrative
rewrites and cryptographic source authentication remain outside that guarantee.
The retained files, manifest and checksum must be kept together. Repeating this
authentic admission returned `already_retained`, preserving all bytes and the
original review timestamp. Original source hashes remained unchanged.

From the repository root, the following command revalidates the authentic source
and performs the idempotent import. RetentionRoot must be an existing local
directory outside the repository, source tree and MT5 common-files directory.

```powershell
$nonce = 'd4542012e0528790739f573642ec1271eb0151d59758bb1c53dd29467d491dfa'
$source = Join-Path $env:APPDATA ('MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick\' + $nonce)
$retention = Join-Path $env:LOCALAPPDATA 'MoneyWay\Evidence\Mt5CorrelatedTick'
New-Item -ItemType Directory -Path $retention -Force | Out-Null
& ./tools/market-data/mt5/Import-Mt5CorrelatedTickEvidence.ps1 `
    -SourceDirectory $source -RetentionRoot $retention `
    -ExpectedEvidenceSHA256 e8627f3c80c4bae81dcfd17f3397df525e855fbdc12d368ffc252f5f2230379b `
    -ExpectedResponseSHA256 4b9760ac783abe49ada999f71a4c7df0e3d7a6481a622ae8a4f202e4badd7557
```

### Admission status, validation and next feature

File integrity, nonce lineage, recorded identity and native progression are
`confirmed` at the local artifact-review level. Authentic terminal execution is
human-observed provenance. The feed observation and handler bounds are
`context_specific`. GMT+2 interpretation remains `candidate` for this session.
The three flags `canonical_utc_conversion_authorized`,
`utc_conversion_authenticated` and `canonical_ready` remain `false`.
Independently authenticated UTC, historical/DST selection and transitions,
ambiguous native times, provider candle closure, historical availability, atomic
multi-timeframe acquisition and canonical current-data integration remain
`unresolved`. Nasdaq session timezone remains `America/Bogota`.

Full .NET solution build passed with 0 warnings/errors. The
[new retention suite](../../tests/tools/Mt5CorrelatedTickRetention.Tests.ps1) passed
33 synthetic tests, and the existing correlated-tick/native-UTC/collector/probe/
comparator PowerShell suites passed 56 + 28 + 39 + 27 + 32 tests. Total is
**215 passed, 0 failed, 0 skipped**. Synthetic tests cover complete lineage,
negative/tampered/partial bundles, exact times/prices, 58 ms progression, semantic
property reordering on PowerShell 5.1, host/reference order, candidate comparison,
idempotency/conflicts, hashes and source/checkpoint preservation. They remain
separate from the authentic admission above. Receipt runtime code was untouched;
.NET receipt tests were outside this change's affected surface. AST, Markdown
format/links, source/retained hash integrity and `git diff --check` are verified
before publication.

The next smallest feature is a read-only feasibility audit of authenticated UTC
references available to this host. It should identify what authority evidence can
be retained with the current tools and how it could accompany a future correlated
capture. This addresses the unresolved reference-authentication contract while
preserving the separate historical/DST/finality blockers.

## Authenticated UTC reference availability audit — November 16, 2026

Initial checkpoint was `main` at `408c1e2804062bef4adbbabbd0a7a88e421ac16a`,
clean and synchronized with `origin/main`. The decision is
**`REQUIRE_ENVIRONMENT_PREREQUISITE`**. This Windows execution environment did not
demonstrate an authenticated UTC reference usable by the current diagnostic.
Cloudflare NTS remains the protocol candidate. A compatible established client,
approved supported environment and validated read-only capture mode remain
prerequisites for an actual authenticated exchange.

The [on-demand audit](../../tools/market-data/mt5/Get-Mt5UtcReferenceCapability.ps1)
and [capability helpers](../../tools/market-data/mt5/Mt5UtcReferenceCapability.ps1)
reuse existing bounded W32Time calls, exact-decimal parsing, immutable publication,
path checks and SHA256. Network activity is confined to this explicit diagnostic.
ADR 0001 and ADR 0004 continue to govern isolation from strategy/replay code.

### Official features and platform scope

The following primary sources were retrieved and reviewed on **2026-11-16**.
Published capability and local availability are recorded independently.

| Source                                                                                                                                         | Documented capability and scope                                                                                                                                                                                                                                                                                                 |
| ---------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [RFC 8915](https://www.rfc-editor.org/rfc/rfc8915.html)                                                                                        | Standards-track NTS requires authenticated NTS-KE over TLS 1.3 or later, `ntske/1` ALPN and authenticated NTP extension fields using negotiated key material. NTS-KE uses TCP 4460. A successful TLS connection alone cannot authenticate a subsequent NTP packet.                                                              |
| [Cloudflare NTS](https://developers.cloudflare.com/time-services/nts/)                                                                         | Public provider documentation identifies `time.cloudflare.com` and separates key establishment from authenticated NTP synchronization. It lists chrony, NTPsec and ntpd-rs implementations.                                                                                                                                     |
| [How Windows Time works](https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/how-the-windows-time-service-works)  | Applies to Windows 10/11 and Windows Server 2016/2019/2022/2025, among others. AD DS hierarchy packets use domain security/session keys. A manually selected external source does not inherit this authentication. The reviewed documentation and this host establish no RFC 8915 query mode for W32Time.                       |
| [Windows Time tools](https://learn.microsoft.com/en-us/windows-server/networking/windows-time-service/Windows-Time-Service-Tools-and-Settings) | Defines status/source queries and bounded stripchart measurements, including local request FILETIME, roundtrip and offset. Query success retains separate synchronization/authentication questions.                                                                                                                             |
| [chrony supported systems](https://chrony-project.org/) and [chronyd 4.7](https://chrony-project.org/doc/4.7/chronyd.html)                     | Upstream lists Linux, FreeBSD, NetBSD, OpenBSD, macOS and illumos. Native Windows is outside that published list. The documented `-Q` mode reports an offset while leaving the clock alone, disables server ports and can run without root when configuration needs no privileged features. `-t` bounds its execution.          |
| [chronyc 4.7](https://chrony-project.org/doc/4.7/chronyc.html) and [configuration](https://chrony-project.org/doc/4.7/chrony.conf.html)        | `-c` supplies CSV monitoring output. `authdata` identifies NTS versus symmetric-key mode; `ntpdata` reports authentication of the last measurement. Certificate trust/name/time checks and exact invocation/configuration need provenance. A certificate-time-check exception cannot silently qualify as complete verification. |
| [NTPsec platform policy](https://www.ntpsec.org/supported-platforms.html)                                                                      | Documents POSIX Unix-like systems and removal of legacy Windows-toolchain support. A working native Windows NTS build was not established. Its [ntpd quit mode](https://docs.ntpsec.org/latest/ntpd.html) sets the clock, so that mode is ineligible for this read-only workflow.                                               |
| [ntpd-rs installation](https://docs.ntpd-rs.pendulum-project.org/latest/guide/installation/)                                                   | Documents Linux/FreeBSD installation and macOS source-build context. The reviewed material did not establish a native Windows build or a bounded, no-clock-adjustment NTS measurement mode.                                                                                                                                     |
| [WSL commands](https://learn.microsoft.com/en-us/windows/wsl/basic-commands)                                                                   | `--list --running --quiet` enumerates running distributions. This query supplies environment-discovery evidence independently of client availability or clock-domain correspondence.                                                                                                                                            |

The chrony 4.7 manuals define the scope of the report-interpretation tests. They
are documentation references rather than a decision to install that particular
version. A future approved client must have its release, features, safe invocation
and output contract verified explicitly.

### Actual local host observations

The audit executed in the Windows workspace containing the repository and the
previously admitted source/retention artifacts. Its scope is this local execution
host. Machine names, account identifiers, domain names, private addresses and
distribution names are excluded from the report.

| Check                       | Actual observation                                                                                                                                                                                           |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Platform                    | Microsoft Windows 11 Pro, version `10.0.26300`, x64                                                                                                                                                          |
| Shell                       | Windows PowerShell Desktop `5.1.26100.9818`, 64-bit process                                                                                                                                                  |
| Current privilege           | Administrative token false; no privileged mutation was attempted                                                                                                                                             |
| Windows Time service        | Running, Automatic startup                                                                                                                                                                                   |
| Domain applicability        | `PartOfDomain=false`; an authenticated AD DS hierarchy is not applicable to this observed setup                                                                                                              |
| Source query                | Access denied, native exit `-2147024891`, exact failure `The following error occurred: Access is denied. (0x80070005)`                                                                                       |
| Status query                | Exit zero, Leap Indicator 3 (not synchronized), Stratum 0, source `time.windows.com,0x9`, Last Sync Error 1                                                                                                  |
| Candidate executables       | No `chronyd`, `chronyc`, `ntpd`, `ntpq`, `ntp-daemon`, `ntp-ctl` or `ntpd-rs` found on PATH. Named Program Files NTP binary locations also had no match. This bounded search cannot exclude other locations. |
| WSL                         | Launcher found. The read-only running-distribution query exited zero and returned count 0. Stopped distributions and their clients were left uninspected; no distribution was started.                       |
| Observed NTS authentication | NTS-KE, certificate verification for the NTS endpoint and authenticated NTP packet exchange remained unexercised                                                                                             |

The report represents each mechanism with ten separate stages covering protocol
documentation, platform support, installed client, service reachability,
authentication attempted/verified, measurement, uncertainty, relation to an actual
MT5 capture and historical applicability. Nullable values preserve unverified
conditions. It supplies no single generic availability flag.

| Mechanism                      | Evidence obtained                                                                                  | Decision and limitation                                                                                                                                         |
| ------------------------------ | -------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| W32Time with external NTP      | Configured source/status observed separately from one transient Cloudflare stripchart sample       | `context_specific` ordinary NTP. The transient target does not change the configured source. Authentication remains unverified.                                 |
| Authenticated domain hierarchy | Domain membership false                                                                            | `not_applicable` on this host. Domain membership/synchronization alone would still require evidence of actual authentication and upstream authority.            |
| RFC 8915 NTS at Cloudflare     | Provider support documented; TCP 4460 reachable                                                    | `candidate`. TCP reachability precedes TLS/NTS-KE, negotiated NTP endpoint validation and packet authentication.                                                |
| Established client             | No candidate executable in inspected locations; native platform/safe-mode requirements unresolved  | `human_validation_required`. No installed approved client was demonstrated.                                                                                     |
| HTTPS/TLS Date comparison      | HTTPS HEAD succeeded under default OS certificate policy and returned a Date header                | Transport authentication observed; authenticated time synchronization false. Date generation, caching, second granularity and network delay retain uncertainty. |
| Existing MoneyWay measurements | Admitted November 16 bundle pins rechecked; prior native/host/NTP observations retained separately | `context_specific` relationship to that actual MT5 capture. This new audit cannot retroactively authenticate it.                                                |

### Real diagnostic output and measurements

The final report is version 1 of `moneyway.mt5-utc-reference-capability`, retained
outside Git at
`%LOCALAPPDATA%\MoneyWay\Evidence\UtcReferenceCapability\audit-423e88b4dbe0424285eaccda25a94f44.json`.
Its SHA256 is
`6fc68716d3bb46a4b4c46383cff19fded02b78895c57c7d108a76191b4f9b49d`.
It is compact JSON, 18678 bytes. Collection spanned host UTC candidates
`2026-11-16T18:27:18.2275688+00:00` through
`2026-11-16T18:27:21.2122680+00:00`; monotonic boundaries and tool hashes are
retained. These host UTC labels remain unauthenticated.

| Actual network check                           | Recorded result                                                                                                             |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Ordinary NTP at `time.cloudflare.com:123/UDP`  | Local request candidate `2026-11-16T18:27:20.7786813Z`, reported offset `-0.0012982` seconds, roundtrip `0.0069120` seconds |
| TCP prerequisite at `time.cloudflare.com:4460` | Connected, elapsed `27.3533` ms; TLS/NTS-KE and authenticated NTP were not attempted                                        |
| HTTPS HEAD of the Cloudflare NTS documentation | HTTP 200 under default OS TLS validation; Date `Mon, 16 Nov 2026 18:27:20 GMT`; Age header absent; elapsed `187.6448` ms    |

Ordinary NTP keeps the native raw CSV and exact-decimal interpretation. The single
sample is a bounded diagnostic choice, with no required sample count or acceptable
offset/latency threshold established. HTTP Date remains a coarse comparison, and
an absent Age header establishes no cache guarantee. Source-query failures are
preserved while source/reference identifiers and unrecognized status lines are
withheld. Both TCP and HTTPS results remain separate from authenticated time.

The read-only audit checked 16 original/retained evidence files before and after
collection; all hashes remained identical. The admitted evidence, response and
retention manifest matched their previously registered pins. This rechecks those
content anchors and does not repeat or alter their original admission. Diagnostic
writes are confined to a fresh report and its temporary publication path outside
the repository, MT5 common files and retained capture directories. Production
receipt/checkpoint operations are absent.

`authenticated_utc_reference_observed=false` and
`host_clock_error_bound_seconds=null`. Canonical flags
`canonical_utc_conversion_authorized`, `utc_conversion_authenticated` and
`canonical_ready` all remain `false`. Server-response authentication, host
synchronization/error bounds, local monotonic time, network asymmetry, EA event
processing, native broker timestamps, publication/availability and provider
timezone conversion remain separate contracts. Historical/DST mapping, source
event UTC bounds, candle closure, availability and atomic multi-timeframe
acquisition retain their earlier blockers. Nasdaq remains `America/Bogota`.

### Operator invocation, validation and next prerequisite

From Git Bash in the repository root, the following exact command was also
executed successfully in the Windows environment.

```bash
powershell.exe -NoLogo -NoProfile -File ./tools/market-data/mt5/Get-Mt5UtcReferenceCapability.ps1 -QueryTimeoutMilliseconds 5000
```

The required timeout argument accepts 1000–30000 ms. The audit makes bounded native
W32Time/TCP/WSL checks and a HEAD request using the .NET request timeout. Native
DNS scheduling can extend an HTTP timeout, so this is not a hard real-time
deadline. Query failure retains an unavailable/unverified result. The caller can
choose a fresh absolute `-ReportPath` whose parent exists outside the protected
directories. Existing output and pending paths are rejected. Default output uses
a new GUID filename for each invocation. No MT5 interaction is required.

Full .NET build passed with zero warnings/errors. The
[new capability tests](../../tests/tools/Mt5UtcReferenceCapability.Tests.ps1)
passed 33 synthetic cases. Existing correlated-tick, native-UTC, collector, probe,
comparator and retention suites passed 56 + 28 + 39 + 27 + 32 + 33 tests.
Total is **248 passed, 0 failed, 0 skipped**. AST, Markdown format/local links,
report/tool provenance, evidence preservation and `git diff --check` are verified
before publication.

Tests of chrony output interpretation use controlled human-readable 4.7
`authdata`/`ntpdata` fixtures, bound endpoint/output hashes and recorded
configuration/certificate-check provenance. A recognized claim is a client-report
interpretation result. The parser performs no cryptographic verification and
always leaves its own authenticated-reference-observed flag false. TLS-only,
key-establishment-only, failed authentication, incomplete provenance/certificate
checks and endpoint mismatch remain insufficient. These tests establish no real
end-to-end NTS authentication. Automated safety checks also cover immutable
outputs and absence of clock/service/registry/trading mutations.

The single next feature is an explicitly authorized supported-client environment
prerequisite for a bounded NTS experiment. Chrony's documented `-Q` plus `-t` mode
makes it the smallest researched candidate for a separately approved supported
environment with an established NTS-enabled build. The combined capture of native
authentication telemetry and uncertainty still needs adapter validation. WSL
launcher presence alone cannot establish that environment or its correspondence
to Windows host observation boundaries. Installing/provisioning a runtime or
client requires separate operator authorization. A bounded adapter can follow
once this prerequisite is demonstrated; no current source is selected as ready.

## Bounded Windows/WSL NTS correlation (2026-11-16)

The operator subsequently authorized an isolated Ubuntu 24.04 WSL 2 environment
and retention of pre-existing Hyper-V implicit synchronization. Provisioning used
the signed Canonical Ubuntu Base 24.04.5 image, a dedicated
`MoneyWay-NTS-Ubuntu-24.04` distribution, Chrony `4.5-1ubuntu4.2` and GnuTLS
`3.8.3-1.1ubuntu3.6`. The external provisioning report is
`%LOCALAPPDATA%/MoneyWay/NtsEnvironment/provision-d6ab96b1308c47f7aba31aaae87bc27c/provisioning-report.json`,
SHA256 `ec0208a28b7852ec9d807e9afe26d904e9b46bd66ee9afd3146040ca61d546ca`.
Its 20 image/procedure/raw-output hash pins were independently rechecked and all
matched. The recovered shell procedure, configuration, authentication reports and
capability records were inspected. The provisioning measurement printed
`-0.003012 seconds`; its scope remains that earlier Linux observation.

This feature adds a [PowerShell 5.1 coordinator](../../tools/market-data/mt5/Get-Mt5NtsTimeCorrelation.ps1),
[protocol/assessment helpers](../../tools/market-data/mt5/Mt5NtsCorrelation.ps1)
and a [private POSIX shell capture](../../tools/market-data/mt5/Invoke-Mt5NtsLinuxCapture.sh).
It reuses existing random-nonce, SHA256, safe local path and immutable publication
helpers. The earlier 4.7 capability-report interpreter remains unchanged. The new
assessment covers the installed 4.5 profile and current-session outputs. No
canonical market-data or strategy component consumes these records.

### Invocation and supported environment

From Git Bash in the repository root, the exact operator command exercised was:

```bash
powershell.exe -NoLogo -NoProfile -File ./tools/market-data/mt5/Get-Mt5NtsTimeCorrelation.ps1 -Distribution MoneyWay-NTS-Ubuntu-24.04 -QueryTimeoutSeconds 20 -SessionTimeoutSeconds 60
```

Both timeouts and the exact distribution are required. Query bounds are 1–60
seconds, session bounds 15–180 seconds, and the latter must allow the query plus
10 seconds for protocol/cleanup. These are diagnostic resource limits, not
latency/skew acceptance thresholds. An outer Linux GNU timeout and kill grace
bound the session and its children. Each Chrony query has its own `-t` limit and
an outer query limit five seconds longer, following the recovered procedure.

Discovery enumerates installed distributions, including stopped ones. It selects
only `MoneyWay-NTS-Ubuntu-24.04`; the running-before flag is recorded. The Linux
guard verifies the exact package version and both previously validated executable
hashes before its network query. Changed versions/binaries require review of the
installed-client contract. The supported executable hashes are:

- `chronyd`: `7a834e478d8a904c39a348606a5d0ac58fd1ccbca24f333a8170c786af8ca508`
- `chronyc`: `271ea54c67206559437bd299d0e08c9e3d42ee6be322b1722e76311cd5d9df8d`

The established configuration is copied into a new nonce-specific private
`/tmp/mw-nts-<64-hex-session>` directory. It contains only
`server time.cloudflare.com nts iburst`, `authselectmode require`, zero server
and UDP command ports, and private Unix socket/pidfile paths. CA name/chain/time
checks use the client's defaults. No certificate-check bypass, key/cookie dump,
ordinary NTP fallback or external configuration include is introduced.

Chrony runs as `_chrony`, using the established `setpriv --bounding-set=-all
--reuid=_chrony --regid=_chrony --init-groups --no-new-privs` restrictions and
`chronyd -Q -t <query-seconds> -f <private-config>`. Monitoring queries only
`authdata -a` and `ntpdata` from that private socket. The retained pair comes from
one running instance and matching remote endpoint. A successful query exit,
printed offset, fresh session/configuration binding, NTS key/cookie evidence,
accepted authenticated response and zero-capability record are all required for
`nts_authenticated_measurement_observed=true`.

The script verifies `systemd=false`, policy exit 101, nine time-unit masks and
absence of an existing time-client process before starting. Cleanup kills/waits
only its own child and removes its private session directory. The coordinator
does not terminate any distribution. Cancellation closes the session stream and
signals its private cancel marker. If cleanup cannot be confirmed, the failure
report retains a requirement for reinspection while the inner GNU timeout still
bounds its processes. No packages, services, `.wslconfig`, Docker settings or
clock controls are changed by the diagnostic.

Optional `-ReportPath` selects a new local file with an existing, non-reparse
parent outside protected source/repository/provisioning locations. Default output
is a unique JSON file under `%LOCALAPPDATA%/MoneyWay/Evidence/NtsCorrelation`.
Publication uses the existing exclusive pending-file and no-overwrite protocol.
Stdout contains the report path, hash and concise assessment. Invocation completion
alone supplies no authentication conclusion; inspect the assessment and diagnostics.
PowerShell callers can pass a cancellation token. Console interruption runs the
session's cleanup path; an interrupted host may require checking the bounded
Linux session's completion.

### Observation domains and ordering

The coordinator measures staging/possible WSL initialization separately from the
capture process's launch-to-ready interval. A single capture process then handles
nonce-bound BEFORE/RUN/AFTER/ACK messages. Windows records `UtcNow` and Stopwatch
before sending each observation request and after receiving its response.
Linux brackets GNU `date -u +%s.%N` with reads of `/proc/uptime`.

The installed kernel's [uptime implementation](https://raw.githubusercontent.com/microsoft/WSL2-Linux-Kernel/linux-msft-wsl-6.6.87.2/fs/proc/uptime.c)
uses `CLOCK_BOOTTIME`, includes suspend and truncates to hundredths of a second.
The parser checks that format, preserves native literals and widens the displayed
elapsed bracket by 0.01 seconds at each end to account for difference
quantization. This is a local elapsed-time observation with explicit resolution.
It establishes no clock-error acceptance threshold.

Windows Stopwatch and Linux boottime have separate epochs and rate guarantees.
They are never subtracted from one another. Elapsed times are calculated only
inside their respective domains. The assessment retains differences between
Windows realtime elapsed and Stopwatch elapsed without applying an invented
tolerance or declaring clock-rate equivalence. Observed decreasing realtime,
invalid monotonic ordering, missing timestamps and domain/frequency mismatches
remain explicit failures of the temporal interpretation.

For each response, the candidate Linux-minus-Windows realtime interval is
`[LinuxRealtime - WindowsReceiptUtc, LinuxRealtime - WindowsSendUtc]`.
This read bracket contains transport, process scheduling and timestamp-read
sequencing. It is conditional on the observed realtime clocks and establishes no
certified UTC correction. Subsample clock changes, rate uncertainty and network
asymmetry remain unresolved. Hyper-V synchronization also prevents an assumption
of independent Windows/Linux clocks.

### Authentic capture and retained failures

The authenticated capture session is
`00ce059575293d5dcaaa168bbfc82627a58d34447e9b46b9dbeac52aec544459`.
Its immutable report is
`%LOCALAPPDATA%/MoneyWay/Evidence/NtsCorrelation/correlation-00ce059575293d5dcaaa168bbfc82627a58d34447e9b46b9dbeac52aec544459.json`,
SHA256 `311d5f74dd1cc0548222f5000c43aed2b544cc4444e8e86373a4222eb9f1b249`.
It retains raw non-secret client text, local observations, configuration and field
hashes. The local interface address is withheld from `ntpdata` before retention;
the report identifies that redaction and hashes the retained representation.

Host UTC candidates span initialization at `2026-11-16T20:22:09.2122414Z` through
completion at `2026-11-16T20:22:16.4386626Z`. Client timestamps report start/end at
`20:22:09Z`/`20:22:16Z`. These local timestamps preserve their candidate provenance.
Chrony reported the following current-session evidence:

- NTS endpoint `162.159.200.123`, from the configured `time.cloudflare.com` source.
- `authdata`: NTS mode, KeyID 1, AEAD type 30, 128-bit key length, last establishment
  age 3 seconds, zero failed attempts/NAKs, eight cookies of length 64.
- `ntpdata`: `Authenticated: Yes`, two received/valid/accepted responses.
- Final printed offset `-0.005158 seconds`, ignored by clock control.
- Last monitored packet offset `-0.012779095 seconds`, peer delay `0.025116688`,
  peer dispersion `0.000050441`, root delay `0.091095`, root dispersion `0.000626`.
- UID 100, zero effective/bounding capabilities, `NoNewPrivs=1`,
  `Disabled control of system clock`, query and capture exit codes 0.

The final estimator output and last monitored packet are different observations;
their values are preserved individually. NTS-KE is evidenced by the established
client's fresh NTS key/cookie report. Packet authentication is evidenced by its
matching accepted-response report. No TLS transcript or cryptographic session
secrets are retained. Interpretation follows the installed
[Chrony 4.5 monitoring contract](https://chrony-project.org/doc/4.5/chronyc.html)
and its [non-adjusting mode](https://chrony-project.org/doc/4.5/chronyd.html).

| Observation                                           | Measured value                 |
| ----------------------------------------------------- | ------------------------------ |
| Initialization and staging, Windows Stopwatch         | 0.4182056 s                    |
| Capture process launch to ready                       | 0.2008534 s                    |
| Capture execution                                     | 6.7968218 s                    |
| BEFORE exchange                                       | 0.0139205 s                    |
| AFTER exchange                                        | 0.0608806 s                    |
| Linux realtime elapsed between observations           | 6.531462162 s                  |
| Linux boottime elapsed bracket including quantization | [6.52, 6.55] s                 |
| BEFORE candidate Linux-minus-Windows interval         | [-0.008743687, +0.005255713] s |
| AFTER candidate Linux-minus-Windows interval          | [-0.016948725, +0.043615275] s |
| BEFORE Windows realtime minus Stopwatch elapsed       | +0.0000789 s                   |
| AFTER Windows realtime minus Stopwatch elapsed        | -0.0003166 s                   |

Quantization handling was tightened after the original report was published.
An offline reassessment preserves the original report and raw measurements:
`reassessment-00ce059575293d5dcaaa168bbfc82627a58d34447e9b46b9dbeac52aec544459.json`
in the same directory, SHA256
`4a0f217147e3120c8cff9cba6835b4f900fed491f7b5c3fd8adb4eb929cd8db4`.
It binds the source report hash and assessment-source hash. The widened bracket
above comes from that reassessment; it required no further NTS query.

Development retained an earlier aborted exchange with session `bd0ed4b6…` before
RUN, caused by Windows CRLF request framing. A second session `b7cd3cc7…` completed
a real Chrony query but retained no authentication reports because the long Unix
directory also had to accommodate `chronyc`'s reply-socket name. Both failure
artifacts remain immutable with authentication false. LF request framing and the
shorter private-directory prefix are regression tested. There were **two actual
NTS query executions**, including that failed evidence capture, followed by the
successful session above. The operator's requested one authentic capture required
this bounded retry; the failed result was not promoted or overwritten.

The post-capture read-only check verified all nine masks, zero service enablement
links, no active time-client process and removal of the private session directory.
The two executable and two Linux protection-file hashes match provisioning.
Selected original record 643, probe 643, durable checkpoint and provisioning
report hashes match before/after. This check covers those four selected inputs,
not every file in the active collector. Docker remained stopped and `.wslconfig`
remained absent. No distribution termination was invoked by the coordinator.

### Conclusions and validation

`nts_authenticated_measurement_observed=true` applies to the Linux client's
current reference measurement. Windows/Linux read exchange timing is measured,
but host UTC correlation is **insufficient** for MT5 authorization.
`overall_utc_uncertainty=unresolved`; total UTC/Windows clock-error bounds remain
null. Network peer delay and server dispersion are recorded components, without
being promoted to a rigorous total uncertainty bound.

`windows_clock_authenticated=false`, `mt5_native_utc_authenticated=false`,
`canonical_utc_conversion_authorized=false`, `utc_conversion_authenticated=false`
and `canonical_ready=false`. The measurement cannot retrospectively authenticate
the earlier provisioning or MT5 captures. Provider historical DST mapping,
provider-confirmed closure and historical information availability retain their
independent evidence requirements. Canonical ingestion remains blocked.

The [synthetic suite](../../tests/tools/Mt5NtsCorrelation.Tests.ps1) passed 48
cases. Existing capability, native-UTC, correlated-tick and retention suites
passed 33 + 28 + 56 + 33. Total **198 passed, 0 failed, 0 skipped**. Full .NET build
passed with zero warnings/errors. PowerShell AST, Linux shell syntax, Markdown
format/local links, immutable report hashes and `git diff --check` are checked
before publication. Synthetic cases supply no authentic NTS evidence.

The single next feature is a read-only uncertainty-contract audit of these
host/guest exchange observations, defining which additional resolution and clock
stability evidence is required before a Windows MT5 event can receive a justified
UTC interval. No total-error threshold is selected by this capture.

## Offline Windows/WSL/NTS uncertainty contract, November 16

Initial checkpoint: `main`, `939daf11533bc557bf6b652da49ece638d18210d`,
clean index/tree and synchronized `origin/main`. This feature adds an
[offline command](../../tools/market-data/mt5/Get-Mt5NtsTemporalUncertainty.ps1),
[deterministic assessment helper](../../tools/market-data/mt5/Mt5NtsTemporalUncertainty.ps1)
and [synthetic contract tests](../../tests/tools/Mt5NtsTemporalUncertainty.Tests.ps1).
Existing acquisition, canonical replay and strategy code remain unchanged.

### Evidence integrity and reproducible output

The original correlation and reassessment named above were independently read
and hashed. Their SHA256 values are respectively
`311d5f74dd1cc0548222f5000c43aed2b544cc4444e8e86373a4222eb9f1b249` and
`4a0f217147e3120c8cff9cba6835b4f900fed491f7b5c3fd8adb4eb929cd8db4`.
All six retained raw-field digests validate: configuration, authdata, ntpdata,
capabilities, client stderr and client stdout. Reassessment schema, session and
source-report bindings validate. Numerical conclusions are recomputed from the
retained capture rather than copied from its earlier assessment.

This independently revalidates content and recorded bindings, not packet
cryptography or every byte of the historical execution environment. Three of the
five current acquisition sources match their recorded runtime hashes. The
coordinator and correlation helper changed after capture; their exact historical
runtime byte snapshots were not separately retained. Comparing current files
does not recover those snapshots. The later reassessment's assessment-source
hash matches the unchanged current correlation helper. This provenance limit
does not retract the previously verified Linux NTS authentication.

The actual offline report is outside Git:
`%LOCALAPPDATA%\MoneyWay\Evidence\NtsUncertainty\uncertainty-5c2e6d3f3685491db34ea9a16036a897.json`,
SHA256 `4c35b2fcbc12def57aa7c1975356190b634c8fffa8d55a03436fefe3c0e9726b`.
It records schema version 1, method
`offline_interval_and_field_provenance_audit_v1`, exact timing inputs, raw-field
references, source identity, field provenance, method-source hashes, missing
evidence and required human review. The pure assessment is deterministic;
publication uses a unique create-only filename. No new measurement occurs.

Reproduce from Git Bash at the repository root; omit ReportPath to create a new
report without replacing this one:

```bash
evidence="$(cygpath -m "$LOCALAPPDATA")/MoneyWay/Evidence/NtsCorrelation"
powershell.exe -NoLogo -NoProfile -File ./tools/market-data/mt5/Get-Mt5NtsTemporalUncertainty.ps1 \
  -CapturePath "$evidence/correlation-00ce059575293d5dcaaa168bbfc82627a58d34447e9b46b9dbeac52aec544459.json" \
  -ExpectedCaptureSha256 311d5f74dd1cc0548222f5000c43aed2b544cc4444e8e86373a4222eb9f1b249 \
  -ReassessmentPath "$evidence/reassessment-00ce059575293d5dcaaa168bbfc82627a58d34447e9b46b9dbeac52aec544459.json" \
  -ExpectedReassessmentSha256 4a0f217147e3120c8cff9cba6835b4f900fed491f7b5c3fd8adb4eb929cd8db4
```

### Clock domains and supported constraints

The report inventories each API, representation, ordering, overhead, possible
clock changes and authentication scope. Printed resolution is not accuracy.
Raw boundaries reside in `capture.initialization_before/after`, `launch`,
`ready`, `before/after.sent/received`, `before/after.linux`, `completion` and
`result` in the pinned source.

| Domain                  | Coordinate and representation                                 | Boundary, overhead and evidential limit                                                                                                                       |
| ----------------------- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Windows wall clock      | `DateTimeOffset.UtcNow`, 100 ns representation                | Host reads around staging, launch and exchanges; API/scheduling latency; endpoint order cannot exclude intervening steps; unauthenticated UTC candidates      |
| Windows counter         | `Stopwatch.GetTimestamp`, ticks divided by recorded frequency | Same-domain elapsed/order only, including process/transport overhead; no UTC rate calibration                                                                 |
| Linux wall clock        | GNU `date +%s.%N`, nine decimal places                        | Read inside session-bound request/response, including shell/process overhead; Hyper-V can change guest time; local reading is not independently authenticated |
| Linux counter           | `/proc/uptime`, CLOCK_BOOTTIME, 0.01 s truncation             | Counter reads bracket `date`; shell scheduling included; includes suspend; rate relative to UTC unmeasured                                                    |
| Chrony reference        | `-Q`, `authdata`, `ntpdata`                                   | Same private client instance; network and estimator latency; authenticated response plus offset estimate, not a total clock-error bound                       |
| Host/guest exchange     | Nonce/session-bound BEFORE/AFTER protocol                     | Request, guest read, response order depends on the recorded procedure; full transport overhead retained; two clock domains                                    |
| WSL lifecycle           | Windows staging, launch, ready, completion                    | Preparation 0.4182056 s; launch-to-ready 0.2008534 s; execution 6.7968218 s; shutdown interval absent                                                         |
| Hyper-V synchronization | Pre-existing platform mechanism; no event telemetry           | Dependent clocks without a measured equality, step schedule, drift limit or authentication guarantee                                                          |

Windows candidate readings range from `2026-11-16T20:22:09.2122414Z` to
`2026-11-16T20:22:16.4386626Z`. Same-domain counter order passes independently
of wall-clock availability. Linux's elapsed coordinate lies conservatively in
`[6.52, 6.55] s`, including both truncated endpoints. The implementation of
[`/proc/uptime`](https://raw.githubusercontent.com/microsoft/WSL2-Linux-Kernel/linux-msft-wsl-6.6.87.2/fs/proc/uptime.c)
supports that quantization treatment. This constrains local counter coordinates,
without calibrating their rate to UTC. No cross-domain counter subtraction is
performed.

Let `L` be a Linux wall reading and `Ws`, `Wr` its surrounding Windows wall
reads. Under trusted protocol/API provenance and a nondecreasing Windows wall
clock throughout the exchange, the instantaneous Linux-minus-Windows difference
is conditionally within `[L - Wr, L - Ws]`:

| Read   | Candidate interval, seconds  | Windows counter exchange elapsed |
| ------ | ---------------------------- | -------------------------------- |
| BEFORE | [-0.008743687, +0.005255713] | 0.0139205 s                      |
| AFTER  | [-0.016948725, +0.043615275] | 0.0608806 s                      |

These conditions are not verified by endpoint order. With the additional
unverified hypothesis that the difference remained constant between the two
guest readings, their intersection is
`[-0.008743687, +0.005255713] s`. The hypothesis is feasible; it is not established.
Disjoint intervals in a synthetic case reject that hypothesis without proving
the underlying measurements contradictory. No intermediate/future propagation
or drift rate follows from an intersection.

Linux realtime elapsed `6.531462162 s` fits the local counter bracket, so a
unit-rate/no-step hypothesis is feasible but unverified. Endpoint wall readings
and counter differences cannot rule out intervening steps. No mathematical
contradiction or realtime reversal was observed in this authentic record.
Observed Windows realtime-minus-counter differences remain `+0.0000789 s` and
`-0.0003166 s`; neither becomes a skew tolerance or a UTC error bound.

### Chrony 4.5 field and mode decision

The version-specific [chronyc manual](https://chrony-project.org/doc/4.5/chronyc.html)
distinguishes local tracking System time from Last offset and from the measured
source offset. Its error formula
`abs(system_time_offset) + root_dispersion + root_delay / 2` is conditional on
the ultimate reference being correct. The needed fields must describe the same
local tracking observation. Peer delay cannot replace cumulative root delay.

The capture has final `-Q` offset `-0.005158 s` and last monitored packet offset
`-0.012779095 s`, peer delay `0.025116688 s`, peer dispersion `0.000050441 s`,
packet root delay `0.091095 s` and packet root dispersion `0.000626 s`.
Packet leap is Normal, reference time is `Mon Nov 16 20:21:27 2026` and precision
is `-26 (0.000000015 seconds)`. The packet root, leap, reference and precision
fields originate from the remote response. They do not provide the missing
local tracking state. No `tracking`, `sources`, `sourcestats` or `selectdata`
report was captured. Missing values remain null, including the actual formula
result; no remote-root/peer-delay/final-offset substitution is permitted.

The [chronyd manual](https://chrony-project.org/doc/4.5/chronyd.html) documents
non-adjusting `-Q`. Upstream [4.5 reference.c](https://raw.githubusercontent.com/mlichvar/chrony/4.5/reference.c)
adds a concrete constraint: `REF_SetReference` returns through `special_mode_sync`
before normal local root/reference/tracking updates. PrintOnce logs an offset
and exits without applying it. Therefore merely adding a `tracking` read to
this `-Q` capture does not establish normal tracking semantics. This is upstream
source inspection, not an independent review of every Ubuntu package patch.
Missing captured fields already suffice to block the formula.

The synthetic formula test supplies all fields and explicit reference,
same-observation, reviewed-mode and observation-only assumptions; it checks exact
decimal arithmetic, not authentic timing accuracy. Its candidate result never
authorizes canonical conversion.

### Authentication scope and next measurement decision

The retained session still shows NTS-KE, eight cookies and two accepted
authenticated packets. [RFC 8915](https://www.rfc-editor.org/rfc/rfc8915.html)
does not turn packet authentication into proof of ultimate reference accuracy
or Windows/MT5 event timing. [Ubuntu's WSL explanation](https://ubuntu.com/wsl/docs/stable/explanation/time-sync/)
also precludes treating host and guest clocks as independent references.

Technical sources in this section were accessed on **2026-11-16**. The concrete
decision is to retain `overall_utc_uncertainty=unresolved`, Windows-host UTC error
bound null and MT5 event UTC interval null. The only defensible finite
constraints concern recorded local elapsed coordinates and quantization. The
cross-clock intervals above remain conditional candidates.

The smallest next implementation is to retain the source-selection interval
from `selection.log` in a future, separately authorized bounded `-Q` session,
bound to the same endpoint/session, raw hashes and local counter brackets.
The [4.5 logging contract](https://chrony-project.org/doc/4.5/chrony.conf.html)
defines selection state, sample age and lower/upper source-offset endpoints.
Upstream [sources.c](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sources.c)
logs the selected source before calling `REF_SetReference`, avoiding the
PrintOnce tracking-update gap. Preserve log rounding, estimator clock semantics
and reference-correctness assumptions; a retained selection interval would
reduce missing Linux estimator evidence, not automatically bound Windows UTC.
If the selected entry is absent or cannot be bound, report `data_unavailable`.
No such capture or acquisition change was made by this offline feature.

Still required independently: reviewed estimator-to-system-clock uncertainty,
bounded clock evolution through the host/guest path, event-to-reference binding,
provider historical timezone/DST mapping, provider candle closure and historical
availability. The November 16 US100 tick is not retrospectively authenticated.
All five flags remain false: `windows_clock_authenticated`,
`mt5_native_utc_authenticated`, `canonical_utc_conversion_authorized`,
`utc_conversion_authenticated` and `canonical_ready`.

### Validation and preservation

The new suite passed **40** cases; NTS correlation, reference capability,
native UTC, correlated tick and retention regressions passed
**48 + 33 + 28 + 56 + 33**, respectively: **238 passed, 0 failed, 0 skipped**.
Cases cover hash/session/raw provenance failures, missing wall readings with
independent counter order, domain rejection, quantization, discontinuities,
conditional formula prerequisites, interval conflicts, deterministic results,
immutable reports and checkpoint preservation. The entry points pass PowerShell
AST checks. Full `dotnet build MoneyWay.sln --no-restore -v quiet` passed with
zero warnings and errors. No shell source changed or shell syntax validation
required a WSL launch. Markdown formatting/local links, method/report integrity
and `git diff --check` are verified before commit.

Before/after SHA256 checks preserve the original correlation, reassessment,
selected durable receipt checkpoint, automatic record 643 and probe 643. This
assertion covers those five selected artifacts, not all collector files. No WSL
process, new NTS measurement, MT5 capture, clock/service/configuration change,
checkpoint advancement or canonical mutation was invoked.

## Chrony 4.5 selection evidence capture (2026-11-16)

Initial Git checkpoint: `main`, `af6de78ee533121eda586885187144779f998838`,
clean and synchronized with `origin/main`. This feature extends the existing
[coordinator](../../tools/market-data/mt5/Get-Mt5NtsTimeCorrelation.ps1),
[protocol/authentication helper](../../tools/market-data/mt5/Mt5NtsCorrelation.ps1)
and [isolated Linux capture](../../tools/market-data/mt5/Invoke-Mt5NtsLinuxCapture.sh).
The [selection helper](../../tools/market-data/mt5/Mt5NtsSelectionEvidence.ps1)
assesses, publishes and independently rereads the retained evidence; there is no
second acquisition coordinator or canonical integration.

### Version-specific contract and implementation

Official sources accessed **2026-11-16**:
[chrony.conf 4.5](https://chrony-project.org/doc/4.5/chrony.conf.html),
[chronyd 4.5](https://chrony-project.org/doc/4.5/chronyd.html),
[chronyc 4.5](https://chrony-project.org/doc/4.5/chronyc.html),
[sources.c](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sources.c) and
[reference.c](https://raw.githubusercontent.com/mlichvar/chrony/4.5/reference.c).
`log selection` writes source decisions to `selection.log` under `logdir`.
Rows contain a source timestamp, address, selection state/options, reach, score,
sample age and two offset endpoints. The endpoints are expected containment
estimates under Chrony's source/root-distance model. They do not certify an
unconditional UTC bound.

In upstream 4.5, `SRC_SelectSource` calls `mark_source(..., SRC_SELECTED)`
before `REF_SetReference`; `mark_source` writes the selection log. The latter
reference call takes the PrintOnce exit path without normal tracking updates.
This establishes an expectation of useful `-Q` logging, independently confirmed
by the single authentic experiment below. The review does not independently
audit every Ubuntu patch. Upstream prints endpoint and age values with
`%10.3e`; original literals are retained without pretending to recover unprinted
digits. The source timestamp label has whole-second resolution and is not
independently authenticated UTC.

The only configuration additions are `log selection` and
`logdir /tmp/mw-nts-<64-hex-session>`. A fresh directory is created exclusively;
pre-existing log/symlink paths are rejected before Chrony starts. The existing
private socket, NTS-required single source, no-capability user, timeout and
BEFORE/AFTER exchanges are preserved. The complete original log is read only
after the writer terminates, transferred with its Linux SHA256 and published
separately without replacing any file. A 64 KiB transfer limit fails closed;
the summary retains at most 16 parsed rows plus the selected event and total
count. The shared safe rereader accepts artifacts up to 32 KiB, so an oversized
retained report remains unavailable to that reader instead of being truncated.

An optional session-bound SELECTION protocol record carries the log, query
counter/realtime bounds, exact inner command and `/etc/os-release`. The version-1
correlation JSON retains its original six raw-field hashes and adds a selection
manifest descriptor. Legacy RESULT messages/configurations and reports remain
accepted; absent selection evidence is explicit. The manifest hash is pinned in
the correlation report; the manifest binds the captured execution, configuration,
raw client hashes and separate log hash. The offline rereader checks these
bindings and recomputes the selection assessment.

Only a selected row, valid interval, correct private-session configuration and
same-execution NTS telemetry can reach gate G. IP equality alone is insufficient.
Missing, empty, header-only, not-selected and unauthenticated-selected cases
remain distinct. Missing selection does not erase independently recognized NTS
authentication. Assumptions about reference correctness, applicability of the
source-selection model and clock evolution remain explicitly unverified.

### One authentic bounded experiment

Session: `f645b4fd27968d7265deec9f69ab63dde8191ab552347bbcb0f245541e4111e2`.
Ubuntu 24.04.5, WSL 2 kernel `6.6.87.2-microsoft-standard-WSL2`, Chrony
`4.5-1ubuntu4.2`, GnuTLS `3.8.3-1.1ubuntu3.6`. Host invocation:

```powershell
powershell.exe -NoLogo -NoProfile -File tools/market-data/mt5/Get-Mt5NtsTimeCorrelation.ps1 -Distribution MoneyWay-NTS-Ubuntu-24.04 -QueryTimeoutSeconds 20 -SessionTimeoutSeconds 50
```

The exact inner command was `/usr/sbin/chronyd -Q -t 20 -f
/tmp/mw-nts-f645b4fd27968d7265deec9f69ab63dde8191ab552347bbcb0f245541e4111e2/chrony.conf`.
It ran through the existing GNU timeout and `setpriv` wrapper. Chrony and the
transport exited with code 0. Client output states clock control was disabled
and final offset `-0.054047 seconds (ignored)`. Query counter bounds were
`[16.87, 23.30]` CLOCK_BOOTTIME seconds; realtime candidates were
`[1794870768.814327955, 1794870775.237575288]`. These bracket process execution,
without authenticating those timestamps or the exact estimator epoch.

The complete log is **616 bytes, four data rows**:

| Source timestamp label | Address       | State | Reach | Sample age | Lower original | Upper original |
| ---------------------- | ------------- | ----- | ----- | ---------- | -------------- | -------------- |
| 2026-11-16 23:12:51    | 162.159.200.1 | M     | 1     | 4.898e-03  | 2.640e-03      | 1.046e-01      |
| 2026-11-16 23:12:53    | 162.159.200.1 | M     | 3     | 3.401e-03  | 4.206e-03      | 1.035e-01      |
| 2026-11-16 23:12:55    | 162.159.200.1 | *     | 7     | 4.138e-03  | 3.768e-03      | 1.043e-01      |
| 2026-11-16 23:12:55    | 162.159.200.1 | S     | 0     | 4.138e-03  | 3.768e-03      | 1.043e-01      |

The selected event's original interval is `[3.768e-03, 1.043e-01] s`, exactly
`[0.003768, 0.1043] s` as decimal representations of the printed endpoints.
The later S row is preserved and means stale source state; the report selects
the earlier historical `*` event, not a claim that the source remains selected
after termination. The exact transition trigger is not separately instrumented.
No manual sign reversal, rounding margin or tracking-formula substitution is
applied to these literals.

Configuration SHA256:
`7eecd53ae357f1da63a729b39d21b9f17db192c21b022d12d1b9f3e89f9f6341`.
The configuration names `time.cloudflare.com` with `nts` and
`authselectmode require`; its private instance's `authdata` and `ntpdata` both
identify `162.159.200.1`. Retained telemetry shows NTS KeyID 1, AEAD type 30,
128-bit key length, eight cookies, no failed attempts/NAKs, `Authenticated: Yes`
and two good received packets. These are counters/diagnostics, not retained
cookies or keys. Monitoring preceded the last selection update; the report does
not assert that the two-packet snapshot individually records every later packet.
The single NTS-only source/configuration, private socket, logdir and nonce-bound
execution establish source-level linkage. This is client-verified authentication
evidence, not independent re-verification of packet cryptography.

Gate **G_authenticated_selected_interval** is observed;
`selected_source_authenticated=true` and
`chrony_selection_interval_status=context_specific`.

### Retention, provenance and safety

All artifacts are outside Git in
`%LOCALAPPDATA%\MoneyWay\Evidence\NtsCorrelation\`. Let `R` denote
`correlation-f645b4fd27968d7265deec9f69ab63dde8191ab552347bbcb0f245541e4111e2.json`:

| Artifact                                           | SHA256                                                             |
| -------------------------------------------------- | ------------------------------------------------------------------ |
| `R`                                                | `3b29d7e74e5511b0e88330dc20b3faca8b6573a91a470141af35bb26a050db40` |
| `R.selection.log`                                  | `30eb24c0ced88baecd17afae11003ecb90a8ee6f66d3fd50eedc48dcdf6b8b46` |
| `R.selection.json` manifest                        | `8d33b33d0198fbc5c84bda00a4542465e5f4b63783dded725301a24466a911de` |
| `R.selection-method.ps1` capture-time helper bytes | `e42abc5745f76d5d28eb31e33ec2a43cdd574f4b039b37e9fcb422833ff537a3` |
| `R.safety.json` before/after verification          | `487ea787f1368b3f720dd5c61a28fc1438d28fc546a94ad0a398ad04edf2459e` |

The original capture-time helper bytes were retained because a subsequent
offline-only correction preserves independent NTS status when selection evidence
is missing or assumptions are absent. That correction leaves the authentic G
assessment unchanged; the rereader verifies it against the immutable report and
manifest. No second authentic capture was performed. The safety report records
both helper hashes rather than silently relabeling the measured method.

Before/after environment output matches: executable hashes, `/etc/wsl.conf`,
`policy-rc.d`, package versions, nine service masks and no active time client.
Chrony ran as UID 100, effective/bounding capabilities zero, NoNewPrivs 1.
Pre-existing SysV links comprise three K stop links and four S start links;
they were not created or removed here. PID 1 is WSL init with `systemd=false`,
not a SysV runlevel launcher; service masks and the start-denial policy remain
in place. This audit does not label those existing links as newly enabled
services or claim that they are absent.

The query directory's absence and no lingering time-client process were verified.
Only the dedicated MoneyWay distribution was then terminated, returning it to
its initial stopped state. Docker stayed stopped; no Docker operation or global
WSL shutdown was invoked. `.wslconfig` remained absent. No clock correction,
package/service change, MT5 capture or checkpoint advancement occurred.
Six selected preservation pins match: original correlation, its reassessment,
prior uncertainty report, receipt checkpoint, record 643 and probe 643. This
scope does not claim a hash check of every collector file.

### Validation and next action

The [selection suite](../../tests/tools/Mt5NtsSelectionEvidence.Tests.ps1) passed
**42** synthetic cases. Existing correlation, uncertainty, capability,
native-UTC, correlated-tick and retention suites passed
**48 + 40 + 33 + 28 + 56 + 33**: **280 passed, 0 failed, 0 skipped**.
Tests include scientific notation and exact precision, missing/empty/header-only
logs, selection/authentication separation, stale session/command/source rejection,
invalid bounds/hashes, partial publication, manifest/log tampering, original
input preservation and legacy compatibility. PowerShell AST and POSIX shell
syntax checks pass. Full .NET build passed with zero warnings/errors.
Markdown format/local links, report/log/manifest hashes, raw client bindings,
preservation pins and `git diff --check` are checked before publication.

The concrete outcome is that bounded non-adjusting `-Q` **does produce usable
selection evidence** in this pinned environment; no different Chrony mode is
required to obtain it. The smallest next action is an offline contract review
mapping the selection estimate's sign, estimator clock and epoch, printed
rounding and evolution assumptions to the Linux system-clock observations.
Do not request another measurement or propagate to Windows/MT5 until that
mapping is justified. The source-selection estimate is not the local tracking
formula and does not resolve ultimate reference accuracy, oscillator drift,
host/guest continuity, event binding, provider historical DST, candle closure
or historical availability.

`overall_utc_uncertainty=unresolved`; Windows UTC bound and MT5 event interval
remain null. `windows_clock_authenticated=false`,
`mt5_native_utc_authenticated=false`, `canonical_utc_conversion_authorized=false`,
`utc_conversion_authenticated=false` and `canonical_ready=false`. The earlier
Linux NTS authentication remains valid within its original scope. No strategy,
active metadata or canonical replay behavior changed.

## Selection interval semantics audit (2026-11-16)

Initial checkpoint was clean `main` at
`138bd006817cf82bd350d4250ef9f1445a38b8cf`, synchronized with `origin/main`.
The [offline command](../../tools/market-data/mt5/Get-Mt5NtsSelectionSemantics.ps1)
uses the existing selection evidence rereader and a
[focused semantic evaluator](../../tools/market-data/mt5/Mt5NtsSelectionSemantics.ps1).
It verifies independently supplied report/log hashes, recomputes existing
selection authentication and emits an immutable version-1 assessment outside Git.

### Ubuntu package compatibility and retained source audit

Official source archives and the amd64 binary package for `4.5-1ubuntu4.2` were
downloaded for inspection on Windows. Archive SHA256 values match the Ubuntu
source descriptor. Both binary executable hashes match the retained capture's
pins. Nine reviewed C files match the upstream 4.5 files byte for byte, covering
NTP acquisition, statistics, source selection, reference handling, logging,
scheduling, local clocks, timestamp formatting and startup. The quilt series
contains two patches touching `examples/chronyd-restricted.service` and
`examples/chrony.nm-dispatcher.dhcp`. Build rules were also reviewed. This
establishes compatibility of the audited source paths with the published package.
The source descriptor's OpenPGP signature was not independently verified and no
reproducible binary build was performed. Downloads used the official Ubuntu HTTPS
archive. [Ubuntu source descriptor](https://archive.ubuntu.com/ubuntu/pool/main/c/chrony/chrony_4.5-1ubuntu4.2.dsc).

The source inspection is retained under
`%LOCALAPPDATA%\MoneyWay\Evidence\NtsSelectionSemantics\sources-7443e7b7ad3e4513af0e6075bdd63c26\`.
Its `source-audit.json` SHA256 is
`dd175d4bbab427d6361f22245a8f6ee2be06df1a4eac89998b97011499c67595`.
It records source/archive hashes, code comparisons, patched paths and binary
matches. Source archive SHA256 is
`19fe1d9f4664d445a69a96c71e8fdb60bcd8df24c73d1386e02287f7366ad422` and
Debian archive SHA256 is
`ad09d3f40cfb28ce22cbc0e266d0a4818d8e4f98ee24634a8cc851065f37a29b`.
The retained inspection also includes the reviewed glibc formatting source.

### Sign, clock and epoch contract

All following source references were accessed on **2026-11-16**. Line locations
refer to the complete C files in the retained Ubuntu original archive.

| Path                                                             | Verified operation                                                                                          | Consequence                                                |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| `ntp_core.c`, `process_rx_known`, lines 2178/2188                | NTP sample offset is remote average minus local average, with sample time set to the local midpoint         | Positive sample offset means reference ahead               |
| `sourcestats.c`, `SST_AccumulateSample`, line 315                | Stores `-sample->offset`                                                                                    | Statistics use local minus reference                       |
| `sourcestats.c`, `SST_GetSelectionData`, lines 648–681           | Best-single-sample offset plus elapsed times estimated frequency, with endpoints formed using root distance | Positive selection endpoints describe local clock ahead    |
| `sources.c`, `SRC_SelectSource` and `mark_source`, lines 887/716 | Uses cached cooked scheduler time for evaluation and the log                                                | Selected interval belongs to that event's model evaluation |
| `reference.c`, `special_mode_sync`, lines 898–899                | PrintOnce prints the negative of its reference-update offset estimate                                       | Final `-Q` output has the corrective sign                  |

The source sample, stored statistics and final PrintOnce value therefore have
explicit sign relationships. The selection interval uses a best-sample-based
estimate. PrintOnce receives the tracking/regression estimate through the
reference update path, and prints six fractional decimal places. Their values
and epochs require their respective estimator contracts. A positive selected
interval supports the conditional interpretation that the local clock is ahead
of the selected reference. [NTP acquisition](https://raw.githubusercontent.com/mlichvar/chrony/4.5/ntp_core.c),
[statistics](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sourcestats.c),
[PrintOnce](https://raw.githubusercontent.com/mlichvar/chrony/4.5/reference.c).

The local domain is Chrony's cooked Linux clock. `LCL_ReadRawTime` reads
`CLOCK_REALTIME`, and `LCL_CookTime` adds the driver's offset correction.
`-Q` selects `SYS_Null`. Its initial offset/frequency correction is zero, and
the captured isolated configuration has no drift file or normal reference
update applying a later software correction. This source path relates the model
to guest realtime within that execution. Hyper-V clock changes retain their
independent uncertainty. Windows Stopwatch and Linux BOOTTIME remain separate
counter domains. [Local clock](https://raw.githubusercontent.com/mlichvar/chrony/4.5/local.c),
[null driver](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sys_null.c).

`SST_GetSelectionData` uses the absolute elapsed difference from the selected
best sample's local midpoint to the cached evaluation time. Its root distance
adds half the sample root delay, sample root dispersion and elapsed times skew.
The displayed sample age concerns the newest sample and can differ from the
best sample's age. The original capture lacks the internal best-sample epoch
and unrounded model parameters. [Statistics implementation](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sourcestats.c).

`SCH_GetLastEventTime` returns cached cooked `last_select_ts`. `mark_source`
passes `now.tv_sec` to `UTI_TimeToLogForm`, which uses `gmtime` and
`%Y-%m-%d %H:%M:%S`. The fractional second is omitted. Thus the printed
`2026-11-16 23:12:55` identifies the whole-second component of the cached
scheduler time. Its subsecond coordinate and the later code-execution instant
remain unresolved. An exact event at the second boundary and propagation to
another instant remain unsupported.
[Scheduler](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sched.c),
[selection logger](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sources.c),
[timestamp formatter](https://raw.githubusercontent.com/mlichvar/chrony/4.5/util.c).

### Formatting enclosure and authentic result

Selection endpoints use `%10.3e`, passed through `vfprintf`. Width 10 supplies
padding and `.3` supplies three fractional mantissa digits, yielding four
significant digits. The normalized exponent determines the least printed digit,
`q = 10^(exponent - 3)`. The original signed literals are retained.
[Selection logger](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sources.c),
[logging implementation](https://raw.githubusercontent.com/mlichvar/chrony/4.5/logging.c).

The reviewed glibc formatter selects rounding through `get_rounding_mode` and
`round_away`, supporting nearest with ties to even, upward, downward and toward
zero. The Chrony source archive contains no locale or floating-point environment
setter calls. C startup locale explains the observed decimal point. Historical
libc bytes and the actual floating-point rounding mode were not captured, so the
assessment states the standard formatter compatibility assumption. It encloses
each printed finite nonzero endpoint `p` by `[p - q, p + q]`, conservatively
covering all four standard modes. Closed endpoints include tie boundaries and
mantissa/exponent carry cases. This margin follows from formatting precision.
[glibc formatting](https://raw.githubusercontent.com/bminor/glibc/glibc-2.39/stdio-common/printf_fp.c),
[rounding decisions](https://raw.githubusercontent.com/bminor/glibc/glibc-2.39/include/rounding-mode.h).

Scientific signed zero retains its sign in text and has a zero-value enclosure.
Nonfinite, malformed, unsupported-format and inexact decimal enclosures produce
an unavailable result. Large/small finite values are accepted when their printed
value and enclosure are exactly representable by Decimal. The actual internal
binary floating-point endpoints remain unrecoverable.

The authentic source/log/manifest bindings all validate for session
`f645b4fd27968d7265deec9f69ab63dde8191ab552347bbcb0f245541e4111e2`.
Its log SHA256 remains
`30eb24c0ced88baecd17afae11003ecb90a8ee6f66d3fd50eedc48dcdf6b8b46`.
The selected source is `162.159.200.1`, with previously verified same-session
NTS authentication preserved.

| Representation            | Lower seconds | Upper seconds | Qualification                                                         |
| ------------------------- | ------------- | ------------- | --------------------------------------------------------------------- |
| Original printed interval | `3.768e-03`   | `1.043e-01`   | Literal bytes retained                                                |
| Parsed printed interval   | `0.003768`    | `0.1043`      | Exact decimal interpretation of text                                  |
| Formatting-aware interval | `0.003767`    | `0.1044`      | Conservative enclosure under the reviewed standard formatter contract |
| Nearest-only candidate    | `0.0037675`   | `0.10435`     | Requires historical FE_TONEAREST, which remains unverified            |

The complete sequence remains `M → M → * → S`. M denotes insufficient
measurement/statistics state, `*` selection at that event, and S the older-sample
state in Chrony's terminology. The later S row remains part of the evidence.
Continuous or final selection is unsupported. The original authenticated
selection event retains its provenance. [Chrony 4.5 state definitions](https://chrony-project.org/doc/4.5/chrony.conf.html).

The actual offline assessment is
`%LOCALAPPDATA%\MoneyWay\Evidence\NtsSelectionSemantics\semantics-724b2c96060b43969b5515c58ec5b2e3.json`,
SHA256 `a2c7a06e07527060706a6bdbd1693bd4bce534e33c63de7b8a6f4ee0ad595f85`.
It contains source paths/pins, semantic contract/version/references, exact
printed values, conditional formatting enclosure, full state sequence,
authentication scope, missing epochs, method hashes and authorization flags.
The CLI requires the original correlation hash and independent original log
hash. Create-only publication protects previous reports.

### Validation and minimum next action

The [new suite](../../tests/tools/Mt5NtsSelectionSemantics.Tests.ps1) passed
**33** cases, including normal CLI publication, independent source/log pins,
tampered-log rejection, signed zero, exponent boundaries, Decimal limits,
unverified rounding mode, state history, session/source mismatches, counter-domain
restrictions and source/checkpoint sentinel preservation. Existing selection,
NTS correlation, uncertainty, capability, native UTC, correlated-tick and
retention suites passed **42 + 48 + 40 + 33 + 28 + 56 + 33**.
Total **313 passed, 0 failed, 0 skipped**. PowerShell AST and full .NET build
pass, with zero build warnings/errors. Markdown formatting/local links,
source/report hashes, before/after preservation and `git diff --check` are
verified before publication.

Eleven selected preservation hashes cover the original selection capture, log,
manifest, method snapshot and safety report, earlier correlation/reassessment/
uncertainty report, checkpoint, record 643 and probe 643. All match. This task
performed offline evaluation and source inspection on Windows. No new timing
measurement, WSL process or MT5 interaction was invoked.

The single next technical action is to define the minimum same-session capture
contract that binds a subsecond selection epoch and the Linux clock evolution
between that event and the existing guest realtime reads. It must state the
required drift/step evidence before permitting interval propagation. The present
record leaves these quantities unresolved. Ultimate reference accuracy,
Windows/MT5 binding, provider historical timezone/DST, closure and availability
retain their independent requirements.

`overall_utc_uncertainty=unresolved`, with Windows UTC bound and MT5 event interval
null. `windows_clock_authenticated=false`, `mt5_native_utc_authenticated=false`,
`canonical_utc_conversion_authorized=false`, `utc_conversion_authenticated=false`
and `canonical_ready=false`. Linux NTS packet authentication retains its original
verified scope.

## Selection epoch and clock continuity contract (2026-11-17)

Contract version 1. Initial checkpoint was clean `main` at
`c61fe9877734e97b5b01128493ba50b78a7ccc94`, synchronized with `origin/main`.
The observability gate concluded **B, supported contract insufficient**, before
any capture extension. Final decision is
**`BLOCKED_CLOCK_CONTINUITY_UNPROVABLE`** for the reviewed workflow.
This independently useful specification records observable bounds, prerequisites
for propagation and the stopping condition for further NTS instrumentation.

### Verified internal epoch and interface limits

Sources were accessed on **2026-11-17**. `SRC_SelectSource` obtains `now` through
`SCH_GetLastEventTime`. `SST_GetSelectionData` evaluates the source interval using
that cached cooked clock coordinate. The scheduler stores `last_select_ts` as
a `struct timespec`, including nanoseconds, after reading guest realtime and
applying the local clock driver's correction. `mark_source` logs its whole-second
component. The internally represented subsecond coordinate therefore exists.
Its whole-second label is externally observable. The original selection epoch's
nanoseconds have no dedicated field in the reviewed selection interfaces.
[Selection](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sources.c),
[scheduler](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sched.c),
[local clock](https://raw.githubusercontent.com/mlichvar/chrony/4.5/local.c),
[statistics](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sourcestats.c).

The retained Ubuntu source audit and official binary matches from the preceding
section were reverified. Four additional files, `reports.h`, `candm.h`, `cmdmon.c`
and `client.c`, were extracted from the pinned Ubuntu original source archive
and compared byte for byte with upstream 4.5. The package's two quilt patches
affect examples. Thus the following field audit covers the published package
paths. Historical libc/fenv and reproducible-build limitations retain the scope
already recorded.

`RPT_SelectReport` and wire `RPY_SelectData` contain source identity, state,
authentication, options, leap, integer sample age, score and endpoints.
`handle_select_data` copies these fields into the reply. The original scheduler
epoch is absent. The age is a source-sample age, with whole-second precision in
this reply. CSV output cannot add an absent protocol field.
[Report structure](https://raw.githubusercontent.com/mlichvar/chrony/4.5/reports.h),
[wire structure](https://raw.githubusercontent.com/mlichvar/chrony/4.5/candm.h),
[handler](https://raw.githubusercontent.com/mlichvar/chrony/4.5/cmdmon.c).

| Mechanism                                                  | Observability classification                                          | Supported meaning and remaining gap                                                                                |
| ---------------------------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `selection.log`                                            | Directly observable through a supported interface                     | Source/state/model endpoints and cached clock second. Subsecond coordinate and counter pairing remain absent       |
| `chronyc selectdata`, including CSV                        | Directly observable source state, selection epoch absent              | State, authentication setting, sample age and interval. The reply supplies no original selection timestamp         |
| `sources`, `sourcestats`, `ntpdata`, `tracking`            | Directly observable reports, original selection epoch absent          | Samples/statistics, remote reference time or tracking reference state. PrintOnce bypasses normal reference updates |
| Measurement/statistics/tracking logs                       | Directly observable logs, original selection epoch absent             | Their own sample/reference events and printed timestamps require their own interpretation                          |
| Existing query start, wait completion and private protocol | Indirectly bounded through observable ordering                        | Coarse process/event enclosure in the Linux counter domain. Cached clock epoch lacks a paired counter observation  |
| Log watcher or file polling                                | Indirectly bounded publication observation                            | Successful observation occurs after publication. Observer latency and cached epoch association remain unbounded    |
| File modification timestamp                                | File metadata directly observable, event-time association conditional | Filesystem update semantics and clock association must be justified separately                                     |
| Internal tracing or modified event logging                 | Only through instrumentation outside this contract                    | Access to internal fields requires an additional implementation/provenance contract                                |

Chrony also supports source-history dumps. `SST_SaveToFile` writes subsecond
sample timestamps. `MAI_CleanupAndExit` dumps sources after cancellation of
offset correction and before source finalization. A same-session dump plus the
formatted newest-sample age from the selected log row is a **candidate indirect
reconstruction** of the cached epoch. It requires exact association to that row's
latest sample and accounting for corrections, steps or state changes before the
dump. The authentic capture has no dump. This candidate does not supply the
missing clock-continuity guarantees. It is recorded without extending capture.
[Sample serialization](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sourcestats.c),
[exit ordering](https://raw.githubusercontent.com/mlichvar/chrony/4.5/main.c),
[supported dump command](https://chrony-project.org/doc/4.5/chronyc.html).

### Existing event bounds and continuity observations

Session `f645b4fd27968d7265deec9f69ab63dde8191ab552347bbcb0f245541e4111e2`
and its original report, log and manifest hashes validate. The log hash is
`30eb24c0ced88baecd17afae11003ecb90a8ee6f66d3fd50eedc48dcdf6b8b46`.
The state sequence remains `M → M → * → S`, with same-session NTS authentication
and the original selection event preserved.

The printed `23:12:55` gives the cached cooked-coordinate bin
`[2026-11-16 23:12:55.000000000, 2026-11-16 23:12:56.000000000)`.
It identifies a component of the internal model epoch. The actual callback
execution and independently authenticated UTC are unresolved. Trusted process
ordering and the 0.01 s truncated query markers give the coarse selection-code
execution enclosure `[16.87, 23.31)` in guest BOOTTIME coordinates. The epoch's
missing counter pairing prevents combining these into an authenticated instant.
Process launch, WSL startup, process exit, polling time and unrelated wall reads
remain observations of their respective operations.

| Retained observation                       | Actual result                   | Evidential scope                                         |
| ------------------------------------------ | ------------------------------- | -------------------------------------------------------- |
| Linux BOOTTIME sample order                | Nondecreasing                   | Same guest counter coordinates                           |
| Windows Stopwatch sample order             | Nondecreasing                   | Same host counter coordinates                            |
| Linux realtime elapsed, BEFORE to AFTER    | `6.463677457 s`                 | Recorded realtime endpoints                              |
| Guest BOOTTIME elapsed bracket             | `[6.45, 6.48] s`                | Includes truncated endpoints and nonatomic read ordering |
| Realtime minus BOOTTIME elapsed bracket    | `[-0.016322543, 0.013677457] s` | Local endpoint-coordinate comparison                     |
| Realtime reversal at sampled endpoints     | None observed                   | Recorded samples only                                    |
| Entire-span absence of steps               | `unresolved`                    | Intermediate jumps remain possible                       |
| Rate bound for local-minus-reference error | `unresolved`                    | No calibrated bound supplied                             |
| Hyper-V synchronization events             | Unobserved in retained evidence | No cumulative step/rate guarantee                        |

Endpoint consistency is compatible with several clock histories, including
unobserved intervening changes. Chrony's scheduler includes heuristic step
detection, and its documentation states that automatic jump detection is not
completely reliable. A retained record without a jump message cannot certify
the entire interval. Further endpoint sampling has the same guarantee limitation.
[Scheduler checks](https://raw.githubusercontent.com/mlichvar/chrony/4.5/sched.c),
[jump detection documentation](https://chrony-project.org/doc/4.5/chronyc.html).

Linux `CLOCK_REALTIME` admits discontinuous and gradual adjustments.
`CLOCK_MONOTONIC` avoids realtime discontinuities while remaining subject to
frequency adjustments. BOOTTIME adds suspend time, and `MONOTONIC_RAW` changes
the adjustment/suspend contract. None supplies the missing local-versus-reference
rate guarantee for this record. Windows Stopwatch uses QPC with an independent
external-reference relationship. Cross-domain counter subtraction is prohibited.
[Linux clock semantics](https://man7.org/linux/man-pages/man3/clock_gettime.3.html),
[Windows timing](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps).

### Minimum propagation contract and stop decision

Propagation requires all of the following evidence together.

1. The original model epoch or a defensible set of possible epochs, associated
   with the exact selected source, row and authenticated session.
2. Explicit clock domains and a validated relationship to the target reading,
   including acquisition order, nonatomic read bounds and elapsed uncertainty.
3. A justified bound on evolution of local-minus-reference error throughout
   every permitted source-to-target span, including reference evolution.
4. A cumulative discontinuity bound accounting for Hyper-V and other clock
   adjustments, with observation completeness adequate to support that bound.
5. Source-model applicability/reference assumptions, formatting uncertainty and
   all additional measurement/domain-mapping uncertainty.

For a conditional mathematical statement, let `theta` be local minus reference
and let its source-epoch interval be `[a,b]`. If independent evidence bounds
`theta(target) - theta(source)` by `[d_lower,d_upper]` for every allowed pair of
epochs, the target interval is `[a + d_lower, b + d_upper]`. A rate model would
need an entire-span rate bound, signed elapsed bounds and cumulative step bounds.
No numerical evolution bound is provided by the retained capture.

`selection_interval_propagation=unresolved` and
`interval_propagation_permitted=false`. The coarse epoch bounds and candidate
dump reconstruction leave the continuity prerequisite unmet. The final blocker
is therefore **`BLOCKED_CLOCK_CONTINUITY_UNPROVABLE`** within the reviewed
non-adjusting workflow. This decision does not claim that all indirect epoch
bounds are impossible. Further Chrony-only instrumentation is unjustified for
closing this propagation contract, and the task ends at the specification gate.
Authentic NTS captures performed by this feature total **zero**.

The version-1 external contract is retained at
`%LOCALAPPDATA%\MoneyWay\Evidence\NtsEpochContinuity\audit-3157c36e69824fbbb93a23539728dff7\epoch-continuity-contract-d39f5bd4a6db4e3cbd2d683b99823340.json`,
SHA256 `f2c68e23dadd0b21e3f83b9ce85663b6439f2b9ae62b776cf106a284d9e49482`.
It contains source/session pins, reviewed interface file hashes, observable
bounds, the candidate dump approach, continuity limitations, propagation
prerequisites, the decision and next action. It is a specification and
existing-evidence audit, with no new runtime status mapping.

### Validation and next feature

Only this documentation file changes in Git. The existing eight PowerShell
suites passed **33 + 42 + 48 + 40 + 33 + 28 + 56 + 33**, totalling
**313 passed, 0 failed, 0 skipped**. These are existing regressions, including
their AST checks. POSIX shell syntax and full .NET build pass, with zero build
warnings/errors. No new evaluator or synthetic propagation implementation was
added. Documentation formatting/local links, evidence hashes and
`git diff --check` are verified before publication.

Twelve selected preservation pins cover the selection capture/log/manifest/
method/safety record, earlier correlation/reassessment/uncertainty/semantics
reports, receipt checkpoint, record 643 and probe 643. All match. Source review
and evidence evaluation ran on Windows with no WSL launch or new measurement.

The next smallest feature is a **causal market-data availability contract for
the existing MT5 acquisition/receipt evidence**. Scope it to source/probe hashes,
predecessor-chain publication order, run identity and run-local acquisition
counter bounds. Specify what source acquisition, publication and later reader
receipt establish, including restarts and missing/conflicting records. Preserve
the distinction between receipt integrity and sample completeness. Cross-run
elapsed comparisons and canonical authorization require their own evidence.
[Existing receipt model](../../src/backend/MoneyWay.Application/MarketData/Receipt/Mt5ReceiptModels.cs),
[receipt semantics](mt5-diagnostic-event-receipts.md).

UTC conversion, provider closure and historical availability remain unresolved
for that proposed contract. Canonical future-data boundaries still require
candles closed at or before `AsOfUtc` and input observations available by that
boundary under [ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md).
Receipt order alone supplies neither required UTC field.

`overall_utc_uncertainty=unresolved`, `windows_clock_authenticated=false`,
`mt5_native_utc_authenticated=false`, `canonical_utc_conversion_authorized=false`,
`utc_conversion_authenticated=false` and `canonical_ready=false`. The verified
Linux NTS packet authentication remains preserved within its original scope.
