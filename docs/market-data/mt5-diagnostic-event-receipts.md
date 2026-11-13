# MT5 diagnostic event receipt semantics

## Audit and root cause

Audit checkpoint: `main`, `9ec686323351f84644f3f0adb65045dda8a7fb95`, clean and
synchronized with `origin/main`. The November 13, 2026 user report described a
148-record snapshot with one `Unresolved` event. Counts are observations, not
permanent expectations or acceptance criteria.

The [collector decision function](../../tools/market-data/mt5/MoneyWayCollectorDecision.mqh)
intentionally maps disconnected/unavailable-head conditions (1/2) to
`source_unavailable`. The [publisher](../../tools/market-data/mt5/MoneyWayReadOnlyCollector.mq5)
captures a referenced probe and publishes this manifest through the same
append-only sequence and predecessor-SHA256 path as other acquisitions.
Successful publication updates `previous_record`/`previous_hash`; it does not
require a usable M1 sample. Last committed native M1 head advances only under
the separate stable/usable/matching-head condition. A later `new_native_m1` links
through, rather than skips, the diagnostic manifest.

In [LocalMt5CollectorReceiptReader](../../src/backend/MoneyWay.Infrastructure/MarketData/Mt5/LocalMt5CollectorReceiptReader.cs),
the source, record/probe hashes, dependencies and chain were validated before an
observation was created. The subsequent quality classification added
`acquisition_unavailable_or_disconnected`, `m1_unusable` and `sample_incomplete`,
yielding `Unresolved`. That status was appropriate for the missing market sample.

The defect was in [Mt5ReceiptCommand](../../src/backend/MoneyWay.Worker/Mt5ReceiptCommand.cs):
both acknowledgment safety and `sourceIdentityValidated` were derived from
`batch.Status == Validated`. The aggregate quality status therefore hid the
successful identity/integrity validation and blocked a valid no-sample event.
The existing durable store already supports explicitly committed unresolved
diagnostic receipts; no checkpoint format, concurrency or recovery change is needed.

## Separate receipt evidence from native sample completeness

The [Application receipt model](../../src/backend/MoneyWay.Application/MarketData/Receipt/Mt5ReceiptModels.cs)
now exposes independent properties without changing `Mt5ReceiptStatus` values:

- `ReceiptIntegrityValidated`: the reader produced a validated observation after
  structure, identity, hashes, dependency and chain checks. Missing/conflicting/
  invalid evidence has no such observation and remains false.
- `SourceIdentityValidated`: validated source identity on that receipt path,
  independent of availability/completeness. Batch claims are fail-closed when
  any record or directory-level validation is unavailable.
- `ReceiptAcknowledgmentEligible`: ordinary `Validated` receipts, or the verified
  no-sample diagnostic profile below. It is not blanket acceptance of `Unresolved`.
- `CompleteNativeSampleObserved`: only the existing complete/stable/connected
  native acquisition criteria. This never authenticates UTC, candle closure,
  freshness, historical availability or canonical usability.

The [wire validator](../../src/backend/MoneyWay.Infrastructure/MarketData/Mt5/Mt5WireContract.cs)
qualifies the narrow `SourceUnavailableWithoutSample` profile **after** normal
probe validation: event `source_unavailable`; poll condition 1/2; stable approved
source; M1 unusable and sample incomplete; captured M1 head null; probe gate
`terminal_disconnected`; disconnected before capture; symbol queries not attempted,
not selected and no existence claim; all four frames `not_attempted` with empty
bars and null copy result/error. Connection may be restored by the after-sample;
that does not fabricate any sampled bars. A disconnected poll (condition 1) cannot
claim a trigger head, according to the actual collector's short-circuit path.

The reader assigns the evidence kind only after all hashes and predecessor checks
have succeeded. Missing probes, broken ancestry, incorrect source identity,
duplicate run/poll identities or changed bytes cannot become eligible diagnostics.
Unknown conditions, condition 0/3/4 on this event, or a fabricated disconnected
trigger fail validation. Unsafe-source or other unresolved acquisition variants
remain blocked. The collector samples poll condition and probe separately, so
other poll/capture races or head-unavailable profiles are not automatically promoted
by this correction; they require their own evidence/contract review.

