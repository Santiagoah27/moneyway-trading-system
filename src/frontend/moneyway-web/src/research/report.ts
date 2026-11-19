// Projection/validation only. All detector decisions originate in the C# V1 runner.
export type Candle = {
    providerId: { value: string };
    symbol: { value: string };
    timeframe: { amount: number; unit: string };
    openTimeUtc: string;
    closeTimeUtc: string;
    open: number;
    high: number;
    low: number;
    close: number;
    volume: number | null;
};
export type Context = {
    seriesId: string;
    providerId: string;
    symbol: string;
    timeframe: string;
    direction: "Ascending" | "Descending";
    evaluationCutoffUtc: string;
    dataOrigin: "Synthetic" | "RetrospectiveDocumentary";
    provenance: string;
    historicalAvailability: string;
    initialImpulsePrice: number | null;
    initialization: {
        referenceId: string;
        anchorOpenTimeUtc: string;
        availableAtUtc: string;
        sourceReference: string;
    } | null;
};
export type Result = {
    algorithm: {
        algorithmId: string;
        algorithmVersion: string;
        startCondition: string;
    };
    status: string;
    reason: string;
    input: Context;
    pullbackStart: Candle | null;
    proposedReferencePrice: number | null;
    referenceCandles: Candle[];
    aggregationCandles: Candle[];
    continuationCandle: Candle | null;
    confirmationAtUtc: string | null;
    isExperimentalReferenceConfirmed: boolean;
    abstained: boolean;
    classification: string;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    mentorRuleConfirmed: false;
};
export type Pair = {
    leftAlgorithm: string;
    rightAlgorithm: string;
    fullAgreement: boolean;
    confirmedReferenceAgreement: boolean | null;
    explanation: string;
    confirmationLatencyDifference: string | null;
    pullbackLatencyDifference: string | null;
};
export type Report = {
    input: Context;
    evidenceCandles: Candle[];
    results: Result[];
    comparisons: Pair[];
    sharedAssumptions: string[];
    schemaVersion: string;
    scope: string;
    allAlgorithmsAgree: boolean;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};
export type Envelope = {
    inputSha256: string;
    inputHashMeaning: string;
    comparison: Report;
};
export const algorithmIds = ["A", "B", "C", "D"].map(
    (k) => `EXP-M5-SWING-${k}-V1`,
);
const statuses = [
    "ExperimentalReferenceConfirmed",
    "AwaitingPullbackStart",
    "AwaitingContinuation",
    "AbstainedAmbiguousStartAndContinuation",
    "AbstainedNoPullbackBeforeContinuation",
    "AbstainedMissingInitialization",
    "AbstainedInitializationUnavailable",
    "AbstainedLateInitialization",
    "RejectedInput",
];
function require(value: unknown): asserts value {
    if (!value)
        throw new Error(
            "Reporte experimental inválido o no compatible. No se sustituirá por otro episodio.",
        );
}
const object = (v: unknown): v is Record<string, unknown> =>
    typeof v === "object" && v !== null && !Array.isArray(v);
const text = (v: unknown): v is string =>
    typeof v === "string" && v.length > 0 && v.length <= 4096;
const finite = (v: unknown): v is number =>
    typeof v === "number" && Number.isFinite(v);
const utc = (v: unknown): v is string =>
    typeof v === "string" &&
    /(?:Z|\+00:00)$/.test(v) &&
    Number.isFinite(Date.parse(v));
const time = (v: string) => Date.parse(v);

