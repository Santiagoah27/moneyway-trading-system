import contextUrl from "./data/june23-context.json?url";
import review from "./data/development-review.json";
import { readReport, type Candle, type Envelope } from "../report";
import { sessionReport } from "./sessionLiquidity";

export type Frame = "H4" | "H1" | "M5" | "M1";
export type ContextCandle = Candle & { nativeOpenTime: string };
export type Gap = { startUtc: string; endUtc: string; classification: string };
export type Series = {
    sourceTimeframe: string;
    derived: false;
    intervalMinutes: number;
    count: number;
    firstOpenUtc: string;
    lastClosedUtc: string;
    sourceGaps: Gap[];
    rawRelativePath: string;
    rawSha256: string;
    normalizedRelativePath: string;
    normalizedSha256: string;
    candles: ContextCandle[];
};
export type ContextData = {
    schemaVersion: number;
    id: string;
    tradingDay: string;
    providerId: string;
    symbol: string;
    timeZone: string;
    classification: string;
    requestedStartUtc: string;
    maximumCutoffUtc: string;
    documentaryMapping: string;
    sessionBoundariesStatus: string;
    windowInstruction: string;
    windowRuntime: string;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    series: Record<Frame, Series>;
};
export const frames: Frame[] = ["H4", "H1", "M5", "M1"];
const check = (v: unknown) => {
    if (!v)
        throw new Error(
            "Contexto documental inválido; no se sustituirá por otra sesión.",
        );
};
const utc = (t: string) =>
    /(?:Z|\+00:00)$/.test(t) && Number.isFinite(Date.parse(t));
export function validateContext(data: ContextData, reviewSession = false) {
    check(
        data.schemaVersion === 1 &&
            data.id ===
                (reviewSession
                    ? "nasdaq-context-20260622-review-v1"
                    : "nasdaq-context-20260623-v1") &&
            data.tradingDay === (reviewSession ? "2026-06-22" : "2026-06-23") &&
            data.providerId === "oanda-global-markets-mt5-demo" &&
            data.symbol === "US100" &&
            data.timeZone === "America/Bogota",
    );
    check(
        data.canonicalAdmissionPermitted === false &&
            data.operationalSignal === false &&
            utc(data.requestedStartUtc) &&
            utc(data.maximumCutoffUtc),
    );
    check(
        data.sessionBoundariesStatus ===
            (reviewSession
                ? "ExplicitCandidateWindows_ADR0028"
                : "UnresolvedForContextualResearch_NoAsiaLondonExtrema"),
    );
    for (const frame of frames) {
        const series = data.series[frame];
        const duration = { H4: 240, H1: 60, M5: 5, M1: 1 }[frame];
        check(
            series &&
                series.derived === false &&
                series.intervalMinutes === duration &&
                series.candles.length === series.count &&
                series.count > 0,
        );
        check(
            series.sourceTimeframe ===
                { H4: "4H", H1: "1H", M5: "5M", M1: "1M" }[frame],
        );
        check(
            /^[a-f0-9]{64}$/.test(series.rawSha256) &&
                /^[a-f0-9]{64}$/.test(series.normalizedSha256),
        );
        let previous = Date.parse(data.requestedStartUtc);
        for (const bar of series.candles) {
            check(
                bar.providerId.value === data.providerId &&
                    bar.symbol.value === data.symbol,
            );
            check(
                bar.timeframe.unit ===
                    (frame.startsWith("H") ? "Hour" : "Minute") &&
                    bar.timeframe.amount ===
                        (frame.startsWith("H") ? duration / 60 : duration),
            );
            check(
                utc(bar.openTimeUtc) &&
                    utc(bar.closeTimeUtc) &&
                    Date.parse(bar.openTimeUtc) >= previous &&
                    Date.parse(bar.closeTimeUtc) -
                        Date.parse(bar.openTimeUtc) ===
                        duration * 60000 &&
                    Date.parse(bar.closeTimeUtc) <=
                        Date.parse(data.maximumCutoffUtc),
            );
            check(
                [bar.open, bar.high, bar.low, bar.close].every(
                    Number.isFinite,
                ) &&
                    bar.high >= Math.max(bar.open, bar.close, bar.low) &&
                    bar.low <= Math.min(bar.open, bar.close),
            );
            check(
                typeof bar.nativeOpenTime === "string" &&
                    bar.nativeOpenTime ===
                        new Date(Date.parse(bar.openTimeUtc) + 3 * 3600000)
                            .toISOString()
                            .slice(0, 19)
                            .replaceAll("-", ".")
                            .replace("T", " "),
            );
            previous = Date.parse(bar.closeTimeUtc);
        }
        check(
            series.firstOpenUtc === series.candles[0].openTimeUtc &&
                series.lastClosedUtc === series.candles.at(-1)!.closeTimeUtc,
        );
    }
    return data;
}
export async function loadContext(signal?: AbortSignal): Promise<ContextData> {
    const response = await fetch(contextUrl, { signal });
    if (!response.ok)
        throw new Error("Contexto documental no disponible; no se sustituirá.");
    const text = await response.text();
    const digest = await crypto.subtle.digest(
        "SHA-256",
        new TextEncoder().encode(text),
    );
    const hash = Array.from(new Uint8Array(digest), (b) =>
        b.toString(16).padStart(2, "0"),
    ).join("");
    if (hash !== sessionReport.inputSha256)
        throw new Error(
            "Contexto documental no disponible: hash fuente incompatible con las referencias de sesión.",
        );
    return validateContext(JSON.parse(text) as ContextData);
}
export type OriginalReview = {
    episodeId: string;
    datasetId: string;
    datasetVersion: number;
    inputSha256: string;
    reportSha256: string;
    originalExposure: string;
    originalRevealRevision: number;
    reviewerAlias: string;
    label: {
        revision: number;
        kind: "Reference";
        selectedOpens: string[];
        extreme: "Low" | "High";
        price: number;
        confirmationOpenUtc: null;
        confirmationCloseUtc: null;
        cutoffUtc: string;
        classification: string;
        canonicalAdmissionPermitted: false;
        operationalSignal: false;
    };
};
export const originalReviews = review as unknown as Omit<
    typeof review,
    "reviews"
