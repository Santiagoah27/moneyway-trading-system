import {
    cleanup,
    fireEvent,
    render,
    screen,
    within,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { NasdaqReceiptCutExplorer } from "./NasdaqReceiptCutExplorer";

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
