import manifest from "./data/contextual-validation/manifest.json";
import contextUrl from "./data/contextual-validation/context.json?url";
import { closedAt, validateContext, type ContextData } from "./model";
import type { Candle } from "../report";

export { manifest };
export const reviewKey = "moneyway.research.contextual-sc.v1";
export const prep = `${manifest.selectedDay}T13:00:00Z`;
export type Stage = "A" | "B" | "C" | "D";
export type Kind =
    "Selected" | "None" | "Ambiguous" | "Insufficient" | "Skip" | "SameCandle";
export type Draft = {
    stage: Stage;
    kind: Kind;
    references: string[];
    candleOpen: string | null;
    side: "High" | "Low";
    relevant: boolean;
    confirmationOpen: string | null;
    note: string;
};
export type Label = Draft & {
    revision: number;
    reviewer: string;
    cutoff: string;
    maximumSeen: string;
    deviceUtc: string;
    source: Candle | null;
    price: number | null;
    formationUtc: string | null;
    confirmationClose: string | null;
    classification:
        | "ChronologicalSelfDeclared"
        | "Retrospective"
        | "PreviouslyExposed"
        | "PostRevealCorrection";
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};
export type Review = {
    schemaVersion: "contextual-label-export-1";
    datasetId: string;
    datasetVersion: number;
    sourceSha256: string;
    reportSha256: string;
    contextId: string;
    maximumSeen: string;
    outsideExposure: boolean;
    labels: Label[];
    revealedRevision: number | null;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};
export function emptyReview(): Review {
    return {
        schemaVersion: "contextual-label-export-1",
        datasetId: manifest.datasetId,
        datasetVersion: manifest.datasetVersion,
        sourceSha256: manifest.inputSha256,
        reportSha256: manifest.reportHashes.comparison,
        contextId: manifest.contextId,
        maximumSeen: prep,
        outsideExposure: false,
        labels: [],
        revealedRevision: null,
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    };
}
export const blank = (stage: Stage): Draft => ({
    stage,
    kind: "Selected",
    references: [],
    candleOpen: null,
    side: "High",
    relevant: true,
    confirmationOpen: null,
    note: "",
});
const fail = (text: string): never => {
    throw new Error(text);
};
const time = (s: string) =>
    /(?:Z|\+00:00)$/.test(s) && Number.isFinite(Date.parse(s));
