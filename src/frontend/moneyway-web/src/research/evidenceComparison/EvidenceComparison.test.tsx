import { webcrypto } from "node:crypto";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../../App";
import {
    appendEvent,
    exportEventReview,
    startEventReview,
} from "../integratedReview/eventReview";
import {
    coverage,
    frames,
    readOriginalPackage,
    seal,
    sha256,
    unseal,
    type OriginalPackage,
} from "../integratedReview/originalPackage";
import { reference } from "../integratedReview/reviewHistory";
import { artifact } from "../observation/sessionReportFixture";
import { parseSessionReport } from "../observation/sessionReport";
import EvidenceComparison from "./EvidenceComparison";
import {
    appendDecision,
    exportComparison,
    humanRows,
    humanVisibility,
    importComparison,
    outcomes,
    prepareComparison,
    recordDisclosure,
    relationship,
} from "./comparison";

const date = "2000-01-03";
const cutoff = date + "T16:30:00Z";
const device = "2026-11-23T20:00:00Z";
const opened = "2026-11-23T20:01:00Z";
const later = "2026-11-23T20:02:00Z";
const originalSchema = "moneyway.nasdaq-blind-original.v1";

beforeEach(() => {
    vi.stubGlobal("crypto", webcrypto);
    localStorage.clear();
});
afterEach(() => {
    vi.unstubAllGlobals();
    window.history.replaceState(null, "", "/");
    localStorage.clear();
});

async function fixtures() {
    const { research } = JSON.parse(
        JSON.stringify(artifact({ simultaneous: true }))
            .replaceAll("2026-06-23", date)
            .replaceAll("fixture-replay", "synthetic-us100-v1"),
    ) as ReturnType<typeof artifact>;
    research.version = "1.1.0";
    research.runId = [
        research.candidateId,
        research.version,
        research.datasetId,
        cutoff,
    ].join("/");
    for (const chain of research.chains)
        Object.assign(chain.branches[0], { realignmentBindingVerified: true });
    const allCandles = new Map<
        string,
        OriginalPackage["sources"]["M5"]["bars"]
    >();
    const visit = (v: unknown) => {
        if (typeof v !== "object" || v === null) return;
        if ("openTimeUtc" in v) {
            const c = v as ReturnType<
                typeof artifact
            >["research"]["sessionLiquidity"][number]["sourceCandles"][number];
            const tf =
                (c.timeframe.unit === "Hour" ? "H" : "M") + c.timeframe.amount;
            const bars = allCandles.get(tf) ?? [];
            if (!bars.some((b) => b.openUtc === c.openTimeUtc)) {
                bars.push({
                    nativeOpen: c.openTimeUtc
                        .slice(0, 19)
                        .replaceAll("-", ".")
                        .replace("T", " "),
                    openUtc: c.openTimeUtc,
                    closeUtc: c.closeTimeUtc,
                    open: c.open,
                    high: c.high,
                    low: c.low,
                    close: c.close,
                    volume: c.volume,
                });
                allCandles.set(tf, bars);
            }
        } else Object.values(v).forEach(visit);
    };
    visit(research);
    const sources = Object.fromEntries(
        await Promise.all(
            frames.map(async (f) => {
                const bars = (allCandles.get(f) ?? []).sort((a, b) =>
                    a.openUtc.localeCompare(b.openUtc),
                );
                return [
                    f,
                    {
                        bars,
                        mapping: "Synthetic",
                        originalSha256: await sha256(JSON.stringify(bars)),
                        coverage: coverage(bars),
                    },
                ];
            }),
        ),
    ) as OriginalPackage["sources"];
    const provenance = await sha256(JSON.stringify(sources));
    for (const f of research.replayCutoff.sources) {
        const s =
            sources[
                (f.timeframe[1].toUpperCase() +
                    f.timeframe[0]) as keyof typeof sources
            ];
        Object.assign(f, {
            lastNominalCloseUtc: s.coverage.lastClosedUtc,
            closedCount: s.coverage.count,
        });
    }
    const originalText = await seal(originalSchema, {
        sessionId: research.datasetId,
        operatingDate: date,
        cutoffUtc: cutoff,
        exposure: "Synthetic",
        provider: research.provider,
        instrument: research.instrument,
        provenanceSha256: provenance,
        sources,
        canonicalAdmissionPermitted: false,
        operationalSignal: false,
    });
    const packet = await readOriginalPackage(originalText);
    const orig = (t: string) =>
        reference(packet, "M5", date + "T" + t + ":00Z");
    let review = startEventReview(packet, "QA", "Unknown", cutoff, device);
    const base = {
        kind: "Reference" as const,
        observation: "LiquidityTake" as const,
        note: "A",
        viewingCutoffUtc: cutoff,
        scopeStartUtc: null,
        references: [orig("13:05")],
    };
    review = appendEvent(packet, review, null, base, device);
    review = appendEvent(
        packet,
        review,
        null,
        { ...base, references: [orig("13:40")], note: "B" },
        device,
    );
    review = appendEvent(
        packet,
        review,
        review.labels[0].targetId,
        { ...base, note: "Corrección A" },
        device,
    );
    const reviewText = await exportEventReview(packet, review);
    const report = {
        schemaVersion: 1,
        inputSha256: provenance,
        documentaryMapping: "Software fixture",
        sourceProvenance: Object.fromEntries(
            frames.map((f) => [
                f,
                {
                    sourceTimeframe: f[1] + f[0],
                    derived: false,
                    rawRelativePath: f + ".tsv",
                    rawSha256: sources[f].originalSha256,
                    normalizedSha256: "9".repeat(64),
                },
            ]),
        ),
        research,
    };
    const reportText = JSON.stringify(report);
    const manifest = {
        schema: "moneyway.nasdaq-evidence-comparison-manifest.v1",
        packageSha256: packet.sha256,
        reviewSha256: await sha256(reviewText),
        reportSha256: await sha256(reportText),
        sessionId: packet.sessionId,
        operatingDate: date,
        cutoffUtc: cutoff,
        exposure: packet.exposure,
        provider: packet.provider,
        instrument: packet.instrument,
        candidateId: research.candidateId,
        candidateVersion: research.version,
        windowId: research.window.candidateConfigurationId,
        configurationId: null,
        observationTypes: [
            "LiquidityTake",
            "M5SC",
            "M5IFVG",
            "M5FVG",
            "M1Realignment",
            "H4Context",
        ],
    };
    return {
        packet,
        originalText,
        report,
        reportText,
        reviewText,
        manifest,
        prepare: () =>
            prepareComparison(
                packet,
                reviewText,
                reportText,
                JSON.stringify(manifest),
            ),
    };
}

