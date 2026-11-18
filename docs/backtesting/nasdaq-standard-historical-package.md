# Standard historical evidence package inspection and causal admission boundary

2026-11-18; initial clean synchronized main:
`14c6db76e234d1b38bcdb18c2db97de7b8d264f5`.

## Discovery and selected case

The approved local evidence directory C:\MoneyWay\session contains the June 29
2026 case, including four original MT5 exports, four normalized files,
normalization provenance, ValidatedInput.cs and report.json. This corrects the
previous integration's incomplete discovery of available Standard material.
The [human ledger](../data/nasdaq/mentor-session-2026-06-29.md) records a NO_TRADE
example. The older [2024 candidate](../data/nasdaq/mentor-session-2024-06-28.md)
remains rejected_ai_inference. September 8 material is WF2, not a substitute.
No stronger approved Standard case was found in those local directories or the
repository's Nasdaq evidence ledgers. Synthetic fixtures remain separate.

Human review C2 confirms TradingView OANDA:NAS100USD. The acquired files and
previous report instead identify oanda-global-markets-mt5-demo / US100. Preserve
both feeds: this is not a price/symbol equivalence. Standard is the candidate
workflow label; no complete typed branch/entry evidence is inferred.

The June report actually inspected is 187,142,873 bytes, 9,660 frames, final
NoTrade; SHA256:
`6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8`.
Do not substitute the older 178,140,291-byte report's B0D5... hash.
ValidatedInput.cs SHA256:
`944bd132b73ad061572a752ddcefc187582812c60a88dccf056bdc4e18806eba`.
The adapter deliberately supplies no positive preparation/H4/FVG/M1/entry
observations. It is inspected as bytes, never executed by this package reader.

| Timeframe | Candles | Unclassified discontinuities | Closes after Bogota session day |
| --- | --- | --- | --- |
| 1M | 9,660 | 6 | 961 |
| 5M | 1,932 | 6 | 193 |
| 1H | 161 | 6 | 17 |
| 4H | 42 | 1 | 5 |

These are exact observations of declared normalized timestamps. Discontinuities
do not independently prove missing data, corruption or a trading schedule.
Later candles remain excluded from any earlier decision by existing AsOfUtc
contracts; their presence in an archive is not contemporaneous availability.

## Registered immutable candidate and source integrity

[Registry](../data/nasdaq/packages/registry.json) and
[version-1 manifest](../data/nasdaq/packages/nasdaq-standard-2026-06-29-documentary-v1.json)
register nasdaq-standard-2026-06-29-documentary. Exact manifest SHA256:
`12684556c3a095ead00d1138b41902b9aba1844cf315383ec5ed87c966665155`.
The manifest lists all 14 original artifacts and their independently inspected
digests: four raw exports, four normalized files, normalization provenance,
adapter, report and three retained human-review texts. Actual source bytes stay
outside Git, unmodified. Only identity/provenance/hash metadata is committed.
Package JSON is pinned to LF by scoped .gitattributes for stable checkout bytes.
Original artifacts are never normalized while checking their digests.

The original raw/normalized digests match normalization-provenance.json. Its
own inspected SHA256 is
`302464759f00e493daa80bb45defe6f7b370e907a121f183182bdb0e7b8ac881`.
The June-only external recipe records server minus 3h and TICKVOL as tick
activity. This inspection does not rerun normalization, generalize that offset
or certify it as publication/capture/availability authentication.

Retained human sources are independently hash-checked:

| Source | Retained attachment | SHA256 |
| --- | --- | --- |
| C1, corrected NO_TRADE case | 5273d5c9-1cfa-449a-955f-924ca83ec599 | 5bf4a59be4ebd9fc76bdaee0d01cdff27645a0131b199d1b2902c9e91e21f4de |
| C2, original chart header | 259d60c0-e5fd-420f-a0ca-40404bc7afd9 | a336af28ab03f599384c74b0ff93f49c498138f4795719704c90424c916df7eb |
| Preparation not historically confirmed | 9d08ffe9-bf37-4c74-ae6e-83b9b13fc588 | 413e8309edfe0e9483b1396192f77efcf3bda2c33d070d997bc8701920c1dcd2 |

Human source review is real retained testimony, not broker authentication or
approval of this newly assembled package. C1/C2 receipt dates are documented as
October 7; no exact UTC creation instant is invented. Preparation testimony says
the chart preparation was reviewed retrospectively on June 30, and June 29
timely preparation cannot be confirmed. Its exact review-creation time remains
unknown. Original mentor media identity/bytes and exact semantic members remain
unestablished; those gaps are not filled from later candles or annotations.

