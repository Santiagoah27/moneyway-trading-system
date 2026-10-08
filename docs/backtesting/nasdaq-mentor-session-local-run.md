# First local Nasdaq mentor session (Feature 29)

## Ownership and one-run path

Application owns `NasdaqMentorSessionReplay`, its bounded input records and immutable report/frame models under `Strategies/Nasdaq/MentorSessions`. Infrastructure owns `RunLocalNasdaqMentorSessionUseCase`: the file boundary calls the existing `CsvMarketDataImporter.ImportFile` four times and composes the already registered Nasdaq definition, evaluators, workflow and lifecycle catalogs. No Application dependency on Infrastructure was added.

The existing integration-test root supplies the locally executable entrypoint `LocalNasdaqMentorSessionTests.RunValidatedLocalSession`. Worker `replay-csv` only runs a neutral single-series replay, and the API has no historical session endpoint. Neither host was changed and no host was added.

One call executes `GenerateCanonicalMultiTimeframeBacktestUseCase.ExecuteMentorSession` -> the existing outcome wrapper -> the existing strategy run owner -> `RunMultiTimeframeReplayUseCase` -> bounded context -> canonical evaluators/workflow/lifecycle -> safe outcomes -> canonical diagnostics. The session-specific overload delegates to this traversal. It does not obtain facts by replaying prefixes or scanning a second market cursor.

## Input and CSV files

`NasdaqMentorSessionReplayInput` contains:

- `SessionId`: stable reviewed session/material identifier.
- `Session`: existing `NasdaqDemoSessionIdentity` with exact registered strategy/version, provider, instrument and America/Bogota trading day.
- `CsvFiles`: paths keyed by exactly (4, Hour), (1, Hour), (5, Minute), (1, Minute).
- `Evidence`: immutable membership of uniquely identified `NasdaqMentorSessionEvidenceRecord` bindings. Each has the original `ObservedAtUtc` and a callback returning an existing typed observation, or null while its exact dependencies are unavailable. Effective event times, source references and reviewed values remain in the existing observation/event constructors.

Each file uses this exact required column set (reordering is allowed by the importer):

```csv
provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume
```

Use explicit UTC ISO 8601 (`Z` or zero offset), invariant decimal points, homogeneous provider/symbol/timeframe, unique ascending non-overlapping candles and valid OHLC. The volume column is mandatory; empty means unknown. Importer codes/messages/line/column and file references are retained. Missing/unreadable/invalid/mismatched files or absence of any 1M close on the selected Bogota day stop before replay and produce input diagnostics with no fabricated strategy result. Nothing resamples, repairs, downloads or guesses timestamps.

Supply source history for actual Asia/London and structural context plus the complete observed session/post-entry interval. Replay bounds are the first and last synchronized closes in the supplied files, including warmup. Human bindings and historical artifacts are scoped to the selected Bogota trading day; other frames carry `OutsideSelectedSession`. File coverage alone is not proof of preparation or any human assertion.

## Existing human records and causal binding

Use the existing preparation, H4 context, structural liquidity, liquidity take/relevance, M5 trigger/FVG/quality, corrective retracement, realignment, observed entry, SL, TP, risk and documented exit types. Applicable H4 reconstruction observations may be supplied through the same existing typed contract. There is no replacement human-evidence schema or invented NQ-M1-003 observation.

Raw observations can use `NasdaqMentorSessionEvidenceRecord.FromObservation(recordId, observation)`. Fact-dependent records use their callback `(context, snapshot)`. It receives only the current bounded market context and earlier canonical observations. It must select the exact owning rule and reviewed setup identity/source ancestry from those observations, not matching approximate times/prices/direction. Return null while that fact is absent. Construct downstream records with that actual RuleFact reference. Use `context.InputObservations` or captured already-reviewed raw observation references for human-observation dependencies.

The binder invokes no callback before its declared availability. Returned observation availability must equal the record's declared original availability, and strategy/version/provider/instrument must match. Realignment pullback, entry/SL eligibility and TP/risk SL references must have been emitted by this run (reference equality). Foreign canonical ancestry is rejected once and recorded separately; it is never rewritten to a similar setup. Existing selectors continue to enforce their full exact contracts and source availability. Constructor errors and mismatched identities/availability are separate binding diagnostics. Null prerequisites remain explicit `PrerequisiteUnavailable` diagnostics. Later-reviewed annotations cannot backdate knowledge.

