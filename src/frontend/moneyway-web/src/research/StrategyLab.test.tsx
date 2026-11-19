import { render, screen, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ResearchViewer, StrategyLab } from "./StrategyLab";
import { readReport, algorithmIds } from "./report";
import { loadEpisode } from "./episodes";
import * as episodeSource from "./episodes";
import synthetic from "./reports/synthetic-v1.json";
import historical from "./reports/june29-v1.json";
import App from "../App";

const fixture = () => readReport(structuredClone(synthetic));
afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    window.location.hash = "";
});

describe("versioned research report boundary", () => {
    it("loads both real runner reports with their distinct classifications", async () => {
        expect(
            (await loadEpisode("synthetic-v1")).comparison.input.dataOrigin,
        ).toBe("Synthetic");
        expect(
            (await loadEpisode("june29-v1")).comparison.input.dataOrigin,
        ).toBe("RetrospectiveDocumentary");
        await expect(loadEpisode("../../secrets.json")).rejects.toThrow(
            "no registrado",
        );
    });
    it.each([
        "schema",
        "admission",
        "signal",
        "ohlc",
        "future",
        "identity",
        "missing",
        "marker",
        "version",
        "pairs",
    ])("rejects invalid %s instead of substituting data", (issue) => {
        const data = structuredClone(synthetic);
        switch (issue) {
            case "schema":
                data.comparison.schemaVersion = "99";
                break;
            case "admission":
                Object.assign(data.comparison, {
                    canonicalAdmissionPermitted: true,
                });
                break;
            case "signal":
                Object.assign(data.comparison.results[0], {
                    operationalSignal: true,
                });
                break;
            case "ohlc":
                data.comparison.evidenceCandles[0].high = 1;
                break;
            case "future":
                data.comparison.evidenceCandles[5].closeTimeUtc =
                    "2030-01-01T00:00:00Z";
                break;
            case "identity":
                data.comparison.evidenceCandles[0].symbol.value = "USTEC";
                break;
            case "missing":
                data.comparison.results.pop();
                break;
            case "marker":
                data.comparison.results[0].referenceCandles[0].low = 42;
                break;
            case "version":
                data.comparison.results[0].algorithm.algorithmVersion = "2.0.0";
                break;
            case "pairs":
                data.comparison.comparisons[0].rightAlgorithm =
                    "EXP-M5-SWING-Z-V1";
                break;
        }
        expect(() => readReport(data)).toThrow("inválido");
    });
});

