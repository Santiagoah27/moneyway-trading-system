import {
    groupEvents,
    importEventReview,
    type EventRevision,
} from "../integratedReview/eventReview";
import {
    frames,
    isSha256,
    objectWith,
    requireValue,
    seal,
    sha256,
    unseal,
    type OriginalPackage,
} from "../integratedReview/originalPackage";
import { targets, type Target } from "../integratedReview/reviewHistory";
import {
    atMost,
    row,
    sameTime,
    timeKey,
    utc,
} from "../observation/sessionEvidence";
import {
    maxReportBytes,
    parseSessionReport,
} from "../observation/sessionReport";
import {
    candleKey,
    candidateEvents,
    type CandidateEvent,
} from "./candidateEvidence";

const manifestSchema = "moneyway.nasdaq-evidence-comparison-manifest.v1";
const outputSchema = "moneyway.nasdaq-evidence-comparison.v1";
const candidateId = "EXP-NQ-END-TO-END-MTF-V1";
const candidateVersion = "1.1.0";
const windowId = "EXP-NQ-TRADING-WINDOW-1130-V1";

type Manifest = {
    schema: typeof manifestSchema;
    packageSha256: string;
    reviewSha256: string;
    reportSha256: string;
    sessionId: string;
    operatingDate: string;
    cutoffUtc: string;
    exposure: "Development" | "Synthetic";
    provider: string;
    instrument: string;
    candidateId: typeof candidateId;
    candidateVersion: typeof candidateVersion;
    windowId: typeof windowId;
    configurationId: string | null;
    observationTypes: Target[];
};

export async function prepareComparison(
    p: OriginalPackage,
    reviewText: string,
    reportText: string,
    manifestText: string,
) {
    const manifest = objectWith(
        JSON.parse(manifestText.replace(/^\uFEFF/, "")),
        "schema packageSha256 reviewSha256 reportSha256 sessionId operatingDate cutoffUtc exposure provider instrument candidateId candidateVersion windowId configurationId observationTypes",
        "manifiesto de comparación",
    );
    requireValue(
        manifest.schema === manifestSchema &&
            manifest.packageSha256 === p.sha256 &&
            manifest.sessionId === p.sessionId &&
            manifest.operatingDate === p.operatingDate &&
            manifest.exposure === p.exposure &&
            manifest.provider === p.provider &&
            manifest.instrument === p.instrument &&
            sameTime(
                utc(manifest.cutoffUtc, "corte del manifiesto"),
                p.cutoffUtc,
            ) &&
            manifest.candidateId === candidateId &&
            manifest.candidateVersion === candidateVersion &&
            manifest.windowId === windowId &&
            isSha256(manifest.reportSha256) &&
            isSha256(manifest.reviewSha256) &&
            (manifest.configurationId === null ||
                (typeof manifest.configurationId === "string" &&
                    manifest.configurationId.length > 0)) &&
            Array.isArray(manifest.observationTypes) &&
            manifest.observationTypes.length > 0 &&
            manifest.observationTypes.every(
                (v) => typeof v === "string" && Object.hasOwn(targets, v),
            ) &&
            new Set(manifest.observationTypes).size ===
                manifest.observationTypes.length,
        "Manifiesto incompatible con la fuente, corte, universo o versión del candidato.",
    );
    requireValue(
        (p.exposure === "Development" &&
            p.operatingDate === "2026-06-23" &&
            p.sessionId === "nasdaq-context-20260623-v1") ||
            (p.exposure === "Synthetic" &&
                p.operatingDate === "2000-01-03" &&
                p.sessionId === "synthetic-us100-v1"),
        "Partición protegida o exposición fuera del alcance.",
    );
    const review = await importEventReview(p, null, reviewText);
    requireValue(
        reportText.length <= maxReportBytes &&
            (await sha256(reviewText)) === manifest.reviewSha256 &&
            (await sha256(reportText)) === manifest.reportSha256,
        "El SHA-256 de las etiquetas o del informe no coincide con el manifiesto.",
    );

    const raw = row(
        JSON.parse(reportText.replace(/^\uFEFF/, "")),
        "informe integrado",
    );
    const header = row(raw.research, "identidad del candidato");
    requireValue(
        header.operatingDate === p.operatingDate &&
            header.datasetId === p.sessionId &&
            header.version === candidateVersion &&
            raw.evidenceReadiness === undefined &&
            raw.registrationSha256 === undefined,
        "Solo se admite el informe documental del Development fijado o el fixture sintético autorizado.",
    );
    const report = parseSessionReport(raw, {
        allowSynthetic: p.exposure === "Synthetic",
    });
    requireValue(
        report.exposure === p.exposure &&
            report.digest === p.provenanceSha256 &&
            report.provider === p.provider &&
            report.instrument === p.instrument &&
            sameTime(report.cutoff, p.cutoffUtc),
        "El informe tiene otra procedencia, exposición, instrumento o corte.",
    );
    requireValue(
        report.sources.length === frames.length &&
            frames.every((tf) =>
                report.sources.some(
                    (s) =>
                        s.timeframe === tf &&
                        s.raw === p.sources[tf].originalSha256,
                ),
            ),
        "Los hashes de las cuatro fuentes originales deben coincidir exactamente.",
    );
    requireValue(
        report.coverage.every(({ timeframe, count, last }) => {
            const s = p.sources[timeframe as keyof OriginalPackage["sources"]];
            return (
                count === s.bars.length &&
                sameTime(last, s.coverage.lastClosedUtc)
            );
        }),
        "El prefijo de fuentes visibles no coincide con el paquete original.",
    );

    const scope = manifest as Manifest;
    return {
        manifest: scope,
        manifestSha256: await sha256(manifestText),
        review,
        reportRunId: report.runId,
        candidates: candidateEvents(p, report, scope.configurationId),
        h4UnavailableReasons: [
            ...new Set(
                report.h4.filter((h) => !h.available).map((h) => h.reason),
            ),
        ].sort(),
        availability: "DocumentaryNominalClosure_NotAuthenticated" as const,
    };
}
export type Comparison = Awaited<ReturnType<typeof prepareComparison>>;

