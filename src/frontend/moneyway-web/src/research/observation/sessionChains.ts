import {
    atMost,
    bogotaTime,
    bool,
    candle,
    check,
    decimal,
    gapLabel,
    index,
    instrument,
    lookup,
    maybeRow,
    num,
    optional,
    optionalUtc,
    provider,
    row,
    rows,
    sameTime,
    str,
    strings,
    timeKey,
    utc,
    type Evidence,
    type Row,
} from "./sessionEvidence";

export type Structural = {
    at: string;
    direction: string;
    close: number;
    price: number | null;
    distance: number | null;
    reason: string;
    profile: string;
    references: Evidence[];
    gaps: string[];
    protective?: boolean;
};
export type Geometry = {
    id: string;
    slProfile: string;
    tpProfile: string;
    stop: Structural | null;
    target: Structural | null;
    ratio: number | null;
    ratioReason: string | null;
    outcome: string;
    completedAt: string | null;
    contexts: {
        profiles: string[];
        outcome: string;
        completedAt: string | null;
    }[];
};
export type Chain = {
    key: string;
    configuration: string;
    m1Profile: string;
    reason: string;
    m1Reason: string;
    firstOutcome: string;
    confirmations: Evidence[];
    zones: Evidence[];
    invalidations: Evidence[];
    branchStates: {
        id: string;
        atRealignment: string | null;
        atCutoff: string;
    }[];
    countertrend: Evidence[];
    realignment: Evidence | null;
    anchor: string | null;
    geometry: Geometry[];
};
export type Episode = {
    id: string;
    category: string;
    policy: string;
    reason: string;
    take: Evidence | null;
    chains: Chain[];
};
type Scope = {
    day: string;
    cutoff: string;
    open: string;
    close: string;
    h4: ReadonlyMap<string, boolean>;
};

function readScan(scan: Row, scope: Scope) {
    const a = row(scan.anchor, "ancla M1");
    const anchor = utc(a.atUtc, "ancla M1", scope.cutoff);
    check(
        a.operatingDate === scope.day &&
            sameTime(utc(a.windowOpenUtc, "inicio M1"), scope.open) &&
            sameTime(utc(a.windowCloseUtc, "fin M1"), scope.close),
        "ventana del ancla M1",
    );
    const attempts = index(
        rows(scan.attempts, "intentos M1").map((v) => {
            const observed = utc(
                v.observedAtUtc,
                "contratendencia",
                scope.cutoff,
            );
            const confirmation = optionalUtc(
                v.confirmedAtUtc,
                "realineación",
                scope.cutoff,
            );
            const close =
                v.realignment === null
                    ? null
                    : candle(v.realignment, scope.cutoff, "M1");
            const first = candle(v.first, observed, "M1");
            check(
                sameTime(confirmation, close?.closeTimeUtc ?? null) &&
                    atMost(anchor, first.openTimeUtc) &&
                    sameTime(first.closeTimeUtc, observed) &&
                    (!confirmation ||
                        timeKey(observed) < timeKey(confirmation)),
                "cierre M1 de realineación",
            );
            return {
                id: str(v.id, "intento M1"),
                observed,
                confirmation,
                close,
                first,
                last: candle(
                    v.lastIncluded,
                    confirmation ?? scope.cutoff,
                    "M1",
                ),
                eligible: bool(v.windowEligible, "elegibilidad documental M1"),
                status: str(v.status, "estado M1"),
                range:
                    num(v.low, "mínimo M1") + " – " + num(v.high, "máximo M1"),
            };
        }),
        (v) => v.id,
        "intento M1",
    );
    const firstIds = strings(
        scan.firstRealignmentIds,
        "realineaciones elegibles",
    );
    const first = optionalUtc(
        scan.firstRealignmentUtc,
        "primera realineación",
        scope.cutoff,
    );
    check(
        (first !== null) === firstIds.length > 0 &&
            firstIds.every((id) => {
                const v = attempts.get(id);
                return (
                    v?.eligible &&
                    sameTime(v.confirmation, first) &&
                    first &&
                    atMost(scope.open, first) &&
                    timeKey(first) < timeKey(scope.close)
                );
            }),
        "primera realineación elegible",
    );
    return {
        anchor,
        anchorId: str(a.id, "identidad del ancla"),
        profile: str(scan.profile, "perfil M1"),
        direction: str(a.direction, "dirección M1"),
        attempts,
        firstIds,
        gaps: rows(scan.sourceGaps, "brechas M1").map(gapLabel),
    };
}

