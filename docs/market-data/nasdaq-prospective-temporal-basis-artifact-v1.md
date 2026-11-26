# Nasdaq prospective temporal basis sidecar V1

The sidecar is a create-only diagnostic for one declared Bogotá session. It
combines the existing correlated-tick importer with a pinned Worker receipt-cut
report. The Worker already uses the validated receipt prefix, causal audit and
`FirstUsableHistorical`. Neither capture process, checkpoint, scheduler task,
nor admission policy changes.

## What the inputs prove

| Input | Retained in the sidecar | Limit |
| --- | --- | --- |
| Validated correlated-tick bundle | Exact source, nonce, file and retention hashes, original tick/baseline, `time_msc`, native context, EA elapsed counters, host monotonic brackets and frequency | The handler and latest tick lookup have bounded local observations. Queued callback identity and provider publication remain unresolved. |
| Before/after Windows stripchart observations | Raw, reparsed measurements and candidate labels, reported offsets and roundtrip, reference identity, hypothesis source and fit | Ordinary unauthenticated NTP. Absolute error bound remains null. No clock correction or cross-domain monotonic subtraction. |
| Verified receipt-cut | Exact sequence and receipt hash, report and prefix hashes, anchor, first frozen historical sample per frame, nominal native close | Receipt visibility is local to that frozen prefix. Position 1+ and advancing heads do not attest provider finalization. |

The sidecar orders each input using its own clock and coordinates. All used
host labels must fall inside the declared session window. The tick must be
inside its recorded native M1 head, which must appear as exactly one usable
historical M1 sample in the cut. Its receipt label must be on or after the
tick's receipt bracket. The reference observations must have been collected
by the cut label. Cross-file ordering remains an inference from local labels.
No bounds or tolerance are invented. A native close is displayed as
native open plus period, without timestamp conversion.

- `NativeTimeObserved` refers to the tick validated for the declared window.
- `ReceiptVisibilityVerified` refers to the pinned receipt cut and its local
  sample lineage, even when the correlated tick cannot be linked.
- `UtcCorrelationCandidate` requires the same-session M1 link and exactly one
  existing hypothesis fitting the uncorrected host bracket. It applies only to
  the observed tick and linked M1 identity. It confers no mapping for another
  bar, timeframe, session, or date.
- `UtcMappingVerified`, `ProviderFinalizationVerified`,
  `StandardEvaluationPermitted`, `CanonicalReady`,
  `CanonicalAdmissionPermitted` and `OperationalSignal` stay false.

## November 30, 2026 — operator procedure

Use Windows PowerShell from the repository root. The authorized scheduled
receipt observation has its own working directory and starts at 07:45 Bogotá.
Store the sidecar outside the scheduled capture directory. The prepared window
is `[2026-11-30T12:45:00Z, 2026-11-30T16:30:00Z)`, expressed as operational
host labels.

1. Obtain explicit authorization for the separate correlated-tick diagnostic
   and its ordinary time-reference queries before running it. Confirm the
   existing EA binary is the reviewed diagnostic build. Keep Algo Trading off.
   On the connected `OANDA_Global-Demo-1` account, open a **separate US100
   chart**. Leave the scheduled collector and chart alone.
2. After the collector has started, target **08:10–08:20 Bogotá** so the
   correlated response, reference and a subsequent historical M1 receipt can
   precede the existing 08:30 cut. In PowerShell, start the existing
   coordinator. The operator must attach `MoneyWayCorrelatedTick` only to the
   separate chart, paste the exact printed `SessionNonce` into Inputs and
   confirm attachment before expiry. Remove only that diagnostic EA afterward.

   ```powershell
   & ./tools/market-data/mt5/Invoke-Mt5CorrelatedTickCapture.ps1 `
       -SessionSeconds 180 -ReferenceHost time.windows.com -QueryTimeoutMilliseconds 5000
   ```

3. Preserve the original seven nonce files. Record hashes of `evidence.json`
   and `response.json` from that completed nonce. Use the pinned
   `after-preparation-receipt-cut-post.json` if it includes the exact historical
   M1 and the reference preceded its cut. Otherwise use a later, already
   recorded cut and state its later sequence. A report cannot supply evidence
   at an earlier cut retroactively. Import and assemble offline as follows:

   ```powershell
   $nonce = '<printed nonce>'
   $bundle = Join-Path $env:APPDATA "MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick\$nonce"
   $retention = Join-Path $env:LOCALAPPDATA 'MoneyWay\Evidence\Mt5CorrelatedTick'
   $cut = 'C:\MoneyWay\session\prospective-temporal-basis-20261130-v1\after-preparation-receipt-cut-post.json'
   $out = 'C:\MoneyWay\session\temporal-basis-artifact-20261130-v1\assembled-20261130.json'
   $pins = @{
       ReceiptCutReport = $cut
       ReceiptCutReportSHA256 = '<verified report SHA-256 from diagnostic-status.json>'
       CorrelationSourceDirectory = $bundle
       RetentionRoot = $retention
       ExpectedEvidenceSHA256 = '<pinned evidence.json SHA-256>'
       ExpectedResponseSHA256 = '<pinned response.json SHA-256>'
   }
   & ./tools/market-data/mt5/New-Mt5TemporalBasisSessionArtifact.ps1 @pins `
       -WindowStartHostUtcCandidate '2026-11-30T12:45:00Z' `
       -WindowEndHostUtcCandidate '2026-11-30T16:30:00Z' -OutputPath $out
   ```

   The output parent and external retention root must exist. Never substitute a
   prior nonce. Without a valid reference, usable cut or exact same-session
   link, the artifact remains unresolved. For a repeatability check use the
   same pinned inputs and a new output name, and compare the two SHA-256 hashes.

## Acceptance evidence before November 30

The authentic November 16 nonce was revalidated offline by the existing
importer. Its GMT+2 hypothesis fits the retained host bracket of 358.4656 ms
and its GMT+3 hypothesis falls outside. Those observations apply to that
November 16 tick. The November 25 cut 5178 verifies independently. Declaring
the November 25 window with both inputs rejects the tick as
`correlated_evidence_outside_session`. Declaring the November 16 window rejects
the later cut as `receipt_cut_outside_session`. Neither produces a joint
candidate. A synthetic same-session case tests the positive wiring and a
missing-reference case confirms conservative failure.
