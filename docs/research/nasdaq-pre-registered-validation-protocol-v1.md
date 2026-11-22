# Nasdaq pre-registered validation protocol V1

The Worker freezes a chronological research plan and registers compatibility
with existing evidence readiness artifacts. The integrated candidate is
`EXP-NQ-END-TO-END-MTF-V1` version `1.1.0`. Freeze captures the full experimental
matrix, including alternative H4 profiles and horizons, M5 event and SC
families, FVG and sequence policies, M1 profiles and SL/TP profiles. Preparation
is 08:00 Bogotá and the research window is `EXP-NQ-TRADING-WINDOW-1130-V1`.
The published replay strategy and the candidate session-liquidity identity are
recorded separately. The matrix has no preferred profile or acceptance
threshold. [Experimental research boundary](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md) ·
[Session definitions](../decisions/0028-define-candidate-nasdaq-session-windows.md)

## Freeze a plan

Run from a clean MoneyWay checkout. The Worker verifies the build's source
revision against Git and records that commit. Keep requests and artifacts
outside the checkout. The times below are actual documentary instants recorded
when performing each step. They do not constitute an authenticated timestamp.

```powershell
$root = Join-Path $env:TEMP ('moneyway-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$freezeRequest = Join-Path $root 'freeze-request.json'
$frozen = Join-Path $root 'frozen-v1'
$created = [DateTimeOffset]::UtcNow
$dates = @(
  @{ operatingDate = '2026-11-23'; partition = 'Validation' }
  @{ operatingDate = '2026-11-24'; partition = 'HeldOut' }
)
@{ protocolId = 'NASDAQ-PROSPECTIVE-VALIDATION'; version = 1
   createdAtUtc = $created.ToString('O'); frozenAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
   dates = $dates
} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -LiteralPath $freezeRequest
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-validation-protocol freeze --request $freezeRequest --output $frozen
```

The collection period begins on November 23, 2026. The example dates require
freezing before November 23 in Bogotá. If starting a new plan later, choose
explicit dates after the actual freeze, ahead of source acquisition. Each
selection fixes a Bogotá operating date, Validation or HeldOut partition, and
an exact UTC cutoff derived from the versioned 11:30 window. Validation
precedes HeldOut. Dates are selected without outcomes, a holiday calendar or
automatic replacement. Absent evidence keeps the original date and cutoff
in the ledger.

`protocol.json` uses schema `moneyway.nasdaq-validation-protocol.v1`.
`protocol.sha256` pins the exact canonical UTF-8 bytes (no BOM or trailing
newline). The manifest also carries the canonical hash of
`candidateConfiguration`, which contains the candidate, component profiles,
original provider and instrument, timeframes and preparation/window clocks.
The manifest includes the source, partition and review policies, unresolved
items and metric definitions with denominators. An amendment uses the next
version, a hash-pinned `predecessor`, a later freeze, fresh dates across the
lineage and a new output directory. Earlier artifacts retain their hashes.

## Assess an existing registration

Use the existing readiness command to obtain `registration.json` and
`readiness.json`. Assessment verifies both pins and reproduces the readiness
diagnostics from the registered local sources. The source original and
normalized hashes, mapping and acquisition evidence, cutoff and existing
blocker codes are retained. No integrated chain replay is invoked.
[Evidence registration and original files](../market-data/nasdaq-multitimeframe-evidence-readiness-v1.md)

```powershell
function Pin-ValidationFile($path) {
  @{ path = (Resolve-Path -LiteralPath $path).Path
     sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant() }
}
$assessmentRequest = Join-Path $root 'assessment-request.json'
$ledger = Join-Path $root 'ledger'
$registrations = @()
# Optional diagnostic of the exposed June 23 Development recording.
# $registrations = @(@{ operatingDate = '2026-06-23'; cutoffUtc = '2026-06-23T16:30:00Z'
#   registration = Pin-ValidationFile (Join-Path $readinessDir 'registration.json')
#   readiness = Pin-ValidationFile (Join-Path $readinessDir 'readiness.json') })
@{ protocol = Pin-ValidationFile (Join-Path $frozen 'protocol.json')
   assessedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
   registrations = $registrations
} | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -LiteralPath $assessmentRequest
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-validation-protocol assess --request $assessmentRequest --output $ledger
```

