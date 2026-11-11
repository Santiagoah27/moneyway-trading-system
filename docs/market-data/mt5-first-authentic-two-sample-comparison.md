# First authentic MT5 US100 two-sample comparison

Recorded 2026-11-11 using the existing offline comparator at repository checkpoint
`d62a45c4c2c01c0061de6eac76d9df8753b7a011`. Technical acquisition evidence only;
no strategy evidence label, evaluator input, order or canonical snapshot is created.

## Inputs, provenance and hash correction

Both human-supplied authentic probe files were found under
`%APPDATA%\MetaQuotes\Terminal\Common\Files\MoneyWayReadOnlyProbe`, read and
independently hashed without modification:

| Argument role | Filename                      |  Bytes | Verified SHA256                                                    |
| ------------- | ----------------------------- | -----: | ------------------------------------------------------------------ |
| Earlier       | `probe-1794333771-28930.json` | 28,246 | `b59af8c40497ab7be2f666775db0ab516d795efbb1a0acc5caa82fb13ed964f0` |
| Later         | `probe-1794333851-3574.json`  | 28,242 | `76ed94be68ec2b582abf35ffa6f92a35245dc5e91edac6b8f7b80569314ca991` |

The initially supplied Earlier digest had 66 characters and failed the integrity
checkpoint. Before evidence publication, the human explicitly confirmed the actual
64-character local SHA256 above. That confirmation resolves the input-identity
discrepancy; it **does not attest acquisition order**. No input bytes were changed
to satisfy an expected digest. Authentic-run attribution rests on the human's
source identification and the independently verified files, not a broker signature.

The existing artifact validator accepted both v1 diagnostics: 64 bars per file,
zero invalid OHLC, `diagnostic_only=true`, `canonical_ready=false`. Source identity
is compatible and exact: MetaTrader 5, OANDA Global Markets Limited,
`OANDA_Global-Demo-1`, verified demo, native `US100`; both terminal builds are 6246. Both gates are `passed`, connections before/after are true, all four
timeframes returned 16 bars, CopyRates errors are zero and synchronization after
acquisition is 1. These findings do not establish USTEC or other-feed equivalence.

## Actual execution and retained report

[Compare-Mt5ProbeArtifacts.ps1](../../tools/market-data/mt5/Compare-Mt5ProbeArtifacts.ps1)
ran with explicit EarlierPath, LaterPath and a unique ReportPath. The
`-CaptureOrderAttested` switch was **omitted**. Actual report fields remain:

```text
schema: moneyway.mt5-offline-comparison
version: 1
capture_order: unverified_caller_labels_only
diagnostic_only: true
canonical_ready: false
utc_conversion_authenticated: false
provider_confirmed_closed: null
historical_available_at_utc: null
```

The full unmodified comparison output remains external, in the same probe folder:

- Filename: `comparison-2026-11-11-9ac0e4ac09f74f5ebb6f8b34a0d42bef.json`.
- Size: **232,396 bytes**.
- SHA256: `487b1de0e40a5ab5679bb305c30b1d68d91f0f3e10a9115daf07ad0e10b1bd88`.

An independent repeat using the same inputs/options, without creating another
report, produced identical serialized content. The report hash and both input
hashes were rechecked. Raw inputs and the full report are not duplicated in Git;
this record retains their exact identities and observed results without personal
account identifiers or credentials.

## Observed comparison results

| Timeframe | Comparable | Overlap | Unchanged OHLC | Changed OHLC | Newly present | Absent from later bounded sample |
| --------- | ---------- | ------: | -------------: | -----------: | ------------: | -------------------------------: |
| M1        | true       |      14 |             13 |            1 |             2 |                                2 |
| M5        | true       |      16 |             15 |            1 |             0 |                                0 |
| H1        | true       |      16 |             15 |            1 |             0 |                                0 |
| H4        | true       |      16 |             15 |            1 |             0 |                                0 |

