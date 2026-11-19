/// <reference types="node" />
import { webcrypto } from "node:crypto";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BlindReview } from "./BlindReview";
import * as data from "./catalog";
import { decodeState, independentLabel, storageKey } from "./labels";
import App from "../../App";

const episode = data.catalog.episodes.find(
    (e) => e.partition === "Development",
)!;
const bars = data.charts[episode.id].evidenceCandles;
const pickName = (index: number) =>
    `Inspeccionar vela ${bars[index].openTimeUtc.slice(0, 10)} ${bars[index].openTimeUtc.slice(11, 19)} UTC`;
async function reviewer() {
    await userEvent.type(
        screen.getByRole("textbox", { name: "Revisor local" }),
        "UI synthetic test reviewer",
    );
}
async function commitNone() {
    await reviewer();
    await userEvent.click(
        screen.getByRole("radio", { name: "Sin referencia válida" }),
    );
    await userEvent.click(
        screen.getByRole("button", { name: "Confirmar selección" }),
    );
}
beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("crypto", webcrypto);
});
afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    window.location.hash = "";
});

describe("chart-first local blind review; all entered judgments are synthetic test input", () => {
    it("demonstrates synthetic selection 90 against the actual C# A/B/D=90 and C=93 report", async () => {
        render(<BlindReview />);
        await reviewer();
        await userEvent.selectOptions(
            screen.getByLabelText("Partición"),
            "SoftwareAcceptance",
        );
        await userEvent.click(
            screen.getByRole("button", {
                name: "Inspeccionar vela 2026-11-19 13:10:00 UTC",
            }),
        );
        expect(screen.getByText("Low 90 · 1 vela(s)")).toBeTruthy();
        expect(screen.getAllByRole("textbox")).toHaveLength(1);
        await userEvent.click(
            screen.getByRole("button", { name: "Confirmar selección" }),
        );
        await userEvent.click(
            screen.getByRole("button", { name: "Revelar algoritmos" }),
        );
        const comparison = await screen.findByRole("region", {
            name: "Comparación después de revelar",
        });
        expect(within(comparison).getAllByText(/ExactReference/)).toHaveLength(
            3,
        );
        expect(
            within(comparison).getAllByText(/IncorrectReference/),
        ).toHaveLength(1);
        expect(within(comparison).getByText("3 / 3")).toBeTruthy();
        expect(
            decodeState(localStorage.getItem(storageKey)).records[
                "software-synthetic"
            ].exposure,
        ).toBe("PreviouslyExposed");
    });
    it("does not load or display any algorithm output until saved commitment AND reveal", async () => {
        const loader = vi.spyOn(data, "loadRevealedReport");
        const { container } = render(<BlindReview />);
        expect(container.querySelectorAll('[data-testid="wick"]')).toHaveLength(
            12,
        );
        expect(
            container.querySelectorAll("[data-human-reference]"),
        ).toHaveLength(0);
        expect(container.querySelectorAll("[data-overlay]")).toHaveLength(0);
        expect(screen.queryByText(/EXP-M5-SWING/)).toBeNull();
        expect(
            screen.queryByRole("button", { name: "Revelar algoritmos" }),
        ).toBeNull();
        await reviewer();
        await userEvent.click(
            screen.getByRole("button", { name: pickName(1) }),
        );
        expect(
            container.querySelectorAll("[data-human-reference]"),
        ).toHaveLength(1);
        expect(screen.getByText(`Low ${bars[1].low} · 1 vela(s)`)).toBeTruthy();
        await userEvent.click(
            screen.getByRole("button", { name: "Confirmar selección" }),
        );
        expect(loader).not.toHaveBeenCalled();
        expect(localStorage.getItem(storageKey)).toContain(bars[1].openTimeUtc);
        await userEvent.click(
            screen.getByRole("button", { name: "Revelar algoritmos" }),
        );
        expect(
            await screen.findByRole("region", {
                name: "Comparación después de revelar",
            }),
        ).toBeTruthy();
        expect(loader).toHaveBeenCalledTimes(1);
        expect(container.querySelectorAll("[data-overlay]")).toHaveLength(4);
        expect(
            screen.getByText(
                "Comparación con revisión 1 · SelfDeclaredIndependent",
            ),
        ).toBeTruthy();
    });
    it("keeps the committed label and exposes an honest error when the reveal resource is unavailable", async () => {
        render(<BlindReview />);
        await commitNone();
        vi.spyOn(data, "loadRevealedReport").mockRejectedValueOnce(
            new Error("Reporte cambiado o no disponible (test)"),
        );
        await userEvent.click(
            screen.getByRole("button", { name: "Revelar algoritmos" }),
        );
        expect((await screen.findByRole("alert")).textContent).toContain(
            "Reporte cambiado",
        );
        expect(
            screen.queryByRole("region", {
                name: "Comparación después de revelar",
            }),
        ).toBeNull();
        const h = decodeState(localStorage.getItem(storageKey)).records[
            episode.id
        ];
        expect(h.revisions).toHaveLength(1);
        expect(h.revealedAtDeviceUtc).not.toBeNull();
    });
    it("restores the committed selection after a reload without exposing results automatically", async () => {
        const view = render(<BlindReview />);
        await commitNone();
        const original = localStorage.getItem(storageKey);
        view.unmount();
        const loader = vi.spyOn(data, "loadRevealedReport");
        render(<BlindReview />);
        expect(
            screen.getByRole("button", { name: "Revelar algoritmos" }),
        ).toBeTruthy();
        expect(loader).not.toHaveBeenCalled();
        expect(localStorage.getItem(storageKey)).toBe(original);
        expect(screen.getByText(/Original: NoReference/)).toBeTruthy();
    });
    it("keeps post-reveal corrections separate from the original independent judgment", async () => {
        render(<BlindReview />);
        await commitNone();
        await userEvent.click(
            screen.getByRole("button", { name: "Revelar algoritmos" }),
        );
        await screen.findByRole("region", {
            name: "Comparación después de revelar",
        });
        await userEvent.click(
            screen.getByRole("button", {
                name: "Registrar corrección (conservar original)",
            }),
        );
        await userEvent.click(
            screen.getByRole("button", { name: pickName(2) }),
        );
        await userEvent.click(
            screen.getByRole("button", {
                name: /Confirmar selección · corrección/,
            }),
        );
        const h = decodeState(localStorage.getItem(storageKey)).records[
            episode.id
        ];
        expect(h.revisions[0].kind).toBe("NoReference");
        expect(h.revisions[1].kind).toBe("Reference");
        expect(h.revisions[1].classification).toBe("PostRevealCorrection");
        expect(independentLabel(h).kind).toBe("NoReference");
        expect(screen.getByText(/Comparación con revisión 1/)).toBeTruthy();
    });
    it.each(["Ambiguo / contexto insuficiente", "Omitir para revisar luego"])(
        "commits %s without treating it as a negative reference",
        async (option) => {
            const { container } = render(<BlindReview />);
            await reviewer();
            await userEvent.click(screen.getByRole("radio", { name: option }));
            await userEvent.click(
                screen.getByRole("button", { name: "Confirmar selección" }),
            );
            const h = decodeState(localStorage.getItem(storageKey)).records[
                episode.id
            ];
            expect(h.revisions[0].price).toBeNull();
            expect(h.revisions[0].selectedOpens).toEqual([]);
            expect(container.querySelectorAll("[data-overlay]")).toHaveLength(
                0,
            );
            await userEvent.click(
                screen.getByRole("button", { name: "Siguiente episodio →" }),
            );
            expect(screen.getByLabelText(/Episodio 2/)).toBeTruthy();
            expect(
                screen.queryByRole("button", { name: "Revelar algoritmos" }),
            ).toBeNull();
            expect(
                screen.getByRole("textbox", { name: "Revisor local" }),
            ).toHaveProperty("value", "UI synthetic test reviewer");
        },
    );
    it("extracts High when the reviewer selects the descending episode", async () => {
        render(<BlindReview />);
        await reviewer();
        const down = data.catalog.episodes.find(
            (e) =>
                e.partition === "Development" && e.direction === "Descending",
        )!;
        await userEvent.selectOptions(
            screen.getByLabelText(/Episodio 1/),
            down.id,
        );
        const bar = data.charts[down.id].evidenceCandles[1];
        await userEvent.click(
            screen.getByRole("button", {
                name: `Inspeccionar vela ${bar.openTimeUtc.slice(0, 10)} ${bar.openTimeUtc.slice(11, 19)} UTC`,
            }),
        );
        expect(screen.getByText(`High ${bar.high} · 1 vela(s)`)).toBeTruthy();
        await userEvent.click(
            screen.getByRole("button", { name: "Confirmar selección" }),
        );
        expect(
            decodeState(localStorage.getItem(storageKey)).records[down.id]
                .revisions[0].price,
        ).toBe(bar.high);
    });
    it("allows a separate independent confirmation pick and keyboard reference selection", async () => {
        const { container } = render(<BlindReview />);
        await reviewer();
        screen.getByRole("button", { name: pickName(1) }).focus();
        await userEvent.keyboard("{Enter}");
        await userEvent.click(
            screen.getByRole("button", {
                name: "Marcar confirmación independiente (opcional)",
            }),
        );
        await userEvent.click(
            screen.getByRole("button", { name: pickName(5) }),
        );
        expect(
            container
                .querySelector("[data-human-confirmation]")
                ?.getAttribute("data-human-confirmation"),
        ).toBe(bars[5].openTimeUtc);
        await userEvent.click(
            screen.getByRole("button", { name: "Confirmar selección" }),
        );
        expect(
            decodeState(localStorage.getItem(storageKey)).records[episode.id]
                .revisions[0].confirmationCloseUtc,
        ).toBe(bars[5].closeTimeUtc);
    });
    it("keeps the accepted V2 episode previously exposed and held-out inaccessible", async () => {
        render(<BlindReview />);
        await userEvent.selectOptions(
            screen.getByLabelText("Partición"),
            "PreviouslyExposed",
        );
        expect(
            screen.getByText(/PreviouslyExposed: no es una observación ciega/),
        ).toBeTruthy();
        expect(
            screen.getByRole("checkbox", { name: /Ya conocía/ }),
        ).toHaveProperty("disabled", true);
        expect(
            screen.queryByRole("option", { name: /HeldOut|Reservado/ }),
        ).toBeNull();
        expect(
            screen.getByText(
                /Dos episodios finales reservados fuera del frontend/,
            ),
        ).toBeTruthy();
        await userEvent.selectOptions(
            screen.getByLabelText("Partición"),
            "SoftwareAcceptance",
        );
        expect(
            screen.getByText(
                /Fixture sintético, sin actividad de mercado auténtica/,
            ),
        ).toBeTruthy();
    });
    it("fails closed on persistence failure with no reveal bypass", async () => {
        render(<BlindReview />);
        await reviewer();
        await userEvent.click(
            screen.getByRole("radio", { name: "Sin referencia válida" }),
        );
        vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
            throw new Error("QuotaExceeded test");
        });
        await userEvent.click(
            screen.getByRole("button", { name: "Confirmar selección" }),
        );
        expect(screen.getByRole("alert").textContent).toContain(
            "QuotaExceeded",
        );
        expect(
            screen.queryByRole("button", { name: "Revelar algoritmos" }),
        ).toBeNull();
        expect(localStorage.getItem(storageKey)).toBeNull();
    });
    it("refuses corrupt local storage without silently clearing the original bytes", () => {
        localStorage.setItem(storageKey, "{corrupt-test-input");
        render(<BlindReview />);
        expect(screen.getByRole("alert").textContent).toContain("ilegible");
        expect(
            screen.queryByRole("button", { name: "Confirmar selección" }),
        ).toBeNull();
        expect(localStorage.getItem(storageKey)).toBe("{corrupt-test-input");
    });
    it("keeps chart scale independent from other episodes or future calendar groups", () => {
        const { container, rerender } = render(<BlindReview />);
        const originalY = container
            .querySelector('[data-testid="wick"]')
            ?.getAttribute("y1");
        const other = data.catalog.episodes.find(
            (e) => e.partition === "Validation",
        )!;
        const bar = data.charts[other.id].evidenceCandles[0];
        const originalHigh = bar.high;
        try {
            bar.high = 999999999;
            rerender(<BlindReview />);
            expect(
                container
                    .querySelector('[data-testid="wick"]')
                    ?.getAttribute("y1"),
            ).toBe(originalY);
        } finally {
            bar.high = originalHigh;
        }
    });
    it("exports committed labels through a local JSON download", async () => {
        const createDescriptor = Object.getOwnPropertyDescriptor(
            URL,
            "createObjectURL",
        );
        const revokeDescriptor = Object.getOwnPropertyDescriptor(
            URL,
            "revokeObjectURL",
        );
        const created: Blob[] = [];
        Object.defineProperty(URL, "createObjectURL", {
            configurable: true,
            value: (blob: Blob) => {
                created.push(blob);
                return "blob:synthetic-test-export";
            },
        });
        Object.defineProperty(URL, "revokeObjectURL", {
            configurable: true,
            value: vi.fn(),
        });
        const click = vi
            .spyOn(HTMLAnchorElement.prototype, "click")
            .mockImplementation(() => {});
        try {
            render(<BlindReview />);
            await commitNone();
            await userEvent.click(
                screen.getByRole("button", {
                    name: "Exportar etiquetas e historial",
                }),
            );
            expect(created).toHaveLength(1);
            expect(created[0].type).toBe("application/json");
            expect(click).toHaveBeenCalledTimes(1);
            expect(
                decodeState(localStorage.getItem(storageKey)).records[
                    episode.id
                ].revisions,
            ).toHaveLength(1);
        } finally {
            if (createDescriptor)
                Object.defineProperty(URL, "createObjectURL", createDescriptor);
            else Reflect.deleteProperty(URL, "createObjectURL");
            if (revokeDescriptor)
                Object.defineProperty(URL, "revokeObjectURL", revokeDescriptor);
            else Reflect.deleteProperty(URL, "revokeObjectURL");
        }
    });
    it("opens the dedicated route with no API or broker calls", async () => {
        window.location.hash = "#strategy-lab-blind";
        const fetcher = vi.fn();
        vi.stubGlobal("fetch", fetcher);
        render(<App />);
        expect(
            screen.getByRole("heading", {
                name: "Seleccionar → Confirmar → Revelar",
            }),
        ).toBeTruthy();
        await waitFor(() => expect(fetcher).not.toHaveBeenCalled());
    });
});