export const decisionKinds = [
    "Confirmed",
    "DoesNotCorrespond",
    "Ambiguous",
    "InsufficientEvidence",
] as const;
export type DecisionKind = (typeof decisionKinds)[number];
export const exposureKinds = [
    "Unknown",
    "PreviouslyExposed",
    "DeclaresNoPriorResults",
] as const;
export type Disclosure = {
    reviewerId: string;
    declaredExposure: (typeof exposureKinds)[number];
    openedAtUtc: string;
    firstOpenedAtUtc: string;
};
export type Decision = {
    revision: number;
    targetId: string;
    humanRevision: number;
    candidateEventId: string;
    kind: DecisionKind;
    note: string;
    reviewerId: string;
    recordedAtUtc: string;
};

export type EvidenceRelationship =
    | "ExactDocumentary"
    | "PartialDocumentary"
    | "DifferentEvidence"
    | "InsideNoReferenceScope"
    | "OutsideNoReferenceScope"
    | "InsufficientTemporalEvidence"
    | "NotComparable"
    | "HumanJudgmentUnresolved";

export function relationship(
    human: EventRevision,
    candidate: CandidateEvent,
): EvidenceRelationship {
    if (candidate.type !== human.observation || human.kind === "Skip")
        return "NotComparable";
    if (
        !candidate.at ||
        !atMost(candidate.at, human.viewingCutoffUtc) ||
        candidate.references.length === 0 ||
        candidate.references.some(
            (r) => !atMost(r.closeUtc, human.viewingCutoffUtc),
        )
    )
        return "InsufficientTemporalEvidence";
    if (human.kind === "NoReference") {
        if (!human.scopeStartUtc || !atMost(human.scopeStartUtc, candidate.at))
            return "OutsideNoReferenceScope";
        return candidate.references.every((r) =>
            atMost(human.scopeStartUtc!, r.openUtc),
        )
            ? "InsideNoReferenceScope"
            : "OutsideNoReferenceScope";
    }
    if (human.kind !== "Reference") return "HumanJudgmentUnresolved";
    const keys = new Set(candidate.references.map(candleKey));
    const matches = human.references.filter((r) =>
        keys.has(candleKey(r)),
    ).length;
    return matches === human.references.length
        ? "ExactDocumentary"
        : matches > 0
          ? "PartialDocumentary"
          : "DifferentEvidence";
}

