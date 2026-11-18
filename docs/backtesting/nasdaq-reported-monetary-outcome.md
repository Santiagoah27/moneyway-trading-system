# Supervised reported monetary outcome

Implementation date: 2026-11-18. Initial clean synchronized `main`:
`f952a0db08a4d607d9324a1f859c0c232c6fc092`.

## Capture and exact meaning

The [existing capture workflow](nasdaq-supervised-execution-capture.md) now allows
the operator to load a saved record and add a reported monetary outcome as a
new revision. Existing partial entry/exit records need no outcome or amount.
No new endpoint, persistence service, database, trading rule or broker adapter
is introduced. Development/localhost restrictions and source-reference validation
remain unchanged.

`NasdaqReportedMonetaryOutcome` references the exact MoneyWay execution record,
evidence revision and original revision hash. Store validation requires that
anchor to exist in this same record and precede the outcome-containing revision.
New-record creation cannot guess a monetary anchor. Subsequent corrections can
retain the earlier execution-evidence reference. Conflicts are checked against
both current evidence and the original referenced revision, including when a
later correction changed instrument or IDs. No USTEC/US100 mapping is inferred.

SourceReference, ReportedBy and Provenance (HumanReported / DocumentReported)
identify the assertion. DocumentReported does not mean the original document
was inspected. Source instrument, original meaning/narrative/event description,
literal reported time/clock/precision and source deal/position references remain
optional. Association is RecordOnly, HumanReported or DocumentarySupported with
its explicit source. Record linkage does not verify an underlying execution.

Amount and Currency can remain null. Supplied amounts are invariant decimal
strings using the existing exact decimal representability check; explicit `+`,
negative signs, zero and original decimal scale are retained. No binary float,
rounding, tolerance, monetary trading threshold or currency conversion is used.
Exponents, locale separators, unrepresentable or silently rounded values fail
validation. Currency uses the existing three-uppercase-letter source format;
it is never copied from an instrument or account assumption.

AmountMeaning is Unspecified, SignedResult, ProfitMagnitude or LossMagnitude:
these are source-declared interpretations, not MoneyWay economic calculations.
An unsigned number without a declared meaning stays unresolved. A negative
literal declared as a magnitude is retained with an interpretation conflict,
not silently sign-flipped. Basis is Unspecified, Gross or Net as reported by the
source. No audited general realized-valuation formula exists here: neither a
net label nor a supplied fee verifies gross/net accounting or strategy compliance.

Costs are optional source components with literal category, amount, currency,
original sign meaning and source reference. Absent/null/empty costs remain
unknown, never zero. A supplied source zero is preserved. Missing cost amounts,
currency or sign meaning remain unresolved. Different currencies are retained
separately; unknown compatibility is distinct from an explicit mismatch. Costs
are not summed, subtracted, converted or used to derive net PnL. No quantity,
price-distance valuation, fees, commission, percentage or R-multiple is inferred.
Narrative such as profit/loss/protected stop remains narrative only.

## Trust, revisions and historical boundaries

The application view exposes ReportedOutcome independently of EconomicOutcome
and MoneyWayCalculatedPnl (both remain null). BrokerRealizedOutcomeVerified and
CompleteSupervisedTrade remain false; StrategyClassification remains null.
SessionClosureEstablished remains false and reentry is not evaluated. A profit
or loss report does not authorize entry, supply SL/TP/risk, classify a loss/error,
close a session or permit/prohibit another attempt.

The durable store and revision endpoint are reused. Amount changes append a new
revision; retries use the existing submission identity and expected revision
hash. Concurrent corrections conflict. Existing records deserialize without
ReportedOutcome. Their SHA256 checks use original stored payload bytes rather
than a reserialization that would add a new null field. Original revisions and
the previous synthetic capture files are not rewritten. No hashes of uninspected
original artifacts or caller-controlled trust/availability are accepted.

Reported outcomes are consumed only by the separate supervised application view.
The selectors, canonical snapshot assembler and StrategyReplayContext receive
no outcome input. Saving/revising a current assertion cannot change an old
entry frame, verdict, SL, risk or TP. Authentic Replay #2 has no monetary amount
prepopulated: 1:1 protection is not realized +1R or a monetary outcome.

