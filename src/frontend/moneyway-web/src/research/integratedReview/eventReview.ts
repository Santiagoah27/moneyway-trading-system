import {
    isUtc,
    objectWith,
    requireValue,
    seal,
    unseal,
    type OriginalPackage,
} from "./originalPackage";
import {
    loadReview,
    newReview,
    validateReview,
    type CandleReference,
    type Exposure,
    type HumanLabel,
    type Kind,
    type Review,
    type Target,
} from "./reviewHistory";

export type EventRevision = HumanLabel & {
    eventNumber: number;
    eventRevision: number;
    scopeStartUtc: string | null;
    reviewMode: "AtViewingFrontier" | "RetrospectiveAfterLaterView";
};
export type EventReview = Omit<Review, "version" | "labels"> & {
    version: 2;
    labels: EventRevision[];
};
export type EventDraft = {
    observation: Target;
    kind: Kind;
    references: CandleReference[];
    note: string;
    viewingCutoffUtc: string;
    scopeStartUtc: string | null;
};

const schema = "moneyway.nasdaq-integrated-human-review.v2";
const storageKey = (p: OriginalPackage) =>
    `${schema}:${p.operatingDate}:${new Date(p.cutoffUtc).toISOString()}`;
const time = Date.parse;
const same = (a: unknown, b: unknown) =>
    JSON.stringify(a) === JSON.stringify(b);
const targetId = (p: OriginalPackage, eventNumber: number) =>
    `${p.sessionId}/event/${eventNumber}`;
const mode = (visible: string, maximum: string) =>
    time(visible) < time(maximum)
        ? "RetrospectiveAfterLaterView"
        : "AtViewingFrontier";

export function groupEvents(labels: readonly EventRevision[]) {
    const events = new Map<string, EventRevision[]>();
    for (const label of labels) {
        const history = events.get(label.targetId) ?? [];
        history.push(label);
        events.set(label.targetId, history);
    }
    return events;
}

export function validateEventReview(
    p: OriginalPackage,
    value: unknown,
): EventReview {
    requireValue(
        value && typeof value === "object" && !Array.isArray(value),
        "Revisión de eventos V2 incompatible.",
    );
    const review = value as EventReview;
    requireValue(
        review.version === 2 && Array.isArray(review.labels),
        "Se requiere un historial V2. Los archivos V1 se consultan como históricos.",
    );
    const seen = new Map<number, EventRevision>();
    const commonLabels = review.labels.map((label) => {
        objectWith(
            label,
            "revision targetId observation kind references note reviewedAtUtc viewingCutoffUtc maximumViewedUtc phase eventNumber eventRevision scopeStartUtc reviewMode",
            "revisión de evento",
        );
        const {
            eventNumber,
            eventRevision,
            scopeStartUtc,
            reviewMode,
            ...common
        } = label;
        const prior = seen.get(eventNumber);
        requireValue(
            Number.isSafeInteger(eventNumber) &&
                eventNumber > 0 &&
                label.targetId === targetId(p, eventNumber) &&
                (prior
                    ? prior.observation === label.observation &&
                      eventRevision === prior.eventRevision + 1
                    : eventNumber === seen.size + 1 && eventRevision === 1),
            "Identidad duplicada, contradictoria o revisión de evento fuera de secuencia.",
        );
        requireValue(
            label.kind === "NoReference"
                ? isUtc(scopeStartUtc) &&
                      time(scopeStartUtc) >= earliestOpen(p) &&
                      time(scopeStartUtc) < time(label.viewingCutoffUtc)
                : scopeStartUtc === null,
            "Sin referencia requiere un alcance explícito anterior al corte visible.",
        );
        requireValue(
            reviewMode === mode(label.viewingCutoffUtc, label.maximumViewedUtc),
            "La revisión debe conservar la exposición a datos posteriores.",
        );
        seen.set(eventNumber, label);
        // Reuse the V1 candle, chronology, package and reveal checks on the common fields.
        return { ...common, targetId: `${p.sessionId}/${common.observation}` };
    });
    validateReview(p, { ...review, version: 1, labels: commonLabels });
    return review;
}

export function earliestOpen(p: OriginalPackage) {
    return Math.min(
        time(p.cutoffUtc),
        ...Object.values(p.sources).flatMap((s) =>
            s.bars.length ? [time(s.bars[0].openUtc)] : [],
        ),
    );
}