> & { reviews: OriginalReview[] };
for (const r of originalReviews.reviews) {
    check(
        originalReviews.sourceSha256 ===
            "8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861" &&
            r.episodeId.startsWith("m5-20260623-") &&
            r.originalExposure === "SelfDeclaredIndependent" &&
            r.originalRevealRevision === 1,
    );
    check(
        r.label.kind === "Reference" &&
            r.label.revision === 1 &&
            r.label.classification === "IndependentRetrospectiveResearch" &&
            r.label.confirmationCloseUtc === null &&
            r.label.canonicalAdmissionPermitted === false &&
            r.label.operationalSignal === false,
    );
}
export function closedAt(data: ContextData, frame: Frame, cutoff: string) {
    if (
        !utc(cutoff) ||
        Date.parse(cutoff) > Date.parse(data.maximumCutoffUtc) ||
        Date.parse(cutoff) < Date.parse(data.requestedStartUtc)
    )
        throw new Error("Cutoff fuera del contexto registrado.");
    return data.series[frame].candles.filter(
        (b) => Date.parse(b.closeTimeUtc) <= Date.parse(cutoff),
    );
}
export function visibleAt(
    data: ContextData,
    frame: Frame,
    cutoff: string,
    hours: number | null,
) {
    const closed = closedAt(data, frame, cutoff);
    return hours === null
        ? closed
        : closed.filter(
              (b) =>
                  Date.parse(b.closeTimeUtc) >
                  Date.parse(cutoff) - hours * 3600000,
          );
}
export function coverageAt(data: ContextData, frame: Frame, cutoff: string) {
    const bars = closedAt(data, frame, cutoff);
    let previous = data.requestedStartUtc;
    const gaps: Gap[] = [];
    for (const b of bars) {
        if (Date.parse(b.openTimeUtc) > Date.parse(previous))
            gaps.push({
                startUtc: previous,
                endUtc: b.openTimeUtc,
                classification: "NoSourceRows_NotATradingScheduleConclusion",
            });
        previous = b.closeTimeUtc;
    }
    const pending =
        bars.length &&
        Date.parse(cutoff) > Date.parse(previous) &&
        Date.parse(cutoff) - Date.parse(previous) <
            data.series[frame].intervalMinutes * 60000;
    if (
        Date.parse(cutoff) - Date.parse(previous) >=
        data.series[frame].intervalMinutes * 60000
    )
        gaps.push({
            startUtc: previous,
            endUtc: cutoff,
            classification: "NoSourceRows_NotATradingScheduleConclusion",
        });
    return {
        count: bars.length,
        firstOpen: bars[0]?.openTimeUtc ?? null,
        lastClose: bars.at(-1)?.closeTimeUtc ?? null,
        gaps,
        pendingInterval: Boolean(pending),
    };
}
export function eligibleReview(id: string, cutoff: string) {
    const r = originalReviews.reviews.find((r) => r.episodeId === id);
    return r &&
        utc(cutoff) &&
        Date.parse(r.label.cutoffUtc) <= Date.parse(cutoff)
        ? r
        : null;
}
export const cutoffOptions = [
    "06:59",
    "07:00",
    "08:00",
    "09:00",
    "10:00",
    "11:00",
    "12:00",
    "12:59",
    "13:00",
    "13:30",
    "14:00",
    "15:00",
    "16:00",
    "16:30",
].map((t) => `2026-06-23T${t}:00Z`);
export const bogota = (t: string) =>
    new Intl.DateTimeFormat("es-CO", {
        timeZone: "America/Bogota",
        dateStyle: "short",
        timeStyle: "short",
        hourCycle: "h23",
    }).format(new Date(t));

export async function loadOriginalComparison(id: string): Promise<Envelope> {
    const r = originalReviews.reviews.find((r) => r.episodeId === id);
    if (!r)
        throw new Error(
            "Comparación no autorizada: solo desarrollo ya revisado.",
        );
    let text: string;
    if (id === "m5-20260623-1300-ascending-v1")
        text = (
            await import("../blind/data/reports/m5-20260623-1300-ascending-v1.json?raw")
        ).default;
    else if (id === "m5-20260623-1400-descending-v1")
        text = (
            await import("../blind/data/reports/m5-20260623-1400-descending-v1.json?raw")
        ).default;
    else throw new Error("No hay comparación contextual registrada.");
    const digest = await crypto.subtle.digest(
        "SHA-256",
        new TextEncoder().encode(text),
    );
    if (
        Array.from(new Uint8Array(digest), (b) =>
            b.toString(16).padStart(2, "0"),
        ).join("") !== r.reportSha256
    )
        throw new Error("El reporte original cambió; no se sustituirá.");
    const envelope = readReport(JSON.parse(text));
    if (
        envelope.inputSha256 !== r.inputSha256 ||
        Date.parse(envelope.comparison.input.evaluationCutoffUtc) !==
            Date.parse(r.label.cutoffUtc)
    )
        throw new Error("Identidad/cutoff original no coinciden.");
    return envelope;
}
