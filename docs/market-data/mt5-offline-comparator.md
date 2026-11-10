# MT5 offline acquisition comparator

## Scope and invocation

[Compare-Mt5ProbeArtifacts.ps1](../../tools/market-data/mt5/Compare-Mt5ProbeArtifacts.ps1)
compares two explicit `moneyway.mt5-read-only-probe` v1 files without a running
terminal, network access, broker connection, evaluator or canonical ingestion.
It prints a concise summary and returns a structured PowerShell object. It never
modifies inputs. An optional UTF-8 JSON report is created exclusively: an existing
report or either input cannot be overwritten. Its parent directory must exist.

From the repository root:

```powershell
$comparison = & ./tools/market-data/mt5/Compare-Mt5ProbeArtifacts.ps1 `
    -EarlierPath '<first diagnostic.json>' `
    -LaterPath '<second diagnostic.json>' `
    -CaptureOrderAttested `
    -ReportPath '<new comparison.json>'

$comparison.timeframes | Select-Object timeframe, comparable, unchanged_count, changed_count,
    newly_present_count, absent_from_later_sample_count
```

Only use `-CaptureOrderAttested` when the capture sequence is known independently
and can be attested by the operator. Without it the comparison still runs, but
reports `unverified_caller_labels_only`. Even with it, ordering is
`caller_attested_not_independently_verified`. Filename order, computer-derived GMT,
server quote candidates and cross-run elapsed counters do not authenticate capture
sequence. Inputs are never swapped automatically. A regressed native head is
reported for review without inventing its cause or a timezone conversion.

## Input boundary

The comparator reuses the [existing validator](../../tools/market-data/mt5/Test-Mt5ProbeArtifact.ps1)
under a read lock denying concurrent writes/deletion while each input is read,
validated and hashed. The existing 2 MiB/file and 128 bars/timeframe limits apply.
The report's SHA256 values identify the actual read bytes; paths and account
identifiers are not embedded. The validator cannot authenticate the origin of an
offline file. Synthetic artifacts remain synthetic.

Required identity is exact, case-sensitive platform/company/server/demo mode and
native symbol/custom flag/profit currency. Each file must contain precisely
M1/M5/H1/H4 with compatible declared durations. A different provider, symbol,
server or malformed schema fails safely with its reason; it is not merged.
Terminal-build differences remain separate earlier/later metadata, not automatic
incompatibility. Source gate and acquisition errors remain visible.

Missing timeframe records are malformed v1 input and rejected. Properly represented
failed, empty or unattempted samples are retained as non-comparable, with null
change counts; the entire opposite sample is not called newly present or absent.
Partial, unsynchronized or uncertain-head samples containing valid rows can still
be compared as raw observations, with explicit limitations and original quality
metadata. No such comparison certifies a complete or healthy snapshot.

Malformed OHLC is rejected even if a diagnostic properly flags it invalid. Native
timestamps and chronological uniqueness must satisfy the original validator;
duplicate/conflicting native identities are reported and rejected, never silently
deduplicated. No timestamp format relaxation or UTC normalization is introduced.
Equivalent JSON whitespace/Unicode escaping does not change a raw native identity;
an unsupported alternate datetime display format still fails original validation.

OHLC numeric tokens are additionally retained as strings and compared by exact
decimal value using integer digits and decimal exponents. There is no conversion
to binary floating point for the comparison, price rounding, tolerance or epsilon.
Equivalent values such as `101.25` and `1.012500e2` compare equal while both original
tokens are retained. Differences beyond `double` precision remain discrepancies.
Exact OHLC inequalities are also checked after normal schema validation.

## Comparison meanings

The key is exact source/timeframe identity plus native raw bar-open timestamp.
Output timeframes always follow M1/M5/H1/H4 order. Within each collection, source
newest-first order is retained. No wall-clock generation timestamp is added, so
the same bytes/options yield the same report content.

