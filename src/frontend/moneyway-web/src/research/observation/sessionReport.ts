import { readEpisodes, type Episode } from "./sessionChains";
import {
    atMost,
    bogotaTime,
    bool,
    candle,
    check,
    count,
    decimal,
    gapLabel,
    hash,
    index,
    instrument,
    lookup,
    maybeRow,
    num,
    optionalUtc,
    provider,
    row,
    rows,
    safe,
    sameTime,
    str,
    strings,
    timeKey,
    utc,
    type Evidence,
    type Row,
} from "./sessionEvidence";

const timeframes = ["H4", "H1", "M5", "M1"];
const protectedDates = new Set([
    "2026-06-22",
    "2026-06-24",
    "2026-06-25",
    "2026-06-26",
    "2026-06-29",
    "2026-06-30",
    "2026-09-08",
]);
const juneDigest =
    "3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c";
const integrated = "EXP-NQ-END-TO-END-MTF-V1";
const publishedVersion = "nasdaq-0.1.0-draft";
const windowVersion = "EXP-NQ-TRADING-WINDOW-1130-V1";
export const maxReportBytes = 64 * 1024 * 1024;

type H4 = {
    id: string;
    at: string | null;
    available: boolean;
    reason: string;
    classification: string | null;
    evidence: Evidence[];
    details: string[];
};
type Source = {
    timeframe: string;
    raw: string;
    normalized: string;
    mapping: string | null;
};

function readSources(
    envelope: Row,
    day: string,
    cutoff: string,
    dataset: string,
) {
    let exposure: "Development" | "Prospective" = "Development";
    let readiness: Row | null = null;
    let sources: Source[];
    let digest: string;
    if (
        envelope.evidenceReadiness !== undefined ||
        envelope.registrationSha256 !== undefined
    ) {
        digest = hash(envelope.registrationSha256);
        readiness = row(envelope.evidenceReadiness, "registro de evidencia");
        const session = row(readiness.session, "sesión registrada");
        safe(readiness, "banderas del registro");
        if (
            session.exposure !== "Development" &&
            session.exposure !== "Prospective"
        )
            throw new Error(
                "Exposición protegida o desconocida. No se puede abrir en esta vista.",
            );
        exposure = session.exposure;
        check(
            session.operatingDate === day &&
                sameTime(utc(session.cutoffUtc, "corte registrado"), cutoff) &&
                readiness.researchReplayPermitted === true &&
                readiness.synchronizedInputReady === true &&
                readiness.timestampEvidenceVerified === true &&
                (exposure !== "Prospective" || day >= "2026-11-23") &&
                (day !== "2026-06-23" || exposure === "Development"),
            "sesión registrada o exposición",
        );
        const receipts = new Set<string>();
        sources = rows(envelope.sourceProvenance, "fuentes registradas").map(
            (s) => {
                str(s.originalPath, "identidad original");
                hash(s.evidenceManifestSha256);
                const timeframe = str(s.timeframe, "escala original");
                if (s.localAcquisition !== null) {
                    const acquisition = row(
                        s.localAcquisition,
                        "adquisición local",
                    );
                    const acquiredAt = utc(
                        acquisition.acquiredAtUtc,
                        "hora de adquisición",
                    );
                    hash(
                        row(acquisition.evidence, "recibo de adquisición")
                            .sha256,
                    );
                    const first = utc(s.firstOpenUtc, "inicio de adquisición");
                    const last = utc(
                        s.lastNominalCloseUtc,
                        "último cierre adquirido",
                    );
                    check(
                        atMost(last, acquiredAt),
                        "recibo anterior a las velas adquiridas",
                    );
                    if (
                        atMost(first, cutoff) &&
                        bogotaTime(last).slice(0, 10) >= day
                    )
                        receipts.add(timeframe);
                }
                return {
                    timeframe,
                    raw: hash(s.originalSha256),
                    normalized: hash(s.canonicalSha256),
                    mapping: str(s.timestampEvidence, "mapeo UTC"),
                };
            },
        );
        check(
            exposure !== "Prospective" || receipts.size > 0,
            "recibo prospectivo ausente",
        );
    } else {
        digest = hash(envelope.inputSha256);
        check(
            day === "2026-06-23" &&
                dataset === "nasdaq-context-20260623-v1" &&
                digest === juneDigest,
            "solo el contexto documental expuesto de junio 23 puede abrirse sin registro",
        );
        const mapping = str(envelope.documentaryMapping, "mapeo documental");
        const originals = row(
            envelope.sourceProvenance,
            "fuentes documentales",
        );
        sources = timeframes.map((timeframe) => {
            const s = row(originals[timeframe], "fuente " + timeframe);
            check(
                s.derived === false &&
                    s.sourceTimeframe === timeframe[1] + timeframe[0],
                "escala original " + timeframe,
            );
            str(s.rawRelativePath, "identidad original " + timeframe);
            return {
                timeframe,
                raw: hash(s.rawSha256),
                normalized: hash(s.normalizedSha256),
                mapping,
            };
        });
    }
    check(
        timeframes.every((tf) => sources.some((s) => s.timeframe === tf)) &&
            sources.every((s) => timeframes.includes(s.timeframe)),
        "identidad de las cuatro escalas",
    );
    const unique = new Map(
        sources.map((s) => [[s.timeframe, s.raw, s.normalized].join("/"), s]),
    );
    return { exposure, readiness, digest, sources: [...unique.values()] };
}

