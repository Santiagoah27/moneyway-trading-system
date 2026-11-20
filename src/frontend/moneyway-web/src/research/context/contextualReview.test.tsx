/// <reference types="node" />
import { webcrypto } from "node:crypto";
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import raw from "./data/contextual-validation/context.json";
import rawText from "./data/contextual-validation/context.json?raw";
import {
    blank,
    commit,
    decode,
    digest,
    emptyReview,
    exportReview,
    first,
    loadReviewContext,
    manifest,
    persist,
    prep,
    referenceEligibility,
    reveal,
    reviewKey,
    visit,
    type Review,
} from "./contextualReview";
import {
    compare,
    denominators,
    loadComparison,
    type Comparison,
} from "./contextualComparison";
import { closedAt, validateContext, type ContextData } from "./model";
import { ContextualScReview } from "./ContextualScReview";
import { CandleChart } from "../StrategyLab";
const context = () => structuredClone(raw) as unknown as ContextData;
const now = "2026-11-20T12:00:00Z";
const at = (t: string) => `${manifest.selectedDay}T${t}:00Z`;
// Human choices and comparison rows below are synthetic software labels, never registered authentic reviews.
function a(s = emptyReview()) {
    return commit(
        s,
        { ...blank("A"), references: ["LL"] },
        context(),
        prep,
        "synthetic reviewer",
        now,
    );
}
function c(s = a(), cutoff = prep) {
    return commit(
        s,
        {
            ...blank("C"),
            candleOpen: closedAt(context(), "M5", prep).at(-1)!.openTimeUtc,
        },
        context(),
        cutoff,
        "synthetic reviewer",
        now,
    );
}
function b(s = c()) {
    return commit(
        s,
        { ...blank("B"), references: ["LL"], candleOpen: at("13:30") },
        context(),
        at("13:35"),
        "synthetic reviewer",
        now,
    );
}
function complete(s = b()) {
    return commit(
        s,
        { ...blank("D"), kind: "None" },
        context(),
        context().maximumCutoffUtc,
        "synthetic reviewer",
        now,
    );
}
function syntheticReport(s: Review, sc = true): Comparison {
    const ref = first(s, "C")!,
        event = first(s, "B")!;
    const candle = context().series.M5.candles.find(
        (c) => c.openTimeUtc === at("13:35"),
    )!;
    return {
        contextId: s.contextId,
        inputSha256: s.sourceSha256,
        comparison: {
            algorithmId: "EXP-NQ-CONTEXT-SWING-SC-V1",
            version: "1.0.0",
            canonicalAdmissionPermitted: false,
            operationalSignal: false,
            tracking: { cutoffUtc: context().maximumCutoffUtc },
            comparisons: [
                {
                    id: "synthetic-comparison",
                    hypothesisId: "synthetic",
                    swingVariantId: null,
                    status: sc
                        ? "ExperimentalScObserved"
                        : "NoQualifyingSCObserved",
                    reason: "synthetic",
                    eventSelection: {
                        policyId: "synthetic",
                        liquidityReference: { category: "LL" },
                        event: { candle: event.source! },
                    },
                    structuralReference: {
                        price: ref.price!,
                        sources: [ref.source!],
                        availableAtUtc: ref.cutoff,
                    },
                    subsequentSc: sc
                        ? { candle, asOfUtc: candle.closeTimeUtc }
                        : null,
                    canonicalAdmissionPermitted: false,
                    operationalSignal: false,
                },
            ],
        },
    };
}
beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("crypto", webcrypto);
    vi.stubGlobal(
        "fetch",
        vi.fn(async () => new Response(rawText)),
    );
});
afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
});
describe("frozen contextual review dataset", () => {
    it("selects earliest eligible date without reserved/exposed dates or outcome inputs", () => {
        expect(manifest.selectedDay).toBe("2026-06-22");
        expect(
            manifest.inventory.filter((i) => i.eligible).map((i) => i.day),
        ).toEqual(["2026-06-22", "2026-06-24", "2026-06-26"]);
        expect(manifest.inventory.map((i) => i.day)).not.toContain(
            "2026-06-25",
        );
        expect(manifest.inventory.map((i) => i.day)).not.toContain(
            "2026-06-30",
        );
        expect(manifest.inventory[0].eligible).toBe(false);
        expect(manifest.inventory[0].missingH1.length).toBeGreaterThan(0);
        expect(manifest.inventory[0].precedingClosedCounts.H4).toBe(0);
    });
    it("preserves authentic original context, H4/H1 coverage and documentary clock only", async () => {
        const data = await loadReviewContext();
        expect(validateContext(data, true)).toEqual(context());
        expect(closedAt(data, "H4", prep)).toHaveLength(4);
        expect(closedAt(data, "H1", prep)).toHaveLength(15);
        expect(
            data.series.M5.candles.filter((c) => c.openTimeUtc >= prep),
        ).toHaveLength(42);
        expect(data.canonicalAdmissionPermitted).toBe(false);
        expect(data.operationalSignal).toBe(false);
        expect(await digest(rawText)).toBe(manifest.inputSha256);
    });
    it("fails closed for unavailable/mismatched source instead of substituting exposed data", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(async () => new Response("{}")),
        );
        await expect(loadReviewContext()).rejects.toThrow("Hash");
        vi.stubGlobal(
            "fetch",
            vi.fn(async () => new Response("", { status: 404 })),
        );
        await expect(loadReviewContext()).rejects.toThrow("no disponible");
    });
    it("rejects wrong provider/instrument and original reserved dates", () => {
        for (const change of [
            { symbol: "USTEC" },
            { providerId: "other" },
            { tradingDay: "2026-06-25" },
            { tradingDay: "2026-06-30" },
        ])
            expect(() =>
                validateContext({ ...context(), ...change }, true),
            ).toThrow();
    });
});
describe("chronological independent research labels", () => {
    it("requires preparation at 08:00 and explicit independent liquidity or alternative", () => {
        expect(() =>
            commit(emptyReview(), blank("A"), context(), prep, "name", now),
        ).toThrow("Seleccione liquidez");
        expect(() =>
            commit(
                emptyReview(),
                { ...blank("A"), kind: "None" },
                context(),
                at("13:05"),
                "name",
                now,
            ),
        ).toThrow("08:00");
        expect(first(a(), "A")!.references).toEqual(["LL"]);
        expect(() =>
            commit(emptyReview(), blank("D"), context(), prep, "name", now),
        ).toThrow("preparación");
    });
    it("commits pre-event exact structural price/source without backdating availability", () => {
        const s = b();
        const label = first(s, "C")!;
        expect(label.price).toBe(label.source!.high);
        expect(label.formationUtc).toBe(label.source!.closeTimeUtc);
        expect(label.confirmationClose).toBeNull();
        expect(referenceEligibility(s)).toBe("SelfDeclaredPreEventCommitment");
        expect(() =>
            commit(
                a(),
                { ...blank("C"), candleOpen: at("13:30") },
                context(),
                prep,
                "name",
                now,
            ),
        ).toThrow("visible");
    });
    it("keeps maximum seen cutoff after moving back and marks pre-event comparison retrospective", () => {
        const exposed = visit(a(), at("13:35"), context());
        const returned = visit(exposed, prep, context());
        expect(returned.maximumSeen).toBe(at("13:35"));
        expect(referenceEligibility(b(c(returned)))).toBe(
            "RetrospectiveOrIneligible",
        );
    });
    it("does not overwrite original reference after later exposure or reveal", () => {
        const initial = complete();
        const later = commit(
            initial,
            { ...blank("C"), kind: "None" },
            context(),
            context().maximumCutoffUtc,
            "name",
            now,
        );
        expect(first(later, "C")).toEqual(first(initial, "C"));
        expect(later.labels.at(-1)!.classification).toBe("Retrospective");
        const exposed = reveal(initial);
        const correction = commit(
            exposed,
            { ...blank("C"), kind: "Ambiguous" },
            context(),
            context().maximumCutoffUtc,
            "name",
            now,
        );
        expect(correction.labels.at(-1)!.classification).toBe(
            "PostRevealCorrection",
        );
        expect(correction.revealedRevision).toBe(initial.labels.length);
    });
    it("keeps previously exposed sources and outside exposure out of independent comparison", () => {
        const d = context();
        d.classification = "PreviouslyExposed_Demonstration";
        const l = commit(
            emptyReview(),
            { ...blank("A"), kind: "None" },
            d,
            prep,
            "name",
            now,
        );
        expect(l.labels[0].classification).toBe("PreviouslyExposed");
        expect(referenceEligibility({ ...b(), outsideExposure: true })).toBe(
            "RetrospectiveOrIneligible",
        );
    });
    it.each(["None", "Ambiguous", "Insufficient", "Skip"] as const)(
        "supports reference alternative %s without fabricated price",
        (kind) => {
            const s = commit(
                a(),
                { ...blank("C"), kind },
                context(),
                prep,
                "name",
                now,
            );
            expect(first(s, "C")!.price).toBeNull();
            expect(first(s, "C")!.source).toBeNull();
        },
    );
    it("requires closed later SC or explicit same-candle ambiguity", () => {
        const s = b();
        expect(() =>
            commit(
                s,
                { ...blank("D"), candleOpen: at("13:30") },
                context(),
                at("13:35"),
                "name",
                now,
            ),
        ).toThrow("otra vela");
        const same = commit(
            s,
            { ...blank("D"), kind: "SameCandle" },
            context(),
            at("13:35"),
            "name",
            now,
        );
        expect(first(same, "D")!.source).toBeNull();
        const later = commit(
            s,
            { ...blank("D"), candleOpen: at("13:35") },
            context(),
            at("13:40"),
            "name",
            now,
        );
        expect(first(later, "D")!.formationUtc).toBe(at("13:40"));
    });
    it("refuses incomplete reveal and loads only exact committed report identity", async () => {
        expect(() => reveal(c())).toThrow("Confirme");
        await expect(loadComparison(complete())).rejects.toThrow("oculta");
        const s = reveal(complete());
        const report = await loadComparison(s);
        expect(report.inputSha256).toBe(s.sourceSha256);
        await expect(
            loadComparison({ ...s, reportSha256: "0".repeat(64) }),
        ).rejects.toThrow("cambió");
    });
    it("roundtrips originals, exposure, reveal and post-reveal revisions through durable JSON", () => {
        const original = reveal(complete());
        const s = commit(
            original,
            { ...blank("C"), kind: "Skip" },
            context(),
            context().maximumCutoffUtc,
            "name",
            now,
        );
        const raw = persist(localStorage, null, s, context());
        expect(decode(raw, context())).toEqual(s);
        const forged = structuredClone(s);
        forged.labels.at(-1)!.classification = "ChronologicalSelfDeclared";
        expect(() => decode(JSON.stringify(forged), context())).toThrow(
            "incompatible",
        );
        expect(
            decode(JSON.stringify({ ...s, outsideExposure: true }), context())
                .outsideExposure,
        ).toBe(true);
        expect(JSON.parse(exportReview(s, context())).labels.labels).toEqual(
            s.labels,
        );
        expect(() => persist(localStorage, null, s, context())).toThrow(
            "pestaña",
        );
        expect(() =>
            decode(raw.replace(s.sourceSha256, "0".repeat(64)), context()),
        ).toThrow("incompatible");
    });
});
describe("separate comparison dimensions and denominators", () => {
    it("reports missing SC separately from abstention and keeps confirmation deltas exact", () => {
        const seed = b();
        const human = commit(
            seed,
            { ...blank("D"), candleOpen: at("13:35") },
            context(),
            context().maximumCutoffUtc,
            "synthetic",
            now,
        );
        const s = reveal(human);
        const report = syntheticReport(s, false);
        expect(compare(s, report)[0].detection).toBe("MissingDetection");
        const observed = syntheticReport(s, true);
        expect(compare(s, observed)[0].confirmationDeltaMs).toBe(0);
        observed.comparison.comparisons[0].subsequentSc!.asOfUtc = at("13:45");
        expect(compare(s, observed)[0].confirmationDeltaMs).toBe(300000);
    });
    it.each(["Ambiguous", "Insufficient", "Skip", "SameCandle"] as const)(
        "excludes SC label %s from its binary denominator",
        (kind) => {
            const s = reveal(
                commit(
                    b(),
                    { ...blank("D"), kind },
                    context(),
                    context().maximumCutoffUtc,
                    "synthetic",
                    now,
                ),
            );
            expect(denominators(compare(s, syntheticReport(s))).sc).toBe(0);
        },
    );
    it("separates price, candle, availability, SC, false detection, sessions and combinations", () => {
        const s = reveal(complete());
        const report = syntheticReport(s);
        const rows = compare(s, report);
        expect(rows[0].priceAgreement).toBe(true);
        expect(rows[0].candleAgreement).toBe(true);
        expect(rows[0].availabilityDeltaMs).toBeNull();
        expect(rows[0].scAgreement).toBe(false);
        expect(rows[0].detection).toBe("FalseDetectionRelativeToLabel");
        expect(denominators(rows)).toMatchObject({
            sessions: 1,
            combinations: 1,
            price: 1,
            sc: 1,
            availability: 0,
        });
    });
    it("excludes ambiguous/insufficient/skipped and retrospective labels, keeps abstentions distinct", () => {
        const s = reveal(complete(b(c(visit(a(), at("13:35"), context())))));
        const rows = compare(s, syntheticReport(s));
        expect(denominators(rows).price).toBe(0);
        expect(denominators(rows).sc).toBe(0);
        const independent = reveal(complete());
        const report = syntheticReport(independent);
        report.comparison.comparisons[0].status = "AbstainedSameCandleBreak";
        const r = compare(independent, report);
        expect(r[0].sameCandle).toBe(true);
        expect(denominators(r)).toMatchObject({ sc: 0, abstentions: 1 });
    });
    it("keeps reveal comparison frozen when human adds a correction", () => {
        const s = reveal(complete());
        const r = syntheticReport(s);
        const corrected = commit(
            s,
            { ...blank("D"), kind: "Skip" },
            context(),
            context().maximumCutoffUtc,
            "name",
            now,
        );
        expect(compare(corrected, r)).toEqual(compare(s, r));
    });
});
describe("existing chart and contextual review interaction", () => {
    it("selects chart references, commits chronology, reveals and hides later comparison when returning", async () => {
        const user = userEvent.setup();
        render(<ContextualScReview />);
        await screen.findByRole("heading", {
            name: /Revisión contextual cronológica/,
        });
        await user.type(
            screen.getByLabelText("Revisor local"),
            "synthetic UI reviewer",
        );
        await user.click(screen.getByRole("checkbox", { name: /^LL / }));
        await user.click(
            screen.getByRole("button", { name: /Confirmar observación/ }),
        );
        await user.click(screen.getByRole("button", { name: "M5" }));
        await user.click(
            screen.getByRole("button", {
                name: "Inspeccionar vela 2026-06-22 12:55:00 UTC",
            }),
        );
        await user.click(
            screen.getByRole("button", { name: /Confirmar observación/ }),
        );
        await user.selectOptions(
            screen.getByLabelText(/Cutoff de revisión/),
            at("13:35"),
        );
        await user.click(screen.getByRole("checkbox", { name: /^LL / }));
        await user.click(
            screen.getByRole("button", {
                name: "Inspeccionar vela 2026-06-22 13:30:00 UTC",
            }),
        );
        await user.click(
            screen.getByRole("button", { name: /Confirmar observación/ }),
        );
        await user.selectOptions(screen.getByLabelText("Observación"), "None");
        await user.selectOptions(
            screen.getByLabelText(/Cutoff de revisión/),
            context().maximumCutoffUtc,
        );
        await user.click(
            screen.getByRole("button", { name: /Confirmar observación/ }),
        );
        expect(
            referenceEligibility(
                decode(localStorage.getItem(reviewKey), context()),
            ),
        ).toBe("SelfDeclaredPreEventCommitment");
        await user.click(
            screen.getByRole("button", {
                name: /Revelar comparación congelada/,
            }),
        );
        await screen.findByRole("region", {
            name: "Comparación contextual revelada",
        });
        await user.selectOptions(
            screen.getByLabelText(/Cutoff de revisión/),
            prep,
        );
        expect(
            screen.queryByRole("region", {
                name: "Comparación contextual revelada",
            }),
        ).toBeNull();
        expect(
            decode(localStorage.getItem(reviewKey), context()).revealedRevision,
        ).toBe(4);
    }, 20000);
    it("does not show future OHLC/scale before cutoff", () => {
        const chart = (data: ContextData) => (
            <CandleChart
                report={{
                    input: {
                        symbol: data.symbol,
                        timeframe: "M5",
                        evaluationCutoffUtc: prep,
                    },
                    evidenceCandles: closedAt(data, "M5", prep),
                    results: [],
                }}
                visible={[]}
                selected={0}
                onSelect={() => {}}
            />
        );
        const { container, rerender } = render(chart(context()));
        const before = container.innerHTML;
        const future = context();
        future.series.M5.candles.at(-1)!.high = 99999999;
        rerender(chart(future));
        expect(container.innerHTML).toBe(before);
    });
    it("explains disabled requirements, hides algorithms, commits preparation and reloads its state", async () => {
        const user = userEvent.setup();
        const { unmount } = render(<ContextualScReview />);
        await screen.findByRole("heading", {
            name: /Revisión contextual cronológica/,
        });
        expect(
            (
                screen.getByRole("button", {
                    name: /Confirmar observación/,
                }) as HTMLButtonElement
            ).disabled,
        ).toBe(true);
        expect(screen.getByText("Indique el nombre del revisor.")).toBeTruthy();
        expect(screen.queryByRole("table")).toBeNull();
        expect(screen.queryByText(/EXP-NQ-CONTEXT-SWING-PREVIOUS/)).toBeNull();
        await user.type(
            screen.getByLabelText("Revisor local"),
            "synthetic reviewer",
        );
        await user.selectOptions(screen.getByLabelText("Observación"), "None");
        await user.click(
            screen.getByRole("button", { name: /Confirmar observación/ }),
        );
        await waitFor(() =>
            expect(
                decode(localStorage.getItem(reviewKey), context()).labels,
            ).toHaveLength(1),
        );
        unmount();
        render(<ContextualScReview />);
        await screen.findByRole("heading", {
            name: /C · Referencia estructural/,
        });
        expect(
            first(decode(localStorage.getItem(reviewKey), context()), "A")!
                .kind,
        ).toBe("None");
    });
});
