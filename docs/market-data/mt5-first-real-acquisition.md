# First real MT5 US100 acquisition and temporal readiness

Recorded 2026-11-10. Technical market-data evidence only; not an S1–S14 human
strategy assertion, a strategy decision, or a canonical replay input.

## Provenance and independent local verification

The human reports running [MoneyWayReadOnlyProbe](../../tools/market-data/mt5/MoneyWayReadOnlyProbe.mq5)
against the existing OANDA demo terminal and supplying its resulting JSON. The
reported local file was accessible and was independently read, hashed and checked
with the existing [diagnostic validator](../../tools/market-data/mt5/Test-Mt5ProbeArtifact.ps1).
The acquisition was performed by the human; the subsequent audit did not run the
terminal, acquire another sample or change the artifact.

| Evidence                         | Locally verified value                                                                          |
| -------------------------------- | ----------------------------------------------------------------------------------------------- |
| External location                | `%APPDATA%\MetaQuotes\Terminal\Common\Files\MoneyWayReadOnlyProbe\probe-1794315975-132125.json` |
| Size                             | 28,249 bytes                                                                                    |
| SHA256                           | `d2dadabb3586bb2378c4b9287cfaa2d1ff3467d70e6f73220d56baebcf7a8f03`                              |
| Schema / version                 | `moneyway.mt5-read-only-probe` / `1`                                                            |
| Gate                             | `passed`                                                                                        |
| Diagnostic / canonical readiness | `diagnostic_only=true`, `canonical_ready=false`                                                 |
| Validator                        | `ContractValid=true`, 64 bars, 0 invalid OHLC bars                                              |

The bytes match the independently supplied hash. The authentic-run attribution
rests on the human's execution report together with the matching diagnostic;
the file is not a broker-signed attestation. The validator's
`ProviderConnectivityAuthenticatedByValidator=false` remains correct: it validates
an offline artifact and has no terminal connection. No JSON, account identifier,
credential, terminal log, or personal filesystem prefix is copied into Git.

## Observed source and acquisition results

The artifact reports MetaTrader 5, build **6246**, company **OANDA Global Markets
Limited**, server **OANDA_Global-Demo-1**, verified demo mode, and terminal connected
before and after acquisition. Native **US100** exists, is not custom, and was
selected successfully. Description: **US Tech 100, USD**; profit currency: **USD**;
reported digits: **1**. Mode, symbol and description/currency/digits query errors
are all zero.

| Timeframe | Requested / returned | Sync before | Sync after | CopyRates error | Historical / current candidate | Native opening range, oldest → latest     |
| --------- | -------------------: | ----------: | ---------: | --------------: | -----------------------------: | ----------------------------------------- |
| M1        |              16 / 16 |           1 |          1 |               0 |                         15 / 1 | 2026.11.10 19:51:00 → 2026.11.10 20:06:00 |
| M5        |              16 / 16 |           0 |          1 |               0 |                         15 / 1 | 2026.11.10 18:50:00 → 2026.11.10 20:05:00 |
| H1        |              16 / 16 |           0 |          1 |               0 |                         15 / 1 | 2026.11.10 05:00:00 → 2026.11.10 20:00:00 |
| H4        |              16 / 16 |           0 |          1 |               0 |                         15 / 1 | 2026.11.06 08:00:00 → 2026.11.10 20:00:00 |

All statuses are `sample_returned`; all head brackets are consistent. Each native
series is strictly ordered newest first with 16 distinct opening timestamps, and
all 64 OHLC payloads are valid. The position-zero role is
`current_bar_completion_unverified`; the other 15 roles per timeframe are
`historical_preceding_bar`. There is no provider finality assertion.

Within these samples, adjacent native openings differ by 60s (M1), 300s (M5)
and 3600s (H1). H4 has differences of 14400s and one 187200s interval. This longer
interval is observed evidence, not a classification of missing trading bars or a
license to fill them; its session/calendar explanation is not established here.
Successful synchronization after copying does not prove ongoing health, and the
sample size does not establish sufficient strategy warmup or full-history coverage.

This establishes one successful read-only native US100 acquisition from the
reported demo terminal. It establishes neither USTEC numerical equivalence nor
correspondence with September's normalized US100 dataset.

## Temporal evidence and its limits

| Artifact field            | Raw datetime integer | Native display / meaning                                                |
| ------------------------- | -------------------: | ----------------------------------------------------------------------- |
| `generated_gmt_candidate` |           1794333974 | 2026.11.10 18:06:14, computer-derived TimeGMT sample before acquisition |
| `server_quote_before`     |           1794341174 | 2026.11.10 20:06:14, last known server quote time                       |
| `server_quote_after`      |           1794341174 | Same server quote time after the four sequential copies                 |

Subtracting these first two raw values yields **+7200 seconds** for this single
sample. It is **not an authenticated server-to-UTC conversion**. No UTC offset,
DST policy, applicability interval, or September offset is inferred. The filename
is an identifier, not an authenticated acquisition time.

The following remain exactly as supplied: `generated_at_utc=null`,
`utc_conversion_authenticated=false`, `feed_equivalence_verified=false`; all 64
`open_time_utc`, `provider_confirmed_closed` and `historical_available_at_utc`
fields are null. They are not populated by this audit.

Official timestamp semantics, reviewed for this record:

