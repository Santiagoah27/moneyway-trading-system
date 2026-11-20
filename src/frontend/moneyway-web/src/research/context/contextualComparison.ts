import {
    digest,
    first,
    manifest,
    referenceEligibility,
    type Review,
} from "./contextualReview";
import type { Candle } from "../report";

type Selection = { price: number; sources: Candle[]; availableAtUtc: string };
export type ComparisonRow = {
    id: string;
    hypothesisId: string;
    swingVariantId: string | null;
    status: string;
    reason: string;
    eventSelection: {
        policyId: string;
        liquidityReference: { category: string };
        event: { candle: Candle } | null;
    };
    structuralReference: Selection | null;
    subsequentSc: { candle: Candle; asOfUtc: string } | null;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
};
export type Comparison = {
    contextId: string;
    inputSha256: string;
    comparison: {
        algorithmId: string;
        version: string;
        canonicalAdmissionPermitted: false;
        operationalSignal: false;
        tracking: { cutoffUtc: string };
        comparisons: ComparisonRow[];
    };
};
export async function loadComparison(state: Review): Promise<Comparison> {
    if (state.revealedRevision === null)
        throw new Error("Comparación oculta hasta confirmar la revisión.");
    const text = (
        await import("./data/contextual-validation/comparison.json?raw")
    ).default;
    if ((await digest(text)) !== state.reportSha256)
        throw new Error("El reporte cambió; comparación no disponible.");
    const report = JSON.parse(text) as Comparison;
    if (
        report.contextId !== state.contextId ||
        report.inputSha256 !== state.sourceSha256 ||
        report.comparison.algorithmId !== "EXP-NQ-CONTEXT-SWING-SC-V1" ||
        report.comparison.version !== "1.0.0" ||
        report.comparison.canonicalAdmissionPermitted !== false ||
        report.comparison.operationalSignal !== false ||
        report.comparison.comparisons.some(
            (r) =>
                r.canonicalAdmissionPermitted !== false ||
                r.operationalSignal !== false,
        )
    )
        throw new Error("Identidad experimental incompatible.");
    return report;
}
export function compare(state: Review, report: Comparison) {
    if (
        state.revealedRevision === null ||
        report.contextId !== manifest.contextId ||
        report.inputSha256 !== state.sourceSha256
    )
        throw new Error("Revisión/reporte no vinculados.");
    // Freeze ORIGINAL labels. Corrections are exported, never silently rescore the independent sequence.
    const original = {
        ...state,
        labels: state.labels.slice(0, state.revealedRevision),
    };
    const a = first(original, "A"),
        b = first(original, "B"),
        c = first(original, "C"),
        d = first(original, "D");
    const eligible =
        !state.outsideExposure &&
        referenceEligibility(original) === "SelfDeclaredPreEventCommitment";
    const binary = (kind?: string) => kind === "Selected" || kind === "None";
    return report.comparison.comparisons.map((row) => {
        const event = row.eventSelection.event?.candle;
        const ref = row.structuralReference;
        const relevantEvent =
            b?.kind === "Selected" &&
            b.relevant &&
            b.references.includes(
                row.eventSelection.liquidityReference.category,
            );
        const eventMatch =
            relevantEvent &&
            Boolean(
                event &&
                b.source &&
                Date.parse(event.openTimeUtc) ===
                    Date.parse(b.source.openTimeUtc),
            );
        const sc = row.subsequentSc;
        const fullD =
            d &&
            Date.parse(d.cutoff) >=
                Date.parse(report.comparison.tracking.cutoffUtc);
        const structuralMatch =
            c?.kind === "Selected" &&
            ref &&
            c.price === ref.price &&
            ref.sources.length === 1 &&
            c.source &&
            Date.parse(ref.sources[0].openTimeUtc) ===
                Date.parse(c.source.openTimeUtc);
        const scEligible =
            eligible &&
            eventMatch &&
            structuralMatch &&
            binary(d?.kind) &&
            fullD &&
            !row.status.startsWith("Abstained");
        return {
            id: row.id,
            hypothesis: row.hypothesisId,
            variant: row.swingVariantId,
            status: row.status,
            reason: row.reason,
            liquidityAgreement:
                binary(a?.kind) &&
                a?.classification === "ChronologicalSelfDeclared" &&
                !state.outsideExposure
                    ? a.references.includes(
                          row.eventSelection.liquidityReference.category,
                      )
                    : null,
            eventAgreement:
                binary(b?.kind) &&
                b?.classification === "ChronologicalSelfDeclared" &&
                !state.outsideExposure
                    ? b.kind === "None"
                        ? event === undefined
                        : relevantEvent
                          ? eventMatch
                          : null
                    : null,
            priceAgreement:
                eligible && eventMatch && c?.kind === "Selected" && ref
                    ? c.price === ref.price
                    : null,
            candleAgreement:
                eligible && eventMatch && c?.source && ref
                    ? ref.sources.length === 1 &&
                      Date.parse(ref.sources[0].openTimeUtc) ===
                          Date.parse(c.source.openTimeUtc)
                    : null,
            availabilityDeltaMs:
                eligible && eventMatch && c?.confirmationClose && ref
                    ? Date.parse(ref.availableAtUtc) -
                      Date.parse(c.confirmationClose)
                    : null,
            scAgreement: scEligible
                ? d.kind === "None"
                    ? !sc
                    : Boolean(
                          sc &&
                          d.source &&
                          Date.parse(sc.candle.openTimeUtc) ===
                              Date.parse(d.source.openTimeUtc),
                      )
                : null,
            confirmationDeltaMs:
                scEligible && sc && d.source
                    ? Date.parse(sc.asOfUtc) - Date.parse(d.source.closeTimeUtc)
                    : null,
            detection: !scEligible
                ? "NotComparable"
                : d.kind === "Selected" && !sc
                  ? "MissingDetection"
                  : d.kind === "None" && sc
                    ? "FalseDetectionRelativeToLabel"
                    : "Comparable",
            referenceDetection:
                !eligible || !eventMatch || !binary(c?.kind)
                    ? "NotComparable"
                    : row.status.startsWith("Abstained")
                      ? "Abstained"
                      : c?.kind === "None" && ref
                        ? "FalseReferenceRelativeToLabel"
                        : c?.kind === "Selected" && !ref
                          ? "MissingReference"
                          : "Comparable",
            sameCandle:
                d?.kind === "SameCandle" ||
                row.status === "AbstainedSameCandleBreak",
            abstained: row.status.startsWith("Abstained"),
            eligiblePreEventLabel: eligible,
        };
    });
}
export function denominators(rows: ReturnType<typeof compare>) {
    return {
        sessions: 1,
        combinations: rows.length,
        price: rows.filter((r) => r.priceAgreement !== null).length,
        event: rows.filter((r) => r.eventAgreement !== null).length,
        liquidity: rows.filter((r) => r.liquidityAgreement !== null).length,
        candle: rows.filter((r) => r.candleAgreement !== null).length,
        availability: rows.filter((r) => r.availabilityDeltaMs !== null).length,
        sc: rows.filter((r) => r.scAgreement !== null).length,
        confirmation: rows.filter((r) => r.confirmationDeltaMs !== null).length,
        abstentions: rows.filter((r) => r.abstained).length,
    };
}
