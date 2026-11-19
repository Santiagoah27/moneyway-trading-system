# Experimental M5 exclusive V1 / inclusive V2 comparison

**MoneyWay research hypotheses, not mentor-confirmed rules or operational signals. No winner.**

Series synthetic-m5-swing-disagreement-v1 · moneyway-synthetic-lab/US100 · Synthetic · cutoff 2026-11-19T13:30:00.0000000+00:00.

Only the confirmation-candle aggregation differs. Strict-close threshold, start hypotheses, fixed anchor and cutoff are identical. Inclusion requires an already established pullback; no intrabar order is inferred. NativeInstrumentPrice only. Documentary availability remains unauthenticated.

| Algorithm / version | Policy | State | Start open UTC | Reference | All reference opens UTC | Confirmation open UTC | Known at close UTC |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EXP-M5-SWING-A-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-11-19T13:05:00.0000000+00:00 | 90 | 2026-11-19T13:10:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-B-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-11-19T13:10:00.0000000+00:00 | 90 | 2026-11-19T13:10:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-C-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-11-19T13:20:00.0000000+00:00 | 93 | 2026-11-19T13:20:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-D-V1 / 1.0.0 | EXP-M5-BOUNDED-CONTINUATION-V1 | ExperimentalReferenceConfirmed | 2026-11-19T13:05:00.0000000+00:00 | 90 | 2026-11-19T13:10:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-A-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-11-19T13:05:00.0000000+00:00 | 80 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-B-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-11-19T13:10:00.0000000+00:00 | 80 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-C-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-11-19T13:20:00.0000000+00:00 | 80 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |
| EXP-M5-SWING-D-V2 / 2.0.0 | EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2 | ExperimentalReferenceConfirmed | 2026-11-19T13:05:00.0000000+00:00 | 80 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:25:00.0000000+00:00 | 2026-11-19T13:30:00.0000000+00:00 |

| Pair | Confirmed-comparable | Reference changed (price or identities) | Price changed | V2 minus V1 | Absolute difference |
| --- | --- | --- | --- | --- | --- |
| EXP-M5-SWING-A-V1 / EXP-M5-SWING-A-V2 | True | True | True | -10 | 10 |
| EXP-M5-SWING-B-V1 / EXP-M5-SWING-B-V2 | True | True | True | -10 | 10 |
| EXP-M5-SWING-C-V1 / EXP-M5-SWING-C-V2 | True | True | True | -13 | 13 |
| EXP-M5-SWING-D-V1 / EXP-M5-SWING-D-V2 | True | True | True | -10 | 10 |

- EXP-M5-SWING-A-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-B-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-C-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-D-V1: A subsequent strict-close continuation confirms this experimental reference; its candle is excluded from aggregation.

- EXP-M5-SWING-A-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

- EXP-M5-SWING-B-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

- EXP-M5-SWING-C-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

- EXP-M5-SWING-D-V2: A subsequent strict-close continuation confirms this experimental reference; include its opposite extreme at closure only, without inferring intrabar order.

No human reference supplied. Synthetic data is a software fixture, excluded from authentic Development agreement statistics.

CanonicalAdmissionPermitted=false · OperationalSignal=false. No contextual structural relevance, strategy activation, orders, profitability or winner inferred.
