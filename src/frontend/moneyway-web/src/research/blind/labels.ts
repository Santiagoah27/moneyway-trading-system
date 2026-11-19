import { catalog, charts, episodeById, type Episode } from "./catalog";

export const storageKey = "moneyway.research.m5-blind.v1";
export type LabelKind = "Reference" | "NoReference" | "Ambiguous" | "Skip";
export type Exposure = "SelfDeclaredIndependent" | "PreviouslyExposed";
export type Label = {
    revision: number;
    kind: LabelKind;
    selectedOpens: string[];
    extreme: "Low" | "High" | null;
    price: number | null;
    confirmationOpenUtc: string | null;
    confirmationCloseUtc: string | null;
    cutoffUtc: string;
    reviewer: string;
    reviewedAtDeviceUtc: string;
    classification:
        | "IndependentRetrospectiveResearch"
        | "PreviouslyExposed"
        | "PreRevealRevision"
        | "PostRevealCorrection";
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};
export type History = {
    episodeId: string;
    inputSha256: string;
    exposure: Exposure;
    revisions: Label[];
    revealedAtDeviceUtc: string | null;
    revealRevision: number | null;
    revealedReportSha256: string | null;
};
export type ReviewState = {
    schemaVersion: 1;
    datasetId: string;
    datasetVersion: number;
    datasetContentSha256: string;
    reviewer: string;
    records: Record<string, History>;
};
export type Draft = {
    kind: LabelKind;
    selectedOpens: string[];
    confirmationOpenUtc: string | null;
};
export const emptyDraft = (): Draft => ({
    kind: "Reference",
    selectedOpens: [],
    confirmationOpenUtc: null,
});
export const emptyState = (): ReviewState => ({
    schemaVersion: 1,
    datasetId: catalog.datasetId,
    datasetVersion: catalog.datasetVersion,
    datasetContentSha256: catalog.datasetContentSha256,
    reviewer: "",
    records: {},
});
const fail = (message: string): never => {
    throw new Error(message);
};
const validReviewer = (v: string) =>
    typeof v === "string" &&
    v.trim() === v &&
    v.length > 0 &&
    v.length <= 120 &&
    [...v].every((c) => c.charCodeAt(0) >= 32 && c.charCodeAt(0) !== 127);
const deviceTime = (v: string) =>
    typeof v === "string" && v.endsWith("Z") && Number.isFinite(Date.parse(v));

export function selection(e: Episode, opens: string[]) {
    const bars = charts[e.id].evidenceCandles;
    if (!opens.length || new Set(opens).size !== opens.length)
        fail("Seleccione una vela o varios extremos iguales.");
    const chosen = opens.map(
        (t) =>
            bars.find((b) => b.openTimeUtc === t) ??
            fail("La vela no pertenece al episodio/cutoff."),
    );
    if (
        chosen.some(
            (b) => Date.parse(b.openTimeUtc) <= Date.parse(e.anchorOpenUtc),
        )
    )
        fail(
            "La referencia del episodio debe seleccionarse después del ancla explícita.",
        );
    const extreme: "Low" | "High" =
        e.direction === "Ascending" ? "Low" : "High";
    const price = extreme === "Low" ? chosen[0].low : chosen[0].high;
    if (chosen.some((b) => (extreme === "Low" ? b.low : b.high) !== price))
        fail("Una selección múltiple debe conservar el mismo extremo exacto.");
    return { extreme, price, chosen };
}

