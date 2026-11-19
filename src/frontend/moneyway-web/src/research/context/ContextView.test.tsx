/// <reference types="node" />
import { webcrypto } from "node:crypto";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, afterEach, describe, it, expect, vi } from "vitest";
import raw from "./data/june23-context.json";
import {
    closedAt,
    coverageAt,
    eligibleReview,
    frames,
    loadContext,
    loadOriginalComparison,
    originalReviews,
    validateContext,
    visibleAt,
    type ContextData,
} from "./model";
import { ContextualResearch } from "./ContextView";
import { CandleChart } from "../StrategyLab";
import { contextExposureKey, markContextSeen } from "../contextExposure";
import { BlindReview } from "../blind/BlindReview";
import {
    commitLabel,
    emptyDraft,
    emptyState,
    persist,
    storageKey,
} from "../blind/labels";
import App from "../../App";

const data = () => structuredClone(raw) as unknown as ContextData;
const firstId = "m5-20260623-1300-ascending-v1";
const secondId = "m5-20260623-1400-descending-v1";
beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("crypto", webcrypto);
    vi.stubGlobal(
        "fetch",
        vi.fn(
            async () =>
                new Response(JSON.stringify(raw), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
        ),
    );
});
afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    window.location.hash = "";
});

describe("registered original timeframe context", () => {
    it("loads only the registered static asset and preserves real original coverage", async () => {
        const c = await loadContext();
        expect(c.series.H4.count).toBe(10);
        expect(c.series.H1.count).toBe(41);
        expect(c.series.M5.count).toBe(498);
        expect(c.series.M1.count).toBe(2490);
        expect(frames.every((f) => c.series[f].derived === false)).toBe(true);
        expect(c.timeZone).toBe("America/Bogota");
        expect(c.canonicalAdmissionPermitted).toBe(false);
        expect(c.operationalSignal).toBe(false);
        expect(JSON.stringify(raw)).not.toContain("2026-06-25T");
        expect(JSON.stringify(raw)).not.toContain("2026-06-30T");
    });
    it.each([
        "identity",
        "timeframe",
        "equivalent-unit",
        "unfinished",
        "ohlc",
        "native",
        "scope",
        "boundaries",
    ])("rejects %s corruption", (issue) => {
        const c = data();
        if (issue === "identity") c.series.M5.candles[0].symbol.value = "USTEC";
        if (issue === "timeframe") c.series.H4.derived = true as false;
        if (issue === "equivalent-unit")
            c.series.H4.candles[0].timeframe = { amount: 240, unit: "Minute" };
        if (issue === "unfinished")
            c.series.H4.candles.at(-1)!.closeTimeUtc = "2026-06-23T17:00:00Z";
        if (issue === "ohlc") c.series.M1.candles[0].low = 999999;
        if (issue === "native")
            c.series.H1.candles[0].nativeOpenTime = "not a native identity";
        if (issue === "scope") c.operationalSignal = true as false;
        if (issue === "boundaries") c.sessionBoundariesStatus = "Confirmed";
        expect(() => validateContext(c)).toThrow("inválido");
    });
    it("excludes unfinished intervals in every timeframe at one shared cutoff", () => {
        const c = data();
        const cutoff = "2026-06-23T13:30:00Z";
        for (const f of frames)
            expect(
                closedAt(c, f, cutoff).every(
                    (b) => Date.parse(b.closeTimeUtc) <= Date.parse(cutoff),
                ),
            ).toBe(true);
        expect(closedAt(c, "H4", cutoff).at(-1)!.closeTimeUtc).toBe(
            "2026-06-23T13:00:00Z",
        );
        expect(closedAt(c, "H1", cutoff).at(-1)!.closeTimeUtc).toBe(
            "2026-06-23T13:00:00Z",
        );
        expect(closedAt(c, "M5", cutoff).at(-1)!.closeTimeUtc).toBe(cutoff);
        expect(closedAt(c, "M1", cutoff).at(-1)!.closeTimeUtc).toBe(cutoff);
        expect(() => closedAt(c, "M5", "2026-06-25T13:00:00Z")).toThrow(
            "Cutoff",
        );
    });
    it("reports source holes separately from a not-yet-closed tail", () => {
        const c = data();
        const coverage = coverageAt(c, "H1", c.maximumCutoffUtc);
        expect(coverage.gaps.map((g) => [g.startUtc, g.endUtc])).toEqual([
            ["2026-06-21T21:00:00Z", "2026-06-21T22:00:00Z"],
            ["2026-06-22T21:00:00Z", "2026-06-22T22:00:00Z"],
        ]);
        expect(coverage.pendingInterval).toBe(true);
        expect(coverageAt(c, "H4", c.maximumCutoffUtc).gaps).toEqual([]);
        expect(coverageAt(c, "M1", c.maximumCutoffUtc).pendingInterval).toBe(
            false,
        );
        expect(coverageAt(c, "H4", c.requestedStartUtc).count).toBe(0);
    });
    it("does not let future candles or unavailable references influence earlier chart geometry", () => {
        const c = data();
        const cutoff = "2026-06-23T13:00:00Z";
        const props = {
            visible: [],
            selected: 0,
            onSelect: () => {},
            report: {
                input: {
                    symbol: "US100",
                    timeframe: "M5",
                    evaluationCutoffUtc: cutoff,
                },
                evidenceCandles: visibleAt(c, "M5", cutoff, 6),
                results: [],
            },
        };
        const view = render(<CandleChart {...props} />);
        const y = view.container
            .querySelector('[data-testid="wick"]')
            ?.getAttribute("y1");
        for (const b of c.series.M5.candles)
            if (Date.parse(b.closeTimeUtc) > Date.parse(cutoff))
                b.high = 999999999;
        view.rerender(
            <CandleChart
                {...props}
                report={{
                    ...props.report,
                    evidenceCandles: visibleAt(c, "M5", cutoff, 6),
                }}
                references={[
                    {
                        id: "future-price",
                        label: "unavailable",
                        price: 999999999,
                        formationUtc: "2026-06-23T13:30:00Z",
                        knownAtUtc: "2026-06-23T14:00:00Z",
                        color: "white",
                    },
                    {
                        id: "formed-earlier-known-later",
                        label: "unavailable",
                        price: 999999999,
                        formationUtc: "2026-06-23T12:00:00Z",
                        knownAtUtc: "2026-06-23T14:00:00Z",
                        color: "white",
                    },
                ]}
            />,
        );
        expect(
            view.container
                .querySelector('[data-testid="wick"]')
                ?.getAttribute("y1"),
        ).toBe(y);
        expect(
            view.container.querySelector("[data-context-reference]"),
        ).toBeNull();
    });
    it("verifies actual original report identities without changing V1 output or human confirmations", async () => {
        expect(originalReviews.sourceSha256).toBe(
            "8368a3b6ed259f19e757edac632341674814185c7adab787697258683ebc4861",
        );
        expect(
            originalReviews.reviews.every(
                (r) =>
                    r.label.revision === 1 &&
                    r.label.confirmationCloseUtc === null,
            ),
        ).toBe(true);
        const first = await loadOriginalComparison(firstId);
        const second = await loadOriginalComparison(secondId);
        expect(
            first.comparison.results.map((r) => r.proposedReferencePrice),
        ).toEqual([29441.2, 29441.2, 29441.2, 29441.2]);
        expect(
            second.comparison.results.map((r) => r.proposedReferencePrice),
        ).toEqual([29811.6, 29811.6, 29811.6, 29811.6]);
        expect(eligibleReview(firstId, "2026-06-23T13:35:00Z")).toBeNull();
        expect(eligibleReview(firstId, "2026-06-23T14:00:00Z")).not.toBeNull();
        await expect(
            loadOriginalComparison("m5-20260625-1300-ascending-v1"),
        ).rejects.toThrow("solo desarrollo");
        await expect(
            loadOriginalComparison("m5-20260630-1300-ascending-v1"),
        ).rejects.toThrow("solo desarrollo");
    });
});