describe("comparación documental por evento", () => {
    it("agrupa observaciones compartidas por perfiles y conserva dos eventos humanos y la revisión de A", async () => {
        const f = await fixtures();
        const c = await f.prepare();
        const [a, b] = humanRows(c);
        expect(a.selected.note).toBe("Corrección A");
        expect(a.history).toHaveLength(2);
        expect(b.selected.note).toBe("B");
        const [take] = c.candidates.filter((e) => e.type === "LiquidityTake");
        expect(take.configurations).toHaveLength(2);
        expect(
            c.candidates.filter((e) => e.type === "M1Realignment"),
        ).toHaveLength(1);
        expect(relationship(a.selected, take)).toBe("ExactDocumentary");
        expect(relationship(b.selected, take)).toBe("DifferentEvidence");
        expect(outcomes(c, [])).toMatchObject({
            humanEvents: 2,
            humanRevisions: 3,
            comparablePairs: 2,
            exactPairs: 1,
            confirmed: 0,
            humanUnassociated: 2,
            candidateUnassociated: expect.any(Number),
        });
        const d = recordDisclosure(
            c,
            "Revisor QA",
            "PreviouslyExposed",
            opened,
        );
        const first = appendDecision(
            c,
            d,
            [],
            a.selected.targetId,
            take.id,
            "Confirmed",
            "Contrasté ambas referencias.",
            later,
        );
        expect(() =>
            appendDecision(
                c,
                d,
                [first],
                b.selected.targetId,
                take.id,
                "Confirmed",
                "Misma sesión.",
                later,
            ),
        ).toThrow(/alcance/);
        expect(() =>
            appendDecision(
                c,
                d,
                [first],
                a.selected.targetId,
                "otro",
                "Ambiguous",
                "Motivo",
                later,
            ),
        ).toThrow(/identifica/);
        const second = appendDecision(
            c,
            d,
            [first],
            b.selected.targetId,
            take.id,
            "DoesNotCorrespond",
            "Las velas difieren.",
            later,
        );
        const third = appendDecision(
            c,
            d,
            [first, second],
            a.selected.targetId,
            take.id,
            "Ambiguous",
            "Sigue en revisión.",
            later,
        );
        expect(outcomes(c, [first, second, third])).toMatchObject({
            decisions: 2,
            confirmed: 0,
            discrepancies: 1,
            ambiguous: 1,
        });
        expect(() => parseSessionReport(f.report)).toThrow(/junio 23/);
    });

    it("respeta el alcance de NoReference y distingue exposición, retrospección y falta temporal", async () => {
        const c = await (await fixtures()).prepare();
        const h = humanRows(c)[0].selected;
        const take = c.candidates.find((e) => e.type === "LiquidityTake")!;
        const absent = {
            ...h,
            kind: "NoReference" as const,
            references: [],
            scopeStartUtc: date + "T13:00:00Z",
        };
        expect(relationship(absent, take)).toBe("InsideNoReferenceScope");
        expect(
            relationship(
                { ...absent, scopeStartUtc: date + "T13:06:00Z" },
                take,
            ),
        ).toBe("OutsideNoReferenceScope");
        expect(
            relationship({ ...h, viewingCutoffUtc: date + "T13:09:00Z" }, take),
        ).toBe("InsufficientTemporalEvidence");
        expect(relationship(h, { ...take, at: null })).toBe(
            "InsufficientTemporalEvidence",
        );
        expect(relationship(h, { ...take, references: [] })).toBe(
            "InsufficientTemporalEvidence",
        );
        expect(relationship({ ...h, kind: "Ambiguous" }, take)).toBe(
            "HumanJudgmentUnresolved",
        );
        expect(relationship({ ...h, kind: "Skip" }, take)).toBe(
            "NotComparable",
        );
        const d = recordDisclosure(c, "QA", "Unknown", opened);
        expect(humanVisibility(h, d)).toMatch(/Anterior/);
        expect(
            humanVisibility(
                {
                    ...h,
                    phase: "PostRevealCorrection",
                    reviewMode: "RetrospectiveAfterLaterView",
                },
                d,
            ),
        ).toMatch(/Posterior/);
        expect(humanVisibility({ ...h, reviewedAtUtc: later }, d)).toMatch(
            /Posterior/,
        );
    });

    it("verifica configuración exacta, versión, procedencia, corte y hashes aunque los precios coincidan", async () => {
        const f = await fixtures();
        const c = await f.prepare();
        const take = c.candidates.find((e) => e.type === "LiquidityTake")!;
        const run = (m: object, report = f.reportText) =>
            prepareComparison(
                f.packet,
                f.reviewText,
                report,
                JSON.stringify({ ...f.manifest, ...m }),
            );
        expect(
            (
                await run({ configurationId: take.configurations[0].id })
            ).candidates.find((e) => e.id === take.id)?.configurations,
        ).toHaveLength(1);
        await expect(run({ configurationId: "otra" })).rejects.toThrow(
            /configuración exacta/,
        );
        await expect(run({ candidateVersion: "1.0.0" })).rejects.toThrow(
            /Manifiesto/,
        );
        await expect(run({ cutoffUtc: date + "T16:29:59Z" })).rejects.toThrow(
            /Manifiesto/,
        );
        await expect(run({}, f.reportText + " ")).rejects.toThrow(/SHA-256/);
        const otherRaw = structuredClone(f.report);
        otherRaw.sourceProvenance.M5.rawSha256 =
            otherRaw.sourceProvenance.M5.normalizedSha256;
        const text = JSON.stringify(otherRaw);
        await expect(
            run({ reportSha256: await sha256(text) }, text),
        ).rejects.toThrow(/fuentes originales/);
        const invalid = structuredClone(f.report);
        invalid.research.sequence.episodes[0].selection.event.candle.open -= 1;
        const bad = JSON.stringify(invalid);
        await expect(
            run({ reportSha256: await sha256(bad) }, bad),
        ).rejects.toThrow(/vela del informe/);
        for (const day of [
            "2026-11-23",
            "2026-11-24",
            "2026-06-25",
            "2026-06-30",
            "2026-09-08",
        ]) {
            const forged = f.reportText.replaceAll(date, day);
            await expect(
                run({ reportSha256: await sha256(forged) }, forged),
            ).rejects.toThrow(/Development fijado/);
        }
    });

    it("reconstruye el mismo historial al importar y rechaza cambios en una exportación resellada", async () => {
        const c = await (await fixtures()).prepare();
        const d = recordDisclosure(c, "QA", "PreviouslyExposed", opened);
        const take = c.candidates.find((e) => e.type === "LiquidityTake")!;
        const decision = appendDecision(
            c,
            d,
            [],
            c.review.labels[0].targetId,
            take.id,
            "Confirmed",
            "Fuente exacta.",
            later,
        );
        const exported = await exportComparison(c, d, [decision]);
        await expect(importComparison(c, exported)).resolves.toEqual({
            disclosure: d,
            decisions: [decision],
        });
        const schema = "moneyway.nasdaq-evidence-comparison.v1";
        const { value } = await unseal(exported, schema);
        expect(value).toMatchObject({
            canonicalAdmissionPermitted: false,
            operationalSignal: false,
            independentValidation: false,
        });
        const changed = await seal(schema, {
            ...(value as object),
            operationalSignal: true,
        });
        await expect(importComparison(c, changed)).rejects.toThrow(/alterados/);
    });

    it("separa la ruta y exige declarar exposición antes de mostrar evidencia experimental", async () => {
        window.history.replaceState(null, "", "/#strategy-lab");
        const route = render(<App />);
        await userEvent.click(
            screen.getByRole("link", {
                name: /Comparación de evidencia Nasdaq/,
            }),
        );
        await screen.findByRole("heading", {
            name: "Comparación de evidencia Nasdaq",
        });
        route.unmount();
        const f = await fixtures();
        render(<EvidenceComparison />);
        for (const [label, text] of [
            ["Paquete original sintético", f.originalText],
            ["Etiquetas humanas V2", f.reviewText],
            ["Informe integrado 1.1.0", f.reportText],
            ["Manifiesto de comparación", JSON.stringify(f.manifest)],
        ]) {
            await userEvent.upload(
                screen.getByLabelText(label),
                new File([text], label + ".json", { type: "application/json" }),
            );
            if (label === "Paquete original sintético")
                await screen.findByText(/Paquete synthetic-us100/);
        }
        fireEvent.click(
            screen.getByRole("button", { name: "Verificar insumos" }),
        );
        await screen.findByText(/Insumos verificados/);
        expect(screen.queryByLabelText("Evento experimental")).toBeNull();
        fireEvent.click(
            screen.getByRole("button", {
                name: "Abrir comparación retrospectiva",
            }),
        );
        expect(screen.getByRole("alert").textContent).toMatch(
            /Declare la exposición/,
        );
        fireEvent.change(screen.getByLabelText("Revisor de comparación"), {
            target: { value: "QA" },
        });
        fireEvent.change(screen.getByLabelText("Exposición al informe"), {
            target: { value: "PreviouslyExposed" },
        });
        fireEvent.click(
            screen.getByRole("button", {
                name: "Abrir comparación retrospectiva",
            }),
        );
        expect(
            screen.getByText(/Coincidencias documentales exactas 1\/2/),
        ).toBeDefined();
        fireEvent.change(screen.getByLabelText("Evento humano"), {
            target: { value: "synthetic-us100-v1/event/1" },
        });
        const pick = screen.getByLabelText(
            "Evento experimental",
        ) as HTMLSelectElement;
        fireEvent.change(pick, { target: { value: pick.options[1].value } });
        await waitFor(() =>
            expect(
                screen.getByText("Coincidencia documental exacta"),
            ).toBeDefined(),
        );
        expect(
            screen.queryByRole("button", {
                name: /orden|ejecutar|comprar|vender/i,
            }),
        ).toBeNull();
    });
});