## Separate temporal meanings and admission result

Keep original native event/open timestamps, computed normalized candle closes,
provider publication/first observation, original capture, later human review,
verified historical availability and historical replay AsOfUtc distinct.
ProviderPublicationAtUtc, OriginalSystemCaptureAtUtc and
VerifiedHistoricalAvailableAtUtc remain null. Normalized UTC is declared source
data, not an authenticated native-to-UTC or availability conversion.

Mt5TemporalEvidenceAdmission currently withholds canonical admission even for
consistent supplied claims; it supplies no trusted export-availability verifier.
This feature reuses that fail-closed boundary rather than manufacturing another
credential. All public package paths currently return:

- ValidatedDocumentaryPackage_NotCanonicallyAdmissible when structure and hashes
  are sound; InvalidPackage for identity, artifact, coverage or chronology errors.
- CanonicalReplayPermitted=false, AdmissionResult=DataUnavailable,
  NewCanonicalVerdict=null and no admissible diagnostic reference.
- Retained human declarations only; no independently established package
  admission approval, broker execution or calculated PnL.

No supported authentic package is currently newly admitted by this feature.
Complete synthetic test packages are explicitly SyntheticFixture and cannot
force admission either. This is a finished validation/inspection capability,
not a claimed successful authentic Standard causal replay.

The actual previous research report remains inspectable: Step 7829 at 13:29Z
is Wait, TIME-003 Waiting; Step 7830 at 13:30Z is NoTrade, TIME-003 Failed;
Step 7980 at 16:00Z remains NoTrade. Its missing preparation is a different
reason from the mentor's documentary absence of the required M1 retracement.
Neither conclusion establishes an entry, SL, TP, loss or economic result.
No report, strategy result or human timestamp is rewritten.

## Application capability, validation and safety

ReadNasdaqHistoricalEvidencePackages consumes a server-side registry with exact
manifest hash/version. It reuses SHA256, CsvMarketDataImporter/CandleSeries and
ReadNasdaqHistoricalReport. It never implements another strategy engine or
constructs retrospectively backdated observations. It checks identity/version,
source hashes, original provider/instrument, four timeframes/counts/durations,
ordering/duplicates/overlaps, unclassified intervals, later candles, retained
review provenance/duplicates/conflicts and documented transformations. Corrupt
sources do not trigger synthetic substitution. Registry/manifest/artifact sizes
and counts are bounded; linked paths and escapes from configured roots fail.

Development/loopback-only routes reuse the existing local security boundary:

| Method | Route | Behavior |
| --- | --- | --- |
| GET | /api/nasdaq/historical-packages | List registered IDs/versions/manifest hashes |
| GET | /api/nasdaq/historical-packages/{id}?manifestSha256=...&frameStep=7830 | Validate/inspect exact candidate and previous research frame |
| GET | /api/nasdaq/historical-packages/{id}/diagnostic | 409 while causal admission unsupported; no fabricated result |

Callers cannot provide filesystem paths, upload files or force admission flags.
Expected manifest digest prevents version substitution. Schema/hash failures
fail closed; invalid source packages stay inspectable with explicit reasons.
Missing registry is unavailable, never an implicit scan of the user's disk.

HistoricalEvidencePackagePanel lists/inspects candidates, shows actual hashes,
coverage, review limits and previous rule evaluations under a research-only
label. It can copy exact native identity into the existing session form without
opening a session or trade. No approval checkbox or entry parameter inference
exists. Registered inadmissible report hashes are also recognized by the prior
Standard diagnostic reader: read-only research is allowed, but new diagnostic
binding through generic capture or EntryIntent is rejected. Old documentary
revisions stay readable/correctable; no package closes or reopens a session.

The 11:00 runtime versus newer human 11:30 claim is always surfaced. No cutoff
is chosen, ACTIVE definition changed or forced exit invented. This archive
covers the disputed interval; any eligibility interpretation remains affected
by the unresolved version/specification conflict. Separate correction requires
candidate version -> independent backtest -> human approval -> activation.
The earlier preparation failure is preserved independently of that dispute.

## Actual HTTP/restart acceptance and manual browser steps