describe("chart-first research viewer", () => {
    it("plots exact OHLC geometry and all original reference/confirmation identities", () => {
        const { container } = render(<ResearchViewer envelope={fixture()} />);
        expect(screen.getByText("Fixture sintético")).toBeTruthy();
        expect(screen.getByText(/Research Only/)).toBeTruthy();
        expect(container.querySelectorAll('[data-testid="wick"]')).toHaveLength(
            6,
        );
        expect(container.querySelectorAll('[data-testid="body"]')).toHaveLength(
            6,
        );
        const row = container.querySelector(
            '[data-candle="2026-11-19T13:00:00+00:00"]',
        )!;
        const wick = row.querySelector('[data-testid="wick"]')!;
        const body = row.querySelector('[data-testid="body"]')!;
        expect(Number(wick.getAttribute("y1"))).toBeLessThan(
            Number(body.getAttribute("y")),
        );
        expect(Number(wick.getAttribute("y2"))).toBeGreaterThan(
            Number(body.getAttribute("y")) +
                Number(body.getAttribute("height")),
        );
        const a = container.querySelector(
            '[data-overlay="EXP-M5-SWING-A-V1"]',
        )!;
        expect(
            a.querySelector('[data-marker="start"]')?.getAttribute("data-open"),
        ).toBe("2026-11-19T13:05:00+00:00");
        expect(
            a
                .querySelector('[data-marker="reference"]')
                ?.getAttribute("data-open"),
        ).toBe("2026-11-19T13:10:00+00:00");
        expect(
            a
                .querySelector('[data-marker="confirmation"]')
                ?.getAttribute("data-known-at"),
        ).toBe("2026-11-19T13:30:00+00:00");
        expect(
            container.querySelectorAll('[data-reference-price="90"]'),
        ).toHaveLength(1);
        expect(screen.getByText("A / B / D · 90 · retrospectiva")).toBeTruthy();
        expect(screen.getByText("C · 93 · retrospectiva")).toBeTruthy();
        expect(
            screen.getByText(/Inspeccionar una vela no cambia el cutoff/),
        ).toBeTruthy();
    });
    it("toggles independent overlays while preserving all result cards and the immutable report", async () => {
        const data = fixture();
        const original = JSON.stringify(data);
        const { container } = render(<ResearchViewer envelope={data} />);
        await userEvent.click(
            screen.getByRole("checkbox", { name: "C Combined Strict" }),
        );
        expect(
            container.querySelector('[data-overlay="EXP-M5-SWING-C-V1"]'),
        ).toBeNull();
        expect(
            screen.getByRole("heading", { name: /C Combined Strict/ }),
        ).toBeTruthy();
        expect(JSON.stringify(data)).toBe(original);
        await userEvent.click(
            screen.getByRole("checkbox", { name: "C Combined Strict" }),
        );
        expect(
            container.querySelector('[data-overlay="EXP-M5-SWING-C-V1"]'),
        ).toBeTruthy();
    });
    it("inspects original candle prices via keyboard and previous/next controls", async () => {
        render(<ResearchViewer envelope={fixture()} />);
        await userEvent.click(
            screen.getByRole("button", { name: "Vela siguiente" }),
        );
        expect(
            within(
                screen.getByText("2026-11-19 13:05:00 UTC", {
                    selector: "strong",
                }).parentElement!,
            ).getByText(/Cierre/),
        ).toBeTruthy();
        const target = screen.getByRole("button", {
            name: "Inspeccionar vela 2026-11-19 13:10:00 UTC",
        });
        target.focus();
        await userEvent.keyboard("{ArrowRight}");
        await userEvent.keyboard("{ArrowRight}{ArrowLeft}");
        expect(
            screen.getByText("2026-11-19 13:15:00 UTC", { selector: "strong" }),
        ).toBeTruthy();
        await userEvent.click(
            screen.getByRole("button", { name: "Vela anterior" }),
        );
        expect(
            screen.getByText("2026-11-19 13:10:00 UTC", { selector: "strong" }),
        ).toBeTruthy();
    });
    it("shows same reference/different start independently of full agreement", () => {
        render(<ResearchViewer envelope={fixture()} />);
        const pair = screen.getByText("A / B", {
            selector: "b",
        }).parentElement!;
        expect(within(pair).getByText("Discrepancia")).toBeTruthy();
        expect(
            within(pair).getByText("Referencia / confirmación: coinciden"),
        ).toBeTruthy();
        expect(
            within(
                screen.getByText("A / D", { selector: "b" }).parentElement!,
            ).getByText("Coincidencia total"),
        ).toBeTruthy();
    });
    it("shows authentic documentary data with honest all-abstained results", () => {
        const { container } = render(
            <ResearchViewer envelope={readReport(historical)} />,
        );
        expect(
            screen.getByText("Histórico auténtico · documental"),
        ).toBeTruthy();
        expect(screen.getByText(/puede ser abstención/)).toBeTruthy();
        expect(
            screen.getAllByText("AbstainedNoPullbackBeforeContinuation"),
        ).toHaveLength(4);
        expect(
            container.querySelectorAll('[data-marker="confirmation"]'),
        ).toHaveLength(0);
        expect(container.querySelectorAll('[data-testid="wick"]')).toHaveLength(
            24,
        );
    });
    it("retains equal-extreme identities and distinguishes provisional references from confirmation", () => {
        // Projection-only synthetic contract variant, not another C# detector outcome.
        const data = fixture();
        const row = data.comparison.results[0];
        const tiedBar = data.comparison.evidenceCandles[3];
        tiedBar.low = 90;
        row.status = "AwaitingContinuation";
        row.isExperimentalReferenceConfirmed = false;
        row.confirmationAtUtc = null;
        row.continuationCandle = null;
        row.referenceCandles.push(tiedBar);
        const { container } = render(<ResearchViewer envelope={data} />);
        expect(screen.getByText("provisional")).toBeTruthy();
        expect(
            container
                .querySelector('[data-overlay="EXP-M5-SWING-A-V1"]')
                ?.querySelectorAll('[data-marker="reference"]'),
        ).toHaveLength(2);
        expect(screen.getByText("AwaitingContinuation")).toBeTruthy();
    });
    it("keeps per-algorithm confirmation timestamps distinct in a projection-only synthetic contract case", () => {
        const data = fixture();
        const a = data.comparison.results[0];
        a.continuationCandle = data.comparison.evidenceCandles[4];
        a.confirmationAtUtc = a.continuationCandle.closeTimeUtc;
        const { container } = render(<ResearchViewer envelope={data} />);
        expect(
            container
                .querySelector(
                    '[data-overlay="EXP-M5-SWING-A-V1"] [data-marker="confirmation"]',
                )
                ?.getAttribute("data-known-at"),
        ).toBe("2026-11-19T13:25:00+00:00");
        expect(
            container
                .querySelector(
                    '[data-overlay="EXP-M5-SWING-B-V1"] [data-marker="confirmation"]',
                )
                ?.getAttribute("data-known-at"),
        ).toBe("2026-11-19T13:30:00+00:00");
    });
    it("shows an explicit error with no fallback chart if a registered resource fails validation", async () => {
        vi.spyOn(episodeSource, "loadEpisode").mockRejectedValueOnce(
            new Error("Reporte inválido"),
        );
        render(<StrategyLab />);
        expect(await screen.findByRole("alert")).toHaveProperty(
            "textContent",
            "Reporte inválido",
        );
        expect(screen.queryByRole("img")).toBeNull();
        expect(screen.queryByText("Fixture sintético")).toBeNull();
    });
    it("opens the dedicated route without querying API, MT5 or operational forms", async () => {
        window.location.hash = "#strategy-lab";
        const fetcher = vi.fn();
        vi.stubGlobal("fetch", fetcher);
        render(<App />);
        expect(await screen.findByText("Fixture sintético")).toBeTruthy();
        expect(fetcher).not.toHaveBeenCalled();
        expect(
            screen.queryByText("Abrir o recuperar ámbito de sesión"),
        ).toBeNull();
        expect(screen.getAllByRole("checkbox")).toHaveLength(
            algorithmIds.length,
        );
    });
    it("switches episodes without retaining synthetic markers or claiming a causal replay", async () => {
        render(<StrategyLab />);
        expect(await screen.findByText("Fixture sintético")).toBeTruthy();
        await userEvent.selectOptions(
            screen.getByRole("combobox"),
            "june29-v1",
        );
        expect(
            await screen.findByText("Histórico auténtico · documental"),
        ).toBeTruthy();
        await waitFor(() =>
            expect(screen.queryByText("C · 93 · retrospectiva")).toBeNull(),
        );
        expect(
            screen.getByText(/Sin señales ni admisión canónica/),
        ).toBeTruthy();
    });
});
