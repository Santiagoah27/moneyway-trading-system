import manifestData from "./data/manifest.json";
import chartData from "./data/candles.json";
import type { Candle, Context, Envelope } from "../report";
import { readReport } from "../report";

export type Partition =
    "Development" | "Validation" | "PreviouslyExposed" | "SoftwareAcceptance";
export type Episode = {
    id: string;
    datasetId: string;
    datasetVersion: number;
    partition: Partition;
    seriesId: string;
    providerId: string;
    symbol: string;
    timeframe: string;
    startUtc: string;
    cutoffUtc: string;
    direction: "Ascending" | "Descending";
    anchorOpenUtc: string;
    anchorPrice: number;
    candleCount: number;
    dataOrigin: "Synthetic" | "RetrospectiveDocumentary";
    priorExposure: "PreviouslyExposed" | "NotDeclaredExposed";
    inputSha256: string;
    reportSha256: string;
    nativeOpenIdentities: string[];
};
export type ChartInput = { input: Context; evidenceCandles: Candle[] };
export type Catalog = {
    schemaVersion: number;
    datasetId: string;
    datasetVersion: number;
    datasetContentSha256: string;
    heldOutEpisodeCount: number;
    uniqueAuthenticSourceCandleCount: number;
    splitAssessment: string;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    episodes: Episode[];
};
const check = (v: unknown) => {
    if (!v)
        throw new Error(
            "Dataset de investigación inválido o incompatible; no se sustituirá.",
        );
};
export function validateCatalog(
    catalog: Catalog,
    charts: Record<string, ChartInput>,
) {
    check(
        catalog.schemaVersion === 1 &&
            catalog.datasetVersion === 1 &&
            catalog.datasetId === "m5-june-documentary-blind-v1" &&
            /^[a-f0-9]{64}$/.test(catalog.datasetContentSha256),
    );
    check(
        catalog.canonicalAdmissionPermitted === false &&
            catalog.operationalSignal === false &&
            catalog.episodes.length > 0,
    );
    const ids = new Set<string>();
    const sourceIdentities = new Set<string>();
    for (const e of catalog.episodes) {
        check(
            !ids.has(e.id) &&
                e.datasetId === catalog.datasetId &&
                e.datasetVersion === catalog.datasetVersion,
        );
        ids.add(e.id);
        check(
            [
                "Development",
                "Validation",
                "PreviouslyExposed",
                "SoftwareAcceptance",
            ].includes(e.partition),
        );
        check(
            ["Ascending", "Descending"].includes(e.direction) &&
                e.timeframe === "5m",
        );
        check(
            /^[a-f0-9]{64}$/.test(e.inputSha256) &&
                /^[a-f0-9]{64}$/.test(e.reportSha256),
        );
        const chart = charts[e.id];
        check(
            chart &&
                Object.keys(chart).sort().join() === "evidenceCandles,input",
        );
        const c = chart.input;
        check(
            c.seriesId === e.seriesId &&
                c.providerId === e.providerId &&
                c.symbol === e.symbol &&
                c.dataOrigin === e.dataOrigin &&
                c.direction === e.direction &&
                c.timeframe === "5m" &&
                Date.parse(c.evaluationCutoffUtc) === Date.parse(e.cutoffUtc),
        );
        check(
            c.initialization &&
                Date.parse(c.initialization.anchorOpenTimeUtc) ===
                    Date.parse(e.anchorOpenUtc) &&
                c.initialImpulsePrice === e.anchorPrice,
        );
        check(
            chart.evidenceCandles.length === e.candleCount && e.candleCount > 0,
        );
        let last = Date.parse(e.startUtc);
        for (const bar of chart.evidenceCandles) {
            check(
                bar.providerId.value === e.providerId &&
                    bar.symbol.value === e.symbol &&
                    bar.timeframe.amount === 5 &&
                    bar.timeframe.unit === "Minute",
            );
            check(
                /(?:Z|\+00:00)$/.test(bar.openTimeUtc) &&
                    /(?:Z|\+00:00)$/.test(bar.closeTimeUtc) &&
                    Date.parse(bar.openTimeUtc) === last &&
                    Date.parse(bar.closeTimeUtc) - last === 300000 &&
                    Date.parse(bar.closeTimeUtc) <= Date.parse(e.cutoffUtc),
            );
            check(
                [bar.open, bar.high, bar.low, bar.close].every(
                    Number.isFinite,
                ) &&
                    bar.high >= Math.max(bar.open, bar.low, bar.close) &&
                    bar.low <= Math.min(bar.open, bar.close),
            );
            const key = `${e.providerId}/${e.symbol}/${Date.parse(bar.openTimeUtc)}`;
            check(!sourceIdentities.has(key));
            sourceIdentities.add(key);
            last = Date.parse(bar.closeTimeUtc);
        }
        check(last === Date.parse(e.cutoffUtc));
        const anchor = chart.evidenceCandles.find(
            (b) => Date.parse(b.openTimeUtc) === Date.parse(e.anchorOpenUtc),
        );
        check(
            anchor &&
                e.anchorPrice ===
                    (e.direction === "Ascending" ? anchor.high : anchor.low),
        );
    }
    check(Object.keys(charts).length === ids.size);
    const dev = catalog.episodes.filter((e) => e.partition === "Development");
    const val = catalog.episodes.filter((e) => e.partition === "Validation");
    if (dev.length && val.length)
        check(
            Math.max(...dev.map((e) => Date.parse(e.cutoffUtc))) <=
                Math.min(...val.map((e) => Date.parse(e.startUtc))),
        );
    return catalog;
}
export const charts = chartData as unknown as Record<string, ChartInput>;
export const catalog = validateCatalog(
    manifestData as unknown as Catalog,
    charts,
);
export function episodeById(id: string) {
    const e = catalog.episodes.find((e) => e.id === id);
    if (!e)
        throw new Error(
            "Episodio no disponible; held-out no está incluido en esta aplicación.",
        );
    return e;
}

