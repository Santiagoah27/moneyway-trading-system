import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { PreparationReceiptBindingPanel } from "./PreparationReceiptBindingPanel";
import type { PreparationView } from "./ProspectivePreparationPanel";

const preparation = {
    record: {
        recordId: "prep-id",
        revision: 2,
        evidence: { sessionKey: "session" },
    },
    revisionHash: "a".repeat(64),
} as PreparationView;
const cut = { id: "nov25-5094", sequence: 5094, reportSha256: "b".repeat(64) };
const entry = {
    associationSha256: "c".repeat(64),
    artifacts: [],
    binding: {
        receiptCutId: cut.id,
        cutSequence: cut.sequence,
        receiptCutReportSha256: cut.reportSha256,
        cutReceiptSha256: "d".repeat(64),
        preparationRevisionSha256: preparation.revisionHash,
        preparationSourceReference: "note:documentary review",
        source: {
            company: "OANDA",
            server: "OANDA_Global-Demo-1",
            nativeSymbol: "US100",
        },
        receiptHostClockCandidate: "2026-11-25T14:59:00Z",
        preparationReceivedAtUtc: "2026-11-26T16:00:00Z",
        preparationRegisteredAtUtc: "2026-11-26T16:00:01Z",
        preparationClockSource: "ordinary clock",
        humanReportedObservationTime: "08:05 operator assertion",
        preparationOriginalTime: null,
        preparationOriginalClockDomain: null,
        associationCreatedAtUtc: "2026-11-26T16:00:02Z",
        matchStatus: "Partial_UnresolvedNativeReferences",
        wallClockOrdering: "PreparationRegistrationAfterCutLabel",
        preparationAvailabilityAtCut: "Unresolved",
        standardEvaluation: "NotEvaluated",
        diagnostics: ["OriginalObservationTimingUnreported"],
        references: [],
    },
};

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

it("pins the loaded revision and report, leaving both rules unevaluated", async () => {
    const fetch = vi.fn(async (_url: string, request?: RequestInit) =>
        Response.json(
            request?.method === "POST"
                ? entry
                : { bindings: [], availableCuts: [cut] },
        ),
    );
    vi.stubGlobal("fetch", fetch);
    render(
        <PreparationReceiptBindingPanel
            apiBaseUrl=""
            preparation={preparation}
        />,
    );
    expect(fetch).not.toHaveBeenCalled();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Vincular esta revisión a un corte MT5",
        }),
    );
    fireEvent.change(
        await screen.findByRole("combobox", {
            name: "Corte registrado de la sesión",
        }),
        { target: { value: cut.id } },
    );
    fireEvent.click(
        screen.getByRole("button", { name: "Guardar vínculo documental" }),
    );
    expect(
        await screen.findByText(/NQ-TIME-003 y NQ-H4-001: NotEvaluated/),
    ).toBeTruthy();
    expect(
        screen.getByText(/Disponibilidad humana al corte: Unresolved/),
    ).toBeTruthy();
    expect(screen.getByText("08:05 operator assertion")).toBeTruthy();
    fireEvent.click(
        screen.getByText("Fuentes, hashes y correspondencia de velas"),
    );
    expect(
        screen.getByText(/OriginalObservationTimingUnreported/),
    ).toBeTruthy();
    const post = fetch.mock.calls[1];
    expect(post[0]).toContain("/preparation/prep-id/revisions/2/receipt-cuts");
    expect(JSON.parse(post[1]?.body as string)).toEqual({
        preparationRevisionSha256: preparation.revisionHash,
        receiptCutId: cut.id,
        receiptCutReportSha256: cut.reportSha256,
    });
    expect(
        screen.queryByRole("button", { name: /aprobar|señal|intento/i }),
    ).toBeNull();
});

it("reopens saved documentary links without requiring an active registration", async () => {
    vi.stubGlobal(
        "fetch",
        vi.fn(async () =>
            Response.json({ availableCuts: [], bindings: [entry] }),
        ),
    );
    render(
        <PreparationReceiptBindingPanel
            apiBaseUrl=""
            preparation={preparation}
        />,
    );
    fireEvent.click(
        screen.getByRole("button", {
            name: "Vincular esta revisión a un corte MT5",
        }),
    );
    expect(
        await screen.findByRole("article", {
            name: "Preparación vinculada al corte 5094",
        }),
    ).toBeTruthy();
    expect(
        screen.getByText(
            "No hay cortes registrados para esta sesión Standard.",
        ),
    ).toBeTruthy();
    expect(
        screen.queryByRole("button", { name: "Guardar vínculo documental" }),
    ).toBeNull();
});