export function commitLabel(
    state: ReviewState,
    episodeId: string,
    draft: Draft,
    reviewer: string,
    alreadyKnown: boolean,
    now: string,
): ReviewState {
    const e = episodeById(episodeId);
    const old = state.records[episodeId];
    reviewer = reviewer.trim();
    if (!validReviewer(reviewer) || !deviceTime(now))
        fail(
            "Indique el revisor local. La fecha del dispositivo no autentica el evento histórico.",
        );
    if (!["Reference", "NoReference", "Ambiguous", "Skip"].includes(draft.kind))
        fail("Tipo de selección no válido.");
    const picked =
        draft.kind === "Reference" ? selection(e, draft.selectedOpens) : null;
    let confirmationOpenUtc = null,
        confirmationCloseUtc = null;
    if (picked && draft.confirmationOpenUtc) {
        const candle =
            charts[e.id].evidenceCandles.find(
                (b) => b.openTimeUtc === draft.confirmationOpenUtc,
            ) ?? fail("Confirmación fuera del episodio.");
        if (
            Date.parse(candle.closeTimeUtc) <=
            Math.max(...picked.chosen.map((b) => Date.parse(b.closeTimeUtc)))
        )
            fail(
                "La confirmación independiente debe ser una vela posterior a la formación seleccionada.",
            );
        confirmationOpenUtc = candle.openTimeUtc;
        confirmationCloseUtc = candle.closeTimeUtc;
    }
    const exposure: Exposure =
        alreadyKnown ||
        e.priorExposure === "PreviouslyExposed" ||
        old?.exposure === "PreviouslyExposed"
            ? "PreviouslyExposed"
            : "SelfDeclaredIndependent";
    const revision = (old?.revisions.length ?? 0) + 1;
    const label: Label = {
        revision,
        kind: draft.kind,
        selectedOpens: picked
            ? picked.chosen.map((b) => b.openTimeUtc).sort()
            : [],
        extreme: picked?.extreme ?? null,
        price: picked?.price ?? null,
        confirmationOpenUtc,
        confirmationCloseUtc,
        cutoffUtc: e.cutoffUtc,
        reviewer,
        reviewedAtDeviceUtc: now,
        classification: old?.revealedAtDeviceUtc
            ? "PostRevealCorrection"
            : exposure === "PreviouslyExposed"
              ? "PreviouslyExposed"
              : revision === 1
                ? "IndependentRetrospectiveResearch"
                : "PreRevealRevision",
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    };
    const history: History = {
        episodeId,
        inputSha256: e.inputSha256,
        exposure,
        revisions: [...(old?.revisions ?? []), label],
        revealedAtDeviceUtc: old?.revealedAtDeviceUtc ?? null,
        revealRevision: old?.revealRevision ?? null,
        revealedReportSha256: old?.revealedReportSha256 ?? null,
    };
    return {
        ...state,
        reviewer,
        records: { ...state.records, [episodeId]: history },
    };
}

export function reveal(
    state: ReviewState,
    id: string,
    now: string,
): ReviewState {
    const old = state.records[id];
    const e = episodeById(id);
    if (!old?.revisions.length || !deviceTime(now))
        fail("Confirme y guarde una selección antes de revelar.");
    if (old.revealedAtDeviceUtc) return state;
    return {
        ...state,
        records: {
            ...state.records,
            [id]: {
                ...old,
                revealedAtDeviceUtc: now,
                revealRevision: old.revisions.at(-1)!.revision,
                revealedReportSha256: e.reportSha256,
            },
        },
    };
}

// One metric observation per episode. Freeze the last pre-reveal revision; never use a correction.
export const independentLabel = (h: History) =>
    h.revisions[(h.revealRevision ?? h.revisions.length) - 1];

