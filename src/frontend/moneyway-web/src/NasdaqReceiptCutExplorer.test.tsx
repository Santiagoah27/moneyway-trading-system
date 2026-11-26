import {
    cleanup,
    fireEvent,
    render,
    screen,
    within,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { NasdaqReceiptCutExplorer, type Cut } from "./NasdaqReceiptCutExplorer";

const route = "/api/nasdaq/historical-demo/receipt-cuts";
const entries = [
    { id: "nov25-5094", sequence: 5094, reportSha256: "a".repeat(64) },
    { id: "nov25-5178", sequence: 5178, reportSha256: "b".repeat(64) },
];
const native = { raw: 1795554000, display: "2026.11.24 21:00:00" };
const sample = {
    timeframe: "H1",
    openTimeNative: native,
    localReceiptClockLabel: "2026-11-25T11:56:24.086372Z",
    laterHistoricalRevisions: 0,
    firstUsableHistorical: {
        usable: true,
        sourcePosition: 1,
        receipt: {
            sequence: 4911,
            recordSha256: "d".repeat(64),
            probeSha256: "e".repeat(64),
        },
        values: { open: "10", high: "15", low: "9", close: "12" },
    },
};

function response(index: number) {
    const entry = entries[index];
    return {
        id: entry.id,
        reportSha256: entry.reportSha256,
        providerId: "oanda-global-markets-mt5-demo",
        diagnostic: {
            expectedSource: {
                company: "OANDA Global Markets Limited",
                server: "OANDA_Global-Demo-1",
                nativeSymbol: "US100",
                demo: true,
            },
            directoryScope: "local",
            anchor: { sequence: 4910, recordSha256: "c".repeat(64) },
            cutReceipt: { sequence: entry.sequence, sha256: "c".repeat(64) },
            cutLocalReceiptClockLabel: "2026-11-25T14:59:03.479994Z",
            integrity: "Validated",
            observationContinuity: "DiscontinuityOrAcquisitionIssueObserved",
            standardEvaluation: "NotEvaluated",
            standardEvaluationReason:
                "NoAdmissibleClosedUtcSeriesOrVerifiedTemporalBasisForStandard",
            nativeToUtcMapping: "Unresolved",
            providerFinalization: "Unresolved",
            marketAvailabilityUtc: "Unresolved",
            humanReviewStatus: "NoVerifiedReviewBoundToReceiptCut",
            canonicalReady: false,
            canonicalAdmissionPermitted: false,
            operationalSignal: false,
            samples:
                index === 0
                    ? [sample]
                    : [sample, { ...sample, timeframe: "M1" }],
            exclusions: [
                {
                    timeframe: "H4",
                    openTimeNative: native,
                    reason: "position_zero_only",
                },
            ],
            nativeGaps: [
                {
                    timeframe: "H1",
                    earlierOpen: native,
                    laterOpen: native,
                    observedDeltaSeconds: 7200,
                },
            ],
            timeframes: [],
            notices: [],
            diagnostics: [],
            excludedHumanReviews: [],
        },
    };
}

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

it("opens the read-only explorer, shows native provenance, and compares verified cuts", async () => {
    const fetch = vi.fn(async (url: string) =>
        Response.json(
            url === route
                ? entries
                : response(url.endsWith(entries[0].id) ? 0 : 1),
        ),
    );
    vi.stubGlobal("fetch", fetch);
    render(<NasdaqReceiptCutExplorer apiBaseUrl="" />);
    expect(fetch).not.toHaveBeenCalled();
    fireEvent.click(
        screen.getByRole("button", { name: "Explorar cortes de recibos" }),
    );
    const cut = await screen.findByRole("article", { name: "Corte 5094" });
    expect(within(cut).getByText(/OANDA_Global-Demo-1 · US100/)).toBeTruthy();
    expect(within(cut).getByText("NotEvaluated")).toBeTruthy();
    expect(
        within(cut).getByText("CanonicalAdmissionPermitted=false"),
    ).toBeTruthy();
    expect(within(cut).getByText(/Recibo 4911.*nativa/)).toBeTruthy();
    expect(
        screen.getByText(/coordenadas nativas MT5 no son UTC autenticada/i),
    ).toBeTruthy();
    fireEvent.click(
        within(cut).getByText("Discontinuidades y exclusiones (1)"),
    );
    expect(within(cut).getByText(/7200 s nativos/)).toBeTruthy();
    expect(within(cut).getByText(/Solo observada en posición 0/)).toBeTruthy();
    fireEvent.click(within(cut).getByText("Inspeccionar muestras nativas"));
    fireEvent.change(
        within(cut).getByRole("combobox", { name: "Temporalidad" }),
        { target: { value: "H1" } },
    );
    expect(within(cut).getByText("10 / 15 / 9 / 12")).toBeTruthy();
    fireEvent.change(screen.getByRole("combobox", { name: "Comparar con" }), {
        target: { value: entries[1].id },
    });
    expect(
        await screen.findByRole("heading", {
            name: "Comparación de cortes 5094 y 5178",
        }),
    ).toBeTruthy();
    expect(
        screen.getByText(
            "1 primeras observaciones conservadas. 0 ausentes o distintas en el corte posterior.",
        ),
    ).toBeTruthy();
    expect(
        screen.queryByRole("button", { name: /aprobar|intento|señal/i }),
    ).toBeNull();
});

it("shows verification failures and clears the previous cut when selection changes", async () => {
    vi.stubGlobal(
        "fetch",
        vi.fn(async (url: string) =>
            url === route
                ? Response.json(entries)
                : url.endsWith(entries[0].id)
                  ? Response.json(response(0))
                  : Response.json({}, { status: 422 }),
        ),
    );
    render(<NasdaqReceiptCutExplorer apiBaseUrl="" />);
    fireEvent.click(
        screen.getByRole("button", { name: "Explorar cortes de recibos" }),
    );
    await screen.findByRole("article", { name: "Corte 5094" });
    fireEvent.change(
        screen.getByRole("combobox", { name: "Corte de recibos" }),
        { target: { value: entries[1].id } },
    );
    expect(screen.queryByRole("article", { name: "Corte 5094" })).toBeNull();
    expect((await screen.findByRole("alert")).textContent).toContain(
        "no pasó la verificación",
    );
    expect(screen.queryByRole("article", { name: "Corte 5178" })).toBeNull();
});

const realApi = process.env.MONEYWAY_NATIVE_CHART_API;
it.runIf(Boolean(realApi))(
    "renders both real Development cuts over HTTP without mixing their samples",
    async () => {
        const originalFetch = globalThis.fetch.bind(globalThis);
        const requests = vi.fn(originalFetch);
        vi.stubGlobal("fetch", requests);
        const before: Cut = await (
            await originalFetch(`${realApi}${route}/nov25_5094`)
        ).json();
        const after: Cut = await (
            await originalFetch(`${realApi}${route}/nov25_5178`)
        ).json();
        expect(before.diagnostic.samples).toHaveLength(283);
        expect(after.diagnostic.samples).toHaveLength(393);
        const known = new Set(
            before.diagnostic.samples
                .filter((sample) => sample.timeframe === "M1")
                .map((sample) => sample.openTimeNative.raw),
        );
        const newOpen = after.diagnostic.samples.find(
            (sample) =>
                sample.timeframe === "M1" &&
                !known.has(sample.openTimeNative.raw),
        )!.openTimeNative.raw;

        render(<NasdaqReceiptCutExplorer apiBaseUrl={realApi!} />);
        fireEvent.click(
            screen.getByRole("button", { name: "Explorar cortes de recibos" }),
        );
        const first = await screen.findByRole("article", {
            name: "Corte 5094",
        });
        const allCharts = (
            article: HTMLElement,
            expected: Record<string, number>,
        ) => {
            const counts: Record<string, number> = {};
            const m1: string[] = [];
            for (const [frame, count] of Object.entries(expected)) {
                fireEvent.change(
                    within(article).getByRole("combobox", {
                        name: "Temporalidad",
                    }),
                    { target: { value: frame } },
                );
                const chart = within(article).getByRole("region", {
                    name: new RegExp(`Gráfico nativo ${frame}`),
                });
                expect(
                    within(chart).getByText(
                        new RegExp(`de ${count} velas ${frame}`),
                    ),
                ).toBeTruthy();
                if (frame === "H1")
                    expect(
                        within(chart).getByText(
                            /7200 s nativos entre aperturas/,
                        ),
                    ).toBeTruthy();
                const ids: string[] = [];
                const next = within(chart).getByRole<HTMLButtonElement>(
                    "button",
                    { name: "Velas siguientes" },
                );
                do {
                    ids.push(
                        ...within(chart)
                            .getAllByRole("button", {
                                name: new RegExp(`^Vela ${frame} `),
                            })
                            .map((button) =>
                                button.getAttribute("data-native-open")!,
                            ),
                    );
                    if (next.disabled) break;
                    fireEvent.click(next);
                } while (ids.length < count);
                expect(ids).toHaveLength(count);
                expect(new Set(ids).size).toBe(count);
                counts[frame] = ids.length;
                if (frame === "M1") m1.push(...ids);
            }
            expect(counts).toEqual(expected);
            return m1;
        };
        expect(
            allCharts(first, { H4: 16, H1: 18, M5: 51, M1: 198 }),
        ).not.toContain(String(newOpen));
        fireEvent.change(
            screen.getByRole("combobox", { name: "Corte de recibos" }),
            { target: { value: "nov25_5178" } },
        );
        const second = await screen.findByRole("article", {
            name: "Corte 5178",
        });
        expect(
            screen.queryByRole("article", { name: "Corte 5094" }),
        ).toBeNull();
        expect(
            allCharts(second, { H4: 16, H1: 20, M5: 69, M1: 288 }),
        ).toContain(String(newOpen));
        expect(
            requests.mock.calls.every(
                ([, init]) => !init?.method || init.method === "GET",
            ),
        ).toBeTruthy();
    },
    15000,
);
