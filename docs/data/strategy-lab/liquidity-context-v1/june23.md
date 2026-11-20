# Nasdaq contextual liquidity tracking V1

EXP-NQ-LIQ-CONTEXT-V1 / 1.0.0. June 23 exposed Development only; no relevance ranking, direction, swing anchor, valid take or order inferred.

Research interval [2026-06-23T07:00:00.0000000+00:00, 2026-06-23T16:30:00.0000000+00:00], not an operational end decision. Preparation 2026-06-23T13:00:00.0000000+00:00; observation start 2026-06-23T13:30:00.0000000+00:00. Availability is documentary, not authenticated.

London historical H1 context: AsiaLowOnlyPenetrated; upper 0, lower 242.5. Independent from M5 reference tracking.

| Reference | Native price | Available UTC | M5 coverage | First strict penetration interval UTC | Strict observations before open | At/after open |
| --- | --- | --- | --- | --- | --- | --- |
| AH | 30463.9 | 2026-06-23T07:00:00.0000000+00:00 | CompleteThroughLastClosedInterval | not observed | 0 | 0 |
| AL | 29715.2 | 2026-06-23T07:00:00.0000000+00:00 | CompleteThroughLastClosedInterval | 2026-06-23T07:00:00.0000000+00:00 / 2026-06-23T07:05:00.0000000+00:00 | 70 | 35 |
| LH | 29818.6 | 2026-06-23T13:00:00.0000000+00:00 | CompleteThroughLastClosedInterval | not observed | 0 | 0 |
| LL | 29472.7 | 2026-06-23T13:00:00.0000000+00:00 | CompleteThroughLastClosedInterval | 2026-06-23T13:05:00.0000000+00:00 / 2026-06-23T13:10:00.0000000+00:00 | 3 | 4 |

Session endpoints retain separate stable identities and all source ties, including price coincidences. AH/AL are unavailable before Asia end; LH/LL before London end. Candles opening before reference availability are not retrospectively used. Formation interval and evidence availability phase are distinct. The candle ending exactly 08:30 is available at the boundary, not before it.

## London Low complete closed-M5 sequence

