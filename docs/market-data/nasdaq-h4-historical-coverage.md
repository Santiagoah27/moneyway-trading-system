# Nasdaq H4 historical coverage (documentary research)

`tools/research/inspect_nasdaq_h4_coverage.py` audits the registered OANDA Global
Markets MT5 demo `US100` original H4 export. It reads the external June package,
checks both registered SHA256 values, and compares selected normalized rows with
native MT5 timestamp, OHLC and tick volume. With `--import-manifest`, it instead
checks a separately versioned imported source and its preserved mapping evidence.
It writes no files or research labels.

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
start (for the 40-hour example, June 20 21:00Z as a requested UTC boundary) through
the preparation cutoff. Preserve the unedited native MT5 export, its native server
timestamps, symbol/provider/timeframe and acquisition metadata. The importer must
use a new versioned package with raw and normalized hashes; never replace the
registered June package or frozen review asset. A separate original H4 export and
audited time conversion are needed for September 8. The current package does not
establish that either requested interval is available from MT5. Native server
timezone and DST must be supported by source evidence before normalization.
If the registered June offset could be independently supported across the earlier
range, its native interval would start June 21 00:00 server time and end June 22
16:00 server time; that extension is currently unverified. Export the MT5 H4
history with its `<DATE>`, `<TIME>`, OHLC and `<TICKVOL>` columns, retaining the
original file. For September, request original H4 source spanning at least the
40 hours preceding September 8 13:00Z and that cutoff; the exact native-server
clock interval cannot be specified until its time mapping is evidenced.

## Importing a new original H4 export

In MT5 connected to **OANDA Global Markets demo**, select **US100**, timeframe
**H4**, and export continuous history as UTF-8 tab-separated text with at least
`<DATE>`, `<TIME>`, `<OPEN>`, `<HIGH>`, `<LOW>`, `<CLOSE>`, `<TICKVOL>`.
`<VOL>`, `<SPREAD>` and other columns remain in the preserved original file.
Keep it unedited. Example filenames and requested native server-clock ranges:

- June research: `US100_H4_202606150000_202607010000.csv`, June 15 00:00 through July 1 00:00.
- September research: `US100_H4_202608300000_202609110000.csv`, August 30 00:00 through September 11 00:00.

These are research acquisition ranges, not strategy lookback rules or assertions
of UTC mapping. September's wider range must be narrowed only after source
evidence establishes its native-clock mapping. If MT5 cannot return the full
range, retain and report actual first/last rows and gaps; never fill missing bars.

Place the export outside the repository, for example in `C:\MoneyWay\incoming`.
Prepare a JSON timestamp map whose evidence document independently supports the
whole native interval of each segment. Split segments at every evidenced offset
change. The existing June package's documentary minus-three-hour mapping applies
only to its registered rows; it cannot justify an earlier June or September
segment by extrapolation. This example describes the **registered** June interval:

```json
{
  "schemaVersion": 1,
  "providerId": "oanda-global-markets-mt5-demo",
  "symbol": "US100",
  "timeframe": "H4",
  "sourceSha256": "<SHA256 of original export>",
  "mappingStatus": "DocumentaryAudited",
  "evidenceReference": "C:/MoneyWay/incoming/june-h4-time-audit.md",
  "evidenceSha256": "<SHA256 of independently reviewed audit>",
  "segments": [{
    "nativeStartInclusive": "2026.06.22 00:00:00",
    "nativeEndExclusive": "2026.07.01 00:00:00",
    "serverUtcOffsetMinutes": 180
  }]
}
```

Alternatively, use `rows` with a `nativeOpenTime` and `openUtc` for every source
row if no entire segment can be justified. Do not combine `rows` and `segments`.
The CLI verifies mapping identity, complete row coverage and evidence-file hash;
it cannot independently prove the evidence's interpretation. Leave an unresolved
mapping unimported. September requires its own reviewed map; no offset is assumed.
The native TSV has no embedded provider or timeframe identifier: its declared
US100/H4/OANDA identity must be checked against the MT5 export context and
retained acquisition record. A filename or mapping declaration alone is not
broker authentication.
For the requested June 15 export, the audit and map must also cover every row
before June 22; copying the example segment alone will fail with an unresolved
timestamp error.

From the repository root, run with actual SHA256 values and an actual independently
recorded UTC import instant (the command does not read wall-clock time):

```powershell
$source = 'C:\MoneyWay\incoming\US100_H4_202606150000_202607010000.csv'
$map = 'C:\MoneyWay\incoming\june-h4-map.json'
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash.ToLowerInvariant()
$mapHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $map).Hash.ToLowerInvariant()
python -B tools/research/import_nasdaq_h4.py --source $source --source-sha256 $sourceHash --timestamp-map $map --mapping-sha256 $mapHash --imported-at-utc 2026-11-20T15:00:00Z --output C:\MoneyWay\imports\june-h4-v1
python -B tools/research/inspect_nasdaq_h4_coverage.py --session-root C:\MoneyWay\session --import-manifest C:\MoneyWay\imports\june-h4-v1\manifest.json --cutoff-utc 2026-06-22T13:00:00Z --history-hours 40
```

Replace the example import instant. For September, use
`US100_H4_202608300000_202609110000.csv`, a separately reviewed
`september-h4-map.json`, output `september-h4-v1`, and coverage cutoff
`2026-09-08T13:00:00Z`. API, Worker and PostgreSQL need not run. Repeating an
identical import returns its manifest; conflicting output is rejected. Original
bytes, map and evidence are copied to the versioned directory. The normalized
CSV matches the existing `CsvMarketDataImporter` input contract; this command
does not create a canonical replay input or expose protected research outcomes.

Infrastructure readiness here is a reproducible import and coverage report. Data readiness
for deeper June 22 or September history and strategy readiness remain unresolved.
Canonical admission and operational signals remain disabled.
