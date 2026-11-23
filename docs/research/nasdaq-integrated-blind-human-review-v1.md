# Nasdaq integrated blind human review V1

Open **Strategy Lab → Revisión humana ciega Nasdaq** at
`/#strategy-lab-integrated-review`. This is a local human annotation
workbench for H4, H1, M5 and M1. It neither runs a candidate nor loads an
integrated research report. It calculates no proposed reference, agreement
metric or trading output.

## Try the workbench

```powershell
Set-Location src/frontend/moneyway-web
npm run dev
```

Open the URL printed by Vite on localhost with the route above. No API,
database, background Worker or open market is required.

1. Select **Abrir caso sintético**. Provide a reviewer name, explicitly
   declare exposure and start the review.
2. Select a timeframe. Use the timeline slider, **Anterior**, **Siguiente**
   or **Corte final**. Only candles closed at or before the visible cutoff
   are passed to `CandleChart`. The chart displays up to the last 80 visible
   candles. Move the cutoff back to inspect older candles.
3. Select an observation and classification. Click a candle or use its
   keyboard button to select its exact identity. Inspect the native opening,
   UTC opening, nominal close and OHLC. Add the evidence or reason and save.
4. Change the classification and save a second revision. Export the JSON
   and use **Importar etiquetas JSON** to read it back for the same package.
5. To exercise post-reveal handling with the synthetic fixture, record
   external visibility and save a further revision. The first judgments
   remain in the file. The workbench has no reveal action.

**Abrir Development del 23 de junio** is an optional functional exercise.
The adapter verifies the retained June source SHA-256
`3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c`
and projects only H4/H1/M5/M1 candle rows, original source digests, cutoff
and coverage. The session keeps its exposed Development classification.
Its nominal documentary UTC mapping does not establish authenticated provider
publication, closure or historic availability.

## Original-only input

The local file envelope has `schema: "moneyway.nasdaq-blind-original.v1"`,
a string `payload` containing the JSON and `sha256` of the exact UTF-8 payload.
The schema is closed at every level. It carries session ID, Bogotá operating
date, exact UTC cutoff, exposure, provider and original instrument,
provenance digest and four source records. A source record contains its
original digest, timestamp mapping classification, bounded native/UTC/OHLC
rows and coverage derived from those rows. Coverage lists internal gaps
without classifying them as scheduled closures. Files with mismatched digests,
foreign identities, duplicate or overlapping rows, candles after the cutoff,
or additional fields are rejected. A timeframe with unresolved mapping can
carry provenance and empty coverage. Its candles stay hidden until the
mapping is established.

V1 accepts local synthetic packages on the reserved synthetic day and the
workbench's built-in projection of pinned Development. It rejects other
real sessions, including the frozen Validation and HeldOut dates, before
rendering any candles. A filename, self-declared exposure or recomputed
envelope hash cannot change this access policy. Synthetic source and
provenance digests also bind the exact fixture rows. A SHA-256 establishes
file integrity against a pin. It supplies no signature or clock attestation.

## Human labels and history

The export envelope uses `moneyway.nasdaq-integrated-human-review.v1`.
It uses the same exact-payload SHA-256 convention. After parsing the outer
JSON, hash the UTF-8 bytes of `payload` before parsing it to verify the
digest. Import also rechecks the package hash, source hashes, session,
cutoff, declared exposure and all candle references.

The payload binds the reviewer, declared exposure, device time of the
declaration, source identities, session and cutoff to consecutive revisions.
Each revision records the human observation type, stable session-scoped
`targetId`, classification, written evidence or uncertainty, review time,
visible cutoff, maximum previously viewed cutoff and exact selected candle
identities. A candle identity includes timeframe, original SHA-256, native
opening, UTC opening and nominal close. The annotation types cover H4
context, H1 liquidity, liquidity take, M5 SC/IFVG/FVG, M1 pullback and
realignment, and structural SL/TP. These are human annotation targets.

The classifications are `Reference`, `NoReference`, `Ambiguous`,
`InsufficientSourceEvidence` and `Skip`. A reference requires one or more
selected original candles. An ambiguous judgment can retain candidate candles.
Other choices record a reason. No geometry, direction or classification is
prefilled from a candidate.

History is stored in this browser per operating date and exact cutoff.
An export can extend only the same local history. Import with a different
reviewer, source, previous judgment or previous reveal record is rejected.
The highest viewed cutoff survives backward navigation and reopening.
Manual recording of external result visibility appends a marker. It does
not reveal results. Later revisions are marked as post-reveal corrections,
and the prior revisions remain in the export. A declaration that the reviewer
already saw results records exposure from the start. Browser clock readings,
reviewer identity and exposure are documentary. The application does not
certify reviewer independence. Both authorization flags are always false.

## Frozen protocol boundary

The workbench does not update `NASDAQ-PROSPECTIVE-VALIDATION` V1 or its
assessment ledger. November 23 remains Validation / `NotEvaluated` and
November 24 remains HeldOut. No real source/label binding for either date is
generated here. The export uses the protocol's label vocabulary and provides
hash-bound evidence for a later reviewed binding. It is a separate browser
artifact, with no automatic ingestion into the protocol ledger.

For an authorized reproduction or assessment, use the Worker at the frozen
commit `a28e9479d83957eebcf325ee93d32a978ff577bf`, for example in a separate
detached worktree. The frontend commit supplies no new candidate code or
protocol configuration. Before using a real Validation session, establish
the approved source and timestamp/availability evidence, a verified reviewer
and a protocol-scoped reference universe. Keep the outcomes hidden.
[Frozen protocol workflow](nasdaq-pre-registered-validation-protocol-v1.md)
