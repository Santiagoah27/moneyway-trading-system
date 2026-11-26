# Nasdaq 11:30 trading-window candidate

`nasdaq-0.4.0-window-1130-candidate` derives from
[`nasdaq-0.3.0-session-candidate`](explicit-session-candidate.md).
Its lifecycle status is `Candidate_NotActivated`. Select it explicitly with
`MoneyWayNasdaqWindow1130Candidate.CreateBacktest()` and its exact
`Definition`, using reviewed inputs bound to that version.

The Standard window reuses [EXP-NQ-TRADING-WINDOW-1130-V1](../../research/nasdaq-trading-window-1130-alignment-v1.md).
Preparation starts at 08:00 America/Bogota. The trading window is
[08:30, 11:30) (13:30–16:30 UTC). Both TIME rules emit the same window fact
for a frame. TIME-001 checks the opening and TIME-002 checks the exclusive
cutoff. The existing lifecycle expires incomplete setups at the first
evaluated frame at or after the applicable cutoff and retains earlier
completed setups.

Wickfill Scenario 2 retains [08:30, 11:00) under this candidate. The shorter
window applies when an unambiguous human review identifies WF2 for the exact
canonical setup. The common take can establish a setup before that review is
available. Standard and WF2 progression both require explicit branch evidence,
and WF2 cannot progress at or after 11:00. This preserves the audit history
when a classification becomes observable in a later frame.

The comparison uses synthetic, version-bound fixtures. Run it with
`dotnet test tests/unit/MoneyWay.Application.UnitTests --filter FullyQualifiedName~NasdaqWindow1130CandidateTests`.
The fixtures cover second-level boundaries before and after US DST changes,
late Standard prerequisites, setup expiry, earlier completion retention and
prefix invariance. Activation requires an independent backtest with
authentically admitted multi-timeframe evidence and explicit human approval.
