import { render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "./App";

const safeStatus = {
    application: "MoneyWay Trading System",
    status: "bootstrap",
    executionMode: "disabled",
    realMoneyTradingEnabled: false,
};

const definitions = [
    {
        strategyId: "moneyway-forex",
        version: "forex-0.1.0-draft",
        displayName: "MoneyWay Forex",
        specificationReference:
            "docs/strategies/forex/strategy-specification.md",
        ruleCount: 2,
        requiredRuleCount: 1,
        rules: [
            {
                ruleId: "FX-W-002",
                name: "Weekly direction",
                stage: "Weekly",
                sequence: 10,
                isRequired: true,
                definitionStatus: "HumanValidationRequired",
                description: "Determine weekly direction.",
                sourceReference: "docs/strategies/forex/rule-catalog.md",
            },
            {
                ruleId: "FX-H4-004",
                name: "Setup maturity",
                stage: "4H / Weekly",
                sequence: 20,
                isRequired: false,
                definitionStatus: "Candidate",
                description: "Record human review of setup maturity.",
                sourceReference: "docs/strategies/forex/rule-catalog.md",
            },
        ],
    },
];

function response(body: object, ok = true): Response {
    return new Response(JSON.stringify(body), {
        status: ok ? 200 : 503,
        headers: { "Content-Type": "application/json" },
    });
}

function endpointFetch(options?: {
    statusFails?: boolean;
    definitionsFail?: boolean;
}) {
    return vi.fn((input: RequestInfo | URL) => {
        const url = input.toString();
        if (url.endsWith("/api/system/status")) {
            return Promise.resolve(response(safeStatus, !options?.statusFails));
        }
        if (url.endsWith("/api/strategy-definitions")) {
            return Promise.resolve(
                response(definitions, !options?.definitionsFail),
            );
        }
        return Promise.resolve(response({}, false));
    });
}

afterEach(() => vi.unstubAllGlobals());

describe("App", () => {
    it("shows the product, loading states, and safety restrictions", () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise<Response>(() => undefined)),
        );
        render(<App />);

        expect(screen.getByRole("heading", { level: 1 }).textContent).toBe(
            "MoneyWay Trading System",
        );
        expect(
            screen.getByText(/deterministic strategy evaluation/i),
        ).toBeTruthy();
        expect(screen.getByText("Strategy definitions loading.")).toBeTruthy();
        expect(screen.getByText("API loading")).toBeTruthy();
        expect(
            screen.getByText("Execution mode").parentElement?.textContent,
        ).toContain("disabled");
        expect(
            screen.getByText("Real-money trading").parentElement?.textContent,
        ).toContain("disabled");
        expect(screen.getByLabelText("Safety notice").textContent).toContain(
            "Real-money trading is prohibited",
        );
    });

    it("renders Forex metadata and every rule from the API response", async () => {
        vi.stubGlobal("fetch", endpointFetch());
        render(<App />);

        const forex = await screen.findByRole("article", {
            name: "MoneyWay Forex",
        });
        expect(within(forex).getByText("moneyway-forex")).toBeTruthy();
        expect(within(forex).getByText("forex-0.1.0-draft")).toBeTruthy();
        expect(
            within(forex).getByText("Total rules").parentElement?.textContent,
        ).toContain("2");
        expect(
            within(forex).getByText("Required rules").parentElement
                ?.textContent,
        ).toContain("1");
        expect(within(forex).getByText("Weekly direction")).toBeTruthy();
        expect(within(forex).getByText("Setup maturity")).toBeTruthy();
        expect(within(forex).getByText("10")).toBeTruthy();
        expect(within(forex).getByText("Weekly")).toBeTruthy();
        expect(within(forex).getAllByText("Required")).toHaveLength(2);
        expect(within(forex).getByText("Optional")).toBeTruthy();
        expect(within(forex).getByText("HumanValidationRequired")).toBeTruthy();
        expect(within(forex).getByText("Candidate")).toBeTruthy();
        expect(
            within(forex).getByText("Determine weekly direction."),
        ).toBeTruthy();
        expect(
            within(forex).getAllByText("docs/strategies/forex/rule-catalog.md"),
        ).toHaveLength(2);
        expect(
            within(forex).getByText(/not the result of a market evaluation/i),
        ).toBeTruthy();
    });

    it("shows Nasdaq as documented but not registered without invented rules", async () => {
        vi.stubGlobal("fetch", endpointFetch());
        render(<App />);

        const nasdaq = await screen.findByRole("article", {
            name: "MoneyWay Nasdaq",
        });
        expect(
            within(nasdaq).getByText("Documentation available."),
        ).toBeTruthy();
        expect(
            within(nasdaq).getByText("Runtime definition: Not registered yet."),
        ).toBeTruthy();
        expect(within(nasdaq).queryByRole("table")).toBeNull();
    });

    it("keeps definitions visible when system status fails", async () => {
        vi.stubGlobal("fetch", endpointFetch({ statusFails: true }));
        render(<App />);

        expect(await screen.findByText("moneyway-forex")).toBeTruthy();
        await waitFor(() =>
            expect(screen.getByText("API disconnected")).toBeTruthy(),
        );
    });

    it("keeps connected system and safety states when definitions fail", async () => {
        vi.stubGlobal("fetch", endpointFetch({ definitionsFail: true }));
        render(<App />);

        expect(
            await screen.findByText("Strategy definitions unavailable."),
        ).toBeTruthy();
        await waitFor(() =>
            expect(screen.getByText("API connected")).toBeTruthy(),
        );
        expect(
            screen.getByText("Execution mode").parentElement?.textContent,
        ).toContain("disabled");
        expect(
            screen.getByText("Real-money trading").parentElement?.textContent,
        ).toContain("disabled");
    });

    it("requests each endpoint by URL rather than call order", async () => {
        const fetchMock = endpointFetch();
        vi.stubGlobal("fetch", fetchMock);
        render(<App />);

        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
        expect(fetchMock.mock.calls.map((call) => call[0].toString())).toEqual(
            expect.arrayContaining([
                "http://localhost:5080/api/system/status",
                "http://localhost:5080/api/strategy-definitions",
            ]),
        );
    });

    it("aborts both independent requests when unmounted", () => {
        const abortSpy = vi.spyOn(AbortController.prototype, "abort");
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise<Response>(() => undefined)),
        );
        const view = render(<App />);

        view.unmount();

        expect(abortSpy).toHaveBeenCalledTimes(2);
        abortSpy.mockRestore();
    });
});
