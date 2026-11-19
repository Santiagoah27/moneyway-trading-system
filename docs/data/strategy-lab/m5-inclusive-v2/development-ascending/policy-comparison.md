# Experimental M5 exclusive V1 / inclusive V2 comparison

**MoneyWay research hypotheses, not mentor-confirmed rules or operational signals. No winner.**

Series m5-20260623-1300-ascending-v1 · oanda-global-markets-mt5-demo/US100 · RetrospectiveDocumentary · cutoff 2026-06-23T14:00:00.0000000+00:00.

Only the confirmation-candle aggregation differs. Strict-close threshold, start hypotheses, fixed anchor and cutoff are identical. Inclusion requires an already established pullback; no intrabar order is inferred. NativeInstrumentPrice only. Documentary availability remains unauthenticated.

| Algorithm / version | Policy | State | Start open UTC | Reference | All reference opens UTC | Confirmation open UTC | Known at close UTC |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EXP-M5-SWING-A-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29441.2 | 2026-06-23T13:25:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-B-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29441.2 | 2026-06-23T13:25:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-C-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29441.2 | 2026-06-23T13:25:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-D-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29441.2 | 2026-06-23T13:25:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-A-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29381.7 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-B-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29381.7 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-C-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29381.7 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |
| EXP-M5-SWING-D-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-06-23T13:05:00.0000000+00:00 | 29381.7 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 |

| Pair | Confirmed-comparable | Reference changed (price or identities) | Price changed | V2 minus V1 | Absolute difference |
| --- | --- | --- | --- | --- | --- |
| EXP-M5-SWING-A-V1 / EXP-M5-SWING-A-V2 | True | True | True | -59.5 | 59.5 |
| EXP-M5-SWING-B-V1 / EXP-M5-SWING-B-V2 | True | True | True | -59.5 | 59.5 |
| EXP-M5-SWING-C-V1 / EXP-M5-SWING-C-V2 | True | True | True | -59.5 | 59.5 |
| EXP-M5-SWING-D-V1 / EXP-M5-SWING-D-V2 | True | True | True | -59.5 | 59.5 |

- EXP-M5-SWING-A-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-B-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-C-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-D-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-A-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

- EXP-M5-SWING-B-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

- EXP-M5-SWING-C-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

- EXP-M5-SWING-D-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

## Original retrospective human reference

Revision 1 · IndependentRetrospectiveResearch · SelfDeclaredIndependent · Low 29381.7 · cutoff 2026-06-23T14:00:00.0000000+00:00. Source export SHA256: 8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861. Not an algorithm input or historical authorization.

| Algorithm | Exact reference agreement | Price agreement | Candle-set agreement | Algorithm minus human | Absolute difference |
| --- | --- | --- | --- | --- | --- |
| EXP-M5-SWING-A-V1 | False | False | False | 59.5 | 59.5 |
| EXP-M5-SWING-B-V1 | False | False | False | 59.5 | 59.5 |
| EXP-M5-SWING-C-V1 | False | False | False | 59.5 | 59.5 |
| EXP-M5-SWING-D-V1 | False | False | False | 59.5 | 59.5 |
| EXP-M5-SWING-A-V2 | True | True | True | 0.0 | 0.0 |
| EXP-M5-SWING-B-V2 | True | True | True | 0.0 | 0.0 |
| EXP-M5-SWING-C-V2 | True | True | True | 0.0 | 0.0 |
| EXP-M5-SWING-D-V2 | True | True | True | 0.0 | 0.0 |

Independent human confirmation and relative latency: unavailable. This is exposed Development evidence, not independent validation or profitability.

CanonicalAdmissionPermitted=false · OperationalSignal=false. No contextual structural relevance, strategy activation, orders, profitability or winner inferred.
