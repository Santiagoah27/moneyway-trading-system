import { beforeEach, describe, expect, it } from "vitest";
import { catalog, charts, validateCatalog, type Episode } from "./catalog";
import {
    commitLabel,
    decodeState,
    emptyDraft,
    emptyState,
    exportResearch,
    independentLabel,
    persist,
    reveal,
    selection,
    storageKey,
    type ReviewState,
} from "./labels";
import {
    compareReference,
    decimalDifference,
    rate,
    summarize,
} from "./metrics";
import { readReport } from "../report";
import synthetic from "../reports/synthetic-v1.json";

const now = "2026-11-19T20:00:00.000Z";
const e = catalog.episodes.find((e) => e.partition === "Development")!;
const source = charts[e.id].evidenceCandles;
const positive = (s = emptyState(), id = e.id) =>
    commitLabel(
        s,
        id,
        {
            kind: "Reference",
            selectedOpens: [charts[id].evidenceCandles[1].openTimeUtc],
            confirmationOpenUtc: null,
        },
        "Test reviewer (synthetic test judgment)",
        false,
        now,
    );
beforeEach(() => localStorage.clear());

describe("fixed documentary review dataset", () => {
    it("has four new ordinary episodes, two known fixtures and no held-out OHLC or labels", () => {
        expect(catalog.episodes).toHaveLength(6);
        expect(
            catalog.episodes.filter((e) =>
                ["Development", "Validation"].includes(e.partition),
            ),
        ).toHaveLength(4);
        expect(catalog.heldOutEpisodeCount).toBe(2);
        expect(catalog.uniqueAuthenticSourceCandleCount).toBe(96);
        expect(catalog.splitAssessment).toBe(
            "InsufficientForIndependentGeneralizationClaims",
        );
        expect(JSON.stringify(charts)).not.toContain("2026-06-30");
        expect(
            Object.values(charts).every(
                (c) => Object.keys(c).sort().join() === "evidenceCandles,input",
            ),
        ).toBe(true);
        expect(
            catalog.episodes.find((e) => e.id === "previously-exposed-june29")
                ?.priorExposure,
        ).toBe("PreviouslyExposed");
    });
    it.each([
        "future",
        "ohlc",
        "anchor",
        "overlap",
        "held-out",
        "partition-order",
    ])("rejects %s corruption", (issue) => {
        const manifest = structuredClone(catalog),
            inputs = structuredClone(charts);
        const first = manifest.episodes[0];
        if (issue === "future")
            inputs[first.id].evidenceCandles.at(-1)!.closeTimeUtc =
                "2030-01-01T00:00:00Z";
        if (issue === "ohlc") inputs[first.id].evidenceCandles[0].high = 1;
        if (issue === "anchor") first.anchorPrice++;
        if (issue === "held-out")
            (first as unknown as { partition: string }).partition = "HeldOut";
        if (issue === "overlap") {
            const duplicate = { ...first, id: "duplicate-episode" };
            manifest.episodes.push(duplicate);
            inputs[duplicate.id] = structuredClone(inputs[first.id]);
        }
        if (issue === "partition-order") {
            manifest.episodes[0].partition = "Validation";
            manifest.episodes[2].partition = "Development";
        }
        expect(() => validateCatalog(manifest, inputs)).toThrow("Dataset");
    });
});