Original status and diagnostics are preserved: the no-data event remains
`Unresolved`, `CompleteNativeSampleObserved=false`, with null captured M1 head
and zero bars. Subsequent recovery never retroactively validates that sample.
The entire applicable receipt set must remain eligible to generate a review token.
The explicit inspect → review token → commit flow and store revalidation remain
unchanged. No skip-record, force, ignore-errors or automatic commit is introduced.

The CLI retains `status` per record and exposes the new evidence properties.
`integrityStatus` now describes receipt integrity, while `acquisitionStatus` retains
the existing aggregate quality status. Thus `integrityStatus=Validated` and
`acquisitionStatus=Unresolved` can coexist truthfully. Counts distinguish eligible
pending receipts, `sourceUnavailableDiagnosticEvents`, `completeNativeSamples`,
`unresolvedAcquisitions` and blocked receipts. Review tokens still bind the full
ledger and unchanged record hashes, including diagnostics.

## Authentic local evidence

The original files were not attached but were present in the local MT5 Common Files
directory. Read-only inspection and the existing PowerShell validator independently
confirmed records 135, 136 and 137; each returned `ContractValid=true`.

| Artifact                   | Actual SHA256                                                      |
| -------------------------- | ------------------------------------------------------------------ |
| `record-000000000135.json` | `1adcf81a5efb46347114fed63b30ef975a96fa23e9aff53ebfc08031ffcfb4d1` |
| `record-000000000136.json` | `e2b84f2350c9e36aa777a3fbd353249c8db1b8cbd7fdb13ba2e1bf524abcce9e` |
| `probe-000000000136.json`  | `6955be20f46f68a50bfc25cd38038fcbb91f14170a152424cde45bbd9f721766` |
| `record-000000000137.json` | `7307905a97cdb852a5b81c94a3a24b19155b8f64664ae04cbff6ffc58c0c759e` |

Record 136 is run `run-000000000001`, poll 1604, condition 1, source stable,
M1 unusable, incomplete, null trigger/captured head, disconnected before/after,
and four unattempted zero-bar frames. Its predecessor digest equals record 135;
record 137's predecessor digest equals record 136. Native committed head remains
`2026.11.12 22:44:00` (raw `1794523440`); the later native head is
`2026.11.13 17:02:00` (raw `1794589320`). These coordinates do not establish UTC
or the interruption's cause, schedule, duration or continuous source availability.
There was no provider-identity, hash, probe dependency or chain failure in these checks.
SHA256 linkage verifies local byte integrity; it is not a provider signature.

The corrected executable's authentic **inspect-only** smoke run returned exit 0:
177 eligible pending receipts, 176 complete native samples, one unresolved
acquisition/verified no-sample diagnostic, zero blocked. `sourceIdentityValidated`
and `receiptIntegrityValidated` were true; `integrityStatus=Validated`,
`acquisitionStatus=Unresolved`, `safeToAcknowledge=true`, with a review token.
An isolated temporary checkpoint path was used. No checkpoint was created and no
authentic commit was performed. All 354 original JSON artifacts hashed before the
check retained their hashes afterward. No authentic input bytes enter repository
fixtures. These are snapshot counts, not hardcoded runtime expectations.

## Validation and next action

[Synthetic diagnostic receipt tests](../../tests/integration/MoneyWay.IntegrationTests/Mt5DiagnosticReceiptTests.cs)
cover the accepted profile, consecutive events, recovery, malformed evidence,
hash/source/chain failures, duplicates, restart, token binding, explicit commit,
no input mutation and unchanged native/closure semantics. Existing reader,
checkpoint and CLI regressions remain applicable. No collector/MQL, strategy,
metadata, canonical replay or durable checkpoint algorithm changes were made.
Validation passed: 28 diagnostic tests plus 121 existing reader/checkpoint/CLI
tests and 98 PowerShell MT5 regressions: 247 passed, zero failed/skipped. The
complete .NET solution built with zero warnings/errors; formatting and 29 local
documentation links were checked.

Next action: rerun the [local CLI inspection](mt5-on-demand-receipt-command.md)
with the operator's chosen paths, review diagnostics, and acknowledge manually
only if the current evidence and review token remain valid. Do not use this
acknowledgment to authorize canonical ingestion or trading. UTC mapping, provider
closure, historical availability, freshness and multi-timeframe atomicity remain
unverified; `CanonicalReady=false` and `UtcConversionAuthenticated=false`.