Canonical evaluators already consume earlier canonical observations. Feature 29 preserves that progression: binding/transport may happen at a later synchronized frame than the source event. It does not add a required market confirmation, change same-close event semantics or fake facts inside the earlier frame. `EligibilityEffectiveAtUtc` still equals the source realignment close. Supply the actual continuous 1M record and enough subsequent canonical boundaries for transport; a dataset ending before required canonical facts exist truthfully reports unavailable artifacts.

At each frame, the existing snapshot assembler is attempted against current prior eligibility and exact constituents. The first Available snapshot is preserved by reference for this one observed trade. Subsequent exit callbacks receive that object. Contacts come only from the existing candle-source collector (this four-CSV entrypoint is candle-only); contact order comes only from the existing resolver. The documented-exit selector and factual composer retain Missing/Unique/Conflict/unresolved/unavailable semantics. Contacts never supply execution or a missing exit.

## Concrete local workflow

Create a **local, reviewed data transcription** C# file outside the repository (for example `C:/MoneyWay/session/ValidatedInput.cs`). Implement this partial method with the actual source values and existing constructors; no input values are provided here because none are validated:

```csharp
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

namespace MoneyWay.IntegrationTests;

public sealed partial class LocalNasdaqMentorSessionTests
{
    static partial void ProvideValidatedInput(ref NasdaqMentorSessionReplayInput? input)
    {
        // Construct the reviewed Session, four absolute CSV paths and existing typed
        // observations/binding callbacks from retained records. Then assign:
        // input = new NasdaqMentorSessionReplayInput(sessionId, session, csvFiles, records);
    }
}
```

This is a typed record adapter, not a new strategy implementation. For a downstream realignment callback, obtain the actual `NasdaqHumanM1CorrectiveRetracementRuleFact` from the latest prior `NQ-M1-001` evaluation, verify the exact retained pullback identity/ancestry required by the realignment record, and call its existing constructor. Entry/SL use emitted `NQ-M1-003`; TP/risk use emitted `NQ-SL-001`; exit uses the exact callback snapshot. Explicit event times, parameters, optional causal proof and provenance must come from reviewed sources. No canonical RuleFact constructors are needed or exposed.