describe("independent local research labels", () => {
    it("extracts exact Low/High from selected original source candles", () => {
        const label = positive().records[e.id].revisions[0];
        expect(label.price).toBe(source[1].low);
        expect(label.extreme).toBe("Low");
        const down = catalog.episodes.find(
            (e) =>
                e.partition === "Development" && e.direction === "Descending",
        )!;
        const other = positive(emptyState(), down.id).records[down.id]
            .revisions[0];
        expect(other.price).toBe(charts[down.id].evidenceCandles[1].high);
        expect(other.extreme).toBe("High");
        expect(label.confirmationCloseUtc).toBeNull();
        expect(label.cutoffUtc).toBe(e.cutoffUtc);
        expect(label.canonicalAdmissionPermitted).toBe(false);
        expect(label.operationalSignal).toBe(false);
    });
    it.each(["NoReference", "Ambiguous", "Skip"] as const)(
        "keeps %s distinct without a fabricated price",
        (kind) => {
            const state = commitLabel(
                emptyState(),
                e.id,
                { ...emptyDraft(), kind },
                "Test",
                false,
                now,
            );
            const label = state.records[e.id].revisions[0];
            expect(label.kind).toBe(kind);
            expect(label.price).toBeNull();
            expect(label.selectedOpens).toEqual([]);
            expect(decodeState(JSON.stringify(state))).toEqual(state);
        },
    );
    it("refuses missing, anchor, foreign, non-equal and future selections", () => {
        expect(() => positive(emptyState(), "held-out-unavailable")).toThrow();
        for (const opens of [
            [],
            [source[0].openTimeUtc],
            ["2030-01-01T00:00:00Z"],
            [source[1].openTimeUtc, source[2].openTimeUtc],
        ]) {
            if (opens.length === 2 && source[1].low === source[2].low) continue;
            expect(() =>
                commitLabel(
                    emptyState(),
                    e.id,
                    {
                        kind: "Reference",
                        selectedOpens: opens,
                        confirmationOpenUtc: null,
                    },
                    "Test",
                    false,
                    now,
                ),
            ).toThrow();
        }
    });
    it("supports multiple human-chosen equal extremes without choosing a preferred candle", () => {
        // Mutate only a controlled in-memory test copy, restoring the real documentary fixture immediately.
        const original = source[2].low;
        try {
            source[2].low = source[1].low;
            const pick = selection(e, [
                source[1].openTimeUtc,
                source[2].openTimeUtc,
            ]);
            expect(pick.chosen).toHaveLength(2);
            expect(pick.price).toBe(source[1].low);
        } finally {
            source[2].low = original;
        }
    });
    it("captures confirmation only through a separate later source candle", () => {
        const draft = {
            kind: "Reference" as const,
            selectedOpens: [source[1].openTimeUtc],
            confirmationOpenUtc: source[1].openTimeUtc,
        };
        expect(() =>
            commitLabel(emptyState(), e.id, draft, "Test", false, now),
        ).toThrow("posterior");
        const state = commitLabel(
            emptyState(),
            e.id,
            { ...draft, confirmationOpenUtc: source[4].openTimeUtc },
            "Test",
            false,
            now,
        );
        expect(state.records[e.id].revisions[0].confirmationCloseUtc).toBe(
            source[4].closeTimeUtc,
        );
    });
    it("persists/reloads the original and freezes the pre-reveal revision against corrections", () => {
        const first = positive();
        const raw = persist(localStorage, null, first);
        const reloaded = decodeState(localStorage.getItem(storageKey));
        expect(reloaded).toEqual(first);
        expect(() => reveal(emptyState(), e.id, now)).toThrow("Confirme");
        const before = commitLabel(
            reloaded,
            e.id,
            {
                kind: "NoReference",
                selectedOpens: [],
                confirmationOpenUtc: null,
            },
            "Test",
            false,
            now,
        );
        const exposed = reveal(before, e.id, now);
        const correction = positive(exposed);
        expect(correction.records[e.id].revisions[0]).toEqual(
            first.records[e.id].revisions[0],
        );
        expect(correction.records[e.id].revisions[2].classification).toBe(
            "PostRevealCorrection",
        );
        expect(independentLabel(correction.records[e.id]).kind).toBe(
            "NoReference",
        );
        const last = persist(localStorage, raw, correction);
        expect(decodeState(last)).toEqual(correction);
        const exported = JSON.parse(exportResearch(correction));
        expect(exported.labels).toEqual(correction);
        expect(exported.manifest.datasetContentSha256).toBe(
            catalog.datasetContentSha256,
        );
        expect(exported.warning).toContain("unauthenticated");
    });
    it("never downgrades prior exposure, including the user-accepted V2 episode", () => {
        const state = commitLabel(
            emptyState(),
            "previously-exposed-june29",
            { ...emptyDraft(), kind: "Ambiguous" },
            "Test",
            false,
            now,
        );
        expect(state.records["previously-exposed-june29"].exposure).toBe(
            "PreviouslyExposed",
        );
        const known = commitLabel(
            positive(),
            e.id,
            { ...emptyDraft(), kind: "Skip" },
            "Test",
            true,
            now,
        );
        const later = positive(known);
        expect(later.records[e.id].exposure).toBe("PreviouslyExposed");
        expect(decodeState(JSON.stringify(later))).toEqual(later);
    });
    it("fails closed on tampering, wrong dataset, quota and cross-tab changes", () => {
        const state = positive();
        const altered = structuredClone(state);
        altered.records[e.id].revisions[0].price!++;
        expect(() => decodeState(JSON.stringify(altered))).toThrow("alterado");
        expect(() => decodeState("{")).toThrow("ilegible");
        expect(() =>
            decodeState(JSON.stringify({ ...state, datasetVersion: 99 })),
        ).toThrow();
        localStorage.setItem(storageKey, "other-tab");
        expect(() => persist(localStorage, null, state)).toThrow(
            "Otra pestaña",
        );
        expect(localStorage.getItem(storageKey)).toBe("other-tab");
        expect(() =>
            persist(
                {
                    getItem: () => null,
                    setItem: () => {
                        throw new Error("QuotaExceeded");
                    },
                },
                null,
                state,
            ),
        ).toThrow("QuotaExceeded");
    });
});