function readH4(prep: Row, preparation: string, cutoff: string) {
    check(
        sameTime(utc(prep.scheduledUtc, "preparación H4"), preparation),
        "identidad de preparación",
    );
    const result = rows(prep.profiles, "perfiles H4").map((p): H4 => {
        const id = str(p.id, "configuración H4"),
            reason = str(p.reasonCode, "diagnóstico H4");
        const available = bool(p.contextAvailable, "disponibilidad H4");
        check(
            /^EXP-NQ-H4-STRICT-[123]-[123]-V1:[123]:[123]:H[12]$/.test(id) &&
                p.independentlyValidated === false,
            "configuración o validación H4",
        );
        if (p.evidence === null) {
            check(!available, "perfil H4 sin evidencia");
            return {
                id,
                available,
                reason,
                classification: null,
                at: null,
                evidence: [],
                details: [],
            };
        }
        const h = row(p.evidence, "evidencia H4"),
            structure = row(h.marketStructure, "estructura H4");
        safe(h, "banderas H4");
        safe(structure, "banderas de estructura H4");
        check(
            h.configurationId === id &&
                h.candidateId === "EXP-NQ-H4-CONTEXT-V1" &&
                h.candidateVersion === "1.0.0" &&
                structure.candidateId === "EXP-NQ-H4-MARKET-STRUCTURE-V1" &&
                structure.candidateVersion === "1.0.0" &&
                structure.providerId === provider &&
                structure.instrument === instrument,
            "identidad H4",
        );
        const at = utc(h.asOfUtc, "corte H4", cutoff);
        check(
            sameTime(at, preparation) &&
                sameTime(at, utc(structure.asOfUtc, "corte de estructura")),
            "corte H4",
        );
        const evidence = rows(structure.confirmedPivots, "pivotes H4").map(
            (p): Evidence => ({
                id: str(p.candidateId, "identidad de pivote"),
                at: utc(p.confirmedAtUtc, "confirmación H4", at),
                kind:
                    str(p.kind, "tipo de pivote") +
                    " · " +
                    num(p.price, "nivel H4"),
                details: [
                    "Extremo " +
                        bogotaTime(
                            utc(
                                p.extremeOpenUtc,
                                "apertura del extremo H4",
                                at,
                            ),
                        ),
                ],
            }),
        );
        for (const c of rows(h.candleHistory, "contexto por vela H4")) {
            const close = utc(c.observedAtUtc, "contexto observable H4", at);
            for (const side of ["upper", "lower", "upperWick", "lowerWick"]) {
                const subject = row(c[side], "observación H4");
                const kind = str(subject.outcome, "resultado H4");
                if (
                    ![
                        "InsufficientEvidence",
                        "NotApplicable",
                        "NoInteraction",
                    ].includes(kind)
                )
                    evidence.push({
                        id: [id, side, close].join("/"),
                        at: close,
                        kind,
                    });
            }
        }
        for (const event of rows(
            h.delayedHistory,
            "confirmación diferida H4",
        )) {
            const confirmed = utc(
                event.breakoutConfirmedUtc,
                "breakout H4",
                at,
            );
            const time = utc(
                event.observedAtUtc,
                "confirmación diferida H4",
                at,
            );
            check(atMost(confirmed, time), "cronología diferida H4");
            evidence.push({
                id: [id, confirmed, time].join("/"),
                at: time,
                kind:
                    "Confirmación diferida · " +
                    str(event.status, "estado diferido H4"),
            });
        }
        return {
            id,
            available,
            reason,
            at,
            classification: available
                ? str(structure.classification, "clasificación H4")
                : null,
            evidence,
            details: [
                str(structure.highRelation, "relación de máximos") +
                    " / " +
                    str(structure.lowRelation, "relación de mínimos"),
                ...rows(structure.sourceGaps, "brechas H4").map(gapLabel),
            ],
        };
    });
    check(result.length > 0, "configuraciones H4");
    return index(result, (h) => h.id, "configuración H4");
}

