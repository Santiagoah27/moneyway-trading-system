import raw from "./data/june23-london-asia-interaction.json";
import type { Candle } from "../report";
import type { ContextData } from "./model";
import { sessionReport } from "./sessionLiquidity";

type Side = {
    referencePrice: number;
    exactExtremeContact: boolean;
    strictPenetration: boolean;
    closeBeyond: boolean;
    penetrationWithoutCloseBeyond: boolean;
    opensBeyondReference: boolean;
    penetrationMagnitude: number;
};
export type InteractionObservation = {
    candle: Candle;
    observableAtUtc: string;
    asiaHigh: Side;
    asiaLow: Side;
    ordering: string;
};
export type LondonAsiaInteraction = {
    operatingDate: string;
    provider: string;
    instrument: string;
    sourceTimeframe: string;
    cutoffUtc: string;
    algorithmId: string;
    algorithmVersion: string;
    status: string;
    reason: string;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    observations: InteractionObservation[];
    completedClassification: string | null;
    observedUpperMagnitude: number | null;
    observedLowerMagnitude: number | null;
    completedUpperMagnitude: number | null;
    completedLowerMagnitude: number | null;
    firstUpperPenetration: InteractionObservation | null;
    firstLowerPenetration: InteractionObservation | null;
    firstUpperCloseBeyond: InteractionObservation | null;
    firstLowerCloseBeyond: InteractionObservation | null;
    firstUpperExactContact: InteractionObservation | null;
    firstLowerExactContact: InteractionObservation | null;
    firstPenetrationOrdering: string;
    asia: { isComplete: boolean; endUtc: string };
    london: { isComplete: boolean; startUtc: string; endUtc: string };
};
export type InteractionReport = {
    schemaVersion: number;
    contextId: string;
    inputSha256: string;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    frames: { cutoffUtc: string; interaction: LondonAsiaInteraction }[];
};
export const interactionReport = raw as unknown as InteractionReport;
const utc = (s: string) =>
    /(?:Z|\+00:00)$/.test(s) && Number.isFinite(Date.parse(s));
export function readInteractionReport(
    data: ContextData,
    report = interactionReport,
) {
    const fail = () => {
        throw new Error(
            "Interacción London–Asia no disponible: fuente, identidad o cutoff incompatibles.",
        );
    };
    if (
        report.schemaVersion !== 1 ||
        report.contextId !== data.id ||
        report.inputSha256 !== sessionReport.inputSha256 ||
        report.canonicalAdmissionPermitted !== false ||
        report.operationalSignal !== false
    )
        fail();
    let previous = -Infinity;
    for (const frame of report.frames) {
        const r = frame.interaction;
        if (
            !utc(frame.cutoffUtc) ||
            Date.parse(frame.cutoffUtc) <= previous ||
            Date.parse(frame.cutoffUtc) > Date.parse(data.maximumCutoffUtc) ||
            r.cutoffUtc !== frame.cutoffUtc ||
            r.algorithmId !== "EXP-NQ-LONDON-ASIA-INTERACTION-V1" ||
            r.algorithmVersion !== "1.0.0" ||
            r.provider !== data.providerId ||
            r.instrument !== data.symbol ||
            r.operatingDate !== data.tradingDay ||
            r.sourceTimeframe !== "1h" ||
            r.canonicalAdmissionPermitted !== false ||
            r.operationalSignal !== false
        )
            fail();
        previous = Date.parse(frame.cutoffUtc);
        if (
            ![
                "AsiaUnavailable",
                "Partial",
                "PartialWithMissingData",
                "LondonIncomplete",
                "Complete",
            ].includes(r.status)
        )
            fail();
        if (r.status === "Complete") {
            if (
                !r.asia.isComplete ||
                !r.london.isComplete ||
                ![
                    "NoAsiaExtremePenetrated",
                    "AsiaHighOnlyPenetrated",
                    "AsiaLowOnlyPenetrated",
                    "BothAsiaExtremesPenetrated",
                ].includes(r.completedClassification ?? "") ||
                Date.parse(r.london.endUtc) > previous ||
                r.completedUpperMagnitude === null ||
                r.completedLowerMagnitude === null
            )
                fail();
        } else if (
            r.completedClassification !== null ||
            r.completedUpperMagnitude !== null ||
            r.completedLowerMagnitude !== null
        )
            fail();
        if (
            !r.asia.isComplete &&
            (r.observations.length ||
                r.firstUpperPenetration ||
                r.firstLowerPenetration)
        )
            fail();
        for (const o of r.observations) {
            const c = o.candle;
            const source = data.series.H1.candles.find(
                (b) => Date.parse(b.openTimeUtc) === Date.parse(c.openTimeUtc),
            );
            if (
                !source ||
                !utc(c.openTimeUtc) ||
                !utc(c.closeTimeUtc) ||
                c.providerId.value !== data.providerId ||
                c.symbol.value !== data.symbol ||
                c.timeframe.amount !== 1 ||
                c.timeframe.unit !== "Hour" ||
                Date.parse(c.closeTimeUtc) !==
                    Date.parse(source.closeTimeUtc) ||
                (["open", "high", "low", "close"] as const).some(
                    (key) => c[key] !== source[key],
                ) ||
                Date.parse(c.openTimeUtc) < Date.parse(r.london.startUtc) ||
                Date.parse(c.closeTimeUtc) > Date.parse(r.london.endUtc) ||
                Date.parse(c.closeTimeUtc) > previous ||
                o.observableAtUtc !== frame.cutoffUtc
            )
                fail();
        }
        for (const first of [
            r.firstUpperPenetration,
            r.firstLowerPenetration,
            r.firstUpperCloseBeyond,
            r.firstLowerCloseBeyond,
            r.firstUpperExactContact,
            r.firstLowerExactContact,
        ])
            if (
                first &&
                !r.observations.some(
                    (o) => JSON.stringify(o) === JSON.stringify(first),
                )
            )
                fail();
    }
    return report;
}
export function londonAsiaAt(
    data: ContextData,
    cutoff: string,
    report = interactionReport,
) {
    readInteractionReport(data, report);
    if (
        !utc(cutoff) ||
        Date.parse(cutoff) < Date.parse(data.requestedStartUtc) ||
        Date.parse(cutoff) > Date.parse(data.maximumCutoffUtc)
    )
        throw new Error("Cutoff fuera del contexto registrado.");
    return report.frames.findLast(
        (f) => Date.parse(f.cutoffUtc) <= Date.parse(cutoff),
    )?.interaction;
}
