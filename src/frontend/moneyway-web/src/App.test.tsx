import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "./App";

const safeStatus = {
    application: "MoneyWay Trading System",
    status: "bootstrap",
    executionMode: "disabled",
    realMoneyTradingEnabled: false,
};

function successfulFetch() {
    return vi
        .fn()
        .mockResolvedValue({ ok: true, json: async () => safeStatus });
}

afterEach(() => vi.unstubAllGlobals());

describe("App", () => {
    it("shows the product heading and purpose", () => {
        vi.stubGlobal("fetch", successfulFetch());
        render(<App />);
        expect(screen.getByRole("heading", { level: 1 }).textContent).toBe(
            "MoneyWay Trading System",
        );
        expect(
            screen.getByText(/deterministic strategy evaluation/i),
        ).toBeTruthy();
    });

    it("shows the safety notice", () => {
        vi.stubGlobal("fetch", successfulFetch());
        render(<App />);
        expect(screen.getByLabelText("Safety notice").textContent).toMatch(
            /real-money trading is prohibited/i,
        );
    });

    it("keeps Forex and Nasdaq visibly separate", () => {
        vi.stubGlobal("fetch", successfulFetch());
        render(<App />);
        expect(
            screen.getByRole("heading", { name: "MoneyWay Forex" }),
        ).toBeTruthy();
        expect(
            screen.getByRole("heading", { name: "MoneyWay Nasdaq" }),
        ).toBeTruthy();
    });

    it("starts with a loading connection state", () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise(() => undefined)),
        );
        render(<App />);
        expect(screen.getByRole("status").textContent).toBe("API loading");
    });

    it("renders the safe contract after connecting", async () => {
        vi.stubGlobal("fetch", successfulFetch());
        render(<App />);
        await waitFor(() =>
            expect(screen.getByRole("status").textContent).toBe(
                "API connected",
            ),
        );
        expect(screen.getAllByText("disabled")).toHaveLength(2);
    });

    it("requests the configured system status endpoint", async () => {
        const fetchMock = successfulFetch();
        vi.stubGlobal("fetch", fetchMock);
        render(<App />);
        await waitFor(() => expect(fetchMock).toHaveBeenCalled());
        expect(fetchMock.mock.calls[0]?.[0]).toBe(
            "http://localhost:5080/api/system/status",
        );
    });

    it("shows a disconnected fallback when the API fails", async () => {
        vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("offline")));
        render(<App />);
        await waitFor(() =>
            expect(screen.getByRole("status").textContent).toBe(
                "API disconnected",
            ),
        );
        expect(screen.getAllByText("disabled")).toHaveLength(2);
    });

    it("aborts the request when unmounted", () => {
        const abortSpy = vi.spyOn(AbortController.prototype, "abort");
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise(() => undefined)),
        );
        const view = render(<App />);
        view.unmount();
        expect(abortSpy).toHaveBeenCalledOnce();
        abortSpy.mockRestore();
    });
});