function readH1(research: Row, day: string, cutoff: string): Evidence[] {
    const liquidity = rows(
        research.sessionLiquidity,
        "sesiones de liquidez",
    ).map((s): Evidence => {
        check(
            s.operatingDate === day &&
                s.provider === provider &&
                s.instrument === instrument &&
                s.sourceTimeframe === "1h" &&
                s.strategyVersion === "nasdaq-0.3.0-session-candidate",
            "identidad H1",
        );
        const at = optionalUtc(s.availableAtUtc, "disponibilidad H1", cutoff);
        const start = utc(s.startUtc, "inicio H1"),
            end = utc(s.endUtc, "fin H1");
        const scope = str(s.session, "sesión H1");
        check(
            ["Asia", "London"].includes(scope) &&
                timeKey(start) < timeKey(end) &&
                (!at || atMost(end, at)),
            "límites H1",
        );
        const high = decimal(s.high, "máximo H1"),
            low = decimal(s.low, "mínimo H1");
        return {
            id: [s.strategyVersion, day, scope].join("/"),
            at,
            kind: scope === "London" ? "Londres" : "Asia",
            reason: str(s.coverageStatus, "cobertura H1"),
            profile: s.strategyVersion,
            details: [
                "Intervalo " + bogotaTime(start) + " – " + bogotaTime(end),
                "Máximo " +
                    (high ?? "sin evidencia") +
                    " · Mínimo " +
                    (low ?? "sin evidencia"),
            ],
            candles: rows(s.sourceCandles, "velas H1").map((v) =>
                candle(v, at ?? cutoff, "H1"),
            ),
        };
    });
    check(
        liquidity.length === 2 &&
            new Set(liquidity.map((x) => x.kind)).size === 2,
        "sesiones Asia y Londres",
    );
    return liquidity;
}