describe("contextual chart workflow", () => {
    it.each(frames)(
        "switches to %s while retaining the shared cutoff and original OHLC",
        async (frame) => {
            const { container } = render(<ContextualResearch />);
            await screen.findByRole("heading", {
                name: "La sesión en contexto",
            });
            const cut = screen.getByLabelText(
                "Cutoff compartido · Bogotá",
            ) as HTMLSelectElement;
            expect(cut.value).toBe("2026-06-23T15:00:00Z");
            await userEvent.click(screen.getByRole("button", { name: frame }));
            expect(cut.value).toBe("2026-06-23T15:00:00Z");
            expect(
                screen
                    .getByRole("button", { name: frame })
                    .getAttribute("aria-pressed"),
            ).toBe("true");
            expect(
                screen.getByRole("img", {
                    name: `Velas OHLC ${frame} y referencias experimentales`,
                }),
            ).toBeTruthy();
            expect(
                container.querySelectorAll('[data-testid="wick"]').length,
            ).toBeGreaterThan(0);
            expect(screen.getByText(/nativo .*MT5 server/)).toBeTruthy();
            expect(screen.getByText(/no agregado/)).toBeTruthy();
        },
    );
    it("hides all later comparison labels and references when moving the cutoff earlier", async () => {
        const { container } = render(<ContextualResearch />);
        await screen.findByLabelText(
            "Comparación original preservada",
            {},
            { timeout: 5000 },
        );
        expect(
            container.querySelector('[data-context-reference="human-0"]'),
        ).toBeTruthy();
        const human = container.querySelector(
            '[data-context-reference="human-0"] circle',
        )!;
        const candle = container.querySelector(
            '[data-candle="2026-06-23T13:30:00Z"] [data-testid="wick"]',
        )!;
        expect(human.getAttribute("cx")).toBe(candle.getAttribute("x1"));
        expect(human.getAttribute("data-formation-utc")).toBe(
            "2026-06-23T13:30:00+00:00",
        );
        await userEvent.selectOptions(
            screen.getByLabelText("Cutoff compartido · Bogotá"),
            "2026-06-23T13:00:00Z",
        );
        expect(screen.getByText(/Comparación oculta/)).toBeTruthy();
        expect(screen.queryByText(/Humano r1:/)).toBeNull();
        expect(container.querySelector("[data-context-reference]")).toBeNull();
        expect(screen.queryByText("29381.7")).toBeNull();
        expect(
            container.querySelector('[data-time-guide="2026-06-23T13:30:00Z"]'),
        ).toBeNull();
    });
    it("keeps original bounded cutoffs and human/algorithm formation times separate", async () => {
        render(<ContextualResearch />);
        await screen.findByLabelText(
            "Comparación original preservada",
            {},
            { timeout: 5000 },
        );
        expect(screen.getByText("29381.7")).toBeTruthy();
        expect(screen.getByText(/1\/2 acuerdos exactos/)).toBeTruthy();
        expect(screen.getByText("no suministrada")).toBeTruthy();
        expect(
            screen.getByText(/Latencia relativa al humano: no disponible/),
        ).toBeTruthy();
        await userEvent.selectOptions(
            screen.getByLabelText("Cutoff compartido · Bogotá"),
            "2026-06-23T14:00:00Z",
        );
        expect(screen.queryByText(/1\/2 acuerdos exactos/)).toBeNull();
        expect(screen.getByText(/Resumen conjunto oculto/)).toBeTruthy();
        await userEvent.selectOptions(
            screen.getByLabelText("Cutoff compartido · Bogotá"),
            "2026-06-23T15:00:00Z",
        );
        await userEvent.selectOptions(
            screen.getByLabelText("Comparación original M5"),
            secondId,
        );
        await waitFor(() =>
            expect(
                screen.getByText(
                    /Experimento M5 original · cutoff 2026-06-23 15:00:00 UTC/,
                ),
            ).toBeTruthy(),
        );
        expect(screen.getByText("29811.6", { selector: "b" })).toBeTruthy();
        await userEvent.selectOptions(
            screen.getByLabelText("Cutoff compartido · Bogotá"),
            "2026-06-23T14:00:00Z",
        );
        expect(screen.getByText(/Comparación oculta/)).toBeTruthy();
    });
    it("shows boundary limitations instead of authoritative Asia/London extrema or new strategy facts", async () => {
        const { container } = render(<ContextualResearch />);
        await screen.findByRole("heading", { name: "La sesión en contexto" });
        expect(
            screen.getByText(/Asia\/London: límites no resueltos/),
        ).toBeTruthy();
        expect(
            screen.getByText(
                /instrucción 08:30–11:30 \/ runtime \[08:30,11:00\)/,
            ),
        ).toBeTruthy();
        expect(
            screen.getByText(
                /08:00 es una referencia horaria; no acredita preparación completada/,
            ),
        ).toBeTruthy();
        expect(
            container.querySelector('[data-time-guide="2026-06-23T13:00:00Z"]'),
        ).toBeTruthy();
        expect(screen.queryByText("AsiaHigh")).toBeNull();
        expect(screen.queryByText("LondonLow")).toBeNull();
        expect(
            screen.queryByRole("option", { name: /25 junio|30 junio|HeldOut/ }),
        ).toBeNull();
    });
    it("loads no replacement session when the static context is missing", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(async () => new Response("missing", { status: 404 })),
        );
        const { container } = render(<ContextualResearch />);
        expect((await screen.findByRole("alert")).textContent).toContain(
            "no disponible",
        );
        expect(container.querySelector("svg")).toBeNull();
    });
    it("records exposure for future first reviews without rewriting original V3 labels", async () => {
        const saved = commitLabel(
            emptyState(),
            firstId,
            { ...emptyDraft(), kind: "NoReference" },
            "Synthetic persistence test",
            false,
            "2026-11-19T20:00:00.000Z",
        );
        const before = persist(localStorage, null, saved);
        const view = render(<ContextualResearch />);
        await screen.findByRole("heading", { name: "La sesión en contexto" });
        await waitFor(() =>
            expect(localStorage.getItem(contextExposureKey)).toContain(firstId),
        );
        expect(localStorage.getItem(storageKey)).toBe(before);
        view.unmount();
        localStorage.removeItem(storageKey);
        render(<BlindReview />);
        expect(
            screen.getByText(
                /PreviouslyExposed: no es una observación ciega independiente/,
            ),
        ).toBeTruthy();
    });
    it("preserves an already committed blind label and prevents post-context pre-reveal rewriting", () => {
        const saved = commitLabel(
            emptyState(),
            firstId,
            { ...emptyDraft(), kind: "NoReference" },
            "Synthetic persistence test",
            false,
            "2026-11-19T20:00:00.000Z",
        );
        const before = persist(localStorage, null, saved);
        markContextSeen([firstId]);
        render(<BlindReview />);
        expect(
            screen.getByRole("button", {
                name: "Revisar selección antes de revelar",
            }),
        ).toHaveProperty("disabled", true);
        expect(localStorage.getItem(storageKey)).toBe(before);
        expect(
            screen.getByRole("button", { name: "Revelar algoritmos" }),
        ).toBeTruthy();
    });
    it("opens the contextual route without an API or MT5 dependency", async () => {
        window.location.hash = "#strategy-lab-context";
        render(<App />);
        expect(
            await screen.findByRole("heading", {
                name: "La sesión en contexto",
            }),
        ).toBeTruthy();
        const urls = vi.mocked(fetch).mock.calls.map((c) => String(c[0]));
        expect(urls.every((url) => !url.includes("/api/"))).toBe(true);
    });
});
