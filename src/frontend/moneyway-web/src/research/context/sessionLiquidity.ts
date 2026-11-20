import raw from "./data/june23-session-liquidity.json";
import type { Candle } from "../report";
import type { ContextData } from "./model";

export type SessionReference = {
    strategyVersion: string;
    operatingDate: string;
    session: "Asia" | "London";
    provider: string;
    instrument: string;
    sourceTimeframe: string;
    startUtc: string;
    endUtc: string;
    evaluatedAtUtc: string;
    coverageStatus: string;
    expectedCount: number;
    sourceCount: number;
    missingOpensUtc: string[];
    high: number | null;
    low: number | null;
    highCandles: Candle[];
    lowCandles: Candle[];
    availableAtUtc: string | null;
    sourceAuthority: string;
};
export type SessionReport = {
    schemaVersion: number;
    contextId: string;
    inputSha256: string;
    candidateVersion: string;
    provenance: string;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    sourceProvenance: { rawSha256: string; normalizedSha256: string };
    frames: { cutoffUtc: string; sessions: SessionReference[] }[];
};
export const sessionReport = raw as unknown as SessionReport;
const utc = (s: string) =>
    /(?:Z|\+00:00)$/.test(s) && Number.isFinite(Date.parse(s));
export function readSessionReport(data: ContextData, report = sessionReport) {
    const fail = () => {
        throw new Error(
            "Referencias de sesión no disponibles: identidad, cobertura o cutoff incompatibles.",
        );
    };
    if (
        report.schemaVersion !== 1 ||
        report.contextId !== data.id ||
        report.candidateVersion !== "nasdaq-0.3.0-session-candidate" ||
        report.canonicalAdmissionPermitted !== false ||
        report.operationalSignal !== false ||
        !/^[a-f0-9]{64}$/.test(report.inputSha256) ||
        report.sourceProvenance.rawSha256 !== data.series.H1.rawSha256 ||
        report.sourceProvenance.normalizedSha256 !==
            data.series.H1.normalizedSha256
    )
        fail();
    let previous = -Infinity;
    for (const frame of report.frames) {
        if (
            !utc(frame.cutoffUtc) ||
            Date.parse(frame.cutoffUtc) <= previous ||
            Date.parse(frame.cutoffUtc) > Date.parse(data.maximumCutoffUtc) ||
            frame.sessions.length !== 2
        )
            fail();
        previous = Date.parse(frame.cutoffUtc);
        for (const [i, s] of frame.sessions.entries()) {
            if (
                s.session !== (i === 0 ? "Asia" : "London") ||
                s.strategyVersion !== report.candidateVersion ||
                s.provider !== data.providerId ||
                s.instrument !== data.symbol ||
                s.operatingDate !== data.tradingDay ||
                s.sourceTimeframe !== "1h" ||
                s.sourceAuthority !== "AuditedExactH1" ||
                !utc(s.startUtc) ||
                !utc(s.endUtc) ||
                Date.parse(s.startUtc) >= Date.parse(s.endUtc) ||
                s.evaluatedAtUtc !== frame.cutoffUtc
            )
                fail();
            if (s.coverageStatus === "Complete") {
                if (
                    s.sourceCount !== s.expectedCount ||
                    s.missingOpensUtc.length ||
                    s.high === null ||
                    s.low === null ||
                    !Number.isFinite(s.high) ||
                    !Number.isFinite(s.low) ||
                    !s.highCandles.length ||
                    !s.lowCandles.length ||
                    s.availableAtUtc !== frame.cutoffUtc ||
                    Date.parse(s.endUtc) > Date.parse(frame.cutoffUtc)
                )
                    fail();
                for (const side of ["high", "low"] as const)
                    for (const candle of side === "high"
                        ? s.highCandles
                        : s.lowCandles) {
                        const source = data.series.H1.candles.find(
                            (c) =>
                                Date.parse(c.openTimeUtc) ===
                                Date.parse(candle.openTimeUtc),
                        );
                        if (
                            !source ||
                            source[side] !== s[side] ||
                            candle[side] !== source[side] ||
                            candle.providerId.value !== data.providerId ||
                            candle.symbol.value !== data.symbol ||
                            Date.parse(candle.openTimeUtc) <
                                Date.parse(s.startUtc) ||
                            Date.parse(candle.closeTimeUtc) >
                                Date.parse(s.endUtc) ||
                            Date.parse(candle.closeTimeUtc) >
                                Date.parse(frame.cutoffUtc)
                        )
                            fail();
                    }
            } else if (
                s.high !== null ||
                s.low !== null ||
                s.availableAtUtc !== null ||
                s.highCandles.length ||
                s.lowCandles.length
            )
                fail();
        }
    }
    return report;
}
export function sessionReferencesAt(
    data: ContextData,
    cutoff: string,
    report = sessionReport,
) {
    readSessionReport(data, report);
    if (
        !utc(cutoff) ||
        Date.parse(cutoff) > Date.parse(data.maximumCutoffUtc) ||
        Date.parse(cutoff) < Date.parse(data.requestedStartUtc)
    )
        throw new Error("Cutoff fuera del contexto registrado.");
    return report.frames.findLast(
        (f) => Date.parse(f.cutoffUtc) <= Date.parse(cutoff),
    );
}