export function decodeState(raw: string | null): ReviewState {
    if (raw === null) return emptyState();
    let value: ReviewState;
    try {
        value = JSON.parse(raw) as ReviewState;
    } catch {
        return fail(
            "Registro local ilegible. Conserve/exporte los bytes; no se reiniciará automáticamente.",
        );
    }
    try {
        if (
            value.schemaVersion !== 1 ||
            value.datasetId !== catalog.datasetId ||
            value.datasetVersion !== catalog.datasetVersion ||
            value.datasetContentSha256 !== catalog.datasetContentSha256 ||
            typeof value.records !== "object" ||
            !value.records ||
            Array.isArray(value.records) ||
            typeof value.reviewer !== "string"
        )
            throw new Error();
        for (const [id, h] of Object.entries(value.records)) {
            const e = episodeById(id);
            if (
                h.episodeId !== id ||
                h.inputSha256 !== e.inputSha256 ||
                !["PreviouslyExposed", "SelfDeclaredIndependent"].includes(
                    h.exposure,
                ) ||
                !Array.isArray(h.revisions) ||
                !h.revisions.length ||
                (e.priorExposure === "PreviouslyExposed" &&
                    h.exposure !== "PreviouslyExposed")
            )
                throw new Error();
            let rebuilt = emptyState();
            for (const label of h.revisions) {
                // Validate each revision against source OHLC rather than trusting stored prices.
                if (label.classification === "PostRevealCorrection") {
                    const current = rebuilt.records[id];
                    if (!current || !h.revealedAtDeviceUtc) throw new Error();
                    rebuilt = reveal(rebuilt, id, h.revealedAtDeviceUtc);
                }
                rebuilt = commitLabel(
                    rebuilt,
                    id,
                    {
                        kind: label.kind,
                        selectedOpens: label.selectedOpens,
                        confirmationOpenUtc: label.confirmationOpenUtc,
                    },
                    label.reviewer,
                    h.exposure === "PreviouslyExposed",
                    label.reviewedAtDeviceUtc,
                );
                const expected = rebuilt.records[id].revisions.at(-1)!;
                // Prior exposure can be declared later, but never undone. Earlier original classification is retained.
                const allowedClass =
                    label.classification === expected.classification ||
                    (h.exposure === "PreviouslyExposed" &&
                        [
                            "IndependentRetrospectiveResearch",
                            "PreRevealRevision",
                        ].includes(label.classification) &&
                        !rebuilt.records[id].revealedAtDeviceUtc);
                if (
                    !allowedClass ||
                    JSON.stringify({
                        ...label,
                        classification: expected.classification,
                    }) !== JSON.stringify(expected)
                )
                    throw new Error();
            }
            if (h.revealedAtDeviceUtc !== null) {
                if (
                    !deviceTime(h.revealedAtDeviceUtc) ||
                    !Number.isInteger(h.revealRevision) ||
                    h.revealRevision! < 1 ||
                    h.revealRevision! > h.revisions.length ||
                    h.revealedReportSha256 !== e.reportSha256
                )
                    throw new Error();
                if (
                    h.revisions.some(
                        (l) =>
                            l.revision > h.revealRevision! !==
                            (l.classification === "PostRevealCorrection"),
                    )
                )
                    throw new Error();
            } else if (
                h.revealRevision !== null ||
                h.revealedReportSha256 !== null ||
                h.revisions.some(
                    (l) => l.classification === "PostRevealCorrection",
                )
            )
                throw new Error();
        }
    } catch {
        return fail(
            "Registro local incompatible o alterado. No se sobrescribirá; exporte una copia antes de investigar.",
        );
    }
    return value;
}

export function persist(
    storage: Pick<Storage, "getItem" | "setItem">,
    expectedRaw: string | null,
    state: ReviewState,
) {
    if (storage.getItem(storageKey) !== expectedRaw)
        fail("Otra pestaña modificó el registro; recargue antes de guardar.");
    const raw = JSON.stringify(state);
    decodeState(raw);
    storage.setItem(storageKey, raw);
    if (storage.getItem(storageKey) !== raw)
        fail("No se pudo comprobar el guardado local.");
    return raw;
}

export function exportResearch(state: ReviewState) {
    decodeState(JSON.stringify(state));
    return JSON.stringify(
        {
            schemaVersion: "research-label-export-1",
            warning:
                "Browser-local unauthenticated research export; not canonical facts or immutable storage",
            manifest: catalog,
            labels: state,
        },
        null,
        2,
    );
}
