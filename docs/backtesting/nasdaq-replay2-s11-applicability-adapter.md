# Replay #2 S11 human-approved applicability adapter

## Authority and scope

[ADR 0026 section 2.10](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md#210-human-approved-conservative-applicability-review-pair-2026-11-03-s11) records S11, the explicit user approval supplied in attachment `1e2a10a8-642f-4457-86f7-0f16365753e2`, decision-text SHA256 `45531472f99082928d831cdf24183a6c6bcd31c44549bb4b26522c744324081d`. Registration date is 2026-11-03, not an invented exact message timestamp. S10 remains the historical operational-selection/pending-order record; this approval supplies its conservative field representation. Screenshot bytes are not locally verified.

For September 8 only, raw `ReviewEffectiveAtUtc = 2026-09-08T13:30:37Z` means selection already established by placement; raw `ReviewObservedAtUtc = 2026-09-08T13:30:42Z` means evidence visible by the screenshot clock. Neither is the first instant of decision/observation. S9's context remains effective at `13:00Z`, observed at `13:27:42Z`. Preserve the pending SELL STOP versus fill distinction and USTEC operational evidence versus native US100 identity; no feed-price equivalence is inferred.

## External artifact checkpoint

| Artifact | Value |
|---|---|
| Adapter | `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs` |
| Exclusive original-byte backup | `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.before-S11-applicability-20261103-b4b36036.cs.txt` |
| Before / backup SHA256 | `b897f56a65cfa0285979232a4f3a96500a99ae840adebbe33ac31a18b7c8ca03` |
| After SHA256 | `e2da62fbb8cb347b7178396867e92f7f8f11bc07e38fd0c357349f7d80b62c1a` |

The external adapter is not in Git. The repository commit publishes approval, reproduction details and tests, not the adapter bytes. Existing S9 backups, raw/normalized CSVs, manifests and prior reports must not be overwritten. The [S9 adapter record](nasdaq-replay2-s9-context-adapter.md) describes the preserved input identities, zone, closed H4 member and context callback.

## Reproduce this isolated adapter change

Start from the before hash, create a unique exclusive backup, and preserve all existing records and CSV paths. Add only the following:

1. The two approved raw review constants, and provenance referencing S10/S11 sections, decision attachments/hashes and conservative meanings. No screenshot path/hash or fill is asserted.
2. A `BindApplicability` callback that reads the latest prior lifecycle's active instance, locates its exact prior Start observation by started-at time and instance identity, and requires its NQ-LIQ-003 fact to belong to the same session and retain the adapter's exact native take as both initiating and current take. Return null when those dependencies do not exist. Let the unchanged constructor validate Passed/progression/Start and SELL ancestry.
3. Construct `NasdaqHumanBranchApplicabilityObservation(owner, NasdaqStrategyPath.WickfillScenario2, reviewEffective, reviewObserved, source)` using that actual owner. Never supply a previous run's owner or a same-side extension.
4. Register `wf2-applicability` at `max(takeAvailable, reviewObserved)` for this documented native-close input, not at raw review time. The existing constructor and binder independently enforce actual derived availability. If Start is later than this registered boundary, equality validation rejects the evidence; this adapter does not silently retime or backdate a different run.

The registration boundary is based on the existing native take available at `13:31Z`, not on the unknown first intraminute crossing. The callback cannot produce a new market event, fake Start or a same-frame positive dependency. All other local source records, including the legacy M5 record, remain intact; Scenario 2 classification does not convert that legacy record into an applicable Standard fact.

## Opt-in real-source validation

Use the actual adapter through the existing [local compilation mechanism](nasdaq-mentor-session-local-run.md#concrete-local-workflow):

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
try {
    dotnet test tests/integration/MoneyWay.IntegrationTests/MoneyWay.IntegrationTests.csproj --no-restore "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter 'FullyQualifiedName~LocalNasdaqMentorSessionTests.ValidateReplay2S11|FullyQualifiedName~LocalNasdaqMentorSessionTests.ValidateReplay2S9|FullyQualifiedName~NasdaqScenario2MentorSessionTests' --logger 'console;verbosity=normal'
} finally {
    Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
}
```

Rebuild after adapter edits; do not use `--no-build`. The S11 test imports all four real CSVs and retains every historical candle through `13:33Z` in memory. It runs the actual registered evaluator/workflow/lifecycle/session binder pipeline twice independently. It truncates only the future tail for this focused first-binding check; no CSV is rewritten and no prior source history is replaced. It does not execute the full session or serialize a historical report.

Coverage checks raw values/provenance, exact native initial take/Start, derived availability and matching registration, absence through Start, first actual binding frame, active owner reference, deterministic repetition, future-review exclusion, foreign-run callback/selector rejection, non-Start rejection, no Standard facts, no manufactured reversal/eligibility/downstream trade artifacts and no Ready. Existing applicability unit tests additionally cover wrong session, extension ownership, invalid review order and conflicting classifications. Scenario 2 harness tests cover foreign-owner references and a dedicated raw-registration mismatch rejection. Synthetic negative cases are test-only, not additional historical evidence.

## Verified focused execution (2026-11-03)

The real opt-in S11 integration test passed. Both independent canonical prefix runs emitted the exact native initial take and active lifecycle Start at `2026-09-08T13:31:00Z`. Both derived applicability fields and record availability were `13:31:00Z`; the first actual `Bound` diagnostic was at the existing `13:32:00Z` frame. No applicability input was present through the Start frame. Raw review values remained `13:30:37Z` / `13:30:42Z` with S10/S11 provenance.

The runs retained all original historical candles through `13:33Z` and used the existing registered evaluator/workflow/lifecycle/session binder pipeline. This is successful real historical applicability binding in a bounded canonical replay, not a complete session run, full Scenario 2 trade replay or execution simulation. No historical report was generated. The missing human reversal kept NQ-WF2-001 at HumanValidationRequired on the first binding frame; no WF2-001/002, Standard M5/M1, SL/TP/risk facts, snapshot, fill, PnL or Ready were manufactured.

Validation completed for this feature:

| Gate | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Real S11 opt-in canonical adapter test (two independent prefix runs) | 1 | 0 | 0 |
| Real S9 opt-in source regressions | 2 | 0 | 0 |
| Scenario 2 harness integration tests, including raw-availability rejection | 15 | 0 | 0 |
| Applicability and mentor-session binder unit tests | 40 | 0 | 0 |

`dotnet build MoneyWay.sln --no-restore --verbosity minimal` passed with zero warnings/errors. Documentation link/anchor checks, original S1-S10 preservation, S11 decision-text hash, exclusive backup equality, 43 pre-existing source/manifest/backup/report artifact hashes and `git diff --check` passed. No runtime or strategy source changed. Unit tests used their existing compiled unchanged test code; both real-source opt-in commands rebuilt with the external adapter property and environment variable.

## Remaining independent blocker

S11 authorizes applicability only. The next logical action is to obtain source-backed human identification and effective/observed times of the subsequent bearish reversal for this same initiating setup. Do not infer its EventId or timing from pending order placement. NQ-WF2-001/002, SL, TP, risk, execution/fill, snapshot and PnL remain separate. No Standard/Forex rule, runtime contract, metadata, ACTIVE version or cutoff discrepancy is changed.