The actual comparator matches the independently supplied observations. Only close
changed in each changed overlap; open/high/low remained equal. Values below are
the original JSON numeric tokens, not rounded display prices:

| Timeframe / native opening | Earlier close literal    | Later close literal      | Actual classification                                 |
| -------------------------- | ------------------------ | ------------------------ | ----------------------------------------------------- |
| M1 / 2026.11.11 01:02:00   | `27764.4000000000014552` | `27776.2999999999992724` | `current_to_historical_changed_completion_unverified` |
| M5 / 2026.11.11 01:00:00   | `27764.4000000000014552` | `27772.9000000000014552` | `current_bar_change_may_be_forming_evolution`         |
| H1 / 2026.11.11 01:00:00   | `27764.5999999999985449` | `27772.9000000000014552` | `current_bar_change_may_be_forming_evolution`         |
| H4 / 2026.11.11 00:00:00   | `27764.5999999999985449` | `27772.9000000000014552` | `current_bar_change_may_be_forming_evolution`         |

M1 native raw opening `1794358920` (01:02) moved from position 0/current candidate
to position 2/historical preceding. Newly present M1 openings are 01:03 and 01:04
on November 11. Earlier M1 openings 23:47 and 23:48 on November 10 are absent from
the later bounded sample; this is not proof of source deletion. M5/H1/H4 retain
their position-zero identities and show no newly present rows.

**Historical-to-historical OHLC discrepancies: 0.** The changed former-current M1
bar is progression/evolution evidence, not a historical-to-historical correction.
Changes in current bars may reflect forming evolution; their cause is not
independently guaranteed. No tolerance, price rounding, correction policy or
strategy conclusion was introduced.

## Native discontinuities and temporal limitations

Both M1 samples show an adjacent native opening difference of **3660 seconds**
between November 10 23:59 and November 11 01:00, versus a declared 60-second
period. The report also retains native differences of 3900s in M5, 7200s in H1,
and 187200s in H4. Their classification remains
`native_interval_difference_cause_unresolved`. There is no missing-market-data,
corruption, confirmed trading-calendar or session-schedule classification, and
nothing is filled or repaired.

Native server quote samples progress from `2026.11.11 01:02:51` to
`2026.11.11 01:04:11`. The computer-derived GMT candidates are respectively
`2026.11.10 23:02:51` and `2026.11.10 23:04:11`. Their 80-second progress and
observed +7200s differences do not authenticate receipt order, UTC conversion,
source latency, DST policy or a session-day assignment. The human reported upload
order, but did not explicitly attest acquisition order. Earlier/later therefore
remain argument labels; no clock or filename is used to upgrade that uncertainty.

All source bar `open_time_utc`, `provider_confirmed_closed` and
`historical_available_at_utc` fields remain null. Position progression does not
authenticate provider close time or first availability. Sequential timeframe
copies are not an atomic snapshot; synchronization and this comparison do not
prove continuous health, sufficient warmup, reconnect guarantees or absence of
future revisions. Current-data ingestion into the canonical engine is **not
available**, and no evaluator, replay, metadata, Worker, UI/API or execution changes
were made.

## Validation and next feature

- Both authentic diagnostic validations passed, with unchanged input hashes.
- The saved report matched deterministic re-execution and retained its hash.
- Comparator tests: **32 passed, 0 failed, 0 skipped**.
- Existing probe regressions: **27 passed, 0 failed, 0 skipped**.
- Documentation format, local links and `git diff --check` passed before commit.

**Smallest next feature:** a focused source-time mapping evidence audit for
`OANDA_Global-Demo-1`, recording independently supported native-to-UTC reference
pairs, clock basis and applicability interval, or explicit unresolved results if
such evidence is unavailable. Do not turn the observed +7200s differences into a
conversion algorithm or infer DST. Completion/availability, freshness, coverage
and snapshot-admission protections from the [acquisition contract](mt5-first-real-acquisition.md#next-acquisition-boundary-and-open-questions)
remain separate blockers; the comparison alone does not authorize a live adapter.
