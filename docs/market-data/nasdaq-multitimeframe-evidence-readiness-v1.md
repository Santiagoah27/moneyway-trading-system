# Nasdaq multi-timeframe evidence readiness V1

`nasdaq-evidence-readiness` registers pinned originals and reports source
coverage at explicit historical cutoffs. The report keeps local acquisition,
documentary native-to-UTC mapping and independently authenticated historical
availability separate. Canonical admission and operational signals remain
false. Registration never starts a service or runs a strategy.

## Reuse and evidence contract

- The existing H4 importer accepts original MT5 UTF-8 tab-separated exports
  for H4, H1, M5 and M1 through `--timeframe`. It preserves raw bytes,
  native timestamps, evidence, normalized CSV and manifest hashes. The H4
  entry point and format also work with the existing coverage tool.
- The existing `CsvMarketDataImporter` reads each canonical CSV. Registration
  verifies the source, map and evidence hashes, compares native and normalized
  rows, and checks provider, instrument, duration, chronological uniqueness
  and nominal closes. Identical overlaps across separate sources are counted.
  Conflicting observations, overlapping intervals and unresolved mappings
  are rejected before publishing. No timeframe is aggregated.
- A registration is a local immutable reference to source artifacts, with
  request fingerprints. Identical reruns return the same records. Changed
  requests require a new versioned directory. Source files, maps and
  evidence must remain at the pinned paths for subsequent replay.
- Coverage uses only intervals nominally closed by the requested cutoff,
  with H4 bounded separately at 08:00 Bogotá. Missing intervals and
  explicitly evidenced closures remain visible. Closures require exact
  UTC bounds and a pinned local evidence file. Source gaps are never
  interpolated. Readiness verifies alignment and coverage across the
  original streams. Their OHLC is intentionally independent.
- H4 reports requested coverage, enough candles for the pivot neighborhood,
  observed pivot counts, structural evidence and whether the unchanged
  context candidate evaluated successfully and found an available context.
  A complete source interval is a separate finding from H4 readiness.
  The chosen history interval has no operational strategy authority.
- A known protected date retains its exposure. June 22 is the frozen review;
  June 24 and 26 remain protected from tuning, June 25 is Validation,
  June 30 is HeldOut, and June 29 and September 8 are mentor-exposed.
  Protected and `Unknown` dates expose coverage only. The registration
  accepts an explicitly declared Development or Prospective date for
  isolated replay when its source evidence qualifies. New imports and
  filenames confer no independence. Track any actual use as exposure.

## Existing originals on this workstation

| Pack | H4 rows | H1 rows | M5 rows | M1 rows | First open to last nominal close in the retained UTC representation |
| --- | ---: | ---: | ---: | ---: | --- |
| June | 42 | 161 | 1,932 | 9,660 | June 21 21:00 H4, 22:00 H1/M5/M1, through June 30 21:00 |
| September | 41 | 157 | 1,884 | 9,419 | August 31 21:00 H4, 22:00 H1/M5/M1, through September 9 21:00 |

The June inputs are pinned to the repository's registered documentary
package. The September hashes match the local normalization provenance.
Its UTC representation is inventory evidence only. The retained audit
scopes the offset around supplied session anchors, so full-interval UTC
normalization remains unresolved for this registration. September 8
retains its mentor exposure.

For the June 23 cutoff at 16:30 UTC, H4 had complete coverage of its
requested 40-hour preparation interval and H1/M5/M1 had 18/114/570 closed
rows in their requested windows. The H4 1/1 profile found 2 highs and
1 low. Each of 2/2 and 3/3 found 1 high and 0 lows. None supplied usable
context. The registered replay reproduced `H4ContextUnavailable`.
June 22 and 29 each had four of ten requested H4 candles. Their missing
UTC intervals were June 20 21:00–June 21 21:00 and June 27 21:00–June 28
21:00, respectively. June 24, 25, 26 and 30 each had ten within the
requested interval and remained protected. September 8 had nine in its
provisional H4 inventory, with an unexplained interval at September 7
17:00–21:00 in the retained UTC representation. Its H1/M5/M1 coverage
counts cannot establish audited synchronization.

### Missing historical acquisition

Retain and reuse the existing originals. Request original exports for the
uncovered parts of the native-clock acquisition intervals June 15 00:00
through July 1 00:00 and August 30 00:00 through September 11 00:00. The
existing June exports begin June 22 at 00:00 H4 and 01:00 H1/M5/M1. The
September exports begin September 1 at 00:00 H4 and 01:00 H1/M5/M1 and end
with September 9 bars. Compare all interior missing ranges with reviewed
closure evidence or obtain those original rows. Prioritize earlier native H4
history before June 23 preparation, including the missing June 22 prefix.
These ranges are acquisition requests. They do not establish session
calendars or imply that more bars guarantee sufficient pivots.

## Reproduce the local audit

Run in PowerShell from the repository root. Each request records the
actual time it was created. Rerun registration with the same request
file and output path for idempotent readback.

