# Nasdaq trading window alignment to 11:30 V1

`EXP-NQ-TRADING-WINDOW-1130-V1` is an opt-in MoneyWay candidate configuration.
The owner's instruction on 2026-11-20 (task attachment
`38179fc2-310b-4893-ad33-8ddbf68f52b6`) confirms preparation at 08:00 and
the 08:30 and 11:30 limits for new setups and entries in America/Bogota.
The published Nasdaq specification still records an earlier review of
Video 3 that set 11:00 as the exclusive pre-entry cutoff. This comparison
preserves the existing inclusive start and exclusive end convention. It does
not reinterpret the original media.
[Audited Nasdaq specification](../strategies/nasdaq/strategy-specification.md) · [Rule catalog](../strategies/nasdaq/rule-catalog.md)

## Audit matrix

| Source | Effective behavior before this change | Effective behavior after this change |
| --- | --- | --- |
| Owner's clock instruction | 08:00 preparation, entry window 08:30–11:30 | Explicitly captured by the candidate configuration with the existing half-open convention |
| Published `nasdaq-0.1.0-draft` definition and default catalog | Rule text for liquidity take, M5 trigger, M1 pre-entry and TIME-002 uses 11:00. The version name says Draft, and the default catalog uses it for canonical evaluations. | Same definition, metadata and registry |
| `nasdaq-0.2.0-sc-candidate` and `nasdaq-0.3.0-session-candidate` | Opt-in versions inherit 11:00. ADR 0028 changes Asia/London for 0.3 only. | Same candidate versions and cutoff |
| `MoneyWayNasdaqTradingWindowStartEvaluator`, `MoneyWayNasdaqTradingWindowEndEvaluator`, `NasdaqTradingWindowFact` | TIME-001 uses the replay `AsOfUtc` for the 08:30 start, TIME-002 fails at or after 11:00. Both emit the same [08:30, 11:00) fact. Time alone cannot establish preparation. | Same canonical evaluators and fact |
| Canonical liquidity/M5/FVG selectors, replay lifecycle and WF2 | Selectors use the canonical time fact to check effective evidence time and/or cutoff. Failed TIME-002 prevents starting a new setup and expires incomplete setup instances. Completed progression and recorded positions have separate handling. WF2 evidence remains governed by existing branch gates. | Same rules, progression and management |
| `NasdaqSupervisedSessionPolicy` | Setup/intent guard reuses [08:30, 11:00) for the current Bogotá date. Closing the window does not close a recorded trade. Terminal state requires explicit closure evidence and prevents reentry. | Same guard and lifecycle, including Standard and WF2 |
| Prospective preparation, historical evidence packages and canonical diagnostic projections | Expose the 11:30 versus 11:00 mismatch as documentary reason strings. Protected reports retain their original version and disclosure. | Same persisted and API behavior. The new Worker comparison shows both windows by timestamp. |
| Frontend Standard diagnostic, supervised session, prospective preparation and contextual review/viewer | Displays the discrepancy or time landmarks. The supervised guard comes from the backend. Chart stepping and documentary labels do not grant admission. | Same displays and behavior |
| M5/M1 research composition and Worker | M5 and M1 compare with 11:30 exclusive. Contextual liquidity's preparation/open boundaries and the protected/frozen reports retain their original experiment semantics. | The Worker sources 08:30 and 11:30 from the named candidate configuration. Existing experiment identities, bounds and reports remain reproducible. |

The discrepancy comes from the earlier audited specification and matching
hardcoded canonical time owners. It is not a DST offset, UI bug, configurable
runtime override or a rule about when to close an existing position. The
current human instruction changes the proposed clock definition. Published
canonical and prior candidate versions retain their historical semantics.
[ADR 0028](../decisions/0028-define-candidate-nasdaq-session-windows.md) · [Supervised session termination](../backtesting/nasdaq-supervised-session-termination.md)

## Candidate behavior

The pure comparison uses the event's Bogotá operating date and the supplied
UTC `AsOfUtc` to check observability. Preparation runs `[08:00, 08:30)`
(13:00–13:30 UTC). The published runtime time gate is `[08:30, 11:00)`
(13:30–16:00 UTC). The candidate is `[08:30, 11:30)` (13:30–16:30 UTC).
Only `[11:00, 11:30)` changes in the diagnostic time gate. At exactly 11:30
the candidate reports the exclusive boundary. Bogotá uses UTC−05:00 on the
tested US DST transition dates. Missing, invalid and future evidence does
not yield a classification. Historic events use their own timestamp even
when evaluated at a later cutoff.

The time gate describes clock eligibility for a new setup or entry only.
Canonical admission, operational signal, management, forced closure and
session termination flags on the comparison are false. No candidate verdict
is authored or registered as a strategy rule. The comparison reads the
existing published time fact for legacy reproduction.

```powershell
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-window-research --as-of-utc 2026-06-23T16:15:00Z --event-utc 2026-06-23T16:15:00Z
```

At 11:15 Bogotá this reports `AfterTradingWindow` for the published runtime
and `InsideTradingWindow` for the candidate. Use the
[M5 contextual sequence](nasdaq-m5-contextual-sequence-v1.md) and
[M1 realignment](nasdaq-m1-realignment-candidate-v1.md) commands to reproduce
the exposed Development study. Closed positions and terminal sessions are
not revived by this clock comparison.

## Path to operational use

The versioned configuration is explicitly opt-in for research. Runtime
alignment needs a separately versioned strategy composition, independent
boundary and replay validation on admissible evidence, and human approval
before activation. These requirements preserve published and prior candidate
reproduction. Current supervised and canonical behavior still uses 11:00.
Running this comparison requires no operator labeling or setup actions.
[ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md)
