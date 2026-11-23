import {
    frames,
    isUtc,
    objectWith,
    requireValue,
    seal,
    unseal,
    type Frame,
    type OriginalPackage,
} from "./originalPackage";

export const targets = {
    H4Context: { label: "Contexto H4", frames: ["H4"] },
    H1Liquidity: { label: "Referencias de liquidez H1", frames: ["H1"] },
    LiquidityTake: { label: "Toma de liquidez", frames: ["M5"] },
    M5SC: { label: "Cambio estructural M5 (SC)", frames: ["M5"] },
    M5IFVG: { label: "IFVG M5", frames: ["M5"] },
    M5FVG: { label: "FVG M5", frames: ["M5"] },
    M1Pullback: { label: "Pullback M1", frames: ["M1"] },
    M1Realignment: { label: "Realineación M1", frames: ["M1"] },
    StructuralSL: { label: "Referencia estructural SL", frames: ["M5"] },
    StructuralTP: { label: "Referencia estructural TP", frames: ["H1", "M5"] },
} satisfies Record<string, { label: string; frames: Frame[] }>;

export type Target = keyof typeof targets;
export const kinds = [
    "Reference",
    "NoReference",
    "Ambiguous",
    "InsufficientSourceEvidence",
    "Skip",
] as const;
export const exposures = [
    "Unknown",
    "SelfDeclaredIndependent",
    "PreviouslyExposed",
] as const;
export type Kind = (typeof kinds)[number];
export type Exposure = (typeof exposures)[number];
export type CandleReference = {
    timeframe: Frame;
    originalSha256: string;
    nativeOpen: string;
    openUtc: string;
    closeUtc: string;
};
export type HumanLabel = {
    revision: number;
    targetId: string;
    observation: Target;
    kind: Kind;
    references: CandleReference[];
    note: string;
    reviewedAtUtc: string;
    viewingCutoffUtc: string;
    maximumViewedUtc: string;
    phase: "PreRevealJudgment" | "PostRevealCorrection";
};
export type Review = {
    version: 1;
    sessionId: string;
    operatingDate: string;
    cutoffUtc: string;
    originalPackageSha256: string;
    provenanceSha256: string;
    provider: string;
    instrument: string;
    sessionExposure: OriginalPackage["exposure"];
    originalSources: Record<Frame, string>;
    reviewerId: string;
    exposure: Exposure;
    declaredAtUtc: string;
    maximumViewedUtc: string;
    reveal: { recordedAtUtc: string; afterRevision: number } | null;
    labels: HumanLabel[];
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};

const reviewSchema = "moneyway.nasdaq-integrated-human-review.v1";
const localKey = (p: OriginalPackage) =>
    `${reviewSchema}:${p.operatingDate}:${new Date(p.cutoffUtc).toISOString()}`;
const time = Date.parse;
const same = (a: unknown, b: unknown) =>
    JSON.stringify(a) === JSON.stringify(b);

function identity(p: OriginalPackage) {
    return {
        version: 1,
        sessionId: p.sessionId,
        operatingDate: p.operatingDate,
        cutoffUtc: p.cutoffUtc,
        originalPackageSha256: p.sha256,
        provenanceSha256: p.provenanceSha256,
        provider: p.provider,
        instrument: p.instrument,
        sessionExposure: p.exposure,
        originalSources: Object.fromEntries(
            frames.map((tf) => [tf, p.sources[tf].originalSha256]),
        ),
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    } as const;
}

function isViewingTime(p: OriginalPackage, at: unknown): at is string {
    return isUtc(at) && time(at) <= time(p.cutoffUtc);
}

export function canPick(target: Target, frame: Frame) {
    return (targets[target].frames as Frame[]).includes(frame);
}

export function reference(
    p: OriginalPackage,
    frame: Frame,
    openUtc: string,
): CandleReference {
    const source = p.sources[frame];
    const candle = source.bars.find((b) => b.openUtc === openUtc);
    requireValue(candle, "La vela no pertenece a la fuente original.");
    return {
        timeframe: frame,
        originalSha256: source.originalSha256,
        nativeOpen: candle.nativeOpen,
        openUtc: candle.openUtc,
        closeUtc: candle.closeUtc,
    };
}