export function humanRows(comparison: Comparison) {
    return [...groupEvents(comparison.review.labels).values()].map(
        (history) => {
            const selected = history.at(-1)!;
            const preReveal =
                history.filter((l) => l.phase === "PreRevealJudgment").at(-1) ??
                null;
            const inScope = comparison.manifest.observationTypes.includes(
                selected.observation,
            );
            return { selected, preReveal, history, inScope };
        },
    );
}

export function recordDisclosure(
    c: Comparison,
    reviewerId: string,
    declaredExposure: Disclosure["declaredExposure"],
    now: string,
    first = now,
): Disclosure {
    const name = reviewerId.trim();
    requireValue(
        name.length > 0 &&
            name.length <= 120 &&
            [...name].every(
                (ch) => ch.charCodeAt(0) >= 32 && ch.charCodeAt(0) !== 127,
            ) &&
            exposureKinds.includes(declaredExposure),
        "Declare el revisor y su exposición antes de abrir resultados.",
    );
    utc(now, "hora de apertura");
    utc(first, "primera apertura local", now);
    requireValue(
        atMost(
            c.review.labels.at(-1)?.reviewedAtUtc ?? c.review.declaredAtUtc,
            now,
        ) &&
            (!c.review.reveal || atMost(c.review.reveal.recordedAtUtc, now)),
        "La apertura es anterior a la revisión o revelación registrada.",
    );
    return {
        reviewerId: name,
        declaredExposure,
        openedAtUtc: now,
        firstOpenedAtUtc: first,
    };
}

export function appendDecision(
    c: Comparison,
    disclosure: Disclosure,
    history: Decision[],
    targetId: string,
    candidateEventId: string,
    kind: DecisionKind,
    note: string,
    now: string,
): Decision {
    const human = humanRows(c).find(
        (r) => r.inScope && r.selected.targetId === targetId,
    )?.selected;
    const candidate = c.candidates.find((e) => e.id === candidateEventId);
    requireValue(
        human && candidate && human.observation === candidate.type,
        "La asociación no identifica dos eventos del mismo tipo y alcance.",
    );
    const evidence = relationship(human, candidate);
    requireValue(
        decisionKinds.includes(kind) &&
            evidence !== "NotComparable" &&
            evidence !== "OutsideNoReferenceScope" &&
            (evidence !== "InsufficientTemporalEvidence" ||
                kind === "InsufficientEvidence") &&
            (evidence !== "HumanJudgmentUnresolved" ||
                kind === "Ambiguous" ||
                kind === "InsufficientEvidence") &&
            (kind !== "Confirmed" || evidence === "ExactDocumentary"),
        "La decisión excede el alcance documental, temporal o la clasificación humana.",
    );
    utc(now, "hora de decisión");
    requireValue(
        note.trim().length > 0 &&
            note.trim().length <= 2000 &&
            atMost(
                history.at(-1)?.recordedAtUtc ?? disclosure.openedAtUtc,
                now,
            ),
        "Indique evidencia escrita y una hora de revisión consecutiva.",
    );
    return {
        revision: history.length + 1,
        targetId,
        humanRevision: human.revision,
        candidateEventId,
        kind,
        note: note.trim(),
        reviewerId: disclosure.reviewerId,
        recordedAtUtc: now,
    };
}

