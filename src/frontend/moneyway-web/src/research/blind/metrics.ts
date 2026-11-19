import type { Envelope, Result } from "../report";
import { algorithmIds } from "../report";
import { catalog, type Episode } from "./catalog";
import {
    independentLabel,
    type History,
    type Label,
    type ReviewState,
} from "./labels";

const equalSet = (a: string[], b: string[]) =>
    a.length === b.length &&
    [...a].sort().every((v, i) => v === [...b].sort()[i]);
// Decimal strings, not binary floating-point subtraction or a trading tolerance.
function parts(value: number) {
    const [mantissa, exponent = "0"] = value
        .toString()
        .toLowerCase()
        .split("e");
    const [whole, fraction = ""] = mantissa.split(".");
    let coefficient = BigInt(whole + fraction);
    let scale = fraction.length - Number(exponent);
    if (scale < 0) {
        coefficient *= 10n ** BigInt(-scale);
        scale = 0;
    }
    return { coefficient, scale };
}
export function decimalDifference(left: number, right: number) {
    const a = parts(left),
        b = parts(right);
    const scale = Math.max(a.scale, b.scale);
    const difference =
        a.coefficient * 10n ** BigInt(scale - a.scale) -
        b.coefficient * 10n ** BigInt(scale - b.scale);
    const negative = difference < 0n;
    const digits = (negative ? -difference : difference)
        .toString()
        .padStart(scale + 1, "0");
    const result = scale
        ? `${digits.slice(0, -scale)}.${digits.slice(-scale)}`.replace(
              /\.?0+$/,
              "",
          )
        : digits;
    return (negative ? "-" : "") + result;
}
export type Agreement = {
    outcome:
        | "ExactReference"
        | "IncorrectReference"
        | "TieAmbiguity"
        | "FalseDetection"
        | "MissedDetection"
        | "CorrectNoReference"
        | "NotComparable";
    priceAgreement: boolean | null;
    candleAgreement: boolean | null;
    equalExtremeAgreement: boolean | null;
    signedDifference: string | null;
    absoluteDifference: string | null;
    algorithmConfirmation: string | null;
    humanConfirmation: string | null;
    latencyMilliseconds: number | null;
};
export function compareReference(
    row: Result,
    label: Pick<
        Label,
        "kind" | "price" | "selectedOpens" | "confirmationCloseUtc"
    >,
): Agreement {
    const detected = row.isExperimentalReferenceConfirmed;
    const base = {
        priceAgreement: null,
        candleAgreement: null,
        equalExtremeAgreement: null,
        signedDifference: null,
        absoluteDifference: null,
        algorithmConfirmation: row.confirmationAtUtc,
        humanConfirmation: label.confirmationCloseUtc,
        latencyMilliseconds:
            row.confirmationAtUtc && label.confirmationCloseUtc
                ? Date.parse(row.confirmationAtUtc) -
                  Date.parse(label.confirmationCloseUtc)
                : null,
    };
    if (label.kind === "Ambiguous" || label.kind === "Skip")
        return { ...base, latencyMilliseconds: null, outcome: "NotComparable" };
    if (label.kind === "NoReference")
        return {
            ...base,
            outcome: detected ? "FalseDetection" : "CorrectNoReference",
        };
    if (!detected) return { ...base, outcome: "MissedDetection" };
    const priceAgreement = row.proposedReferencePrice === label.price;
    const candleAgreement = equalSet(
        row.referenceCandles.map((c) => c.openTimeUtc),
        label.selectedOpens,
    );
    const signedDifference = decimalDifference(
        row.proposedReferencePrice!,
        label.price!,
    );
    const tied =
        row.referenceCandles.length > 1 || label.selectedOpens.length > 1;
    const partialTie =
        tied &&
        priceAgreement &&
        !candleAgreement &&
        row.referenceCandles.some((c) =>
            label.selectedOpens.includes(c.openTimeUtc),
        );
    return {
        ...base,
        outcome:
            priceAgreement && candleAgreement
                ? "ExactReference"
                : partialTie
                  ? "TieAmbiguity"
                  : "IncorrectReference",
        priceAgreement,
        candleAgreement,
        equalExtremeAgreement: tied ? priceAgreement && candleAgreement : null,
        signedDifference,
        absoluteDifference: signedDifference.replace(/^-/, ""),
    };
}
export const rate = (numerator: number, denominator: number) =>
    denominator
        ? { numerator, denominator, percent: (numerator / denominator) * 100 }
        : null;