function structural(v: Row, target: boolean, cutoff: string): Structural {
    const at = utc(v.asOfUtc, "corte SL/TP", cutoff);
    const m1 = row(v.closeReference, "precio hipotético");
    const close = num(m1.price, "precio hipotético");
    const evidence = candle(m1.candle, at, "M1");
    check(
        sameTime(at, evidence.closeTimeUtc) && close === evidence.close,
        "origen del precio hipotético",
    );
    const profile = str(
        row(v.profile, "perfil SL/TP").id,
        "identidad de perfil SL/TP",
    );
    const selected = target ? null : maybeRow(v.selected, "pivote SL");
    const pivot = selected ? row(selected.pivot, "pivote SL") : null;
    if (pivot) check(pivot.profileId === profile, "perfil del pivote SL");
    const price = target
        ? decimal(v.targetPrice, "precio TP")
        : pivot
          ? num(pivot.price, "precio SL")
          : null;
    const distance = decimal(
        target ? v.rewardDistancePoints : v.distancePoints,
        "distancia SL/TP",
    );
    check(distance === null || distance > 0, "distancia SL/TP");
    const references: Evidence[] = pivot
        ? [
              {
                  id: str(pivot.id, "identidad SL"),
                  at: utc(pivot.confirmedAtUtc, "confirmación SL", at),
                  kind: str(selected!.relation, "tipo SL"),
                  details: [
                      "Cierre de la vela del extremo " +
                          bogotaTime(
                              utc(pivot.extremeCloseUtc, "extremo SL", at),
                          ),
                  ],
              },
          ]
        : [];
    if (target) {
        const candidates = index(
            rows(v.candidates, "candidatos TP"),
            (c) => str(c.id, "identidad TP"),
            "objetivo",
        );
        for (const id of strings(v.selectedIds, "objetivos seleccionados")) {
            const c = lookup(candidates, id, "objetivo seleccionado");
            const timeframe = str(c.timeframe, "escala del TP");
            check(
                c.provider === provider &&
                    c.instrument === instrument &&
                    c.eligibleAtCutoff === true &&
                    c.favorableDirection === true &&
                    c.price === price &&
                    ["5m", "1h"].includes(timeframe),
                "identidad del objetivo",
            );
            references.push({
                id,
                at: utc(c.availableAtUtc, "disponibilidad de objetivo", at),
                kind: str(c.category, "tipo TP"),
                timeframe: timeframe === "1h" ? "H1" : "M5",
                details: [
                    "Fuente " +
                        bogotaTime(utc(c.sourceTimestampUtc, "fuente TP", at)),
                    ...(c.priorInteraction
                        ? [
                              str(
                                  row(c.priorInteraction, "interacción TP")
                                      .status,
                                  "interacción TP",
                              ),
                          ]
                        : []),
                ],
            });
        }
        check(
            (price !== null) === references.length > 0,
            "precio TP sin referencia",
        );
    }
    return {
        at,
        close,
        price,
        distance,
        profile,
        references,
        gaps: rows(
            target ? v.pivotSourceGaps : v.sourceGaps,
            "brechas SL/TP",
        ).map(gapLabel),
        direction: str(v.direction, "dirección SL/TP"),
        reason: str(v.reasonCode, "diagnóstico SL/TP"),
        protective:
            !target && typeof v.onProtectiveSide === "boolean"
                ? v.onProtectiveSide
                : undefined,
    };
}

