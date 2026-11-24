import type { Candle } from "../report";

export const frames = ["H4", "H1", "M5", "M1"] as const;
export type Frame = (typeof frames)[number];
const minutes = { H4: 240, H1: 60, M5: 5, M1: 1 };
const provider = "oanda-global-markets-mt5-demo";
const originalSchema = "moneyway.nasdaq-blind-original.v1";
const juneDigest =
    "3ecd8aec92c7ba5b5f36b27cc41d709512bf11797259be799c9f050aaf06a67c";
const syntheticDay = "2000-01-03";
const developmentDay = "2026-06-23";
export const maxFileBytes = 8 * 1024 * 1024;

type OriginalBar = {
    nativeOpen: string;
    openUtc: string;
    closeUtc: string;
    open: number;
    high: number;
    low: number;
    close: number;
    volume: number | null;
};
type OriginalSource = {
    originalSha256: string;
    mapping: "DocumentaryAudited" | "Unresolved" | "Synthetic";
    bars: OriginalBar[];
    coverage: ReturnType<typeof coverage>;
};
type OriginalPayload = {
    sessionId: string;
    operatingDate: string;
    cutoffUtc: string;
    exposure: "Development" | "Synthetic";
    provider: typeof provider;
    instrument: "US100";
    provenanceSha256: string;
    sources: Record<Frame, OriginalSource>;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};
export type OriginalPackage = OriginalPayload & { sha256: string };

export const isPinnedDevelopment = (packet: OriginalPackage) =>
    packet.exposure === "Development" &&
    packet.operatingDate === developmentDay &&
    packet.provenanceSha256 === juneDigest;

export function requireValue(
    condition: unknown,
    message: string,
): asserts condition {
    if (!condition) throw new Error(message);
}

export function objectWith(value: unknown, keys: string, name: string) {
    requireValue(
        value && typeof value === "object" && !Array.isArray(value),
        `Objeto inválido en ${name}.`,
    );
    const object = value as Record<string, unknown>;
    const expected = keys.split(" ");
    requireValue(
        Object.keys(object).length === expected.length &&
            expected.every((key) => Object.hasOwn(object, key)),
        `Campos incompatibles en ${name}.`,
    );
    return object;
}

export function isSha256(value: unknown): value is string {
    return typeof value === "string" && /^[0-9a-f]{64}$/.test(value);
}

export function isUtc(value: unknown): value is string {
    if (
        typeof value !== "string" ||
        !/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{3})?Z$/.test(value)
    )
        return false;
    const time = Date.parse(value);
    return (
        Number.isFinite(time) &&
        new Date(time).toISOString() ===
            (value.length === 20 ? value.replace("Z", ".000Z") : value)
    );
}

export function coverage(bars: OriginalBar[]) {
    return {
        count: bars.length,
        firstOpenUtc: bars[0]?.openUtc ?? null,
        lastClosedUtc: bars.at(-1)?.closeUtc ?? null,
        recordedGaps: bars.flatMap((b, i) =>
            i > 0 && Date.parse(bars[i - 1].closeUtc) < Date.parse(b.openUtc)
                ? [{ startUtc: bars[i - 1].closeUtc, endUtc: b.openUtc }]
                : [],
        ),
    };
}

const bogota = new Intl.DateTimeFormat("en-CA", {
    timeZone: "America/Bogota",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
});

function operatingDate(utc: string) {
    const parts = bogota.formatToParts(new Date(utc));
    return ["year", "month", "day"]
        .map((type) => parts.find((part) => part.type === type)?.value)
        .join("-");
}

export async function sha256(text: string) {
    requireValue(
        globalThis.crypto?.subtle,
        "SHA-256 requiere localhost o un contexto seguro.",
    );
    const bytes = await crypto.subtle.digest(
        "SHA-256",
        new TextEncoder().encode(text),
    );
    return Array.from(new Uint8Array(bytes), (n) =>
        n.toString(16).padStart(2, "0"),
    ).join("");
}

export async function seal(schema: string, value: unknown) {
    const payload = JSON.stringify(value);
    return JSON.stringify({ schema, payload, sha256: await sha256(payload) });
}

