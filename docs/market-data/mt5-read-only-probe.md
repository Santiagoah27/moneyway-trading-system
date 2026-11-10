# MT5 OANDA demo read-only market data probe

## Scope and current validation

Standalone diagnostic discovery tool, separate from the .NET Application,
Infrastructure adapters, Worker, API and strategy evaluators. It implements no
production current-data adapter and cannot authorize a trade. Architecture remains
governed by [ADR 0001](../decisions/0001-select-application-stack.md), the bounded
canonical pipeline in [ADR 0004](../decisions/0004-support-observable-market-data-resolution-in-canonical-replay.md)
and [ADR 0025 causal availability](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md#identity-and-causal-availability).

The human-reported candidate is OANDA Global Markets Limited, server
`OANDA_Global-Demo-1`, demo, native `US100`, description `US Tech 100, USD`.
The displayed historical chart alone did not certify current availability. The
[first real acquisition record](mt5-first-real-acquisition.md) now records a
human-executed terminal run and independent local validation of its exact bytes.
No account identifier or credential belongs in this feature or its output.

The source was compiled locally with MetaEditor: **0 errors, 0 warnings**.
Focused contract/safety checks use explicitly synthetic fixtures. **One actual
US100 acquisition is now evidenced:** M1/M5/H1/H4 each returned 16 bars with zero
CopyRates error; the local artifact matches the supplied SHA256. Continuous
health, authenticated UTC conversion, provider-confirmed closure and canonical
current-data ingestion remain unverified or unimplemented. The validator alone
does not authenticate connectivity.

## Files

- [MQL5 script](../../tools/market-data/mt5/MoneyWayReadOnlyProbe.mq5).
- [Diagnostic validator](../../tools/market-data/mt5/Test-Mt5ProbeArtifact.ps1).
- [Focused checks](../../tests/tools/Mt5ReadOnlyProbe.Tests.ps1).
- [Offline two-artifact comparator and instructions](mt5-offline-comparator.md).

## Manual installation and execution

1. In the existing MT5 terminal, verify **Demo** and server
   `OANDA_Global-Demo-1`. Keep algorithmic trading disabled. Do not change accounts
   while the script is running. Do not send screenshots containing account IDs.
2. Choose **File > Open Data Folder**. Under `MQL5\Scripts`, create a `MoneyWay`
   folder and copy `MoneyWayReadOnlyProbe.mq5` into it. Do not replace historical
   CSVs, adapters, reports or evidence.
3. Open that source in MetaEditor (F4 from MT5), then F7. Require zero errors;
   review warnings before running. Compilation produces the `.ex5` alongside the
   installed source, outside this repository.
4. Refresh **Navigator > Scripts**. Run `MoneyWayReadOnlyProbe` on a chart, with
   `SampleBars=16` initially. The script always queries **US100**, independently
   of the chart's symbol or timeframe. It selects US100 in Market Watch if needed;
   it leaves that selection in place. There are no DLL, network-request, timer,
   trading-library or order permissions to enable.
5. Check **Experts** for the relative output filename or an export failure code.
   If MT5 refuses to run the script under the existing configuration, stop and
   report that blocker. Do not enable algorithmic order execution or bypass a
   security restriction to run it.
6. Open **File > Open Common Data Folder**, then `Files\MoneyWayReadOnlyProbe`.
   If that menu is unavailable, the usual Windows location is
   `%APPDATA%\MetaQuotes\Terminal\Common\Files\MoneyWayReadOnlyProbe`.
   Select the filename printed by this run, not an older file.
7. From the repository root, validate that exact artifact and record its SHA256:

   ```powershell
   & ./tools/market-data/mt5/Test-Mt5ProbeArtifact.ps1 -Path '<actual diagnostic.json>'
   ```

   `ContractValid` means only that the diagnostic satisfies the checks.
   `ProviderConnectivityAuthenticatedByValidator` and `CanonicalReady` remain
   false. The validator has no terminal connection and does not authenticate the
   file's origin. Inspect `gate`, symbol fields, acquisition statuses, errors and
   synchronization separately. Preserve a failing or incomplete artifact as
   diagnostic evidence; never relabel it a valid canonical input.

The script runs once. It requests 2–128 bars per timeframe, including position
zero, with one `CopyRates` call for each of M1/M5/H1/H4. There are no automatic
retries. CopyRates can initiate history downloading/building and can return only
part of the requested history. After a partial/unavailable result, allow the
terminal to load history and rerun manually; retain both outputs.

Export filenames combine a local clock value and elapsed microseconds under an
isolated directory. Existing filename collisions are refused; read/write opening
and a size check avoid truncating a nonempty collision. An incomplete write is
reported in Experts and must not be treated as a complete artifact.

## Diagnostic schema v1

Schema: `moneyway.mt5-read-only-probe`, version `1`. JSON UTF-8; maximum validator
input size 2 MiB. No canonical CSV conversion or timestamp normalization occurs.

| Fields                                                                 | Meaning                                                                                                                                                                    |
| ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `diagnostic_only`, `canonical_ready`                                   | Always true / false respectively                                                                                                                                           |
| `generated_gmt_candidate`, `generation_time_basis`, `generated_at_utc` | Sample of `TimeGMT()` before acquisition, explicitly computer-derived and unauthenticated; authenticated generation UTC remains null                                       |
| `source`                                                               | Platform, server, company, demo-mode query/error, connection flag before acquisition, terminal build; no login, name, balance, account history or filesystem identity      |
| `gate`                                                                 | Passed, tester rejected, terminal disconnected, demo unverified, server mismatch or sample bound rejected; rejection prevents symbol/history acquisition                   |
| `symbol`                                                               | Exact US100, query attempt, existence/custom/selection and error; description, profit currency, digits and individual metadata error codes                                 |
| `timeframes`                                                           | Exactly M1=60s, M5=300s, H1=3600s, H4=14400s; requested count, raw CopyRates result/error and returned bars                                                                |
| `synchronized_before/after`                                            | SeriesInfoInteger value 0/1, or null on query failure, with error code; synchronization is not a freshness or finality certificate                                         |
| `last_bar_before/after`, `head_consistent`                             | Bracket the copy with native last-bar identities; consistent only when both queries succeed and match copied position zero                                                 |
| `terminal_bar_count`, `terminal_first_date`, `server_first_date`       | Source-reported count/date values plus errors; these do not prove contiguous history or sufficient strategy warmup                                                         |
| `copy_started_elapsed_us`, `copy_finished_elapsed_us`                  | Process-relative local acquisition bracket, not UTC or historical source availability                                                                                      |
| Bar `open_time_native`                                                 | Unmodified MqlRates opening datetime integer and native `yyyy.MM.dd HH:mm:ss` display; no `Z`, offset or UTC assertion                                                     |
| Bar OHLC, `ohlc_valid`                                                 | Native doubles serialized with 16 fractional digits, without symbol-digit rounding; invalid numbers become null and malformed OHLC is explicitly flagged                   |
| Bar `series_position`, `role`                                          | Position zero/current-series candidate separate from preceding historical bars; unstable head leaves all roles unverified                                                  |
| Bar `newer_than_next_seconds`                                          | Actual native opening-time difference to the next older returned bar; final bar has null. Deviations are evidence to review, not automatically missing trading candles     |
| Bar UTC/closed/availability fields                                     | `open_time_utc`, `provider_confirmed_closed`, `historical_available_at_utc` always null                                                                                    |
| `server_quote_before/after`, `connected_after`                         | TimeCurrent samples and terminal connection after acquisition; TimeCurrent is last known server quote time for any Market Watch symbol, not proof of a current US100 quote |
| Conversion/equivalence flags, `limitations`                            | UTC conversion and feed equivalence remain false; explicit unresolved limitations                                                                                          |

`copy_failed`, `empty`, `partial_sample`, `sample_returned` and `not_attempted`
describe acquisition, not strategy outcomes. A failed API retains its numeric error.
Unavailable fields stay null. There is no synthesized history, gap filling or
provider substitution. Non-custom US100 and successful selection are required to
call CopyRates. Non-demo, wrong-server, disconnected and tester contexts cannot
acquire samples. The probe does not change those configurations or reconnect.

## Time, finality and provenance limits

- MqlRates time is the **opening** timestamp. Keep terminal/server-native
  coordinates until the broker's conversion and alignment are independently
  authenticated. Raw datetime integers are not claimed as Unix UTC timestamps.
- Position zero is the current/latest series slot, potentially forming. It can
  also be stale or refer to a closed market. A stable preceding bar demonstrates
  an earlier interval in the series, not an independent provider finality flag.
  CopyRates returning a row does not certify source-confirmed closure.
- TimeGMT derives from the computer clock/time settings. Its documented basis is
  known; clock accuracy is not authenticated. Do not derive a broker offset by
  subtracting it from TimeCurrent, or assign either sample to a historical bar's
  source availability. No September-pack offset or DST assumption is reused.
- A single pass does not establish first availability of a newly exposed bar,
  freshness, reconnect behavior or revision policy. Manual repeated runs around
  a new bar may reveal changes/arrival brackets and gaps, but cannot establish an
  exact historical availability time. Do not deliberately disconnect the terminal
  as part of initial validation.
- Copies across four timeframes are sequential and not atomic. Head bracketing
  cannot detect every historical price revision or an account switch. Keep the
  terminal source unchanged during a run. No emitted artifact is canonical-ready.
- Sample count and earliest-history metadata do not establish enough initial
  history for strategy evaluation. No unapproved warmup or staleness threshold is
  introduced.
- Current native US100 versus September normalized US100 correspondence remains
  unverified. Mentor USTEC remains a separate unresolved source. This artifact
  supplies neither cross-feed equivalence nor S1–S14 strategy evidence.

## Checks and next boundary

Run from the repository root, without a terminal connection:

```powershell
& ./tests/tools/Mt5ReadOnlyProbe.Tests.ps1
git diff --check
```

Checks exercise synthetic success/partial/empty/failure/disconnected/head-race
diagnostics, raw time consistency, malformed OHLC, timeframe/source identity,
forbidden finality/availability assertions, and a strict source-function/metadata
allowlist with unsafe mutations. These are not execution tests of MQL5 functions
or evidence of actual provider connectivity. MetaEditor compilation is a separate
check. No .ex5, generated diagnostic, compilation log or real account data should
be committed. The script has no external includes/imports or broker execution
calls. Keep Algo Trading disabled during manual validation.

The first acquisition reconciliation is [documented](mt5-first-real-acquisition.md),
including open source-contract questions. The [offline comparator](mt5-offline-comparator.md)
now diagnoses repeated bars, newly present native bars and OHLC changes between
two compatible diagnostics while preserving both inputs and unauthenticated times.
Its real two-sample validation awaits a second authentic capture. No current-data
ingestion is authorized by this diagnostic evidence; do not connect unauthenticated
native times to `StrategyReplayContext.AsOfUtc`.

## Official technical references

- [CopyRates](https://www.mql5.com/en/docs/series/copyrates): position zero,
  array layout, partial results and history acquisition behavior.
- [MqlRates](https://www.mql5.com/en/docs/constants/structures/mqlrates):
  opening timestamp and OHLC structure.
- [SeriesInfoInteger](https://www.mql5.com/en/docs/series/seriesinfointeger) and
  [series properties](https://www.mql5.com/en/docs/constants/tradingconstants/enum_series_info_integer).
- [Account properties](https://www.mql5.com/en/docs/constants/environment_state/accountinformation),
  [symbol metadata](https://www.mql5.com/en/docs/marketinformation/symbolinfostring),
  [terminal metadata](https://www.mql5.com/en/docs/constants/environment_state/terminalstatus).
- [TimeCurrent](https://www.mql5.com/en/docs/dateandtime/timecurrent),
  [TimeGMT](https://www.mql5.com/en/docs/dateandtime/timegmt),
  [TimeTradeServer](https://www.mql5.com/en/docs/dateandtime/timetradeserver) and
  [local/server time](https://www.mql5.com/en/book/common/timing/timing_local_server).
- [FileOpen](https://www.mql5.com/en/docs/files/fileopen),
  [file flags](https://www.mql5.com/en/docs/constants/io_constants/fileflags),
  [MetaEditor CLI](https://www.metatrader5.com/es/metaeditor/help/beginning/integration_ide).