## Functional acceptance and actual process restart

The real Development API on localhost:5097 used the isolated synthetic directory
`C:\Users\Santi\AppData\Local\Temp\moneyway-monetary-smoke-20261118-f1bbf822`.
It created partial record `0127bcd4-3ff8-4991-add1-c8418459cc1f`, then saved a
clearly synthetic source-reported `+125.0000 USD`, Net/SignedResult, and independent
reported `2.50 EUR` cost as revision 2. No currency conversion or net calculation
occurred; the currency conflict remains visible. Repeating the same submission
returned the same revision. After stopping and restarting the API, both revisions,
original entry data and hashes were retrieved unchanged:

- Revision 1: `0518f5633d16bf0d07f407fd5b9a3b1fe3a59dd878794a6620b244800b73afba`.
- Revision 2: `f7632423db439cbb861578b1ef8d2aabd84220dd5b4540a45b0ed4cedc92e8e8`.

The frontend component tests exercise enabling the outcome on a saved record,
exact amount/currency/cost text, partial narrative-only saving, revision request
binding, saved rendering and retrieval. A backend success is not presented as
browser visual acceptance. Browser control was attempted but failed to initialize
with missing `@oai/cua/tinyskyAlt`; the Windows fallback lacked `@oai/sky`.
No browser smoke pass is claimed. Manual acceptance:

1. Open `/#nasdaq-historical-demo`, retrieve an existing synthetic record and
   inspect its original entry/exit values.
2. Enable Registrar resultado monetario reportado. Supply a `note:` source,
   reporter and correction reason. Save first with narrative only and absent
   amount/currency/costs; verify unknown values are not displayed as zero.
3. Add an explicitly synthetic amount/currency and source-declared meaning and
   gross/net label; optionally add separately sourced costs. Save a new revision.
4. Restart the API with the same storage directory and retrieve by ID. Check
   the monetary panel, exact sign/scale and verification/calculation limitations.
5. Retrieve the original revision to confirm no amount was added there. Verify
   keyboard/narrow-screen behavior and conflict handling after a stale revision.

## Validation

Passed: 2,086 relevant Application tests; 208 Nasdaq API/integration tests
(36 nonlocal monetary cases included); 28 related backtesting cases; 19 domain
replay tests; two opt-in authentic-preservation tests. Total: 2,343 distinct
relevant .NET cases, zero failed/skipped in the final validation runs. The latest
focused execution/monetary run passed 65 cases, including authentic preservation;
it overlaps the totals above. All 46 frontend component tests, backend/frontend
builds, ESLint, full frontend Prettier, scoped C# formatting, 42 local documentation
links and git diff whitespace checks passed. Backend build: zero warnings/errors.

The authentic report retains SHA256
`48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db`.
Its external adapter, four normalized CSVs and original source ADR hashes were
independently checked. The earlier synthetic record and both revisions were
read with their original hashes and file bytes intact. No MT5 source/checkpoint,
collector, strategy definition/version, canonical engine or admission component
was modified. The tests compare the original selected replay frame before/after
an isolated synthetic outcome and retain the original final SL/risk/TP/verdict.
No monetary amount is registered as an authentic Replay #2 result.

## Remaining Demo v0 path

Execution, exit and monetary reports can now be captured, revised and inspected
without pretending to be independently verified trades. Calculated economics
still need approved valuation, quantity, costs and execution evidence. Replay #2
still lacks an original closing artifact, native correspondence/linkage and
supported native SL/exposure/TP and monetary outcome. Current-data admission
remains subject to existing temporal restrictions; no clock research is reopened.

**One next feature:** supervised session-closure evidence and UI linked to the
session's saved trade revisions, preserving open/missing trade evidence and
separating operator-declared closure from strategy authorization. Do not enforce
unapproved no-reentry or trade-count rules. NQ-REENTRY-001's exact unresolved
question is whether “if it takes you out” concerns SL/BE, every exit, and the same
setup versus a new setup; NQ-RISK-003's attempt limits also remain undefined.
The full-session workflow and approved no-reentry enforcement are not delivered
by registering a monetary amount. Nasdaq Demo v0 remains incomplete.
