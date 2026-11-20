# Nasdaq H4 historical coverage (documentary research)

`tools/research/inspect_nasdaq_h4_coverage.py` audits the registered OANDA Global
Markets MT5 demo `US100` original H4 export. It reads the external June package,
checks both registered SHA256 values, and compares selected normalized rows with
native MT5 timestamp, OHLC and tick volume. It writes no files or research labels.

Example at the 08:00 America/Bogota preparation cutoff:

```powershell
python -B tools/research/inspect_nasdaq_h4_coverage.py --session-root C:\MoneyWay\session --cutoff-utc 2026-06-22T13:00:00Z --history-hours 40
```

`--history-hours` specifies the research interval, not a strategy lookback rule.
Only original H4 rows opening within that interval and nominally closed by the
explicit cutoff appear. `requestedCoverageAvailable` means that the specified
interval has no uncovered range in the registered source; it does not attest
historical provider publication or authorize a strategy conclusion. All missing
ranges remain unclassified as to market closure. The June-only native server minus
three hours conversion is inherited from the registered documentary package; it
must not be generalized to September or a DST rule.

At 13:00Z with 40 requested hours, June 22 has four closed H4 candles, from
June 21 21:00Z through June 22 13:00Z, and a leading missing range beginning
June 20 21:00Z. June 23, 24, 25, 26 and 30 each have ten closed H4 rows within
their requested 40-hour intervals with no row gaps. September 8 has no H4 row in
this registered June source. These counts say nothing about structural sufficiency.
June 23 is previously exposed; June 25 is Validation and June 30 HeldOut. June 22
has a frozen chronological review selection and may be exposed for its reviewer.
June 24 and 26 are unused coverage candidates protected from retrospective tuning.

For deeper June 22 history, obtain an **original** `US100` H4 export from the
same `oanda-global-markets-mt5-demo` provider covering at least the requested
start (for the example, June 20 21:00Z under the audited June mapping) through
the preparation cutoff. Preserve the unedited native MT5 export, its native server
timestamps, symbol/provider/timeframe and acquisition metadata. The importer must
use a new versioned package with raw and normalized hashes; never replace the
registered June package or frozen review asset. A separate original H4 export and
audited time conversion are needed for September 8. The current package does not
establish that either requested interval is available from MT5. Native server
timezone and DST must be supported by source evidence before normalization.
For the June example, the corresponding **documentary** native interval starts
June 21 00:00 server time and ends June 22 16:00 server time. Export the MT5 H4
history with its `<DATE>`, `<TIME>`, OHLC and `<TICKVOL>` columns, retaining the
original file. For September, request original H4 source spanning at least the
40 hours preceding September 8 13:00Z and that cutoff; the exact native-server
clock interval cannot be specified until its time mapping is evidenced. There is
currently no general H4 CSV importer in `tools/market-data/mt5`; a new versioned
raw/normalized package and audited normalization are required before this command
can inspect a new export. Placing an unregistered CSV in the session directory is
insufficient and will not alter this report.

Infrastructure readiness here is a reproducible coverage report. Data readiness
for deeper June 22 or September history and strategy readiness remain unresolved.
Canonical admission and operational signals remain disabled.