export function startEventReview(
    p: OriginalPackage,
    reviewer: string,
    exposure: Exposure,
    visible: string,
    now: string,
    legacy: Review | null = null,
) {
    const common = newReview(p, reviewer, exposure, visible, now);
    if (!legacy) return validateEventReview(p, { ...common, version: 2 });
    return validateEventReview(p, {
        ...common,
        version: 2,
        reviewerId: legacy.reviewerId,
        exposure: legacy.exposure,
        declaredAtUtc: legacy.declaredAtUtc,
        maximumViewedUtc:
            time(legacy.maximumViewedUtc) > time(visible)
                ? legacy.maximumViewedUtc
                : visible,
        reveal: legacy.reveal
            ? { recordedAtUtc: legacy.reveal.recordedAtUtc, afterRevision: 0 }
            : null,
    });
}

export function appendEvent(
    p: OriginalPackage,
    review: EventReview,
    selectedTargetId: string | null,
    draft: EventDraft,
    now: string,
) {
    const events = groupEvents(review.labels);
    const prior =
        selectedTargetId === null ? null : events.get(selectedTargetId)?.at(-1);
    requireValue(
        selectedTargetId === null || prior?.observation === draft.observation,
        "La corrección debe pertenecer al evento seleccionado y conservar su tipo.",
    );
    const eventNumber = prior?.eventNumber ?? events.size + 1;
    const label: EventRevision = {
        ...draft,
        note: draft.note.trim(),
        reviewedAtUtc: now,
        targetId: targetId(p, eventNumber),
        eventNumber,
        eventRevision: (prior?.eventRevision ?? 0) + 1,
        revision: review.labels.length + 1,
        maximumViewedUtc: review.maximumViewedUtc,
        phase: review.reveal ? "PostRevealCorrection" : "PreRevealJudgment",
        reviewMode: mode(draft.viewingCutoffUtc, review.maximumViewedUtc),
    };
    return validateEventReview(p, {
        ...review,
        labels: [...review.labels, label],
    });
}

export function recordEventReveal(
    p: OriginalPackage,
    review: EventReview,
    now: string,
) {
    requireValue(!review.reveal, "La revelación externa ya quedó registrada.");
    return validateEventReview(p, {
        ...review,
        reveal: { recordedAtUtc: now, afterRevision: review.labels.length },
    });
}

export function loadEventReview(p: OriginalPackage) {
    const raw = localStorage.getItem(storageKey(p));
    return raw === null ? null : validateEventReview(p, JSON.parse(raw));
}

function preservesHistory(current: EventReview, incoming: EventReview) {
    return (
        current.reviewerId === incoming.reviewerId &&
        current.exposure === incoming.exposure &&
        current.declaredAtUtc === incoming.declaredAtUtc &&
        time(incoming.maximumViewedUtc) >= time(current.maximumViewedUtc) &&
        (current.reveal
            ? same(current.reveal, incoming.reveal)
            : !incoming.reveal ||
              incoming.reveal.afterRevision >= current.labels.length) &&
        same(current.labels, incoming.labels.slice(0, current.labels.length))
    );
}

export function respectsLegacy(review: EventReview, legacy: Review | null) {
    return (
        !legacy ||
        (review.reviewerId === legacy.reviewerId &&
            review.exposure === legacy.exposure &&
            review.declaredAtUtc === legacy.declaredAtUtc &&
            time(review.maximumViewedUtc) >= time(legacy.maximumViewedUtc) &&
            (!legacy.reveal ||
                (review.reveal?.recordedAtUtc === legacy.reveal.recordedAtUtc &&
                    review.reveal.afterRevision === 0)))
    );
}

export function persistEventReview(
    p: OriginalPackage,
    previous: EventReview | null,
    next: EventReview,
    legacy: Review | null = null,
) {
    requireValue(
        same(previous, loadEventReview(p)),
        "Otra pestaña cambió el historial V2. Recargue antes de continuar.",
    );
    requireValue(
        !previous || preservesHistory(previous, next),
        "El cambio sustituiría exposición o revisiones anteriores.",
    );
    requireValue(
        respectsLegacy(next, legacy) && respectsLegacy(next, loadReview(p)),
        "El historial V2 es incompatible con la exposición o el corte ya registrado en V1.",
    );
    localStorage.setItem(
        storageKey(p),
        JSON.stringify(validateEventReview(p, next)),
    );
}

export function exportEventReview(p: OriginalPackage, review: EventReview) {
    return seal(schema, validateEventReview(p, review));
}

export async function importEventReview(
    p: OriginalPackage,
    current: EventReview | null,
    text: string,
) {
    const { value } = await unseal(text, schema);
    const incoming = validateEventReview(p, value);
    requireValue(
        !current || preservesHistory(current, incoming),
        "El archivo sustituiría exposición, revelación o revisiones anteriores.",
    );
    return incoming;
}
