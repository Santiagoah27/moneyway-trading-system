# Replay #2 S13 real reversal adapter and causal WF2 evaluation

## Scope and authority

This feature adds the already-approved documentary recognition to the external September 8, 2026 adapter. [ADR 0026 section 2.12](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md#212-human-approved-historical-bearish-reversal-recognition-2026-11-04-s13), committed at `f6231e1d353609080faf8399879540d09b4d3dd6`, supplies S13 approval attachment `8fddfee6-00d6-4675-bc79-6e7fcd4eafc8`, approval-text SHA256 `d9a90be436fe59b345a8b170c967c3c69d55c87e04ba44841d3eb1080844860a`.

Both effective and observed values are exactly `2026-09-08T13:33:10Z`: recognition/availability of an already-visible genuine bearish reversal at 08:33:10 AM America/Bogota, not a video offset, first bearish tick, original onset or first mentor recognition. S12 keeps conditional preparation, activation/fill and strategic eligibility separate. This assertion cannot authorize the earlier historical 08:32 entry. USTEC visual evidence is not native US100 price equivalence; screenshot bytes remain external / not locally verified.

## External artifact checkpoint (2026-11-04)

| Artifact | Value |
|---|---|
| Adapter | `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs` |
| Exclusive original-byte backup | `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.before-S13-reversal-20261104-c79188b5.cs.txt` |
| Before / backup SHA256 | `e2da62fbb8cb347b7178396867e92f7f8f11bc07e38fd0c357349f7d80b62c1a` |
| After SHA256 | `8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2` |

The adapter is outside Git; this commit includes tests and reproduction documentation, not its source bytes. Preserve the [S9 input/context](nasdaq-replay2-s9-context-adapter.md) and [S11 applicability](nasdaq-replay2-s11-applicability-adapter.md), all original records/identities/CSV paths, and previous backups/reports. No source CSV, manifest or prior report is rewritten. No new historical report is required.

## Documentary identity and source reference

The stable `EventId` is:

```text
human-recognition:8fddfee6-00d6-4675-bc79-6e7fcd4eafc8:2026-09-08T13:33:10Z
```

It namespaces the retained S13 approval identity and approved recognition time. It is not a market-tick, broker, price-event or screenshot identifier. The adapter's exact `SourceReference` is:

```text
human:S13 ADR 0026 section 2.12, committed at f6231e1d353609080faf8399879540d09b4d3dd6; approval attachment 8fddfee6-00d6-4675-bc79-6e7fcd4eafc8, text SHA256 d9a90be436fe59b345a8b170c967c3c69d55c87e04ba44841d3eb1080844860a; registered 2026-11-04; original September 8 mentor recording, human-identified reversal bajista already visible at 08:33:10 AM America/Bogota, not playback minute 08:33; documented recognition/availability, not first tick/onset/mentor recognition; reported MT5 USTEC M30 clock 16:33:10 under September-only server-minus-three-hours mapping; image bytes external/not locally verified, no native US100 price equivalence; S12 section 2.11 keeps conditional order preparation/fill separate, no retroactive authorization of 08:32 entry
```

The hash verifies human approval text only. No invented image path/hash, video offset or immutable image identity is attached.

## Reproduce the isolated adapter change

Start from the before hash and create a unique exclusive backup. Add `using MoneyWay.Domain.MarketData.Replay;` and the following local S13 binding before the unchanged legacy M5 callback:

1. Define `reversalRecognized = new DateTimeOffset(2026, 9, 8, 13, 33, 10, TimeSpan.Zero)` and the exact documentary ID/source above.
2. Return null before `context.AsOfUtc >= reversalRecognized`. Reuse the existing `BindApplicability(context)` and `BindPreOpenContext(context)` callbacks; require both and the adapter's native take. S11 already verifies the exact current-run active Start and its native initiating take, not an extension or matching foreign ordinal.
3. The historical binder does not expose its accumulated bound inputs to callbacks. For source composition only, create a `Dictionary<Timeframe, ReplayFrame>` by reading each actual `context.AvailableTimeframes` child with `TryGetFrame`. Construct `MultiTimeframeReplayFrame` using the unchanged provider, symbol, Step, AsOfUtc, ConfiguredTimeframes, UpdatedTimeframes and those exact bounded children. No candle, market event, price, replay boundary or canonical owner is created.
4. Use `CreateStrategyReplayContextUseCase.Execute(definition, frame, [h4, preOpen, nativeTake]).WithPriorObservations(context.PriorObservations)` to expose only approved available sources to selectors. No evaluator is called here. Select Unique pre-open context and call `NasdaqWickfillScenario2OpeningSweepContext.From(view, unique, applicability.InitialTakeObservation)`. The actual owner and canonical H4/take fact references are retained; this view is not a replacement owner or an independent evaluation.
5. Return `new NasdaqHumanWickfillScenario2PostSweepReversalObservation(sweep, reversalEventId, reversalRecognized, reversalRecognized, reversalSource)` without optional supporting candles. No final OHLC from the forming M30 is consumed.
6. Immediately after the existing applicability record, register `new("wf2-S13-reversal", reversalRecognized, (c, _) => BindReversal(c))`. Keep the S9/S11 and legacy take/M5 records unchanged. Existing binder/selector/evaluator guards perform all actual transport/evaluation.

## Opt-in execution and regression coverage

Use the real adapter through the [existing local mechanism](nasdaq-mentor-session-local-run.md#concrete-local-workflow):

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
try {
    dotnet test tests/integration/MoneyWay.IntegrationTests/MoneyWay.IntegrationTests.csproj --no-restore "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter FullyQualifiedName~LocalNasdaqMentorSessionTests.ValidateReplay2S13 --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
}
```

Do not use `--no-build` after editing the adapter. Without the opt-in variable the test is skipped, not counted as a real run. Run builds sequentially. Relevant regressions are `ValidateReplay2S9`, `ValidateReplay2S11` and `NasdaqScenario2MentorSessionTests`; unit coverage is in the reversal observation/evaluator, opening-sweep, applicability, scoped eligibility and Scenario 2 lifecycle test classes.

The S13 test imports the actual 1M/5M/1H/4H CSVs and retains all original historical members through each selected endpoint in memory. It executes the existing registered canonical evaluator/workflow/lifecycle/session binder twice with independent adapter objects, through 13:35Z and 13:36Z. Common-prefix fingerprints compare canonical step/time, evaluation evidence and reversal inputs; including the later source tail must not mutate any prior frame, including pre-S13 results. No candles are generated to fill gaps and no evaluator is invoked with a synthetic owner as evidence of historical success.

Tests assert the exact source/ID/time, opening-sweep/native take/active Start/H4 reference ancestry and S11 compatibility; absence at 13:32/13:33; no invented 13:33:10 frame; first actual binding and WF2-001/002 frames; prior canonical reversal reference for eligibility; rejection of a callback with missing/foreign owner history; empty optional M30 candle support; Standard NotApplicable without fabricated facts; and no SL/TP/risk, trade snapshot, contact/exit/factual result or Ready. Existing harness tests cover foreign reversal-owner rejection even when coordinates match; unit tests cover duplicate/conflicting and future assertions. These synthetic guards remain test-only evidence.

## Verified real causal results (2026-11-04)

The opt-in S13 test successfully executed two independent real-source canonical windows with 7,595 / 7,596 frames, retaining every native source candle through 13:35Z / 13:36Z. The complete common-prefix fingerprint was identical, including all pre-S13 evaluations. No historical CSV or prior result was rewritten.

| Event / canonical result | Actual UTC on 2026-09-08 |
|---|---|
| Native initiating event `native-M1-close-2026-09-08T13:31Z` | `13:31:00Z` |
| Exact active Start owner | Step `7591`, replay-local instance ordinal `1`, AsOf `13:31:00Z` |
| S11 first applicability binding | `13:32:00Z` (retained prior validation; owner reference verified again) |
| S13 effective / observed | Both `13:33:10Z`, unchanged |
| At/before `13:33:00Z` | No S13 input or WF2-001/002 fact; no backfill |
| First S13 `Bound` diagnostic | `13:34:00Z` |
| First canonical NQ-WF2-001 fact | Passed at `13:34:00Z` |
| NQ-WF2-002 at that same frame | Waiting; no current-frame positive propagation |
| First canonical NQ-WF2-002 fact | Passed at `13:35:00Z`; exact prior reversal reference |
| Eligibility effective time | `13:33:10Z`, preserved recognition time |
| NQ-SL-001 at eligibility frame | Waiting; existing downstream contract not changed |

Session identity remained strategy `moneyway-nasdaq`, version `nasdaq-0.1.0-draft`, provider `oanda-global-markets-mt5-demo`, native symbol `US100`, date `2026-09-08`. Start ordinal/step/time are diagnostic coordinates, never sufficient owner identity: tests retain and compare the exact in-run Start, initiating-take fact and canonical H4 object references, and reject another run's matching owner. The composition used the S9 reviewed SELL/Wickfill context and the same native London High initiating take as S11; optional reversal supporting candles remained empty.

Standard M5/M1 results were NotApplicable at eligibility, with no fabricated Standard facts. Both windows produced no SL/TP/risk fact, trade snapshot, contact/exit/factual artifact or Ready. No full historical trading-session replay, broker execution, historical fill or PnL was reproduced, and no report was written. The 08:32 historical entry is not retroactively authorized by this later recognition/evaluation.

## Validation gates

| Executed validation | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Real S13 opt-in test, two independent native canonical windows | 1 | 0 | 0 |
| Real S9/S11 opt-in regressions | 3 | 0 | 0 |
| Scenario 2 synthetic harness regression tests | 15 | 0 | 0 |
| Focused reversal/eligibility/sweep/applicability/lifecycle/binder unit tests | 178 | 0 | 0 |

The unchanged S11 default prefix still emitted Start at 13:31Z and bound applicability at 13:32Z, twice over 7,593 frames. S9 still became visible first at 13:28Z. Both real opt-in invocations rebuilt with `MONEYWAY_MENTOR_INPUT_SOURCE` and `NasdaqMentorSessionInputSource` pointing to the actual external adapter; no skipped opt-in test is counted as historical success. All builds/test commands ran sequentially. `dotnet build MoneyWay.sln --no-restore --verbosity minimal` passed with zero warnings and zero errors; documentation links and `git diff --check` passed.

Provenance checks verified the actual retained S13 approval-text SHA256, exact adapter source reference, exclusive backup equality and final adapter hash. Integrity checks retained 44 pre-existing source/manifest/backup/report hashes. Only directly related tracked tests/docs changed; original S1-S13 records and all production sources remained untouched.

## Remaining scope

This feature does not modify S1-S13, runtime, rule definitions/evaluators, metadata, strategy activation, canonical architecture, Standard/Forex, SL/TP/risk or broker execution. A focused real-source causal evaluation is not a complete session/trade replay. Exact executable StopPrice, downstream Scenario 2 historical diagnostic ancestry and other risk/TP/execution evidence remain independent; do not generalize M30 selection or stop offsets from this result.

The smallest next diagnostic feature is to persist/read a bounded real WF2 pre-entry diagnostic through existing canonical report tools, showing the proven chain and downstream blockers without creating a trade or choosing SL/TP/risk values.

Follow-up: the [bounded canonical pre-entry diagnostic](nasdaq-replay2-canonical-preentry-diagnostic.md) now records actual safe outcomes, the complete Required matrix and the Standard-ancestry SL blocker using this unchanged adapter.