export function readReport(value: unknown): Envelope {
    require(
        object(value) &&
            typeof value.inputSha256 === "string" &&
            /^[a-f0-9]{64}$/.test(value.inputSha256) &&
            text(value.inputHashMeaning),
    );
    const r = value.comparison;
    require(
        object(r) &&
            r.schemaVersion === "1" &&
            r.scope ===
                "IsolatedExperimentalResearch_NotCanonicalStrategyEvaluation",
    );
    require(
        r.canonicalAdmissionPermitted === false &&
            r.operationalSignal === false &&
            typeof r.allAlgorithmsAgree === "boolean",
    );
    const c = r.input;
    require(
        object(c) &&
            text(c.seriesId) &&
            text(c.providerId) &&
            text(c.symbol) &&
            c.timeframe === "5m",
    );
    require(
        ["Ascending", "Descending"].includes(String(c.direction)) &&
            ["Synthetic", "RetrospectiveDocumentary"].includes(
                String(c.dataOrigin),
            ),
    );
    require(
        utc(c.evaluationCutoffUtc) &&
            text(c.provenance) &&
            c.historicalAvailability ===
                "Unauthenticated_NotEstablishedByThisExperiment",
    );
    require(c.initialImpulsePrice === null || finite(c.initialImpulsePrice));
    if (c.initialization !== null) {
        require(
            object(c.initialization) &&
                text(c.initialization.referenceId) &&
                text(c.initialization.sourceReference) &&
                utc(c.initialization.anchorOpenTimeUtc) &&
                utc(c.initialization.availableAtUtc),
        );
    }
    require(
        Array.isArray(r.evidenceCandles) && r.evidenceCandles.length <= 10000,
    );
    const candles = new Map<string, Candle>();
    let previousClose = -Infinity;
    for (const bar of r.evidenceCandles) {
        require(
            object(bar) &&
                object(bar.providerId) &&
                bar.providerId.value === c.providerId &&
                object(bar.symbol) &&
                bar.symbol.value === c.symbol,
        );
        require(
            object(bar.timeframe) &&
                bar.timeframe.amount === 5 &&
                bar.timeframe.unit === "Minute",
        );
        require(
            utc(bar.openTimeUtc) &&
                utc(bar.closeTimeUtc) &&
                time(bar.closeTimeUtc) > time(bar.openTimeUtc) &&
                time(bar.openTimeUtc) >= previousClose &&
                time(bar.closeTimeUtc) <= time(c.evaluationCutoffUtc),
        );
        require(
            finite(bar.open) &&
                finite(bar.high) &&
                finite(bar.low) &&
                finite(bar.close) &&
                bar.high >= Math.max(bar.open, bar.close, bar.low) &&
                bar.low <= Math.min(bar.open, bar.close),
        );
        require(bar.volume === null || (finite(bar.volume) && bar.volume >= 0));
        candles.set(bar.openTimeUtc, bar as Candle);
        previousClose = time(bar.closeTimeUtc);
    }
    // Marker identity includes original OHLC and source, not merely array position.
    const member = (v: unknown): v is Candle => {
        if (!object(v) || typeof v.openTimeUtc !== "string") return false;
        const source = candles.get(v.openTimeUtc);
        return (
            !!source &&
            [
                "openTimeUtc",
                "closeTimeUtc",
                "open",
                "high",
                "low",
                "close",
                "volume",
            ].every((k) => v[k] === source[k as keyof Candle]) &&
            JSON.stringify(v.providerId) ===
                JSON.stringify(source.providerId) &&
            JSON.stringify(v.symbol) === JSON.stringify(source.symbol) &&
            JSON.stringify(v.timeframe) === JSON.stringify(source.timeframe)
        );
    };
    if (candles.size && c.initialization !== null) {
        require(object(c.initialization));
        const anchor = candles.get(String(c.initialization.anchorOpenTimeUtc));
        require(
            anchor &&
                c.initialImpulsePrice ===
                    (c.direction === "Ascending" ? anchor.high : anchor.low),
        );
    }
    require(
        Array.isArray(r.sharedAssumptions) && r.sharedAssumptions.every(text),
    );
    require(Array.isArray(r.results) && r.results.length === 4);
    const ids = new Set<string>();
    for (const row of r.results) {
        require(
            object(row) &&
                object(row.algorithm) &&
                algorithmIds.includes(String(row.algorithm.algorithmId)) &&
                row.algorithm.algorithmVersion === "1.0.0" &&
                text(row.algorithm.startCondition),
        );
        require(!ids.has(String(row.algorithm.algorithmId)));
        ids.add(String(row.algorithm.algorithmId));
        require(
            statuses.includes(String(row.status)) &&
                text(row.reason) &&
                row.classification ===
                    "ExperimentalMoneyWayHypothesis_NotMentorConfirmed",
        );
        require(
            row.canonicalAdmissionPermitted === false &&
                row.operationalSignal === false &&
                row.mentorRuleConfirmed === false &&
                typeof row.abstained === "boolean",
        );
        require(JSON.stringify(row.input) === JSON.stringify(c));
        require(row.pullbackStart === null || member(row.pullbackStart));
        require(
            row.continuationCandle === null || member(row.continuationCandle),
        );
        require(
            Array.isArray(row.referenceCandles) &&
                row.referenceCandles.every(member) &&
                Array.isArray(row.aggregationCandles) &&
                row.aggregationCandles.every(member),
        );
        require(
            row.proposedReferencePrice === null ||
                finite(row.proposedReferencePrice),
        );
        require(
            row.abstained ===
                (String(row.status).startsWith("Abstained") ||
                    row.status === "RejectedInput"),
        );
        if (row.proposedReferencePrice !== null) {
            require(
                member(row.pullbackStart) && row.referenceCandles.length > 0,
            );
            require(
                row.referenceCandles.every(
                    (bar: Candle) =>
                        (c.direction === "Ascending" ? bar.low : bar.high) ===
                        row.proposedReferencePrice,
                ),
            );
        }
        require(
            row.isExperimentalReferenceConfirmed ===
                (row.status === "ExperimentalReferenceConfirmed"),
        );
        if (row.isExperimentalReferenceConfirmed) {
            require(
                member(row.continuationCandle) &&
                    member(row.pullbackStart) &&
                    utc(row.confirmationAtUtc) &&
                    row.confirmationAtUtc ===
                        row.continuationCandle.closeTimeUtc &&
                    row.referenceCandles.length > 0 &&
                    finite(row.proposedReferencePrice),
            );
            require(
                time(row.pullbackStart.closeTimeUtc) <
                    time(row.confirmationAtUtc),
            );
            require(
                row.referenceCandles.every(
                    (bar: Candle) =>
                        time(bar.closeTimeUtc) <
                        time(String(row.confirmationAtUtc)),
                ),
            );
        } else require(row.confirmationAtUtc === null);
    }
    require(Array.isArray(r.comparisons) && r.comparisons.length === 6);
    const pairs = new Set<string>();
    for (const pair of r.comparisons) {
        require(
            object(pair) &&
                ids.has(String(pair.leftAlgorithm)) &&
                ids.has(String(pair.rightAlgorithm)) &&
                String(pair.leftAlgorithm) < String(pair.rightAlgorithm),
        );
        const key = `${pair.leftAlgorithm}/${pair.rightAlgorithm}`;
        require(!pairs.has(key));
        pairs.add(key);
        require(
            typeof pair.fullAgreement === "boolean" &&
                (typeof pair.confirmedReferenceAgreement === "boolean" ||
                    pair.confirmedReferenceAgreement === null) &&
                text(pair.explanation),
        );
        require(
            [
                pair.confirmationLatencyDifference,
                pair.pullbackLatencyDifference,
            ].every((v) => v === null || text(v)),
        );
    }
    // No hypothesis, verdict, agreement or eligibility is calculated here.
    return value as unknown as Envelope;
}

export const formatTime = (v: string | null) =>
    v ? `${v.slice(0, 10)} ${v.slice(11, 19)} UTC` : "Sin confirmación";