Real localhost:5097 acceptance used the registered June files and an isolated
synthetic capture root, %TEMP%\moneyway-june-package-http-20261118-1\capture.
Inspection verified all 14 hashes and the original 9,660-frame NoTrade report.
Package diagnostic admission and attempted generic diagnostic binding returned
409. Before explicit synthetic ownership/closure confirmations the own session
remained open with no execution; the mentor NO_TRADE did not terminate it.
The synthetic lock-test record is 76b730bf-9584-4d0f-af9e-e74b8851dab5 and is not
authentic June execution evidence. API restart/read-back, unchanged package
identity/report, terminal rejection and documentary read results are preserved
in %TEMP%\moneyway-june-package-http-20261118-1\http-proof-final-restarted.json.
The persisted synthetic terminal hash is
`5296c02f2aaca38593a86ed29970f3ca7f18f75e509e8ce2f24b5c6342c3ef87`.
After restart, package admission, EntryIntent, ActivateSetup and a stale manifest
selection each returned 409; the original documentary record stayed readable
and the real research NoTrade frame stayed unchanged.

No browser-control tool is available in this turn; no visual acceptance is
claimed. After a full build, start the actual local application from repository
root in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5097'
$env:NasdaqHistoricalPackages__RegistryDirectory=Join-Path $pwd 'docs/data/nasdaq/packages'
$env:NasdaqHistoricalPackages__Roots__session='C:\MoneyWay\session'
$env:NasdaqHistoricalPackages__Roots__attachments=Join-Path $env:USERPROFILE '.codex/attachments'
$env:NasdaqHistoricalDemo__ReportPath='C:\MoneyWay\session\report.json'
$env:NasdaqHistoricalDemo__ExpectedSha256='6978e3faf8f35e5e2eb812db785eec83ab02095b0805c76a5fdd27fa8f533bb8'
$env:NasdaqExecutionEvidence__Directory=Join-Path $env:TEMP 'moneyway-package-manual-demo'
dotnet run --no-build --no-launch-profile --project src/backend/MoneyWay.Api
```

In another PowerShell terminal at repository root:

```powershell
$env:VITE_API_BASE_URL='http://localhost:5097'
Set-Location src/frontend/moneyway-web
npm run dev -- --host localhost --port 5173
```

1. Open http://localhost:5173/#nasdaq-historical-demo. List packages; select June
   and inspect Step 7830. Verify native US100 and separate NAS100USD testimony.
2. Inspect actual/expected hashes, temporal nulls/limits, coverage, human claims,
   NoTrade research frame and explicit current DataUnavailable admission.
3. Use native identity for the documentary session form; open/recover explicitly.
   Generic diagnostic binding must remain blocked for this registered report.
4. Check the package admission rejection, earlier/later research frames, narrow
   layout/keyboard and package digest pin. Do not change original source files.
5. Restart API using the same registry/capture root; reselect the exact hash.
   For synthetic own-closure acceptance only, confirm a synthetic closing record
   and verify second setup/entry rejection while inspection/documents still work.

## Validation results

Final nonoverlapping .NET sets: 2,124 related Application cases, 277 nonlocal
Nasdaq integration cases (21 package validation cases included), 35 related
backtesting cases, 19 Domain replay cases and five opt-in authentic-report cases,
including June source inspection and separate Replay #2 regressions. Total
2,460 passed, zero failed/skipped in these sets. Earlier focused/repeated runs
overlap these totals. The 60 frontend tests passed with one worker and the
existing timeouts. Two earlier loaded runs timed out in the existing monetary
capture case; the isolated case and final complete suite passed without changing
assertions or timeouts. Backend/frontend builds, ESLint, Prettier, scoped C#
formatting, 21 local documentation links/anchors and git diff whitespace checks
passed. Full backend build had zero warnings/errors.

Temporal regressions cover unverified native times/claims, retrospective and
conflicting reviews, later candles, prior-frame immutability in the existing
canonical shorter/extended replay, no artificial approvals, missing/altered
sources, exact identities, package version pins, read-back and terminal behavior.
Original June report and every registered artifact retain their inspected
digests. Replay #2 remains separate with its original report hash
`48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db`,
SL HumanValidationRequired, risk/TP Waiting and incomplete supervised trade.
No authentic Standard canonical replay was newly admitted or generated here.

## December 18, 2026 assessment

An authentic export candidate and human-reviewed NO_TRADE case are available.
Their byte integrity and prior research diagnostics can be displayed and used
for supervised documentary review. Current causal admission and diagnostic
attempt association are blocked; authentic live-data admission remains blocked.
The timing conflict, human observations/availability and browser acceptance are
unresolved. A supervised documentary historical demo is supported; an authentic
causally admitted Standard or real-time signal demo is not established, and
the December 18 target cannot be guaranteed from these inputs alone.

ONE next functional feature: supervised intake of a source-backed Standard
preparation-completion record with its original availability evidence, using
the existing preparation selector and this package boundary. Its purpose is
to resolve the first mandatory missing historical gate, not infer preparation
from candles or later review. If contemporaneous proof is absent, retain the
documentary record and blocker rather than create Passed or resume clock audits.