| M5 open UTC | Close UTC | O / H / L / C | Interval phase / availability phase | Equality | Strict | Open beyond | Close beyond | Prior strict count | Prior pre-open strict | Later-other-side-close support |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-06-23T13:00:00.0000000+00:00 | 2026-06-23T13:05:00.0000000+00:00 | 29499.4 / 29510.2 / 29473.2 / 29483.6 | Preparation / Preparation | False | False | False | False | 0 | False | False |
| 2026-06-23T13:05:00.0000000+00:00 | 2026-06-23T13:10:00.0000000+00:00 | 29483.9 / 29494.4 / 29467.1 / 29477.4 | Preparation / Preparation | False | True | False | False | 0 | False | False |
| 2026-06-23T13:10:00.0000000+00:00 | 2026-06-23T13:15:00.0000000+00:00 | 29477.8 / 29510.0 / 29457.4 / 29499.7 | Preparation / Preparation | False | True | False | False | 1 | True | False |
| 2026-06-23T13:15:00.0000000+00:00 | 2026-06-23T13:20:00.0000000+00:00 | 29500.1 / 29501.3 / 29463.2 / 29495.7 | Preparation / Preparation | False | True | False | False | 2 | True | True |
| 2026-06-23T13:20:00.0000000+00:00 | 2026-06-23T13:25:00.0000000+00:00 | 29495.6 / 29519.6 / 29490.9 / 29509.9 | Preparation / Preparation | False | False | False | False | 3 | True | True |
| 2026-06-23T13:25:00.0000000+00:00 | 2026-06-23T13:30:00.0000000+00:00 | 29503.9 / 29503.9 / 29441.2 / 29484.2 | Preparation / Observation | False | True | False | False | 3 | True | True |
| 2026-06-23T13:30:00.0000000+00:00 | 2026-06-23T13:35:00.0000000+00:00 | 29484.1 / 29536.9 / 29381.7 / 29532.7 | Observation / Observation | False | True | False | False | 4 | True | True |
| 2026-06-23T13:35:00.0000000+00:00 | 2026-06-23T13:40:00.0000000+00:00 | 29531.9 / 29561.5 / 29436.2 / 29544.7 | Observation / Observation | False | True | False | False | 5 | True | True |
| 2026-06-23T13:40:00.0000000+00:00 | 2026-06-23T13:45:00.0000000+00:00 | 29544.4 / 29570.3 / 29503.8 / 29520.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T13:45:00.0000000+00:00 | 2026-06-23T13:50:00.0000000+00:00 | 29519.1 / 29621.6 / 29490.2 / 29582.6 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T13:50:00.0000000+00:00 | 2026-06-23T13:55:00.0000000+00:00 | 29583.2 / 29680.0 / 29582.1 / 29642.2 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T13:55:00.0000000+00:00 | 2026-06-23T14:00:00.0000000+00:00 | 29641.2 / 29698.6 / 29609.5 / 29691.6 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:00:00.0000000+00:00 | 2026-06-23T14:05:00.0000000+00:00 | 29691.4 / 29768.1 / 29686.7 / 29750.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:05:00.0000000+00:00 | 2026-06-23T14:10:00.0000000+00:00 | 29750.2 / 29762.1 / 29699.3 / 29740.4 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:10:00.0000000+00:00 | 2026-06-23T14:15:00.0000000+00:00 | 29740.4 / 29779.4 / 29714.2 / 29767.0 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:15:00.0000000+00:00 | 2026-06-23T14:20:00.0000000+00:00 | 29767.1 / 29787.4 / 29742.9 / 29777.7 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:20:00.0000000+00:00 | 2026-06-23T14:25:00.0000000+00:00 | 29777.2 / 29811.6 / 29742.2 / 29756.7 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:25:00.0000000+00:00 | 2026-06-23T14:30:00.0000000+00:00 | 29756.1 / 29766.9 / 29705.7 / 29747.9 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:30:00.0000000+00:00 | 2026-06-23T14:35:00.0000000+00:00 | 29748.5 / 29762.4 / 29668.2 / 29703.4 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:35:00.0000000+00:00 | 2026-06-23T14:40:00.0000000+00:00 | 29703.7 / 29762.4 / 29659.2 / 29665.7 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:40:00.0000000+00:00 | 2026-06-23T14:45:00.0000000+00:00 | 29665.9 / 29709.8 / 29637.6 / 29649.4 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:45:00.0000000+00:00 | 2026-06-23T14:50:00.0000000+00:00 | 29649.9 / 29664.8 / 29597.5 / 29634.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:50:00.0000000+00:00 | 2026-06-23T14:55:00.0000000+00:00 | 29634.6 / 29693.4 / 29593.9 / 29616.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T14:55:00.0000000+00:00 | 2026-06-23T15:00:00.0000000+00:00 | 29615.1 / 29629.9 / 29578.2 / 29618.9 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:00:00.0000000+00:00 | 2026-06-23T15:05:00.0000000+00:00 | 29618.7 / 29650.4 / 29581.4 / 29593.4 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:05:00.0000000+00:00 | 2026-06-23T15:10:00.0000000+00:00 | 29593.7 / 29604.6 / 29530.5 / 29561.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:10:00.0000000+00:00 | 2026-06-23T15:15:00.0000000+00:00 | 29561.2 / 29577.5 / 29538.6 / 29556.4 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:15:00.0000000+00:00 | 2026-06-23T15:20:00.0000000+00:00 | 29556.5 / 29567.9 / 29510.2 / 29511.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:20:00.0000000+00:00 | 2026-06-23T15:25:00.0000000+00:00 | 29511.2 / 29541.2 / 29487.4 / 29494.1 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:25:00.0000000+00:00 | 2026-06-23T15:30:00.0000000+00:00 | 29494.2 / 29515.5 / 29475.7 / 29491.7 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:30:00.0000000+00:00 | 2026-06-23T15:35:00.0000000+00:00 | 29491.6 / 29542.9 / 29479.4 / 29533.8 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:35:00.0000000+00:00 | 2026-06-23T15:40:00.0000000+00:00 | 29533.4 / 29562.9 / 29474.3 / 29488.5 | Observation / Observation | False | False | False | False | 6 | True | True |
| 2026-06-23T15:40:00.0000000+00:00 | 2026-06-23T15:45:00.0000000+00:00 | 29488.4 / 29520.4 / 29470.8 / 29508.9 | Observation / Observation | False | True | False | False | 6 | True | True |
| 2026-06-23T15:45:00.0000000+00:00 | 2026-06-23T15:50:00.0000000+00:00 | 29509.2 / 29533.9 / 29496.9 / 29500.2 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T15:50:00.0000000+00:00 | 2026-06-23T15:55:00.0000000+00:00 | 29500.0 / 29596.2 / 29495.3 / 29573.4 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T15:55:00.0000000+00:00 | 2026-06-23T16:00:00.0000000+00:00 | 29573.8 / 29588.7 / 29535.8 / 29573.8 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T16:00:00.0000000+00:00 | 2026-06-23T16:05:00.0000000+00:00 | 29572.8 / 29635.5 / 29565.4 / 29632.6 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T16:05:00.0000000+00:00 | 2026-06-23T16:10:00.0000000+00:00 | 29632.8 / 29664.8 / 29613.6 / 29647.3 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T16:10:00.0000000+00:00 | 2026-06-23T16:15:00.0000000+00:00 | 29647.4 / 29689.8 / 29647.0 / 29679.4 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T16:15:00.0000000+00:00 | 2026-06-23T16:20:00.0000000+00:00 | 29679.9 / 29690.4 / 29633.0 / 29639.5 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T16:20:00.0000000+00:00 | 2026-06-23T16:25:00.0000000+00:00 | 29639.8 / 29679.0 / 29609.5 / 29667.8 | Observation / Observation | False | False | False | False | 7 | True | True |
| 2026-06-23T16:25:00.0000000+00:00 | 2026-06-23T16:30:00.0000000+00:00 | 29667.3 / 29672.2 / 29634.0 / 29652.0 | Observation / Observation | False | False | False | False | 7 | True | True |

All references/rows, exact OHLC, IDs, prior-event links, missing slots, H1 context and same-candle ambiguity groups are in liquidity-context.json. Returning closes and further exceedances do not establish a new sweep or a crossing path. First means first observable supplied interval, not an event in missing history. No reset at 08:30.

CanonicalAdmissionPermitted=false; OperationalSignal=false. Replacement, invalidation, reuse and operational relevance remain unresolved. No independent validation sample count is inferred from multiple references in one Development episode.