export function visit(
    state: Review,
    cutoff: string,
    context: ContextData,
): Review {
    if (
        !time(cutoff) ||
        Date.parse(cutoff) < Date.parse(prep) ||
        Date.parse(cutoff) > Date.parse(context.maximumCutoffUtc)
    )
        fail("Cutoff fuera del episodio congelado.");
    return {
        ...state,
        maximumSeen:
            Date.parse(cutoff) > Date.parse(state.maximumSeen)
                ? cutoff
                : state.maximumSeen,
    };
}
export function first(state: Review, stage: Stage) {
    return state.labels.find((l) => l.stage === stage);
}
export function commit(
    state: Review,
    draft: Draft,
    context: ContextData,
    cutoff: string,
    reviewer: string,
    deviceUtc: string,
): Review {
    if (
        !["A", "B", "C", "D"].includes(draft.stage) ||
        !["High", "Low"].includes(draft.side)
    )
        fail("Etapa o coordenada inválida.");
    if (!reviewer.trim() || reviewer.trim().length > 120 || !time(deviceUtc))
        fail("Indique un revisor local válido.");
    let next = visit(state, cutoff, context);
    if (draft.stage !== "A" && !first(state, "A"))
        fail("Confirme primero la preparación A.");
    if (draft.stage === "B" && !first(state, "C"))
        fail(
            "Registre C antes de confirmar B; si ya vio el evento, C será retrospectiva.",
        );
    if (draft.stage === "D" && !first(state, "B"))
        fail("Confirme primero la observación B.");
    if (
        draft.stage === "A" &&
        !first(state, "A") &&
        Date.parse(next.maximumSeen) > Date.parse(prep)
    )
        fail("La preparación inicial se confirma a las 08:00 Bogotá.");
    const allowedKinds: Kind[] =
        draft.stage === "A" || draft.stage === "C"
            ? ["Selected", "None", "Ambiguous", "Insufficient", "Skip"]
            : draft.stage === "D"
              ? [
                    "Selected",
                    "None",
                    "Ambiguous",
                    "Insufficient",
                    "Skip",
                    "SameCandle",
                ]
              : ["Selected", "None", "Ambiguous", "Insufficient", "Skip"];
    if (!allowedKinds.includes(draft.kind) || draft.note.length > 1000)
        fail("Tipo de observación inválido.");
    if (
        new Set(draft.references).size !== draft.references.length ||
        draft.references.some((r) => !["AH", "AL", "LH", "LL"].includes(r))
    )
        fail("Referencia de liquidez inválida.");
    if (
        draft.kind === "Selected" &&
        draft.stage === "A" &&
        !draft.references.length
    )
        fail("Seleccione liquidez o indique ninguna/ambigua/insuficiente.");
    if (
        draft.kind === "Selected" &&
        draft.stage === "B" &&
        draft.references.length !== 1
    )
        fail(
            "Seleccione una referencia para documentar esta interacción; no establece prioridad.",
        );
    const source =
        draft.kind === "Selected" && draft.stage !== "A"
            ? (closedAt(context, "M5", cutoff).find(
                  (c) => c.openTimeUtc === draft.candleOpen,
              ) ?? fail("Seleccione directamente una vela M5 cerrada visible."))
            : null;
    const confirmation =
        draft.confirmationOpen && draft.stage === "C"
            ? (closedAt(context, "M5", cutoff).find(
                  (c) => c.openTimeUtc === draft.confirmationOpen,
              ) ?? fail("Confirmación humana no visible al cutoff."))
            : null;
    if (
        confirmation &&
        source &&
        Date.parse(confirmation.closeTimeUtc) < Date.parse(source.closeTimeUtc)
    )
        fail("La confirmación no puede preceder a la formación.");
    const event = first(state, "B");
    if (
        draft.stage === "D" &&
        source &&
        event?.source &&
        Date.parse(source.openTimeUtc) < Date.parse(event.source.closeTimeUtc)
    )
        fail(
            "SC posterior requiere otra vela; use secuencia ambigua en la misma vela.",
        );
    const prior = state.labels.some((l) => l.stage === draft.stage);
    const classification: Label["classification"] =
        state.revealedRevision !== null
            ? "PostRevealCorrection"
            : state.outsideExposure ||
                context.classification.startsWith("PreviouslyExposed")
              ? "PreviouslyExposed"
              : prior || Date.parse(cutoff) < Date.parse(next.maximumSeen)
                ? "Retrospective"
                : "ChronologicalSelfDeclared";
    const label: Label = {
        ...draft,
        revision: state.labels.length + 1,
        reviewer: reviewer.trim(),
        cutoff,
        maximumSeen: next.maximumSeen,
        deviceUtc,
        source: source ? structuredClone(source) : null,
        price:
            source && draft.stage === "C"
                ? draft.side === "High"
                    ? source.high
                    : source.low
                : null,
        formationUtc: source?.closeTimeUtc ?? null,
        confirmationClose: confirmation?.closeTimeUtc ?? null,
        classification,
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    };
    next = { ...next, labels: [...state.labels, label] };
    return next;
}
export function referenceEligibility(state: Review) {
    const c = first(state, "C"),
        b = first(state, "B");
    if (
        state.outsideExposure ||
        !c ||
        !b?.source ||
        c.classification !== "ChronologicalSelfDeclared" ||
        Date.parse(c.maximumSeen) > Date.parse(b.source.openTimeUtc) ||
        c.revision >= b.revision
    )
        return "RetrospectiveOrIneligible";
    return "SelfDeclaredPreEventCommitment";
}
export function reveal(state: Review): Review {
    if (!["A", "B", "C", "D"].every((s) => first(state, s as Stage)))
        fail("Confirme A, C, B y D antes de revelar.");
    return state.revealedRevision === null
        ? { ...state, revealedRevision: state.labels.length }
        : state;
}
export function decode(raw: string | null, context: ContextData): Review {
    if (!raw) return emptyReview();
    try {
        const value = JSON.parse(raw) as Review;
        const seed = emptyReview();
        if (
            value.schemaVersion !== seed.schemaVersion ||
            value.datasetId !== seed.datasetId ||
            value.datasetVersion !== seed.datasetVersion ||
            value.sourceSha256 !== seed.sourceSha256 ||
            value.reportSha256 !== seed.reportSha256 ||
            value.contextId !== context.id ||
            !Array.isArray(value.labels) ||
            value.labels.length > 1000 ||
            typeof value.outsideExposure !== "boolean" ||
            value.canonicalAdmissionPermitted !== false ||
            value.operationalSignal !== false
        )
            fail("Identidad incompatible.");
        let replay = { ...seed, outsideExposure: value.outsideExposure };
        for (const label of value.labels) {
            if (
                value.revealedRevision !== null &&
                replay.labels.length === value.revealedRevision
            )
                replay = reveal(replay);
            replay = visit(replay, label.maximumSeen, context);
            replay = commit(
                replay,
                label,
                context,
                label.cutoff,
                label.reviewer,
                label.deviceUtc,
            );
            const generated = replay.labels.at(-1)!;
            // Exposure may have been declared later; it can only downgrade old labels.
            const validClassification =
                generated.classification === label.classification ||
                (value.outsideExposure &&
                    generated.classification === "PreviouslyExposed" &&
                    ["ChronologicalSelfDeclared", "Retrospective"].includes(
                        label.classification,
                    ));
            if (
                JSON.stringify({
                    ...generated,
                    classification: label.classification,
                }) !== JSON.stringify(label) ||
                !validClassification
            )
                fail("Etiqueta alterada.");
        }
        if (
            value.revealedRevision === replay.labels.length &&
            value.revealedRevision !== null
        )
            replay = reveal(replay);
        if (replay.revealedRevision !== value.revealedRevision)
            fail("Secuencia de revelación inválida.");
        visit(replay, value.maximumSeen, context);
        if (Date.parse(value.maximumSeen) < Date.parse(replay.maximumSeen))
            fail("Exposición retrocedida.");
        return value;
    } catch {
        return fail(
            "Registro contextual incompatible o alterado; no se sobrescribirá. Exporte e investigue.",
        );
    }
}
// Same compare-and-write/read-back convention as V3; dedicated schema never overwrites V3 labels.
export function persist(
    storage: Pick<Storage, "getItem" | "setItem">,
    expected: string | null,
    state: Review,
    context: ContextData,
) {
    if (storage.getItem(reviewKey) !== expected)
        fail("Otra pestaña modificó la revisión; recargue.");
    const raw = JSON.stringify(state);
    decode(raw, context);
    storage.setItem(reviewKey, raw);
    if (storage.getItem(reviewKey) !== raw)
        fail("No se pudo comprobar el guardado local.");
    return raw;
}
export async function digest(text: string) {
    return Array.from(
        new Uint8Array(
            await crypto.subtle.digest(
                "SHA-256",
                new TextEncoder().encode(text),
            ),
        ),
        (b) => b.toString(16).padStart(2, "0"),
    ).join("");
}
export async function loadReviewContext() {
    const response = await fetch(contextUrl);
    if (!response.ok) fail("Dataset de revisión no disponible.");
    const text = await response.text();
    if ((await digest(text)) !== manifest.inputSha256)
        fail("Hash del contexto incompatible.");
    return validateContext(JSON.parse(text) as ContextData, true);
}
export function exportReview(state: Review, context: ContextData) {
    decode(JSON.stringify(state), context);
    return JSON.stringify(
        {
            warning:
                "Unauthenticated browser-local research; no canonical admission or operational signal",
            manifest,
            labels: state,
        },
        null,
        2,
    );
}
