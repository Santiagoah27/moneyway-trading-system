# Replay #2 bounded canonical Scenario 2 SELL pre-entry diagnostic

## Scope and canonical origin

This is a real-source diagnostic for September 8, 2026, not a trade simulation. The unchanged external adapter `C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs` has SHA256 `8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2` under the [S13 integration checkpoint](nasdaq-replay2-s13-reversal-adapter.md). Native strategy/runtime baseline is Git `6ae551f5e2fed7e112732db8c13191b8a517167b`; only tracked test/export documentation changes in this feature.

S9 complete SELL/Wickfill context, S11 applicability and S13 recognition remain governed by [ADR 0026](../decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md), especially sections 2.8/2.10/2.12, and [ADR 0025 causal availability](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md#identity-and-causal-availability). S13 recognition is effective/observed at 13:33:10Z; it does not date the original turning tick, convert USTEC visual prices into US100 prices, or authorize the earlier historical 08:32 entry. Screenshot bytes remain external / not locally verified.

The shared real-prefix test helper now invokes `GenerateCanonicalMultiTimeframeBacktestUseCase.ExecuteMentorSession`, using the same registered Application strategy-run coordinator and exact native input/owner objects as before. Its outcome-run stage invokes the existing safe outcome use case, and its diagnostics stage produces `MultiTimeframeStrategyBacktestDiagnosticsReport` / `MultiTimeframeStrategyFrameDiagnostic` objects. No evaluator, outcome, status, coverage or blocking rule is recomputed by the exporter.

Both independent runs import the original 1M/5M/1H/4H CSVs and retain every historical member up to their endpoints. The diagnostic selects only native frames 13:31Z-13:36Z for export, after evaluating the complete prerequisite prefix; it does not trim the source prefix to accelerate replay or fill any candle gaps. The comparison run extends to 13:37Z. Exact evaluation/evidence fingerprints and native verdict/coverage/blocking diagnostic fingerprints over the entire common prefix must match.

## Artifact format and reproduction

The artifact is a test-local JSON window envelope, schema `moneyway.canonical-preentry-window`, version `1`, serialized with the existing .NET JSON serializer and reference preservation. It embeds the actual completed native canonical frame diagnostics and strategy observations, their original inputs/provenance, workflow/lifecycle and absent trade artifacts. It also preserves the native strategy definition and capability report. This is not a hand-authored outcome summary, the full mentor-report schema v2, or a new production API/report infrastructure. Engine enums retain the serializer's native numeric representation; definitions, capability, runtime results and verdict remain separate fields.

The envelope records runtime base commit, generator source path, exact adapter hash, all four CSV identities/paths/hashes, evaluated prefix boundaries/count, selected diagnostic window and comparison boundary. The actual output path/hash and findings are recorded after successful execution below. The external artifact is not committed to Git.

```powershell
$env:MONEYWAY_MENTOR_INPUT_SOURCE = 'C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs'
try {
    dotnet test tests/integration/MoneyWay.IntegrationTests/MoneyWay.IntegrationTests.csproj --no-restore "-p:NasdaqMentorSessionInputSource=$env:MONEYWAY_MENTOR_INPUT_SOURCE" --filter FullyQualifiedName~LocalNasdaqMentorSessionTests.ExportReplay2CanonicalPreEntryDiagnostic --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:MONEYWAY_MENTOR_INPUT_SOURCE
}
```

The existing [opt-in mechanism](nasdaq-mentor-session-local-run.md#concrete-local-workflow) compiles the actual adapter. Do not count a skipped test as historical execution. The test creates a unique `canonical-preentry-{Guid}.json` in the September reports directory with `FileMode.CreateNew` only after validation, reads the JSON back and prints path, SHA256, byte size and native rule/frame states. A repeat creates a new destination; never overwrite prior reports, especially `report-optional-FVG.json`. All original raw/normalized/evidence/report hashes and the adapter remain protected.

## Verified external artifact and native findings (2026-11-04)

| Item | Verified value |
|---|---|
| Report path | `C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0d69f182a0b456ba4662896192dc242.json` |
| SHA256 | `41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2` |
| Bytes | `4493832` |
| Schema / version | `moneyway.canonical-preentry-window` / `1` (test-local envelope of native canonical objects) |
| Evaluated prefix | `2026-08-31T22:01:00Z` through `2026-09-08T13:36:00Z`, 7,596 frames |
| Independent comparison prefix | Same start through `2026-09-08T13:37:00Z`, 7,597 frames; entire common history identical |
| Exported diagnostic window | `2026-09-08T13:31:00Z` through `13:36:00Z`, six actual native frames |
| Strategy / version | `moneyway-nasdaq` / `nasdaq-0.1.0-draft` |
| Session / native instrument / provider | `2026-09-08` / `US100` / `oanda-global-markets-mt5-demo` |
| Start owner / initiating take | step `7591`, local instance `1`, AsOf `13:31Z`; `native-M1-close-2026-09-08T13:31Z` |

Owner identity was verified by exact object references, not only timestamp/ordinal. S9 supplies the reviewed SELL/Wickfill context (effective 13:00Z, observed 13:27:42Z); S11 supplies this exact setup's Scenario 2 applicability (first binding 13:32Z); S13 retains documentary recognition EventId/source as recorded in the S13 adapter checkpoint, effective/observed 13:33:10Z. No S13 observation was visible at 13:33Z. Actual input paths/hashes, provenance and definitions/capabilities are embedded in the artifact.

| Frame UTC | Canonical verdict | Native blocker / result | WF2-001 | WF2-002 |
|---|---|---|---|---|
| 13:31 | Wait | NQ-WF2-001 / Waiting | Waiting | Waiting |
| 13:32 | HumanValidationRequired | NQ-WF2-001 / HumanValidationRequired | HumanValidationRequired | Waiting |
| 13:33 | HumanValidationRequired | NQ-WF2-001 / HumanValidationRequired | HumanValidationRequired | Waiting |
| 13:34 | Wait | NQ-WF2-002 / Waiting | Passed | Waiting |
| 13:35 | Wait | NQ-SL-001 / Waiting | Passed | Passed |
| 13:36 | Wait | NQ-SL-001 / Waiting | Passed | Passed |

All six frames have complete Required coverage and no missing Required evaluation IDs. The capability catalog has 34 rules, 18 Implemented evaluators, 13 Required and all 13 Required Implemented. This means evaluation coverage, not strategy readiness. Scoped lifecycle completion likewise creates no trade.

Complete Required-rule matrix at 13:36Z (same runtime results at 13:35Z):

| RuleId | Definition status | Runtime result |
|---|---|---|
| NQ-TIME-003 | Confirmed | Passed |
| NQ-H4-001 | Confirmed | Passed |
| NQ-LIQ-001 | Confirmed | Passed |
| NQ-TIME-001 | Confirmed | Passed |
| NQ-LIQ-003 | Confirmed | Passed |
| NQ-WF2-001 | HumanValidationRequired | Passed |
| NQ-WF2-002 | Confirmed | Passed |
| NQ-M5-001 | Confirmed | NotApplicable |
| NQ-M1-001 | HumanValidationRequired | NotApplicable |
| NQ-M1-002 | HumanValidationRequired | NotApplicable |
| NQ-SL-001 | HumanValidationRequired | Waiting |
| NQ-TIME-002 | Confirmed | Passed |
| NQ-RISK-001 | Confirmed | Waiting |

At the final frame the other emitted evaluations are optional: NQ-LIQ-002 HumanValidationRequired; NQ-FVG-001/002 and NQ-TP-001 Waiting; NQ-M1-003 NotApplicable (definition Unresolved). Across the 18 emitted evaluations: 8 Passed, 5 Waiting, 4 NotApplicable, 1 HumanValidationRequired, 0 Failed and 0 DataUnavailable. Optional unevaluated definitions are preserved in metadata/capability, not assigned invented runtime results. Continuation FVG remains optional.

The exact native SL reason is `The exact canonical prerequisite has not established eligible progression.` Its workflow missing-prerequisite list contains NQ-M1-003. The first blocking Required rule after WF2 is SL-001, not optional FVG/liquidity/TP. Risk also waits downstream. The native safe outcome is Wait, not Ready, with no trade artifacts, SL/TP/risk facts, order/fill or PnL. No historical report or CSV was overwritten.

## Why NQ-SL-001 cannot yet progress for this path

The current [SL evaluator](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayEvaluators/MoneyWayNasdaqHumanStructuralStopLossEvaluator.cs) is registered, Required and capability Implemented; its definition status is HumanValidationRequired. These categories are independent. Its first causal contract requires an established prior `NQ-M1-003` Passed fact of type `NasdaqPreEntryEligibilityRuleFact`. The [workflow](../../src/backend/MoneyWay.Application/StrategyReplay/Workflow/MoneyWayReplayWorkflowDefinitions.cs) also requires NQ-M1-003 before SL. Scenario 2 intentionally makes Standard M1 NotApplicable and supplies a distinct `NasdaqWickfillScenario2PreEntryEligibilityRuleFact` under NQ-WF2-002. That fact is not accepted as Standard ancestry.

The existing [SL observation](../../src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqHumanStructuralStopLossObservation.cs) likewise requires the Standard eligibility object, a closed 5M HL/LH protection anchor and a separately documented StopPrice. It cannot be populated faithfully by relabeling an M30 historical wick or by manufacturing Standard M1 Passed. Thus the relevant path/causal integration gap is separate from missing evaluator capability or market-data unavailability. The source-specific historical StopPrice/reference observation is also absent, but the evaluator does not reach that selector while its predecessor is missing.

S5 preserves the historical M30 wick/high selection and later 1:1 management; it is not a universal M30 selector. S6 confirms protection above this setup's initiating sweep peak, and S7 defines its owned measurement interval. Exact offset, historical StopPrice, causal/boundary handling and branch-specific typed convergence remain independent. No native algorithm currently authorizes an automatic M30 wick choice or an arbitrary Standard M5 LH substitution. This feature implements none of those rules.

Risk and TP currently require the prior canonical SL fact. Risk is Required; TP-001 is optional. Their upstream Waiting must not be recast as an unimplemented risk evaluator, an invented target, or a need to guess sizing. The historical snapshot assembler also remains Standard-eligibility-dependent: its native `CanonicalEligibilityUnavailable` label does not deny the independently present WF2 scoped eligibility. No snapshot/fill/entry/PnL is created here.

## Validation performed

| Gate | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Real opt-in canonical export (two independent full-prefix runs) | 1 | 0 | 0 |
| Real S9 opt-in regressions | 2 | 0 | 0 |
| Scenario 2 harness regression cases | 15 | 0 | 0 |
| Focused canonical/diagnostics/gating/SL/WF2 unit cases | 138 | 0 | 0 |

The new test verifies native outcome/observation identity, the exact complete Required matrix, no missing coverage, actual WF2 frame transitions, S9/S11/S13 provenance and exact Start reference, future-evidence exclusion, full common-prefix evaluation/diagnostic equality, no Standard facts or trade artifacts, SL's missing Standard prerequisite and Implemented capability. Existing harness guards cover foreign owners, conflicts, unavailable/future evidence and raw/derived availability mismatch rather than duplicating them in the exporter.

`dotnet build MoneyWay.sln --no-restore --verbosity minimal` passed with zero warnings/errors. Documentation links and `git diff --check` passed.

The exported JSON was read back, its six frame count/schema validated, its SHA256 independently recomputed, and all six WF2 transition pairs checked against the persisted objects. A separate integrity snapshot verified 45 pre-existing raw/normalized/evidence/adapter/backup/report hashes unchanged. The full `GenerateCanonicalMultiTimeframeBacktestUseCase` path was used; no isolated synthetic evaluator call substitutes for the real diagnostic. No runtime, external adapter, native CSV, metadata or strategy evidence changed.

## Next smallest feature

Audit and specify the minimum human-assisted Scenario 2 SELL SL evidence contract/convergence from exact WF2-002 ancestry, using existing S5-S7 source authority and explicitly exposing any missing historical StopPrice/reference. Do not implement a general M30 selector, stop offset or sizing formula. This addresses the first Required dependency gap before attempting downstream risk/TP or a historical trade snapshot.
