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