Each immutable ledger revision contains `assessment.json`,
`assessment.sha256`, a request fingerprint and `next-actions.md`. The
assessment schema is `moneyway.nasdaq-validation-assessment.v1`. Use the same
ledger directory across registrations and amendments of this protocol family.
It carries forward exposure and review history using operating date, exact
cutoff and reviewer identity. The latest assessment hash links revisions.
A registration at another filename or under another hash confers no exposure
upgrade. Known historical exposure also comes from the existing date policy,
independent of any local ledger. Share and retain the original ledger for
audit. Separate local ledgers and reviewer identities cannot establish a
global exposure history.

June 23 remains exposed Development and can show adequate source coverage
with H4 context unavailable. June 22, 24 and 26 remain protected chronological
review. June 25 is historical Validation, June 30 is historical HeldOut,
and June 29 and September 8 are MentorExposed. These dates and Unknown
exposure cannot qualify under this prospective protocol. September's
full-interval UTC mapping remains unresolved. A new assessment reports Git
commit and candidate configuration discrepancies. Reproduce a frozen plan
with the Worker built at its recorded commit.

## Availability and human review evidence

Six separate checks govern eligibility for a future independent evaluation.

1. Acquisition receipts and original source hashes establish the documented
   acquisition history after configuration freeze.
2. Full-interval reviewed UTC mapping and synchronized coverage establish
   source readiness. The optional `availability` pin points to a JSON object
   with `operatingDate`, `cutoffUtc`, `readinessSha256`,
   `lastObservedAvailableAtUtc`, `verifiedAtUtc` and an `evidence` file pin.
   The observed availability must cover the last used nominal close and precede
   or equal the replay cutoff. Pinning a report or knowing candle close times
   cannot authenticate historical publication.
3. The optional `review` pin points to a JSON object with `operatingDate`,
   `cutoffUtc`, `readinessSha256` and `review`. Its `reviewerId` and
   `exposure` document a distinct person and a declaration of `Unknown`,
   `SelfDeclaredIndependent` or `PreviouslyExposed`. Verify reviewer
   identity and independence from the provenance before evaluating.
4. The protocol hash pins the candidate matrix and the explicit dates before
   any eligible acquisition. Git and the running matrix must match.
5. In `review`, provide `declaredAtUtc`, explicit
   `outcomesRevealedAtUtc` (null while hidden), and append-only `labels`.
   Labels use consecutive `revision` numbers from 1, a stable `targetId`,
   `kind`, `reviewedAtUtc` and a pinned `sourceReference`. The kinds are
   `Reference`, `NoReference`, `Ambiguous`, `InsufficientSourceEvidence`,
   and `Skip`. Post-reveal corrections remain separate. A revised or
   renamed source needs its own bound labels. Earlier judgments retain their
   original source identity.
6. Eligibility requires a frozen selected session, prospective exposure,
   verified source and availability evidence, H4 context and a reviewable
   pre-reveal independent judgment. It is a readiness finding for a separately
   authorized evaluation. No automatic promotion or evaluation follows.

This local JSON contract reuses the blind review distinction between prior
exposure, pre-reveal judgments and post-reveal revisions. The browser's
existing M5 labels and exposure records do not attest an integrated session.
An independent integrated labeling interface and the reference matching
universe remain separate work. [Blind review](m5-blind-validation-v3.md) ·
[Supervised observation](nasdaq-supervised-session-observation-v1.md)

Assessment always records `NotEvaluated`, `CanonicalAdmissionPermitted=false`
and `OperationalSignal=false`. Validation metrics are unavailable pending an
independent evaluation with real labels and a defined matching universe.
Configuration associations are reported separately from distinct evidence
events. Hypothetical geometry supplies no profitability, win rate or P&L.
No API, database, background Worker or broker connection is required.
