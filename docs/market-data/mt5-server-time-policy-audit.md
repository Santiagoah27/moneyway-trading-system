# OANDA Global Markets MT5 server-time policy audit

Audit date: 2026-11-11. Initial Git checkpoint: `main`,
`93dff96a3e2ca86d8e10cb7fdeafc35db850983d`, clean and synchronized.
Documentation only: no timestamp converter, changed evidence or strategy behavior.

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

No code or tests changed. Focused runtime tests were inspected, not executed for
this documentary change. Validation consists of three actual artifact checks,
hash verification, primary-reference inspection and documentation/link/diff checks.

## Smallest independent next evidence step

Obtain one retained, dated provider clarification for **OANDA_Global-Demo-1** that
defines the governing seasonal-clock authority, exact changeover instants and
historical bar labeling/validity, resolving the EST wording and supplying an
independently UTC-anchored native example. A support request may be drafted for
human submission; this audit sends no message and requests no credentials. If the
provider cannot establish those facts, keep them unresolved rather than implement
a guessed converter. Closure and historical availability still require their own
evidence after any clock-policy clarification.