export function newReview(
    p: OriginalPackage,
    reviewerId: string,
    exposure: Exposure,
    maximumViewedUtc: string,
    now: string,
) {
    return validateReview(p, {
        ...identity(p),
        reviewerId: reviewerId.trim(),
        exposure,
        declaredAtUtc: now,
        maximumViewedUtc,
        reveal:
            exposure === "PreviouslyExposed"
                ? { recordedAtUtc: now, afterRevision: 0 }
                : null,
        labels: [],
    });
}

export function appendLabel(
    p: OriginalPackage,
    r: Review,
    observation: Target,
    kind: Kind,
    references: CandleReference[],
    note: string,
    viewingCutoffUtc: string,
    now: string,
) {
    const label: HumanLabel = {
        revision: r.labels.length + 1,
        targetId: `${p.sessionId}/${observation}`,
        observation,
        kind,
        references,
        note: note.trim(),
        reviewedAtUtc: now,
        viewingCutoffUtc,
        maximumViewedUtc: r.maximumViewedUtc,
        phase: r.reveal ? "PostRevealCorrection" : "PreRevealJudgment",
    };
    requireValue(
        time(now) >= time(r.reveal?.recordedAtUtc ?? r.declaredAtUtc),
        "El reloj local retrocedió respecto a la declaración o revelación.",
    );
    return validateReview(p, { ...r, labels: [...r.labels, label] });
}

export function recordExternalReveal(
    p: OriginalPackage,
    r: Review,
    now: string,
) {
    requireValue(!r.reveal, "La revelación externa ya quedó registrada.");
    return validateReview(p, {
        ...r,
        reveal: { recordedAtUtc: now, afterRevision: r.labels.length },
    });
}