export async function loadRevealedReport(e: Episode): Promise<Envelope> {
    // Called only AFTER persisted commitment/reveal. These are not ordinary chart payloads.
    let raw: string;
    switch (e.id) {
        case "m5-20260623-1300-ascending-v1":
            raw = (
                await import("./data/reports/m5-20260623-1300-ascending-v1.json?raw")
            ).default;
            break;
        case "m5-20260623-1400-descending-v1":
            raw = (
                await import("./data/reports/m5-20260623-1400-descending-v1.json?raw")
            ).default;
            break;
        case "m5-20260625-1300-ascending-v1":
            raw = (
                await import("./data/reports/m5-20260625-1300-ascending-v1.json?raw")
            ).default;
            break;
        case "m5-20260625-1400-descending-v1":
            raw = (
                await import("./data/reports/m5-20260625-1400-descending-v1.json?raw")
            ).default;
            break;
        case "previously-exposed-june29":
            raw = (
                await import("./data/reports/previously-exposed-june29.json?raw")
            ).default;
            break;
        case "software-synthetic":
            raw = (await import("./data/reports/software-synthetic.json?raw"))
                .default;
            break;
        default:
            throw new Error("Reporte no registrado; sin acceso a held-out.");
    }
    const digest = await crypto.subtle.digest(
        "SHA-256",
        new TextEncoder().encode(raw),
    );
    const hash = Array.from(new Uint8Array(digest), (b) =>
        b.toString(16).padStart(2, "0"),
    ).join("");
    if (hash !== e.reportSha256)
        throw new Error(
            "El reporte cambió; referencia experimental no disponible.",
        );
    const envelope = readReport(JSON.parse(raw));
    if (
        envelope.inputSha256 !== e.inputSha256 ||
        JSON.stringify(envelope.comparison.input) !==
            JSON.stringify(charts[e.id].input) ||
        JSON.stringify(envelope.comparison.evidenceCandles) !==
            JSON.stringify(charts[e.id].evidenceCandles)
    )
        throw new Error("Reporte/episodio/cutoff no coinciden.");
    return envelope;
}