export function outcomes(c: Comparison, decisions: Decision[]) {
    const humans = humanRows(c);
    const scoped = c.candidates.filter((e) =>
        c.manifest.observationTypes.includes(e.type),
    );
    const pairs = humans.flatMap((h) =>
        h.inScope
            ? scoped
                  .filter((e) => e.type === h.selected.observation)
                  .map((e) => ({
                      targetId: h.selected.targetId,
                      candidateEventId: e.id,
                      relationship: relationship(h.selected, e),
                  }))
            : [],
    );
    const latest = [
        ...new Map(
            decisions.map((d) => [
                JSON.stringify([d.targetId, d.candidateEventId]),
                d,
            ]),
        ).values(),
    ];
    const assigned = new Set(latest.map((d) => d.targetId));
    const usedCandidates = new Set(latest.map((d) => d.candidateEventId));
    const comparisonRelationships = new Set<EvidenceRelationship>([
        "ExactDocumentary",
        "PartialDocumentary",
        "DifferentEvidence",
        "InsideNoReferenceScope",
    ]);
    const comparable = pairs.filter((p) =>
        comparisonRelationships.has(p.relationship),
    );
    return {
        humanEvents: humans.length,
        humanRevisions: c.review.labels.length,
        scopedHumanEvents: humans.filter((h) => h.inScope).length,
        candidateEvents: c.candidates.length,
        scopedCandidateEvents: scoped.length,
        candidateConfigurationAssociations: scoped.reduce(
            (n, e) => n + e.associationCount,
            0,
        ),
        comparableHumanEvents: new Set(comparable.map((p) => p.targetId)).size,
        comparableCandidateEvents: new Set(
            comparable.map((p) => p.candidateEventId),
        ).size,
        comparablePairs: comparable.length,
        exactPairs: pairs.filter((p) => p.relationship === "ExactDocumentary")
            .length,
        temporalInsufficiencyPairs: pairs.filter(
            (p) => p.relationship === "InsufficientTemporalEvidence",
        ).length,
        decisions: latest.length,
        confirmed: latest.filter((d) => d.kind === "Confirmed").length,
        discrepancies: latest.filter((d) => d.kind === "DoesNotCorrespond")
            .length,
        ambiguous: latest.filter((d) => d.kind === "Ambiguous").length,
        insufficient: latest.filter((d) => d.kind === "InsufficientEvidence")
            .length,
        humanUnassociated: humans.filter(
            (h) => h.inScope && !assigned.has(h.selected.targetId),
        ).length,
        candidateUnassociated: scoped.filter((e) => !usedCandidates.has(e.id))
            .length,
        humansOutsideUniverse: humans.filter(
            (h) =>
                !h.inScope ||
                h.selected.kind === "Skip" ||
                !scoped.some((e) => e.type === h.selected.observation),
        ).length,
        candidatesOutsideUniverse: c.candidates.length - scoped.length,
    };
}

export function exportComparison(
    c: Comparison,
    disclosure: Disclosure,
    decisions: Decision[],
) {
    const linked = new Set(decisions.map((d) => d.candidateEventId));
    return seal(outputSchema, {
        manifest: c.manifest,
        manifestSha256: c.manifestSha256,
        reportRunId: c.reportRunId,
        disclosure,
        review: c.review,
        candidates: c.candidates.filter((e) => linked.has(e.id)),
        decisions,
        counts: outcomes(c, decisions),
        availability: c.availability,
        independentValidation: false,
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    });
}

export async function importComparison(c: Comparison, text: string) {
    const { value } = await unseal(text, outputSchema);
    const saved = objectWith(
        value,
        "manifest manifestSha256 reportRunId disclosure review candidates decisions counts availability independentValidation canonicalAdmissionPermitted operationalSignal",
        "comparación exportada",
    );
    requireValue(
        saved.manifestSha256 === c.manifestSha256 &&
            Array.isArray(saved.decisions),
        "La comparación exportada corresponde a otros insumos.",
    );
    const d = objectWith(
        saved.disclosure,
        "reviewerId declaredExposure openedAtUtc firstOpenedAtUtc",
        "apertura documentada",
    );
    const disclosure = recordDisclosure(
        c,
        d.reviewerId as string,
        d.declaredExposure as Disclosure["declaredExposure"],
        utc(d.openedAtUtc, "apertura"),
        utc(d.firstOpenedAtUtc, "primera apertura"),
    );
    const decisions: Decision[] = [];
    for (const value of saved.decisions) {
        const decision = value as Decision;
        decisions.push(
            appendDecision(
                c,
                disclosure,
                decisions,
                decision.targetId,
                decision.candidateEventId,
                decision.kind,
                decision.note,
                decision.recordedAtUtc,
            ),
        );
    }
    requireValue(
        JSON.parse(await exportComparison(c, disclosure, decisions)).payload ===
            JSON.stringify(value),
        "El historial o la evidencia de la comparación fueron alterados.",
    );
    return { disclosure, decisions };
}

export function humanVisibility(human: EventRevision, disclosure: Disclosure) {
    return human.phase === "PostRevealCorrection" ||
        timeKey(human.reviewedAtUtc) >= timeKey(disclosure.firstOpenedAtUtc)
        ? "Posterior a revelación registrada"
        : "Anterior a revelación registrada en este navegador";
}
