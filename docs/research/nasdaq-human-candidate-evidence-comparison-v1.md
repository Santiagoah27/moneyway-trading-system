# Nasdaq human-to-candidate evidence comparison V1

Open **Strategy Lab → Comparación de evidencia Nasdaq** at
`/#strategy-lab-evidence-comparison`. This separate retrospective workbench
consumes a human review V2, its exact original-only package, an integrated
candidate report 1.1.0 and a scope manifest. It uses the existing report
parser and original candle chart. The blind route does not load candidate
results or gain a reveal action.

The comparison accepts only the exposed June 23 Development package and
controlled synthetic inputs using the reserved synthetic date and identity.
The configured Development input is the pinned June context and the
matching unregistered documentary report. Registration-backed, protected
and unknown sessions stay closed, including November 23 Validation and
November 24 HeldOut. The frozen protocol and strategy remain separate.

## Prepare Development files

Record and export your own V2 review through the existing blind view using
**Abrir Development del 23 de junio**. Retain that file before opening the
report. Generate an unregistered documentary replay at the frozen package
cutoff with the current research Worker.

```powershell
$inputFile = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-evidence-comparison-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $inputFile --output $destination --compare-end-to-end --cutoff-utc 2026-06-23T16:30:00Z
```

Build the manifest from that report and the exact label file. Select the
observation types before opening the comparison. Use null `configurationId`
to retain all report configurations, or provide one full ID in the form
`H4Id/geometry.configurationId`. H1 endpoint evidence is session-wide.

```powershell
$reviewFile = 'C:\path\moneyway-nasdaq-integrated-human-review-v2.json'
$reportFile = Join-Path $destination 'nasdaq-end-to-end-research.json'
$label = (Get-Content -Encoding UTF8 -Raw -LiteralPath $reviewFile | ConvertFrom-Json).payload | ConvertFrom-Json
@{
  schema = 'moneyway.nasdaq-evidence-comparison-manifest.v1'
  packageSha256 = $label.originalPackageSha256
  reviewSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $reviewFile).Hash.ToLowerInvariant()
  reportSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $reportFile).Hash.ToLowerInvariant()
  sessionId = $label.sessionId
  operatingDate = $label.operatingDate
  cutoffUtc = $label.cutoffUtc
  exposure = $label.sessionExposure
  provider = $label.provider
  instrument = $label.instrument
  candidateId = 'EXP-NQ-END-TO-END-MTF-V1'
  candidateVersion = '1.1.0'
  windowId = 'EXP-NQ-TRADING-WINDOW-1130-V1'
  configurationId = $null
  observationTypes = @('H4Context','H1Liquidity','LiquidityTake','M5SC','M5IFVG','M5FVG','M1Pullback','M1Realignment','StructuralSL','StructuralTP')
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $destination 'comparison-manifest.json')
```

Run `npm run dev` in `src/frontend/moneyway-web`. Open the comparison route,
choose **Abrir paquete original Development**, and load the review, the
generated report and the manifest. Select **Verificar insumos**. Declare
the comparison reviewer and exposure before **Abrir comparación
retrospectiva**. The browser records the first local opening for the source
session and human reviewer, and preserves the review as exported. Imported
comparison histories must extend the recorded decisions. The workflow needs
no API, database or persistent Worker.

## Exact evidence and decisions

All four raw source hashes, the package provenance, the provider,
instrument, date, exposure, cutoff and visible source prefix must match.
The manifest pins the exact file bytes and candidate identity. A normalized
hash is not accepted in place of a raw hash. A candle must resolve to its
bounded original timeframe, native opening and exact UTC interval. Where
the report supplies OHLC and volume, those values must also match. Its
observed close and when the report uses a structural SL/TP selection are
checked against the human label's own visible cutoff. Provider publication
and historical availability remain unauthenticated.

The scoped index considers reported H4 observations with available context,
H1 extrema with original source candles, selected M5 take/SC/IFVG/FVG,
M1 pullback and realignment, and structural SL/TP source references.
Where such evidence is absent, the type is `NotComparable`. Repeated
candidate evidence IDs collect their configuration associations. The
existing candidate's SC and M1 realignment counting identity uses its
direction and exact original source candle; the workbench retains all
observation IDs and profiles. These are documentary event groupings and
configuration associations. They provide no count of trade opportunities.

A matching set of human-selected candle references produces an exact
documentary coincidence. Partial overlap and distinct evidence remain
visible. For example, if labels A and B describe two different takes and
only A shares the report's original candle, A has an exact documentary
coincidence while B has distinct references. Neither pair has a confirmed
association until a reviewer records a decision and justification.
`NoReference` applies within the recorded interval. Temporal evidence that
exceeds the human cutoff cannot authorize a confirmed or negative match.

The pinned V2 export supplies one latest judgment per target, its complete
revision history, the prior-reveal revision where available and the
retrospective classification. The comparison preserves these fields and
records its own disclosure time. A later judgment cannot be represented
as independent because it lacks an exposure marker. An event that has
no human annotation remains unassociated.

The JSON export `moneyway.nasdaq-evidence-comparison.v1` hashes the exact
payload and includes the manifest, run identity, original review, disclosure,
explicitly linked candidate evidence, decisions and diagnostic counts.
Import revalidates the original inputs, then reconstructs and verifies the
export. Decisions append in review order. Counts that describe decisions
use the latest decision for each pair. Exact coincidences use comparable
pairs as the denominator, and human revision counts stay separate from
unique events. There are no independent agreement, precision, recall,
win-rate or P&L measures. Both authorization flags remain false.