| Observation                                        | Diagnostic meaning                                                                                                              |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Overlap, equal exact OHLC                          | `unchanged_ohlc`; both original payloads and roles retained                                                                     |
| Same current bar, changed OHLC                     | `current_bar_change_may_be_forming_evolution`; possible evolution, not a guaranteed cause                                       |
| Current bar later historical, changed OHLC         | `current_to_historical_changed_completion_unverified`; progression evidence without source-close or availability authentication |
| Historical in both, changed OHLC                   | `historical_value_discrepancy_requires_investigation`; no automatic correction/corruption diagnosis                             |
| Changed OHLC with uncertain/other roles            | `unverified_or_changed_role_discrepancy`                                                                                        |
| Any role transition, even unchanged OHLC           | Separate `role_transitions` entry; historical-to-current or uncertain transitions remain visible                                |
| Later-only native identities                       | `newly_present`, not proof of first provider creation or first receipt                                                          |
| Earlier-only native identities                     | `absent_from_later_sample`, not provider deletion; bounded-window eviction or incomplete sampling remain possible               |
| No overlap                                         | Counts describe the two observed sets, with an explicit inability to assess overlapping-value changes                           |
| No newly present rows                              | No disconnection, market-closed or stale-data conclusion                                                                        |
| Adjacent native delta differs from declared period | `native_interval_difference_cause_unresolved`; no calendar inference, gap filling or threshold                                  |

When capture order is unverified, “earlier/later” in these labels refers solely to
the supplied argument roles. A row leaving a bounded sample does not prove source
deletion, and a value change in this order does not establish a broker revision
policy. Earlier emitted replay frames are never touched.

## Comparison report v1

Schema: `moneyway.mt5-offline-comparison`, version `1`.

- `diagnostic_only=true`, `canonical_ready=false`,
  `utc_conversion_authenticated=false`, `provider_confirmed_closed=null`,
  `historical_available_at_utc=null` are fixed boundaries.
- `capture_order` records only the explicit attestation state.
- `source` contains the compatible non-sensitive identity. `earlier` and `later`
  contain SHA256, terminal build, gate, connection flags, native generation-time
  candidate and server quote samples. These clocks preserve their original basis.
- `timeframes` contains duration, comparability, both acquisition/quality records,
  unchanged/changed/newly-present/absent counts, detailed overlaps, newly present
  and absent payloads, role transitions and native discontinuities. Non-comparable
  counts are null; empty diagnostic arrays do not imply successful acquisition.
- Each bar view retains native opening raw/display values, position, role and
  `ohlc_literals` strings. Changed overlaps preserve both earlier and later values.
- `limitations` exists at report and timeframe levels. No generated UTC,
  inferred offset, source latency, finality, historical receipt, warmup sufficiency,
  correction window, staleness limit, strategy verdict or execution is produced.

This is a diagnostic schema, not a new Candle/CandleSeries contract. It does not
populate `StrategyReplayContext.AsOfUtc` or change canonical future-data guards.

## Real capture status and obtaining a second sample

The [first authentic acquisition](mt5-first-real-acquisition.md) remains external:
`probe-1794315975-132125.json`, SHA256
`d2dadabb3586bb2378c4b9287cfaa2d1ff3467d70e6f73220d56baebcf7a8f03`.
It was independently rehashed during this feature. Only that one real artifact was
present in the reported probe output directory. **A real two-sample comparison is
UNVERIFIED.** Fixtures and any same-file self-comparison do not establish a second
authentic acquisition or real feed progression.

1. Keep the original JSON unchanged. In MT5 verify demo and
   `OANDA_Global-Demo-1`; keep Algo Trading disabled and source unchanged.
2. Run the already compiled `MoneyWayReadOnlyProbe` again from Navigator > Scripts,
   initially with `SampleBars=16`. No source change or recompilation is needed.
   If MT5 blocks execution under existing settings, stop and report the blocker.
3. Read Experts for the new filename under Common Data Folder >
   `Files\MoneyWayReadOnlyProbe`. Retain both files and attest actual capture order
   independently. Do not rename or alter the first file to manufacture a capture.
4. Validate the second file with `Test-Mt5ProbeArtifact.ps1`, retain its actual
   SHA256, then use the explicit command above with a new report path.

A successful comparison still establishes **no authenticated UTC conversion,
confirmed bar closure, historical availability, or operational current-data
ingestion**. The one observed +7200s difference remains a single-sample observation.

## Validation and smallest next feature

```powershell
& ./tests/tools/Mt5ProbeComparator.Tests.ps1
& ./tests/tools/Mt5ReadOnlyProbe.Tests.ps1
git diff --check
```

The standalone suites share explicitly synthetic fixture builders. They test
comparison behavior and probe contract/safety without MT5. Backend/frontend
runtime tests are not needed for this isolated tool; neither runtime changes.

**Next feature:** register the first authentic two-sample comparison with both
hashes, operator-attested sequence, observed changes and remaining temporal
blockers. Independent UTC/completion evidence will still be required before
implementing current-data ingestion; the comparator cannot supply those facts.