```powershell
$root = 'C:\MoneyWay\session'
$base = Join-Path $env:TEMP ('moneyway-evidence-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $base | Out-Null
$packs = @(
  @{ Name='june'; Format='JuneDocumentary'; Manifest=(Resolve-Path 'docs/data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json').Path
     Sessions=[ordered]@{'2026-06-22'='ChronologicalReview'; '2026-06-23'='Development'; '2026-06-24'='ChronologicalReview'; '2026-06-25'='Validation'; '2026-06-26'='ChronologicalReview'; '2026-06-29'='MentorExposed'; '2026-06-30'='HeldOut'} },
  @{ Name='september'; Format='SeptemberMentorInventory'; Manifest=(Join-Path $root 'normalized/2026-09-08/normalization-provenance.json')
     Sessions=[ordered]@{'2026-09-08'='MentorExposed'} }
)
foreach ($pack in $packs) {
  $sessions = foreach ($date in $pack.Sessions.Keys) {
    $prep = [DateTimeOffset]::Parse("${date}T13:00:00Z", [Globalization.CultureInfo]::InvariantCulture)
    @{ operatingDate=$date; cutoffUtc=$prep.AddMinutes(210).ToString('O'); exposure=$pack.Sessions[$date]
       requestedStartsUtc=@{H4=$prep.AddHours(-40).ToString('O'); H1=$prep.AddHours(-15).ToString('O'); M5=$prep.AddHours(-6).ToString('O'); M1=$prep.AddHours(-6).ToString('O')} }
  }
  $request = Join-Path $base ($pack.Name + '.json')
  @{ datasetId=('nasdaq-' + $pack.Name + '-original-evidence-v1'); providerId='oanda-global-markets-mt5-demo'; symbol='US100'
     registeredAtUtc=[DateTimeOffset]::UtcNow.ToString('O'); provenance=('Retained documentary ' + $pack.Name + ' source')
     sources=@(@{format=$pack.Format; manifestPath=$pack.Manifest; manifestSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $pack.Manifest).Hash.ToLowerInvariant(); sourceRoot=$root})
     sessions=@($sessions) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $request -Encoding UTF8
  dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-evidence-readiness --request $request --output (Join-Path $base $pack.Name)
  if ($LASTEXITCODE -ne 0) { throw 'Evidence registration failed' }
}
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input (Join-Path $base 'june/registration.json') --output (Join-Path $base 'june23-replay') --compare-end-to-end --cutoff-utc 2026-06-23T16:30:00Z
```

Registration writes `registration.json`, `readiness.json` and
`readiness.md`. June 23 is permitted for diagnostic replay even though
the separately reported H4 context remains unavailable. Replay checks the
selected cutoff and source hashes and invokes the existing synchronized
research evaluator. Existing component commands and reports retain their
formats. The dataset envelope hashes can change in a new version when
future sources are appended while readiness at the earlier cutoff remains
the same.

## Manual original-file collection from November 23

1. In MT5 on the existing OANDA Global Markets demo server, open **Symbols**
   (`Ctrl+U`), select **US100** and use **Bars** to request and export H4,
   H1, M5 and M1 separately. Use closed candles and save the native UTF-8
   tab-separated exports outside the repository. Retain the server,
   symbol, timeframe and actual local observation or acquisition record.
   MT5 only needs to be open while requesting or exporting bars.
2. Prepare reviewed native-to-UTC evidence for every row. A map binds
   the source SHA256, provider, symbol and timeframe to a hashed evidence
   document. Use explicit `rows` or non-overlapping `segments` as shown
   in the existing H4 guide, updating `timeframe` for each export. Split at
   evidenced offset changes. The June mapping, September anchor and a
   generic GMT/DST statement do not establish a new full-interval mapping.
   Keep any unresolved source outside replay.
   [Native export and map schema](nasdaq-h4-historical-coverage.md) · [MT5 clock policy audit](mt5-server-time-policy-audit.md)
3. Import each original. The following assumes one unedited export and
   one reviewed map per timeframe with matching names.

```powershell
$batch = 'C:\MoneyWay\incoming\nov23-v1'
foreach ($tf in @('H4','H1','M5','M1')) {
  $source = Join-Path $batch ("US100_" + $tf + ".tsv")
  $map = Join-Path $batch ($tf + '-map.json')
  $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash.ToLowerInvariant()
  $mapHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $map).Hash.ToLowerInvariant()
  python -B tools/research/import_nasdaq_h4.py --timeframe $tf --source $source --source-sha256 $sourceHash --timestamp-map $map --mapping-sha256 $mapHash --imported-at-utc ([DateTimeOffset]::UtcNow.ToString('O')) --output (Join-Path 'C:\MoneyWay\imports\nov23-v1' $tf)
  if ($LASTEXITCODE -ne 0) { throw "Import failed for $tf" }
}
```

4. Use `format: "OriginalImport"` with each new absolute
   `manifestPath` and its lowercase `manifestSha256` in the registration
   request above. Declare the session exposure as `Prospective` only for
   cutoffs from November 23 Bogotá onward. Supply
   `localAcquisition: { acquiredAtUtc, evidence: { path, sha256 } }` on
   each source batch or once at dataset level if all share the same actual
   receipt. A closure entry, when verified, uses
   `{ timeframe, startUtc, endUtc, evidence: { path, sha256 } }` in
   `documentedClosures`. Choose explicit cutoffs and history, then run the
   same readiness command and use its registration path for research.
   Keep later acquisitions in new versioned directories through the
   December 18 milestone.

API, PostgreSQL, a background Worker and a broker connection are unnecessary
for import, registration, reporting and replay. No new prospective
observations were collected in this audit. Independent validation still
requires admissible chronology, independently reviewed evidence and a
protected validation protocol. Operational strategy approval remains
outside data registration.