export async function unseal(text: string, schema: string) {
    requireValue(
        text.length <= maxFileBytes,
        "El archivo excede el límite de 8 MiB.",
    );
    const envelope = objectWith(
        JSON.parse(text.replace(/^\uFEFF/, "")),
        "schema payload sha256",
        "envoltura",
    );
    requireValue(
        envelope.schema === schema &&
            typeof envelope.payload === "string" &&
            isSha256(envelope.sha256),
        "Esquema de revisión incompatible.",
    );
    requireValue(
        (await sha256(envelope.payload)) === envelope.sha256,
        "El SHA-256 del archivo no coincide.",
    );
    return {
        value: JSON.parse(envelope.payload) as unknown,
        sha256: envelope.sha256,
    };
}

function parsePayload(
    value: unknown,
    trustedDevelopment: boolean,
): OriginalPayload {
    const p = objectWith(
        value,
        "sessionId operatingDate cutoffUtc exposure provider instrument provenanceSha256 sources canonicalAdmissionPermitted operationalSignal",
        "paquete original",
    );
    requireValue(
        (p.exposure === "Synthetic" &&
            p.operatingDate === syntheticDay &&
            p.sessionId === "synthetic-us100-v1") ||
            (trustedDevelopment &&
                p.exposure === "Development" &&
                p.operatingDate === developmentDay &&
                p.sessionId === "nasdaq-context-20260623-v1"),
        "Sesión o exposición fuera del alcance permitido. Las particiones protegidas permanecen cerradas.",
    );
    requireValue(
        p.provider === provider &&
            p.instrument === "US100" &&
            isUtc(p.cutoffUtc) &&
            operatingDate(p.cutoffUtc) === p.operatingDate &&
            isSha256(p.provenanceSha256) &&
            p.canonicalAdmissionPermitted === false &&
            p.operationalSignal === false,
        "Identidad US100, procedencia, fecha Bogotá o corte incompatibles.",
    );
    const sources = objectWith(
        p.sources,
        frames.join(" "),
        "temporalidades originales",
    );
    for (const tf of frames) {
        const s = objectWith(
            sources[tf],
            "originalSha256 mapping bars coverage",
            tf,
        );
        requireValue(
            isSha256(s.originalSha256) &&
                Array.isArray(s.bars) &&
                (s.mapping === "Unresolved"
                    ? s.bars.length === 0
                    : s.mapping ===
                      (p.exposure === "Synthetic"
                          ? "Synthetic"
                          : "DocumentaryAudited")),
            `Procedencia o mapeo incompatibles para ${tf}.`,
        );
        let previous = -Infinity;
        const native = new Set<string>();
        for (const item of s.bars) {
            const b = objectWith(
                item,
                "nativeOpen openUtc closeUtc open high low close volume",
                "vela original",
            );
            requireValue(
                typeof b.nativeOpen === "string" &&
                    /^\d{4}\.\d\d\.\d\d \d\d:\d\d:\d\d$/.test(b.nativeOpen) &&
                    isUtc(
                        b.nativeOpen.replaceAll(".", "-").replace(" ", "T") +
                            "Z",
                    ) &&
                    !native.has(b.nativeOpen) &&
                    isUtc(b.openUtc) &&
                    isUtc(b.closeUtc),
                `Identidad de vela incompatible en ${tf}.`,
            );
            const open = Date.parse(b.openUtc);
            const close = Date.parse(b.closeUtc);
            requireValue(
                open >= previous &&
                    close - open === minutes[tf] * 60_000 &&
                    close <= Date.parse(p.cutoffUtc),
                `Vela posterior al corte o intervalo incompatible en ${tf}.`,
            );
            const ohlc = [b.open, b.high, b.low, b.close];
            requireValue(
                ohlc.every(
                    (n) => typeof n === "number" && Number.isFinite(n),
                ) &&
                    Number(b.low) <=
                        Math.min(Number(b.open), Number(b.close)) &&
                    Number(b.high) >=
                        Math.max(
                            Number(b.low),
                            Number(b.open),
                            Number(b.close),
                        ) &&
                    (b.volume === null ||
                        (typeof b.volume === "number" &&
                            Number.isSafeInteger(b.volume) &&
                            b.volume >= 0)),
                `OHLC o volumen incompatible en ${tf}.`,
            );
            native.add(b.nativeOpen);
            previous = close;
        }
        requireValue(
            JSON.stringify(s.coverage) === JSON.stringify(coverage(s.bars)),
            `La cobertura declarada no coincide con las velas de ${tf}.`,
        );
    }
    return p as OriginalPayload;
}