export type AlgorithmMetrics = {
    id: string;
    comparedEligible: number;
    detections: number;
    abstentions: number;
    unconfirmed: number;
    falseDetections: number;
    missedDetections: number;
    incorrectReferences: number;
    tieAmbiguities: number;
    correctNoReference: number;
    comparableReferences: number;
    exactReferences: number;
    priceAgreements: number;
    candleAgreements: number;
    comparableTies: number;
    equalExtremeAgreements: number;
};
export type Cohort = {
    key: string;
    partition: Episode["partition"];
    origin: Episode["dataOrigin"];
    exposure: History["exposure"];
    reviewed: number;
    eligibleLabels: number;
    ambiguity: number;
    skipped: number;
    pendingReveal: number;
    algorithms: AlgorithmMetrics[];
};
export function summarize(
    state: ReviewState,
    reports: Record<string, Envelope>,
    includeExposedOrSynthetic = false,
): Cohort[] {
    const groups = new Map<string, Cohort>();
    for (const e of catalog.episodes) {
        const h = state.records[e.id];
        if (!h) continue;
        if (
            !includeExposedOrSynthetic &&
            (e.dataOrigin === "Synthetic" || h.exposure === "PreviouslyExposed")
        )
            continue;
        const label = independentLabel(h);
        const key = `${e.partition}/${e.dataOrigin}/${h.exposure}`;
        let group = groups.get(key);
        if (!group) {
            group = {
                key,
                partition: e.partition,
                origin: e.dataOrigin,
                exposure: h.exposure,
                reviewed: 0,
                eligibleLabels: 0,
                ambiguity: 0,
                skipped: 0,
                pendingReveal: 0,
                algorithms: algorithmIds.map((id) => ({
                    id,
                    comparedEligible: 0,
                    detections: 0,
                    abstentions: 0,
                    unconfirmed: 0,
                    falseDetections: 0,
                    missedDetections: 0,
                    incorrectReferences: 0,
                    tieAmbiguities: 0,
                    correctNoReference: 0,
                    comparableReferences: 0,
                    exactReferences: 0,
                    priceAgreements: 0,
                    candleAgreements: 0,
                    comparableTies: 0,
                    equalExtremeAgreements: 0,
                })),
            };
            groups.set(key, group);
        }
        group.reviewed++;
        if (label.kind === "Ambiguous") {
            group.ambiguity++;
            continue;
        }
        if (label.kind === "Skip") {
            group.skipped++;
            continue;
        }
        group.eligibleLabels++;
        const report = reports[e.id];
        if (!h.revealedAtDeviceUtc || !report) {
            group.pendingReveal++;
            continue;
        }
        for (const metrics of group.algorithms) {
            const row = report.comparison.results.find(
                (r) => r.algorithm.algorithmId === metrics.id,
            )!;
            const comparison = compareReference(row, label);
            metrics.comparedEligible++;
            if (row.isExperimentalReferenceConfirmed) metrics.detections++;
            if (row.abstained) metrics.abstentions++;
            if (!row.isExperimentalReferenceConfirmed && !row.abstained)
                metrics.unconfirmed++;
            if (comparison.outcome === "FalseDetection")
                metrics.falseDetections++;
            if (comparison.outcome === "MissedDetection")
                metrics.missedDetections++;
            if (comparison.outcome === "IncorrectReference")
                metrics.incorrectReferences++;
            if (comparison.outcome === "TieAmbiguity") metrics.tieAmbiguities++;
            if (comparison.outcome === "CorrectNoReference")
                metrics.correctNoReference++;
            if (comparison.priceAgreement !== null) {
                metrics.comparableReferences++;
                if (comparison.outcome === "ExactReference")
                    metrics.exactReferences++;
                if (comparison.priceAgreement) metrics.priceAgreements++;
                if (comparison.candleAgreement) metrics.candleAgreements++;
            }
            if (comparison.equalExtremeAgreement !== null) {
                metrics.comparableTies++;
                if (comparison.equalExtremeAgreement)
                    metrics.equalExtremeAgreements++;
            }
        }
    }
    return [...groups.values()];
}