export function readEpisodes(
    research: Row,
    sequence: Row | null,
    m1: Row | null,
    sl: Row | null,
    tp: Row | null,
    scope: Scope,
): Episode[] {
    const { cutoff, day } = scope;
    const confirmations = index(
        (sequence
            ? rows(sequence.confirmationEvidence, "confirmaciones")
            : []
        ).map((c): Evidence => {
            const at = utc(c.confirmedAtUtc, "confirmación M5", cutoff);
            return {
                id: str(c.id, "identidad de confirmación"),
                at,
                kind: str(c.kind, "tipo de confirmación"),
                direction: str(c.direction, "dirección M5"),
                profile:
                    str(c.candidateId, "candidato M5") +
                    " · " +
                    str(c.configuration, "perfil M5"),
                reason: str(c.evidenceQuality, "calidad de confirmación"),
                details: [str(c.sourceReference, "referencia M5")],
                candles: [candle(c.evidenceCandle, at, "M5")],
            };
        }),
        (e) => e.id,
        "confirmación",
    );
    const zones = index(
        (sequence ? rows(sequence.zones, "zonas") : []).map((z): Evidence => {
            const f = row(z.formation, "formación FVG");
            const at = utc(f.formationUtc, "formación FVG", cutoff);
            return {
                id: str(f.id, "identidad FVG"),
                at,
                kind: "FVG direccional",
                direction: str(f.originalDirection, "dirección FVG"),
                profile: str(f.profile, "perfil FVG"),
                details: [
                    "Zona " +
                        num(f.lowerBoundary, "límite FVG") +
                        " – " +
                        num(f.upperBoundary, "límite FVG"),
                ],
                candles: [f.c1, f.c2, f.c3].map((v) => candle(v, at, "M5")),
            };
        }),
        (e) => e.id,
        "FVG",
    );
    const episodes = (sequence ? rows(sequence.episodes, "episodios") : []).map(
        (e): Episode => {
            const s = row(e.selection, "selección de liquidez");
            const ref = row(s.liquidityReference, "referencia de liquidez");
            check(
                e.operatingDate === day &&
                    ref.operatingDate === day &&
                    ref.provider === provider &&
                    ref.instrument === instrument,
                "identidad del episodio",
            );
            const event = maybeRow(s.event, "toma de liquidez");
            let take: Evidence | null = null;
            if (event) {
                const at = utc(event.availableAtUtc, "toma observable", cutoff);
                const c = candle(event.candle, at, "M5");
                check(
                    event.referenceId === ref.id &&
                        sameTime(at, c.closeTimeUtc),
                    "origen de toma",
                );
                take = {
                    id: str(event.id, "identidad de toma"),
                    at,
                    kind: "Toma observada",
                    candles: [c],
                    profile: str(s.policyId, "perfil de toma"),
                    details: [
                        str(ref.id, "nivel de liquidez"),
                        "Nivel " + num(ref.price, "precio de liquidez"),
                    ],
                };
            }
            return {
                id: str(e.id, "episodio"),
                category: str(ref.category, "categoría"),
                policy: str(s.policyId, "selección de liquidez"),
                reason: str(s.reason, "diagnóstico de liquidez"),
                take,
                chains: [],
            };
        },
    );
    const episodeIndex = index(episodes, (e) => e.id, "episodio");
    const scanIndex = index(
        m1 ? rows(m1.scans, "escaneos M1") : [],
        (s) => str(s.id, "identidad M1"),
        "escaneo M1",
    );
    const scans = new Map<string, ReturnType<typeof readScan>>();
    const stopRows = sl ? row(sl.evaluations, "evaluaciones SL") : {};
    const targetRows = tp ? row(tp.evaluations, "evaluaciones TP") : {};
    const ratioRows = tp ? row(tp.rewardRiskComparisons, "razones SL/TP") : {};
    const observations = new Map<string, Structural>();
    function getStructural(id: string, target: boolean) {
        const key = (target ? "TP/" : "SL/") + id;
        const value =
            observations.get(key) ??
            structural(
                row(
                    (target ? targetRows : stopRows)[id],
                    "referencia estructural",
                ),
                target,
                cutoff,
            );
        observations.set(key, value);
        return value;
    }
    const uniqueChains = new Set<string>();
    for (const c of rows(research.chains, "asociaciones del replay")) {
        const episode = lookup(
            episodeIndex,
            str(c.episodeId, "episodio asociado"),
            "episodio asociado",
        );
        const configuration = str(c.m5ConfigurationId, "configuración M5"),
            m1Profile = str(c.m1Profile, "perfil M1");
        check(
            /^(StrictAfterSweep|SameCloseDiagnostic)\/(PostSweepObservation|WindowEventOnly|WindowConfirmationWithPriorSweep)\/(FormationAfterConfirmation|FormationSequenceAfterConfirmation)\/(GeometryOnly|MiddleCloseConfirmation)\/08:30:00\.0000000-11:30:00\.0000000$/.test(
                configuration,
            ) &&
                ["BodyDirectionTurn", "PullbackRangeReclaim"].includes(
                    m1Profile,
                ),
            "configuración M5/M1",
        );
        const scanId = optional(c.scanId, "escaneo M1"),
            realignmentId = optional(c.realignmentId, "realineación M1");
        const time = optionalUtc(
            c.realignmentUtc,
            "realineación asociada",
            cutoff,
        );
        const anchorTime = optionalUtc(
            c.directionalFvgUtc,
            "FVG asociado",
            cutoff,
        );
        const firstAt = optionalUtc(
            c.firstConfirmationUtc,
            "primera confirmación",
            cutoff,
        );
        const takeAt = optionalUtc(c.liquidityTakeUtc, "toma asociada", cutoff);
        check(
            sameTime(takeAt, episode.take?.at ?? null) &&
                (!firstAt || (takeAt && timeKey(takeAt) < timeKey(firstAt))) &&
                (!anchorTime ||
                    (firstAt && timeKey(firstAt) < timeKey(anchorTime))),
            "cronología toma, confirmación y FVG",
        );
        const first = strings(
            c.firstConfirmationIds,
            "primeras confirmaciones",
        ).map((id) => {
            const event = lookup(confirmations, id, "confirmación vinculada");
            check(
                sameTime(event.at, firstAt),
                "tiempo de confirmación vinculada",
            );
            return event;
        });
        check(
            first.length > 0 === (firstAt !== null),
            "primera confirmación sin identidad",
        );
        const branchZones = new Map<string, Evidence>();
        const invalidations: Evidence[] = [];
        const branchStates = rows(c.branches, "ramas M5").map((b) => {
            const bind = row(b.binding, "vínculo M5"),
                id = str(bind.fvgId, "FVG vinculado");
            const fvg = lookup(zones, id, "FVG vinculado");
            check(
                sameTime(fvg.at, anchorTime) &&
                    first.some((e) => e.id === bind.confirmationId),
                "identidad de rama M5",
            );
            branchZones.set(id, fvg);
            for (const zone of [
                row(b.fvg, "cronología FVG"),
                maybeRow(b.ifvg, "cronología IFVG"),
            ]) {
                if (!zone) continue;
                const zoneId = str(zone.zoneId, "zona invalidada"),
                    kind = str(zone.role, "rol de zona");
                check(
                    kind === "DirectionalFVG"
                        ? zoneId === id
                        : kind === "IFVG" && zones.has(zoneId),
                    "identidad de zona",
                );
                const invalidated = optionalUtc(
                    zone.invalidatedAtUtc,
                    "invalidación",
                    cutoff,
                );
                const limited = optionalUtc(
                    zone.evidenceLimitedAtUtc,
                    "cobertura limitada",
                    cutoff,
                );
                if (invalidated)
                    invalidations.push({
                        id: zoneId,
                        at: invalidated,
                        kind: "Invalidación " + kind,
                    });
                if (limited)
                    invalidations.push({
                        id: zoneId,
                        at: limited,
                        kind: "Evidencia limitada " + kind,
                    });
            }
            return {
                id,
                atRealignment: optional(
                    b.stateAtRealignment,
                    "estado M5 en M1",
                ),
                atCutoff: str(b.stateAtCutoff, "estado M5 al corte"),
            };
        });
        let realignment: Evidence | null = null;
        let countertrend: Evidence[] = [];
        if (scanId) {
            const scan =
                scans.get(scanId) ??
                readScan(
                    lookup(scanIndex, scanId, "escaneo M1 vinculado"),
                    scope,
                );
            scans.set(scanId, scan);
            check(
                scan.anchorId === c.m1AnchorId &&
                    scan.profile === m1Profile &&
                    sameTime(scan.anchor, anchorTime) &&
                    branchZones.size > 0,
                "identidad del ancla M1",
            );
            const attempt = realignmentId
                ? lookup(scan.attempts, realignmentId, "realineación vinculada")
                : null;
            if (attempt) {
                check(
                    attempt.close &&
                        sameTime(attempt.confirmation, time) &&
                        scan.firstIds.includes(attempt.id),
                    "primera realineación vinculada",
                );
                realignment = {
                    id: attempt.id,
                    at: time,
                    kind: "Realineación observada",
                    direction: scan.direction,
                    profile: m1Profile,
                    candles: [attempt.close],
                    details: scan.gaps,
                };
            } else
                check(
                    scan.firstIds.length === 0 && time === null,
                    "realineación ausente",
                );
            countertrend = (
                attempt ? [attempt] : [...scan.attempts.values()]
            ).map((a) => ({
                id: a.id,
                at: a.observed,
                kind: "Contratendencia observada",
                direction: scan.direction,
                reason: a.status,
                profile: m1Profile,
                candles: [a.first, a.last],
                details: ["Rango congelado " + a.range, ...scan.gaps],
            }));
        } else
            check(
                realignmentId === null &&
                    time === null &&
                    c.m1AnchorId === null &&
                    anchorTime === null,
                "ancla ausente",
            );
        const key = [
            episode.id,
            configuration,
            m1Profile,
            realignmentId ?? "",
        ].join("|");
        check(!uniqueChains.has(key), "asociación duplicada");
        uniqueChains.add(key);
        const geometry = rows(c.geometry, "configuraciones SL/TP").map(
            (g): Geometry => {
                const id = str(g.configurationId, "identidad SL/TP");
                const slProfile = str(g.stopProfileId, "perfil SL"),
                    tpProfile = str(g.targetProfileId, "perfil TP");
                check(
                    id ===
                        [configuration, m1Profile, slProfile, tpProfile].join(
                            "/",
                        ),
                    "configuración SL/TP inconsistente",
                );
                const stopId = optional(g.stopEvaluationId, "evaluación SL"),
                    targetId = optional(g.targetEvaluationId, "evaluación TP");
                const stop = stopId ? getStructural(stopId, false) : null,
                    target = targetId ? getStructural(targetId, true) : null;
                check(
                    (!stop ||
                        (sameTime(stop.at, time) &&
                            stop.profile === slProfile &&
                            stop.direction === realignment?.direction)) &&
                        (!target ||
                            (sameTime(target.at, time) &&
                                target.profile === tpProfile &&
                                target.direction === realignment?.direction)) &&
                        (!stop || !target || stop.close === target.close),
                    "vínculo causal SL/TP",
                );
                const pairId = optional(
                    g.rewardRiskComparisonId,
                    "comparación SL/TP",
                );
                let ratio = null,
                    ratioReason = null;
                if (pairId) {
                    const comparison = row(
                        ratioRows[pairId],
                        "comparación SL/TP",
                    );
                    check(
                        stop &&
                            target &&
                            comparison.stopEvaluationId === stopId &&
                            comparison.targetEvaluationId === targetId &&
                            row(comparison.stopProfile, "perfil de comparación")
                                .id === slProfile,
                        "identidad de comparación SL/TP",
                    );
                    const pairs = rows(comparison.pairs, "pares SL/TP");
                    check(
                        pairs.length &&
                            pairs.every(
                                (p) =>
                                    target.references.some(
                                        (r) => r.id === p.targetId,
                                    ) &&
                                    p.hypotheticalRewardRisk ===
                                        pairs[0].hypotheticalRewardRisk,
                            ),
                        "objetivos emparejados",
                    );
                    ratio = decimal(
                        pairs[0].hypotheticalRewardRisk,
                        "R:R hipotético",
                    );
                    ratioReason = str(pairs[0].reasonCode, "diagnóstico R:R");
                    check(
                        ratio === null ||
                            (ratio > 0 &&
                                stop.distance !== null &&
                                target.distance !== null),
                        "R:R sin referencias válidas",
                    );
                }
                const contexts = rows(
                    g.contextOutcomes,
                    "perfiles de contexto",
                ).map((o) => {
                    const profiles = strings(
                        o.h4ConfigurationIds,
                        "identidades H4",
                    );
                    const completedAt = optionalUtc(
                        o.completedAtUtc,
                        "finalización histórica",
                        cutoff,
                    );
                    check(
                        !completedAt ||
                            (sameTime(completedAt, time) &&
                                profiles.every(
                                    (id) => scope.h4.get(id) === true,
                                )),
                        "finalización histórica incompatible",
                    );
                    return {
                        profiles,
                        completedAt,
                        outcome: str(o.outcome, "resultado de contexto"),
                    };
                });
                const all = contexts.flatMap((v) => v.profiles);
                check(
                    all.length === scope.h4.size &&
                        new Set(all).size === all.length &&
                        all.every((id) => scope.h4.has(id)),
                    "identidades H4 asociadas",
                );
                const completedAt = optionalUtc(
                    g.structuralReferencesCompletedAtUtc,
                    "geometría histórica",
                    cutoff,
                );
                check(
                    !completedAt || sameTime(completedAt, time),
                    "tiempo de geometría histórica",
                );
                return {
                    id,
                    slProfile,
                    tpProfile,
                    stop,
                    target,
                    ratio,
                    ratioReason,
                    completedAt,
                    outcome: str(g.downstreamOutcome, "resultado estructural"),
                    contexts,
                };
            },
        );
        check(
            geometry.length &&
                new Set(geometry.map((g) => g.id)).size === geometry.length,
            "geometría duplicada o ausente",
        );
        episode.chains.push({
            key,
            configuration,
            m1Profile,
            reason: str(c.m5Reason, "diagnóstico M5"),
            m1Reason: str(c.m1Reason, "diagnóstico M1"),
            firstOutcome: str(
                c.firstConfirmationOutcome,
                "primera confirmación",
            ),
            confirmations: first,
            zones: [...branchZones.values()],
            realignment,
            countertrend,
            geometry,
            invalidations,
            branchStates,
            anchor: optional(c.m1AnchorId, "identidad del ancla"),
        });
    }
    return episodes.sort(
        (a, b) =>
            a.category.localeCompare(b.category) ||
            a.policy.localeCompare(b.policy) ||
            a.id.localeCompare(b.id),
    );
}