export async function readOriginalPackage(
    text: string,
): Promise<OriginalPackage> {
    const { value, sha256: digest } = await unseal(text, originalSchema);
    // External packets in V1 can only contain clearly identified synthetic evidence.
    const packet = parsePayload(value, false);
    for (const frame of frames)
        requireValue(
            (await sha256(JSON.stringify(packet.sources[frame].bars))) ===
                packet.sources[frame].originalSha256,
            `El SHA-256 de la fuente ${frame} no coincide.`,
        );
    requireValue(
        (await sha256(JSON.stringify(packet.sources))) ===
            packet.provenanceSha256,
        "La procedencia sintética no coincide.",
    );
    return { ...packet, sha256: digest };
}

export async function projectDevelopment(
    text: string,
): Promise<OriginalPackage> {
    requireValue(
        (await sha256(text)) === juneDigest,
        "El SHA-256 de Development no coincide.",
    );
    const raw = JSON.parse(text);
    requireValue(
        raw.classification ===
            "PreviouslyExposed_RetrospectiveDocumentaryResearch" &&
            raw.canonicalAdmissionPermitted === false &&
            raw.operationalSignal === false,
        "La fuente Development perdió su clasificación documental.",
    );
    const sources = Object.fromEntries(
        frames.map((tf) => {
            const s = raw.series[tf];
            requireValue(
                s.sourceTimeframe === tf[1] + tf[0] && s.derived === false,
                "Se requiere una fuente original por escala.",
            );
            const bars = s.candles.map(
                (b: Candle & { nativeOpenTime: string }) => ({
                    nativeOpen: b.nativeOpenTime,
                    openUtc: b.openTimeUtc,
                    closeUtc: b.closeTimeUtc,
                    open: b.open,
                    high: b.high,
                    low: b.low,
                    close: b.close,
                    volume: b.volume,
                }),
            );
            return [
                tf,
                {
                    originalSha256: s.rawSha256,
                    mapping: "DocumentaryAudited",
                    bars,
                    coverage: coverage(bars),
                },
            ];
        }),
    );
    const value = parsePayload(
        {
            sessionId: raw.id,
            operatingDate: raw.tradingDay,
            cutoffUtc: raw.maximumCutoffUtc,
            exposure: "Development",
            provider: raw.providerId,
            instrument: raw.symbol,
            provenanceSha256: juneDigest,
            sources,
            canonicalAdmissionPermitted: false,
            operationalSignal: false,
        },
        true,
    );
    return { ...value, sha256: await sha256(JSON.stringify(value)) };
}

export async function syntheticPacket() {
    const start = Date.parse(syntheticDay + "T12:00:00Z");
    const sources = Object.fromEntries(
        await Promise.all(
            frames.map(async (tf, scale) => {
                const bars = Array.from(
                    { length: [2, 8, 24, 36][scale] },
                    (_, index): OriginalBar => {
                        const step = minutes[tf] * 60_000;
                        const open = 100 + (index % 5);
                        const openUtc = new Date(
                            start + index * step,
                        ).toISOString();
                        return {
                            nativeOpen: openUtc
                                .slice(0, 19)
                                .replaceAll("-", ".")
                                .replace("T", " "),
                            openUtc,
                            closeUtc: new Date(
                                start + (index + 1) * step,
                            ).toISOString(),
                            open,
                            high: open + 3,
                            low: open - 2,
                            close: open + 1,
                            volume: index + 1,
                        };
                    },
                );
                return [
                    tf,
                    {
                        originalSha256: await sha256(JSON.stringify(bars)),
                        mapping: "Synthetic",
                        bars,
                        coverage: coverage(bars),
                    },
                ];
            }),
        ),
    );
    return seal(originalSchema, {
        sessionId: "synthetic-us100-v1",
        operatingDate: syntheticDay,
        cutoffUtc: syntheticDay + "T20:00:00Z",
        exposure: "Synthetic",
        provider,
        instrument: "US100",
        provenanceSha256: await sha256(JSON.stringify(sources)),
        sources,
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    });
}

export function chartCandle(tf: Frame, b: OriginalBar): Candle {
    return {
        providerId: { value: provider },
        symbol: { value: "US100" },
        timeframe: {
            amount: tf === "H4" ? 4 : tf === "M5" ? 5 : 1,
            unit: tf.startsWith("H") ? "Hour" : "Minute",
        },
        openTimeUtc: b.openUtc,
        closeTimeUtc: b.closeUtc,
        open: b.open,
        high: b.high,
        low: b.low,
        close: b.close,
        volume: b.volume,
    };
}