- [MqlRates](https://www.mql5.com/en/docs/constants/structures/mqlrates) supplies
  the bar **opening** time. [CopyRates](https://www.mql5.com/en/docs/series/copyrates)
  position zero refers to the current series bar; a returned row is not an
  independent source-confirmed closure or historical receipt timestamp.
- [TimeCurrent](https://www.mql5.com/en/docs/dateandtime/timecurrent) is the last
  known server quote time, potentially from any selected Market Watch symbol in
  this script context. It does not certify a fresh US100 quote or copy completion.
- [TimeGMT](https://www.mql5.com/en/docs/dateandtime/timegmt) is calculated using
  the computer's time settings, including DST. Its clock accuracy and the
  broker's UTC mapping have not been independently authenticated.
- [TimeTradeServer](https://www.mql5.com/en/docs/dateandtime/timetradeserver) is a
  client-calculated server-time estimate dependent on computer time settings.
  The current probe does not call or export it; adding it alone would not
  authenticate UTC or prove historical availability.

The copy calls' process-relative elapsed-microsecond brackets are M1
7237–7587, M5 83624–88229, H1 115765–115782 and H4 131402–131432. They distinguish
local acquisition operations; they are not UTC, broker latency measurements, or
timestamps of first historical source availability. The four samples are
sequential, not an atomic multi-timeframe snapshot. Source-native bar-open time,
last quote time, local acquisition, provider-confirmed completion and historical
availability remain separate facts.

## Next acquisition boundary and open questions

These are requirements and unresolved questions for a future acquisition
boundary, not an implemented adapter or new strategy semantics. Preserve the
bounded canonical pipeline in [ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md)
and the causal distinction in [ADR 0025](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md#identity-and-causal-availability).
Do not stamp a newly received historical row with its opening/closing time as
though it had been observable then.

| Protection                      | Required boundary behavior / unresolved evidence                                                                                                                                                                        |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Native preservation             | Retain raw opening times, OHLC, exact source/symbol/timeframe and acquisition provenance; normalized coordinates must not replace the raw evidence                                                                      |
| Verified UTC                    | Require independent broker/source mapping evidence, its validity period and clock basis. Mapping, DST behavior and accuracy are unresolved; +7200 is not a default                                                      |
| Forming-bar exclusion           | Position zero and unverified-head rows cannot enter a completed-candle snapshot. Excluding them alone does not certify the remaining rows                                                                               |
| Completed-bar availability      | Require a documented source-backed completion/availability criterion. MT5 series position and synchronization are insufficient on their own; criterion remains unresolved                                               |
| Receipt/acquisition times       | Preserve source event times separately from local acquisition start/end and receipt. UTC receipt needs a verified clock basis; receipt of backfill must not retroactively authorize an earlier frame                    |
| Multi-timeframe synchronization | Build an immutable identity-compatible bounded snapshot, admitting only data demonstrably observable by its AsOfUtc. Read skew, delayed-timeframe release/watermark behavior and alignment evidence are unresolved      |
| Gaps, duplicates, reconnect     | Preserve exact bar identity and duplicate acquisition provenance. Report discontinuities without filling; session/calendar interpretation and reconnect/backfill guarantees remain unresolved                           |
| Corrections and immutability    | Record changed payloads separately with their actual observation provenance; never silently replace evidence in an already emitted frame. Provider revision policy and reconciliation authority are unresolved          |
| Stale/unavailable data          | Expose acquisition errors, disconnected/unsynchronized/unknown-quality conditions and blockers. Staleness limits, cadence and their exact outcome mappings remain unresolved; do not manufacture Ready or trade signals |
| Initial warmup                  | Establish required coverage separately for the exact analysis and strategy version. Sixteen returned rows and source total counts do not supply an approved warmup requirement                                          |

**Canonical current-data ingestion is not available.** No data from this artifact
was passed to an evaluator. SL/TP/risk completion is not required to display an
honest incomplete analysis later; unresolved market-data prerequisites still must
remain explicit. There is no change to Worker, API/UI, replay, strategy metadata,
execution or historical strategy evidence.

## One smallest next technical feature

**Offline native-bar comparison of two probe diagnostics**, reusing the existing
validator and source contract, without a terminal loop or canonical ingestion.

Acceptance scope:

1. Read two bounded, validated artifacts, retaining their real hashes and exact
   source/symbol/timeframe identity. Reject incompatible sources. Capture order
   must be explicitly supplied/attested; do not infer it from filenames,
   unauthenticated clock candidates or cross-run process counters.
2. Compare exact native opening identities: unchanged/repeated payloads, newly
   seen rows, rows leaving the bounded sample and changed OHLC. A row leaving a
   sample is not a source deletion; a newly seen row is not necessarily newly
   created at the provider.
3. Diagnose current-slot movement and head uncertainty, without promoting a
   formerly current row to provider-confirmed closure or assigning UTC or exact
   first availability. Retain both payloads for any change; neither file changes.
4. Report observed native discontinuities without trading-calendar inference,
   auto-repair, broker revision-policy claims, strategy results or execution.

Tests should cover identical acquisition/idempotence, source mismatch, new native
head, partial/failed samples, head races, bounded-window eviction, changed OHLC,
and preservation of null UTC/finality/availability. Synthetic tests verify the
comparator only; a second genuine capture is still needed for empirical change
evidence. This feature reduces duplicate/correction/arrival-observation unknowns
without pretending to solve the independent UTC and completion blockers.