export function parseSessionReport(value: unknown) {
    const envelope = row(value, "esquema");
    check(envelope.schemaVersion === 1, "versión del esquema");
    const r = row(envelope.research, "replay de investigación");
    safe(r, "banderas de investigación");
    check(
        r.candidateId === integrated &&
            r.version === "1.0.0" &&
            r.provider === provider &&
            r.instrument === instrument &&
            r.replayStrategyVersion === publishedVersion &&
            r.historicalAvailability ===
                "DocumentaryNominalClosure_NotAuthenticated",
        "identidad del replay",
    );
    const day = str(r.operatingDate, "fecha operativa");
    if (protectedDates.has(day))
        throw new Error(
            "Sesión protegida por su exposición. No se puede abrir en esta vista.",
        );
    const dataset = str(r.datasetId, "dataset"),
        runId = str(r.runId, "identificador del replay");
    const replay = row(r.replayCutoff, "corte del replay");
    const cutoff = utc(replay.requestedUtc, "corte del replay");
    const runPrefix = [integrated, dataset, ""].join("/");
    check(
        bogotaTime(cutoff).slice(0, 10) === day &&
            runId.startsWith(runPrefix) &&
            sameTime(
                utc(runId.slice(runPrefix.length), "identificador del corte"),
                cutoff,
            ),
        "fecha o identidad del replay",
    );
    optionalUtc(replay.canonicalFrameUtc, "marco canónico", cutoff);
    const { sources, exposure, readiness, digest } = readSources(
        envelope,
        day,
        cutoff,
        dataset,
    );
    const visible = index(
        rows(replay.sources, "fuentes visibles"),
        (f) => str(f.timeframe, "escala visible"),
        "escala visible",
    );
    const registered = index(
        readiness ? rows(readiness.frames, "cobertura registrada") : [],
        (f) => str(f.timeframe, "escala registrada"),
        "escala registrada",
    );
    const coverage = timeframes.map((timeframe) => {
        const f = lookup(
            visible,
            timeframe[1] + timeframe[0].toLowerCase(),
            "cobertura " + timeframe,
        );
        const ready = readiness
            ? lookup(registered, timeframe, "cobertura registrada " + timeframe)
            : null;
        return {
            timeframe,
            count: count(f.closedCount, "velas visibles"),
            complete: ready
                ? bool(ready.coverageSufficient, "continuidad")
                : null,
            gaps: ready
                ? rows(ready.missingRanges, "brechas").map(gapLabel)
                : [],
            last: optionalUtc(
                f.lastNominalCloseUtc,
                "último cierre visible",
                cutoff,
            ),
        };
    });

    const w = row(r.window, "ventanas");
    safe(w, "banderas de las ventanas");
    check(
        w.candidateConfigurationId === windowVersion &&
            w.timeZone === "America/Bogota" &&
            w.inclusion === "StartInclusive_EndExclusive" &&
            w.lifecycleStatus === "CandidateConfiguration_NotActivated" &&
            sameTime(utc(w.asOfUtc, "corte de ventana"), cutoff),
        "versión de la ventana",
    );
    const candidate = row(w.candidate, "ventana experimental"),
        published = row(w.publishedRuntime, "ventana publicada");
    const preparation = utc(w.preparationUtc, "preparación");
    const start = utc(candidate.startUtc, "inicio de ventana");
    const researchEnd = utc(candidate.endUtc, "fin experimental"),
        publishedEnd = utc(published.endUtc, "fin publicado");
    check(
        sameTime(start, utc(published.startUtc, "inicio publicado")) &&
            [preparation, start, publishedEnd, researchEnd].every((t, i) =>
                sameTime(
                    t,
                    day +
                        "T" +
                        ["13:00", "13:30", "16:00", "16:30"][i] +
                        ":00Z",
                ),
            ),
        "límites de las ventanas",
    );
    const h4 = readH4(
        row(r.preparation, "evidencia de preparación"),
        preparation,
        cutoff,
    );
    const liquidity = readH1(r, day, cutoff);

    const sequence = maybeRow(r.sequence, "secuencia M5"),
        m1 = maybeRow(r.realignment, "realineación M1");
    const sl = maybeRow(r.structuralStop, "referencia SL"),
        tp = maybeRow(r.structuralTakeProfit, "referencia TP");
    const components = [
        [sequence, "EXP-NQ-M5-CONTEXTUAL-SEQUENCE-V1"],
        [m1, "EXP-NQ-M1-REALIGNMENT-V1"],
        [sl, "EXP-NQ-M5-STRUCTURAL-SL-V1"],
        [tp, "EXP-NQ-STRUCTURAL-TP-V1"],
    ] as const;
    for (const [component, candidateId] of components) {
        check(!!component === !!sequence, "componentes sincronizados");
        if (!component) continue;
        safe(component, "banderas de componentes");
        check(
            component.candidateId === candidateId &&
                component.version === "1.0.0" &&
                component.sessionId === dataset &&
                sameTime(
                    utc(component.asOfUtc, "corte de componente"),
                    cutoff,
                ) &&
                (component === sequence ||
                    (component.provider === provider &&
                        component.instrument === instrument)),
            "identidad de componente",
        );
    }
    const episodes: Episode[] = readEpisodes(r, sequence, m1, sl, tp, {
        day,
        cutoff,
        open: start,
        close: researchEnd,
        h4: new Map([...h4].map(([id, p]) => [id, p.available])),
    });
    const s = row(r.summary, "resumen");
    const counts = {
        episodes: count(s.distinctEpisodes, "episodios"),
        takes: count(s.episodesWithObservedLiquidityTake, "tomas"),
        confirmations: count(
            s.distinctSelectedConfirmationEvents,
            "confirmaciones",
        ),
        anchors: count(s.distinctM1Anchors, "anclas"),
        realignments: count(s.distinctRealignmentEvents, "realineaciones"),
        associations: count(
            s.totalResearchConfigurationAssociations,
            "asociaciones",
        ),
        complete: count(s.completeH4ToM1Associations, "cadenas completas"),
        paired: count(s.distinctPairsWithPositiveRewardRisk, "pares R:R"),
    };
    const chains = episodes.flatMap((e) => e.chains),
        geometry = chains.flatMap((c) => c.geometry);
    check(
        counts.episodes === episodes.length &&
            counts.takes === episodes.filter((e) => e.take).length &&
            counts.anchors ===
                new Set(chains.flatMap((c) => (c.anchor ? [c.anchor] : [])))
                    .size &&
            counts.realignments ===
                new Set(
                    chains.flatMap((c) =>
                        c.realignment?.at
                            ? [
                                  timeKey(c.realignment.at) +
                                      "/" +
                                      c.realignment.direction,
                              ]
                            : [],
                    ),
                ).size &&
            counts.associations === geometry.length * h4.size &&
            counts.complete ===
                geometry
                    .flatMap((g) => g.contexts)
                    .reduce(
                        (n, g) => n + (g.completedAt ? g.profiles.length : 0),
                        0,
                    ),
        "recuentos del replay",
    );
    const outcome = str(r.outcome, "resultado del replay");
    check(
        outcome !== "H4ContextUnavailable" ||
            (![...h4.values()].some((p) => p.available) &&
                counts.complete === 0),
        "resultado contradictorio H4",
    );
    return {
        day,
        cutoff,
        dataset,
        runId,
        instrument,
        provider,
        exposure,
        digest,
        provenance: str(r.sourceProvenance, "procedencia"),
        candidate: integrated,
        version: r.version,
        publishedVersion,
        windowVersion,
        window: { start, researchEnd, publishedEnd },
        preparation,
        sources,
        h4: [...h4.values()],
        liquidity,
        coverage,
        episodes,
        counts,
        outcome,
        blockers: readiness ? strings(readiness.blockers, "bloqueos") : [],
        actions: readiness
            ? strings(readiness.acquisitionActions, "acciones")
            : [],
        registered: readiness !== null,
        timestampVerified: readiness?.timestampEvidenceVerified === true,
        versions: components
            .filter(([c]) => c)
            .map(([c, id]) => id + " " + c?.version),
    };
}

export type SessionReport = ReturnType<typeof parseSessionReport>;
