# Replay #2 S9 real pre-open context adapter

## Scope and evidence

This local change represents the September 8, 2026 human-reviewed H4 SELL/Wickfill context in the existing external `ValidatedInput.cs`. It uses [ADR 0026 S1/S8/S9](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md#28-human-confirmed-pre-open-context-temporal-pair-2026-10-30-s9) and the existing [local input mechanism](nasdaq-mentor-session-local-run.md#concrete-local-workflow).

The external adapter is not published by this repository commit. The repository supplies opt-in source-aware tests and this reproduction record. No full real WF2 session replay or new report is claimed by these context tests.

## Local checkpoint (2026-10-30)

| Artifact | Recorded value |
|---|---|
| Adapter | `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs` |
| Original bytes backup, created exclusively | `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.before-S9-context-20261030-d10cc778.cs.txt` |
| Original / backup SHA256 | `cd7a26397b889aa05dbe5ce48da8047012235c682a1c862b63491a0cfdeddb8f` |
| Updated adapter SHA256 | `b897f56a65cfa0285979232a4f3a96500a99ae840adebbe33ac31a18b7c8ca03` |

The backup preserves the old Fakeout interpretation and original bytes. Do not overwrite it. The adapter in `C:\MoneyWay\session\ValidatedInput.cs` belongs to June and is not this input. The four normalized September CSV files and their manifest remain unchanged.

## Reproduce the adapter update

Start from the verified original adapter, preserve another non-overwriting backup if editing a separate copy, and make only these focused edits:

1. Retain session `2026-09-08`, strategy/version, provider `oanda-global-markets-mt5-demo`, symbol `US100`, four normalized CSV paths, and the existing preparation/take/M5 records.
2. Keep the approximate LH upper zone `29618.6–29633.1` and its member open `2026-09-08T09:00:00Z`. Do not use a scalar or substitute TradingView screenshot OHLC for native MT5 values.
3. Represent this session's H4 as SELL/Wickfill under S1/S8, with effective time `2026-09-08T13:00:00Z` and observed time `2026-09-08T13:27:42Z` under S9. Keep the separate preparation available-by boundary `13:27:00Z`; it does not make the complete H4/WF2 interpretation visible then.
4. Retain in H4/context source references S1 attachment `a348f363-cf13-4fd8-bb90-4bd329420656`, ADR 0026 sections 2.7/2.8 at `f42223f4b9d76256b8a92f88296abe110ea79a6f`, S9 attachment `49ff660c-601f-49ce-bf92-643afba282e3` and its verified text SHA256, plus the existing local zone-decision reference. S8 supplies external screenshot descriptions, not repository image bytes or hashes. Preserve the cross-feed zone qualification.
5. Add a uniquely named record `wf2-pre-open-context`, observed at `13:27:42Z`, whose callback uses the existing `NasdaqHumanWickfillScenario2ContextObservation` constructor. Return null before that availability or when the H4 frame/member is unavailable. Resolve only the approved member by its exact open time from `context.TryGetFrame(NasdaqHumanH4ContextObservation.H4, ...)`, verify its close is `13:00Z`, and pass `[member]`, the same reviewed H4 observation and zone, and the S9 temporal pair. No additional candle selection or geometry is needed.

In the updated adapter the callback is:

```csharp
NasdaqHumanWickfillScenario2ContextObservation? BindPreOpenContext(StrategyReplayContext context)
{
    if (context.AsOfUtc < contextObserved || !context.TryGetFrame(NasdaqHumanH4ContextObservation.H4, out var frame)) return null;
    var member = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == h4Open);
    if (member is null || member.CloseTimeUtc != contextEffective) return null;
    return new(h4, zone, [member], contextEffective, contextObserved, contextSource);
}
```

The existing liquidity evidence still describes the native M1 close at `13:31Z`, not the first intraminute crossing. Branch applicability, reversal identity/times and all execution-related records remain absent; the complete WF2 chain is still pending.

## Opt-in validation against the actual adapter

These two local tests require the September S9 adapter specifically. Use their explicit filter; the separate `RunValidatedLocalSession` entrypoint is not invoked.

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
try {
    dotnet test tests/integration/MoneyWay.IntegrationTests/MoneyWay.IntegrationTests.csproj --no-restore "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter FullyQualifiedName~LocalNasdaqMentorSessionTests.ValidateReplay2S9 --logger 'console;verbosity=normal'
} finally {
    Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
}
```

Do not use `--no-build` to incorporate a changed adapter. Without the existing opt-in variable, these tests are skipped. The tests import the real four CSV files and use the existing synchronized candle cursor, context builder, selectors, registered evaluators and safe outcome evaluator to inspect bounded source contexts. They do not run the full mentor-session orchestration, construct snapshots, generate orders or write reports.

Coverage includes the exact S9 pair and native member; invisibility from `13:00Z` through the last pre-availability frame; first usable existing frame `13:28Z`; no synthetic `13:27:42Z` boundary; deterministic repetition; future assertions unable to change earlier outcomes; test-only missing/conflicting evidence and rejected Fakeout conversion; and no WF2/Standard entry/SL/TP/risk facts or Ready from context alone. Test-only contradictory assertions are not historical evidence and are not persisted.

The next independent human evidence task is to establish Scenario 2 applicability review times for this initiating setup; reversal identification and effective/observed times remain separate. Do not infer any of them from S9.
