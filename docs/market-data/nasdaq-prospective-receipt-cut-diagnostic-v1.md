# Nasdaq prospective receipt-cut diagnostic V1

The Worker command `mt5-causal-availability --view receipt-cut` reports which
OANDA Global Markets Limited / `OANDA_Global-Demo-1` / demo `US100` samples were
usable within a pinned receipt cut. Output is create-only and outside the
repository and receipt directory. The payload carries the exact cut receipt,
SHA-256, directory scope, and receipt/probe manifest SHA-256. A nonempty
diagnostic range can additionally pin an earlier anchor. Only observations in
`(anchor, cut]` contribute samples. The anchor establishes the incoming hash
link, and lineage before it is outside the scope of the report.

## Runbook for an authorized Development session

1. Declare the session and evidence directory before starting. Use the existing
   read-only collector and `mt5-receipt observe`, with isolated diagnostic
   progress and a pinned anchor if the source has a prior history. Keep the
   file names and bytes intact. The receipt and its probe must be present.
2. At the intended evaluation, select the last receipt actually seen by the
   observer by its sequence and SHA-256. Save that pair before running the
   diagnostic. Pin the anchor from the recorded setup. Optionally copy the
   observer's receipt-discovery NDJSON and pin its SHA-256 at the same time.
   The journal uses `sequence`, `sha256`, and `observedAtHostUtcCandidate` per
   row. The capture supervisor already writes this format.
3. Run the following with a new report path, from the repository root. With an
   isolated chain beginning at sequence 1, omit both anchor options. Without
   a preserved receipt-discovery journal, omit both journal options.

```powershell
dotnet src/backend/MoneyWay.Worker/bin/Debug/net10.0/MoneyWay.Worker.dll `
  mt5-causal-availability --view receipt-cut --source $receiptDirectory `
  --anchor-sequence $anchorSequence --anchor-sha256 $anchorHash `
  --through-sequence $cutSequence --through-sha256 $cutHash `
  --observer-journal $frozenJournal --observer-journal-sha256 $journalHash `
  --report $newReportPath
```

4. Preserve the report and its emitted SHA-256. Re-run the same pins and journal
   into a different output file in the same evidence scope for byte comparison.
   A cut selected from a retained journal after the session reconstructs the
   recorded prefix for inspection. It cannot demonstrate that an evaluation
   actually ran during that session.

## Evidence boundary

- The result uses `FirstUsableHistorical` from the existing causal projection.
  It preserves native identity, literal OHLC, source position and exact receipt
  and probe references. Position zero and samples without a usable historical
  observation are listed with exclusions. A later revision is counted while
  the original frozen value is preserved. Prefix validation failure leaves the
  report without samples and exits 3.
- The continuity notices and native gaps describe observed intervals, acquisition
  issues and run changes. The report retains intact evidence on either side of
  a gap for inspection. An unclassified gap can include a market closure.
  Receipt order cannot establish continuous capture. Receipt-clock and collector
  GMT regressions can be reported. Forward clock steps and clock uncertainty
  remain unresolved without continuity evidence.
- The optional pinned journal supplies local receipt labels only. Exact sequence
  and receipt SHA-256 must match, and conflicting labels are excluded. A journal
  hash attests to byte identity and does not authenticate when it was created.
- The existing preparation records have no verified receipt-cut binding.
  Supplied unbound references in the service remain exclusions. The CLI uses
  no human assertions, and the report makes no H4 direction selection.
- `StandardEvaluation=NotEvaluated`. A reviewed temporal basis for native UTC
  opens and closed candles, plus availability enforced at each evaluation
  boundary, is required before calling Standard. Bogotá rule windows need a
  supported clock basis. The report cannot make canonical replay inputs or
  bind a supervised attempt. `CanonicalReady`, `CanonicalAdmissionPermitted`
  and `OperationalSignal` are always false.

## Existing Development evidence

The November 25 retained receipts permit a repeatable test with anchor 4910.
Run cut 5094 with only the scoped bytes through that cut, add records and probes
5095–5178, then rerun 5094 with the same pins and frozen journal. The two
reports must match byte for byte. Cut 5178 may expose the additional usable
samples. Retain source, copy, and report hashes. This procedure needs neither
terminal access nor a new official capture.

The retained Development acceptance produced 283 usable samples at cut 5094
(H4 16, H1 18, M5 51, M1 198). Adding the 84 later receipt/probe pairs left the
earlier report byte-identical. Cut 5178 produced 393 usable samples (H4 16,
H1 20, M5 69, M1 288). It preserved the 7,200-second native H1 gap and the
M5/M1 head advances that skipped observed periods. Standard remained
`NotEvaluated` at both cuts.
