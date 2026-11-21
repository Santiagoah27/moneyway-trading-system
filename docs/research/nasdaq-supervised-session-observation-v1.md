# Nasdaq supervised session observation V1

Open **Strategy Lab → Observación supervisada Nasdaq** at
`/#strategy-lab-observation`. The workbench inspects a single local JSON
report from `EXP-NQ-END-TO-END-MTF-V1`. It shows historical Development
replays and registered Prospective observations as distinct case types,
each with its explicit historical cutoff. The file remains in the browser.

## Generate and open a report

From the repository root, generate the exposed June 23 Development report.

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-observation-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-end-to-end --cutoff-utc 2026-06-23T16:30:00Z
```

For a registered eligible session, set `$source` to that dataset's
`registration.json` and use its exact registered UTC cutoff. The registered
report carries the coverage and UTC mapping evidence for the selected
session. The direct documentary June 23 report carries source provenance
and visible candle counts. Its coverage section labels the registered
continuity diagnostic as unavailable.
[Evidence registration](../market-data/nasdaq-multitimeframe-evidence-readiness-v1.md)

Serve the existing frontend locally.

```powershell
Set-Location src/frontend/moneyway-web
npm run dev
```

Open `http://localhost:5173/#strategy-lab-observation` or use the address
printed by Vite. Choose **Abrir informe local** and select
`nasdaq-end-to-end-research.json` inside the output directory. The frontend
serves the page. Report generation uses a one-shot Worker command. The
viewer needs neither the API nor PostgreSQL nor a background Worker.

## Manual visual acceptance

1. Confirm **Replay histórico · Development expuesto**, **US100 ·
   2026-06-23** and a cutoff of **11:30:00 Bogotá**. Compare the published
   `[08:30, 11:00)` and experimental `[08:30, 11:30)` windows. Expand
   **Cobertura, hashes y versiones** to inspect the dataset, original and
   normalized hashes and candidate identities.
2. Confirm **H4ContextUnavailable** alongside **12** episodes, **6** takes,
   **4** M5 anchors, **5** M1 realignments and **0** complete H4-to-M1
   chains. The **41,472** count belongs to configuration associations.
3. Select **LL · E1**. The first stored association with an observed
   realignment opens for inspection. Check the **08:10** take, **08:35**
   SC, **08:55** directional FVG, **08:57** countertrend and **08:58**
   realignment. Expand realignment and **Inspeccionar velas originales**
   to inspect the original M1 candle and its close. The **10:10** FVG
   invalidation appears separately. Use **Configuraciones de investigación**
   to change the M5/M1 association, SL/TP profiles or H4 profile and inspect
   the evidence identities.
4. Verify that **Referencias hipotéticas** shows the M1 close, the SL, TP,
   point distances and R:R when supplied by the report. Check the
   negative cases under **Admisión a Demo v0**, including unresolved
   execution policies, local supervision and unverified session
   connectivity. Confirm that the only controls load or inspect evidence.

## Report boundary

The viewer validates the Worker V1 artifact, four original source identities,
cutoff and window versions, component identities, episode associations,
chronology and structural joins. An unsupported, truncated, contradictory or
oversized report clears the previous selection and shows an error. The local
file limit is 64 MiB. Source hashes are retained as reported evidence.
Documentary availability remains unauthenticated. The viewer groups evidence
at shared timestamps and retains distinct identities without assigning
intrabar ordering.

The direct documentary input is restricted to the pinned exposed June 23
context. Registered reports require eligible Development or Prospective
exposure and matching readiness. June 22, 24 and 26 review, June 25
Validation, June 30 HeldOut, and mentor-exposed June 29 and September 8 are
blocked. Unknown exposure is blocked. Existing frozen review facilities
maintain their own contracts.

`CanonicalAdmissionPermitted=false` and `OperationalSignal=false` are
required and displayed. The UI uses the canonical research report for
observations and calculated geometry. It renders original evidence with
Strategy Lab's candle chart. Independent validation, admission approval,
entry and exit policies, instrument risk evidence and session-specific
demo connectivity remain separate readiness requirements.
[Research lifecycle](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md) · [Supervision evidence](../decisions/0025-define-nasdaq-demo-human-evidence-contract.md)