Run from the repository root in PowerShell:

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\ValidatedInput.cs'
$env:MONEYWAY_MENTOR_REPORT_PATH = 'C:\MoneyWay\session\report.json'
dotnet test tests/integration/MoneyWay.IntegrationTests/MoneyWay.IntegrationTests.csproj --no-restore "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter FullyQualifiedName~LocalNasdaqMentorSessionTests.RunValidatedLocalSession --logger "console;verbosity=detailed"
Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
Remove-Item Env:MONEYWAY_MENTOR_REPORT_PATH
```

Do not use `--no-build` when supplying/changing the adapter. The existing test project compiles the specified source using its optional Compile item. Absent the environment variable, the real-data entrypoint is explicitly skipped. Supplying an empty adapter fails `Assert.NotNull`, rather than claiming a real run. Invalid input is written to the report before the assertions fail. An unresolved strategy or historical artifact is retained in the report; this entrypoint does not require or fabricate Ready/Available.

The result can also be consumed directly from C#: `new RunLocalNasdaqMentorSessionUseCase().Execute(input)`. Optional `NasdaqMentorSessionReportJson.Serialize(report)` prints the separate domains and polymorphic artifact states with shared references (`$id`/`$ref`), avoiding repeated expansion of ancestry. It does not serialize callbacks. Consumers can inspect exact objects directly in the returned DTO.

## Consolidated immutable report

- Session identity, supplied paths, successful imported timeframe identities/counts/source ranges, truthful import/input diagnostics and replay start/end.
- Exact canonical diagnostics and safe StrategyVerdict, canonical ordered evaluations with RuleId/definition status/required metadata/time/evidence, RuleFacts and workflow/lifecycle history.
- Per-frame causally visible typed inputs and separate binding diagnostics.
- Snapshot Available/Unavailable, or explicit `CanonicalEligibilityUnavailable` when assembly prerequisites do not exist; entry/SL/TP/risk and full ancestry when available.
- Exact contacts with timeframe/resolution, evidence windows and relation to entry; closed resolution variants/pairwise diagnostics.
- Documented exit selection with all supporting/alternative/unavailable/unresolved records; literal values/provenance when available.
- Factual Available/Unavailable and diagnostics. Before a snapshot exists these dependent sections are Unavailable in JSON and null in the typed frame, with its snapshot diagnostic. No synthetic Missing selection is constructed for an imaginary trade.

There is no aggregate verdict combining strategy and factual sections. Collections are copied/read-only; canonical and historical artifact references are retained. Earlier reports/frames are not mutated by later output.

## Post-feature readiness audit (1-10)

| # | Answer |
| --- | --- |
| 1 | The Feature 28 P0 code blocker is removed: incremental actual-fact binding and the per-frame artifact/report consumer now share the canonical traversal. |
| 2 | Exact locally executable workflow: the PowerShell integration-test command above; programmatic entrypoint `RunLocalNasdaqMentorSessionUseCase.Execute(input)`. |
| 3 | Four validated local CSVs plus a local typed record adapter constructing the bounded input above; no real input adapter/files are committed. |
| 4 | The exact CSV schema/timeframe/UTC/decimal/source requirements are listed above and enforced by the existing importer. |
| 5 | Current source correction (2026-10-07): the [2024 execution candidate](../data/nasdaq/mentor-session-2024-06-28.md), including Open 14:02:xx / Close 14:03:xx, is rejected_ai_inference. The replacement [2026-06-29 NO_TRADE case](../data/nasdaq/mentor-session-2026-06-29.md) has no entry or exit; those instances and MetaTrader alignment are not_applicable. Exact instrument/provider, four OHLC series, reviewed source memberships/levels, event classification/times, preparation/quality and authentic availability remain pending. No trade snapshot or execution evidence is to be fabricated. |
| 6 | All currently registered Nasdaq stages can feed one run; the synthetic four-file integration fixture reaches snapshot, contacts, resolution, unique exit and Available factual composition. This proves wiring, not real trading accuracy or the validity of an unprovided session. |
| 7 | All requested report sections are exposed, retaining unavailable/ambiguous states and exact source contracts. StrategyVerdict is separate. |
| 8 | No remaining code blocker is identified for this bounded factual Demo after the validation recorded in the task report. No claim is made for UI, downloader, economic simulation or execution. |
| 9 | No remaining specification blocker prevents this bounded factual replay; unresolved contact-versus-execution comparison remains an explicit human-review diagnostic rather than a required economic policy. |
| 10 | Acquisition, transcription and validation of actual session files/records remain. No actual mentor replay has occurred. |

**READY_FOR_FIRST_REAL_SESSION_DATA**

NEXT_RECOMMENDED_ACTION: ACQUIRE_AND_VALIDATE_FIRST_REAL_MENTOR_SESSION_DATA

No fake mentor data, new RuleId/evaluator/strategy primitive, timeframe synthesis, simulated fill, Win/Loss/PnL/R, broker/order action, UI or downloader is introduced. Capability remains 32 RuleIds / 14 Required / 16 evaluators / 0 Required gaps / full=true.

## Feature 29 validation record

- Focused session linkage: 8 Application unit tests passed (canonical fact/snapshot identity, causal visibility, future callback suppression, immutable earlier output, foreign run/snapshot rejection, input failures and strategy/factual separation).
- Focused CSV/session import: 8 integration tests passed (all four imports reaching factual Available, each missing timeframe, invalid CSV diagnostics, identity/unreadable file and absent selected-day data).
- Historical/canonical/multi-timeframe regression selection: 435 Application tests passed.
- Full Application suite: 2292 passed; full integration suite: 115 passed, 1 explicitly skipped because real mentor data was not supplied.
- Domain suite: 223 passed after the internal shared-context clone extension.
- The documented local command was exercised with an external **synthetic test-only** adapter: 1 test passed, producing a 36-frame JSON report with Available snapshot, Unique documented exit and Available factual evaluation. This is not a real mentor session or dataset validation.
- `dotnet build MoneyWay.sln --no-restore`: passed, 0 warnings/errors. `dotnet format MoneyWay.sln --verify-no-changes --no-restore`: passed. `git diff --check`: passed.
- Existing M5Fixture and LiquidityFixture bodies were moved without changes into shared test files; no existing tests were deleted. UTF-8/LF and local documentation links were checked.