describe("descriptive agreement metrics, synthetic test judgments only", () => {
    const row = () =>
        readReport(structuredClone(synthetic)).comparison.results[0];
    const label = () => ({
        ...positive().records[e.id].revisions[0],
        price: 90,
        selectedOpens: [row().referenceCandles[0].openTimeUtc],
    });
    it("separates exact, price and candle-identity agreement including ties", () => {
        expect(compareReference(row(), label()).outcome).toBe("ExactReference");
        const other = {
            ...label(),
            selectedOpens: ["different-original-candle"],
        };
        expect(compareReference(row(), other)).toMatchObject({
            outcome: "IncorrectReference",
            priceAgreement: true,
            candleAgreement: false,
        });
        const tied = row();
        tied.referenceCandles.push({
            ...tied.referenceCandles[0],
            openTimeUtc: "equal-extreme",
        });
        expect(compareReference(tied, label()).outcome).toBe("TieAmbiguity");
        expect(compareReference(tied, label()).equalExtremeAgreement).toBe(
            false,
        );
        expect(
            compareReference(tied, {
                ...label(),
                selectedOpens: [...label().selectedOpens, "equal-extreme"],
            }).equalExtremeAgreement,
        ).toBe(true);
    });
    it("distinguishes false/missed detections, abstentions, ambiguity and skips", () => {
        expect(
            compareReference(row(), {
                ...label(),
                kind: "NoReference",
                price: null,
                selectedOpens: [],
            }).outcome,
        ).toBe("FalseDetection");
        const abstained = {
            ...row(),
            isExperimentalReferenceConfirmed: false,
            confirmationAtUtc: null,
            abstained: true,
        };
        expect(compareReference(abstained, label()).outcome).toBe(
            "MissedDetection",
        );
        expect(
            compareReference(abstained, { ...label(), kind: "NoReference" })
                .outcome,
        ).toBe("CorrectNoReference");
        for (const kind of ["Ambiguous", "Skip"] as const)
            expect(compareReference(row(), { ...label(), kind }).outcome).toBe(
                "NotComparable",
            );
    });
    it("uses exact signed decimal differences and only independent human confirmation", () => {
        expect(decimalDifference(29492.3, 29492.4)).toBe("-0.1");
        expect(decimalDifference(100.01, 90)).toBe("10.01");
        expect(decimalDifference(90, 90)).toBe("0");
        expect(compareReference(row(), label()).latencyMilliseconds).toBeNull();
        const actual = compareReference(row(), {
            ...label(),
            price: 93,
            confirmationCloseUtc: "2026-11-19T13:25:00Z",
        });
        expect(actual).toMatchObject({
            signedDifference: "-3",
            absoluteDifference: "3",
            latencyMilliseconds: 300000,
        });
        expect(rate(0, 0)).toBeNull();
        expect(rate(1, 4)?.percent).toBe(25);
    });
    it("counts one frozen observation, separates cohorts and never counts post-reveal improvements", () => {
        let state: ReviewState = positive();
        state = reveal(state, e.id, now);
        state = commitLabel(
            state,
            e.id,
            { ...emptyDraft(), kind: "NoReference" },
            "Test",
            false,
            now,
        );
        const fixtureReport = readReport(structuredClone(synthetic));
        const syntheticEpisode: Episode = catalog.episodes.find(
            (e) => e.partition === "SoftwareAcceptance",
        )!;
        state = commitLabel(
            state,
            syntheticEpisode.id,
            { ...emptyDraft(), kind: "NoReference" },
            "Test",
            false,
            now,
        );
        state = reveal(state, syntheticEpisode.id, now);
        const report = {
            ...fixtureReport,
            comparison: {
                ...fixtureReport.comparison,
                results: fixtureReport.comparison.results.map((r) => ({
                    ...r,
                    isExperimentalReferenceConfirmed: false,
                    abstained: true,
                    confirmationAtUtc: null,
                })),
            },
        };
        const groups = summarize(state, {
            [e.id]: report,
            [syntheticEpisode.id]: fixtureReport,
        });
        expect(groups).toHaveLength(1);
        expect(groups[0].reviewed).toBe(1);
        expect(groups[0].algorithms[0]).toMatchObject({
            comparedEligible: 1,
            missedDetections: 1,
            falseDetections: 0,
            abstentions: 1,
        });
        expect(
            summarize(
                state,
                { [e.id]: report, [syntheticEpisode.id]: fixtureReport },
                true,
            ),
        ).toHaveLength(2);
        expect(summarize(emptyState(), {})).toEqual([]);
    });
    it("keeps validation/development and ambiguous denominators separate", () => {
        const val = catalog.episodes.find((e) => e.partition === "Validation")!;
        let state = commitLabel(
            emptyState(),
            e.id,
            { ...emptyDraft(), kind: "Ambiguous" },
            "Test",
            false,
            now,
        );
        state = commitLabel(
            state,
            val.id,
            { ...emptyDraft(), kind: "NoReference" },
            "Test",
            false,
            now,
        );
        state = reveal(state, val.id, now);
        const groups = summarize(state, {
            [val.id]: readReport(structuredClone(synthetic)),
        });
        expect(groups).toHaveLength(2);
        expect(groups[0]).toMatchObject({
            partition: "Development",
            ambiguity: 1,
            eligibleLabels: 0,
        });
        expect(groups[0].algorithms[0].comparedEligible).toBe(0);
        expect(groups[1]).toMatchObject({
            partition: "Validation",
            eligibleLabels: 1,
        });
        expect(groups[1].algorithms[0]).toMatchObject({
            comparedEligible: 1,
            falseDetections: 1,
            comparableReferences: 0,
        });
    });
});