export function validateReview(p: OriginalPackage, value: unknown): Review {
    const expected = identity(p);
    const r = objectWith(
        value,
        Object.keys(expected).join(" ") +
            " reviewerId exposure declaredAtUtc maximumViewedUtc reveal labels",
        "revisión humana",
    );
    for (const [key, val] of Object.entries(expected))
        requireValue(
            same(r[key], val),
            "La revisión pertenece a otra sesión, fuente o versión.",
        );
    requireValue(
        typeof r.reviewerId === "string" &&
            r.reviewerId === r.reviewerId.trim() &&
            r.reviewerId.length > 0 &&
            r.reviewerId.length <= 120 &&
            [...r.reviewerId].every(
                (c) => c.charCodeAt(0) >= 32 && c.charCodeAt(0) !== 127,
            ) &&
            exposures.some((e) => e === r.exposure) &&
            isUtc(r.declaredAtUtc) &&
            time(r.declaredAtUtc) >= time(p.cutoffUtc) &&
            isViewingTime(p, r.maximumViewedUtc) &&
            Array.isArray(r.labels),
        "Indique revisor, exposición y cronología válida.",
    );
    const review = r as unknown as Review;
    const { reveal } = review;
    requireValue(
        review.exposure !== "PreviouslyExposed" || reveal?.afterRevision === 0,
        "La exposición previa debe quedar registrada desde la declaración.",
    );
    if (reveal !== null) {
        objectWith(reveal, "recordedAtUtc afterRevision", "revelación externa");
        requireValue(
            isUtc(reveal.recordedAtUtc) &&
                time(reveal.recordedAtUtc) >= time(review.declaredAtUtc) &&
                Number.isSafeInteger(reveal.afterRevision) &&
                reveal.afterRevision >= 0 &&
                reveal.afterRevision <= review.labels.length,
            "Cronología de revelación incompatible.",
        );
    }
    let lastTime = time(review.declaredAtUtc);
    let lastView = -Infinity;
    for (const [index, label] of review.labels.entries()) {
        objectWith(
            label,
            "revision targetId observation kind references note reviewedAtUtc viewingCutoffUtc maximumViewedUtc phase",
            "etiqueta",
        );
        requireValue(
            Object.hasOwn(targets, label.observation) &&
                label.targetId === `${p.sessionId}/${label.observation}` &&
                label.revision === index + 1 &&
                kinds.includes(label.kind) &&
                isUtc(label.reviewedAtUtc) &&
                time(label.reviewedAtUtc) >= lastTime &&
                isViewingTime(p, label.viewingCutoffUtc) &&
                isViewingTime(p, label.maximumViewedUtc) &&
                time(label.viewingCutoffUtc) <= time(label.maximumViewedUtc) &&
                time(label.maximumViewedUtc) <= time(review.maximumViewedUtc) &&
                time(label.maximumViewedUtc) >= lastView &&
                typeof label.note === "string" &&
                label.note.length > 0 &&
                label.note.length <= 2000 &&
                label.note === label.note.trim() &&
                Array.isArray(label.references),
            "Etiqueta, fecha de revisión o número de revisión incompatible.",
        );
        const after = reveal !== null && label.revision > reveal.afterRevision;
        requireValue(
            label.phase ===
                (after ? "PostRevealCorrection" : "PreRevealJudgment") &&
                (reveal === null ||
                    (after
                        ? time(label.reviewedAtUtc) >=
                          time(reveal.recordedAtUtc)
                        : time(label.reviewedAtUtc) <=
                          time(reveal.recordedAtUtc))),
            "No se puede cambiar el juicio anterior a la revelación.",
        );
        requireValue(
            label.kind === "Reference"
                ? label.references.length > 0
                : label.kind === "Ambiguous" || label.references.length === 0,
            "Seleccione evidencia para una referencia o registre un motivo sin velas.",
        );
        const seen = new Set<string>();
        for (const ref of label.references) {
            objectWith(
                ref,
                "timeframe originalSha256 nativeOpen openUtc closeUtc",
                "referencia de vela",
            );
            const key = `${ref.timeframe}/${ref.openUtc}`;
            requireValue(
                frames.includes(ref.timeframe) &&
                    canPick(label.observation, ref.timeframe) &&
                    !seen.has(key) &&
                    time(ref.closeUtc) <= time(label.viewingCutoffUtc) &&
                    same(ref, reference(p, ref.timeframe, ref.openUtc)),
                "Referencia duplicada, futura o ajena a la fuente original.",
            );
            seen.add(key);
        }
        lastTime = time(label.reviewedAtUtc);
        lastView = time(label.maximumViewedUtc);
    }
    return review;
}

export function loadReview(p: OriginalPackage) {
    const current = localStorage.getItem(localKey(p));
    return current === null ? null : validateReview(p, JSON.parse(current));
}

export function persistReview(
    p: OriginalPackage,
    previous: Review | null,
    next: Review,
) {
    requireValue(
        same(previous, loadReview(p)),
        "Otra pestaña cambió el historial. Recargue antes de continuar.",
    );
    localStorage.setItem(localKey(p), JSON.stringify(validateReview(p, next)));
}

export function exportReview(p: OriginalPackage, r: Review) {
    return seal(reviewSchema, validateReview(p, r));
}

export async function importReview(
    p: OriginalPackage,
    current: Review | null,
    text: string,
) {
    const { value } = await unseal(text, reviewSchema);
    const incoming = validateReview(p, value);
    if (current)
        requireValue(
            current.reviewerId === incoming.reviewerId &&
                current.exposure === incoming.exposure &&
                current.declaredAtUtc === incoming.declaredAtUtc &&
                time(incoming.maximumViewedUtc) >=
                    time(current.maximumViewedUtc) &&
                (!current.reveal || same(current.reveal, incoming.reveal)) &&
                (current.reveal ||
                    !incoming.reveal ||
                    incoming.reveal.afterRevision >= current.labels.length) &&
                same(
                    current.labels,
                    incoming.labels.slice(0, current.labels.length),
                ),
            "El archivo sustituiría exposición, revelación o revisiones anteriores.",
        );
    return incoming;
}
