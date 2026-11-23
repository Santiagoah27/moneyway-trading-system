# Nasdaq integrated multi-event human review V2

Open **Strategy Lab → Revisión humana ciega Nasdaq** at
`/#strategy-lab-integrated-review`. The existing original-only package and
`CandleChart` continue to bound the four source timeframes by the visible
cutoff. The browser keeps the complete local event history and its exposure
declaration.

## Create, inspect and correct events

1. Use the synthetic case or **Abrir Development del 23 de junio**.
   Declare exposure and start the review. Use **Corte final** to exercise
   the controls with a complete visible prefix of the selected package.
2. Choose **Nuevo evento**, select the observation type, select the original
   candles and enter the judgment and justification. Save the event. The
   selector identifies it by event number and type.
3. Repeat **Nuevo evento** with the same type to create a separate instance.
   For example, two liquidity takes in the fixture have targets
   `synthetic-us100-v1/event/1` and `synthetic-us100-v1/event/2`.
4. Select event 1 under **Eventos registrados** and choose
   **Editar evento seleccionado**. Save a correction. The edit keeps that
   target and type and appends its second revision. Event 2 retains its first
   revision. Select an entry under **Historial de revisiones** to inspect its
   exact native and UTC candle references. The chart marks the selected
   revision or the current draft.
5. Select **Sin referencia** and specify the start of the reviewed interval.
   Its end is the visible cutoff at save time. Ambiguity, insufficient source
   evidence and skipped review can be recorded with a note and zero candles.

The reviewer establishes identity through a new event or an explicit correction.
The application does not infer relationships from time, price, direction or
shared candles. This version has no causal-link field. A future causal annotation
requires an explicit and validated reference between events.

## Export contract

The envelope is `moneyway.nasdaq-integrated-human-review.v2`. The exact
UTF-8 `payload` string is bound by the SHA-256 in the envelope. Its root
records the package hash, original hashes, session and cutoff, reviewer and
declared exposure, maximum viewed cutoff and the optional record of external
result visibility. Both authorization flags remain false.

The payload carries an append-only sequence of `labels`. Each entry contains
the stable `targetId`, `eventNumber`, observation type and judgment, exact candle
references, visible and maximum viewed cutoffs, documentary device time,
justification and reveal phase. `revision` orders all entries in the session.
`eventRevision` starts at 1 for each event and increments only for corrections
to that event. New event numbers increase by one and corrections retain their
original number. Local IDs identify annotations. They carry no canonical
setup or operational authority.

`NoReference` requires a nonempty interval from `scopeStartUtc` to
`viewingCutoffUtc`. It describes the reviewer's judgment inside the stated
scope. Other classifications use a null `scopeStartUtc`.
`RetrospectiveAfterLaterView` records that a later market cutoff was already
visible when the reviewer saved at an earlier cursor. The maximum persisted
view cannot be reduced by moving the cursor or importing a file. Recording
external result visibility preserves the earlier revisions and marks later
entries as post-reveal corrections. The browser clock and exposure declaration
remain documentary evidence.

Import verifies the exact package and source, candle identity, revision
sequence, cutoff bounds, reveal chronology and consistency of event IDs and
types. A valid extension must preserve the stored prefix, exposure and
reveal record. SHA-256 detects file changes against a pin and supplies no
signature or independent review attestation.

## Read V1 as historical evidence

Open **Historial V1 · solo lectura** to inspect same-package local V1
records or use **Consultar archivo de etiquetas V1**. An imported V1 file
is displayed for that browser session. Its original bytes and the original
V1 storage key are preserved. V1 imports through the V2 control are
rejected. Type-level V1 identities remain in V1 because their histories
cannot identify the underlying event instances.

The explicit **Iniciar revisión V2 con la declaración V1** action begins an
empty V2 history with the same declaration, maximum cutoff and any prior
exposure to results. This preserves chronology and creates no V2 label from
a V1 judgment. The formats use separate storage keys.

## Scope and local acceptance

Run `npm run dev` from `src/frontend/moneyway-web` and use the route above.
The original fixtures and source policy still apply. The frozen November 23
Validation session remains `NotEvaluated` and November 24 remains HeldOut.
The workbench needs neither a Worker nor an API or database. The next
integration is a separately reviewed binding between human event targets,
the admissible source and a predeclared comparison universe. The frozen
protocol and its pinned Worker revision still govern assessment.
[V1 source contract and data limits](nasdaq-integrated-blind-human-review-v1.md)
