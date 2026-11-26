import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { ProspectivePreparationPanel } from "./ProspectivePreparationPanel";
import type { PreparationDraft } from "./ProspectivePreparationPanel";
import type { SupervisedSessionState } from "./SupervisedSessionPanel";

afterEach(() => vi.unstubAllGlobals());
const key = "a".repeat(64);
const id = "11111111-1111-4111-8111-111111111111";
const session = {
    sessionKey: key,
    sessionId: "synthetic:prospective",
    path: "Standard",
    status: "Open_NoTrade",
    terminal: false,
    identity: {
        tradingDay: "2026-11-18",
        providerId: { value: "synthetic-provider" },
        symbol: { value: "US100" },
    },
} as SupervisedSessionState;
function view(evidence: PreparationDraft, revision = 1) {
    return {
        record: {
            recordId: id,
            revision,
            submissionSequence: revision,
            evidence,
            receivedAtUtc: `2026-11-18T13:0${revision}:00Z`,
            registeredAtUtc: `2026-11-18T13:0${revision}:01Z`,
            receiptClockSource:
                "synthetic UI sample: ordinary unauthenticated clock",
            submissionContext: "CurrentBogotaDate_ReportOnly",
            identity: {
                strategyId: { value: "moneyway-nasdaq" },
                strategyVersion: { value: "nasdaq-0.1.0-draft" },
            },
        },
        revisionHash: (revision === 1 ? "a" : "b").repeat(64),
        documentaryStatus: "PartialPreparation",
        missingDocumentaryInputs: [
            "HumanStructuralReferenceMissing",
            "OriginalArtifactReferenceMissing",
        ],
        admissionBlockers: ["NoTrustedTemporalAdmissionFromApplicationReceipt"],
        selectorContract: "NasdaqPreparationCompletionObservationSeries",
        h4SelectorContract: "NasdaqHumanH4ContextObservationSelector",
        selectorResult:
            "NotEvaluated_AdmissibleSourceAvailabilityAndCanonicalMarketContextMissing",
        preparationRuleResult: null,
        strategyVerdict: null,
        admissibleCompletionObservationCount: 0,
        admissionResult: "DataUnavailable",
        canonicalAdmissionPermitted: false,
        receiptMeaning: "synthetic service-receipt sample, not availability",
        originalSourceIndependentlyVerified: false,
        utcAuthenticated: false,
    };
}
function fill() {
    for (const [label, value] of [
        ["Preparación: revisor declarante", "synthetic:operator"],
        [
            "Preparación: fuente general (note: o https://)",
            "note:synthetic original observation",
        ],
        [
            "Preparación: motivo de registro/corrección",
            "synthetic initial report",
        ],
    ])
        fireEvent.change(screen.getByLabelText(label), { target: { value } });
}
function submit() {
    fireEvent.submit(
        screen
            .getByRole("button", {
                name: /Guardar preparación parcial|Guardar nueva revisión de preparación/,
            })
            .closest("form")!,
    );
}
it("captures partial H4 and reference claims without inventing a native ID, price, receipt or rule result", async () => {
    const fetcher = vi.fn().mockImplementation(async (_url, options) => ({
        ok: true,
        json: async () => view(JSON.parse(options.body).evidence),
    }));
    vi.stubGlobal("fetch", fetcher);
    render(<ProspectivePreparationPanel apiBaseUrl="" session={session} />);
    expect(fetcher).not.toHaveBeenCalled();
    fill();
    fireEvent.change(
        screen.getByLabelText("Preparación: dirección H4 humana"),
        { target: { value: "Sell" } },
    );
    fireEvent.change(screen.getByLabelText("Preparación: contexto H4 humano"), {
        target: { value: "Fakeout" },
    });
    fireEvent.click(
        screen.getByRole("button", {
            name: "Añadir referencia humana de preparación",
        }),
    );
    fireEvent.change(screen.getByLabelText("Referencia 1: rol humano"), {
        target: { value: "LH" },
    });
    submit();
    await screen.findByRole("region", { name: "Preparación persistida" });
    const d = JSON.parse(fetcher.mock.calls[0][1].body).evidence;
    expect(d.direction).toBe("Sell");
    expect(d.references[0].localReferenceId).toMatch(/^documentary-/);
    expect(d.references[0].nativeReferenceId).toBeNull();
    expect(d.references[0].price).toBeNull();
    expect(d.references[0].sourceProviderId).toBeNull();
    expect(d.references[0].originalSymbol).toBeNull();
    expect(d.receivedAtUtc).toBeUndefined();
    expect(d.observedAtUtc).toBeUndefined();
    expect(d.canonicalAdmissionApproved).toBeUndefined();
    expect(screen.getByText(/HumanStructuralReferenceMissing/)).not.toBeNull();
    expect(screen.getByText(/Resultado de regla: No evaluado/)).not.toBeNull();
    expect(screen.getByText(/2026-11-18T13:01:00Z/)).not.toBeNull();
});
it("submits the exact native source on the individual preparation reference", async () => {
    const fetcher = vi.fn().mockImplementation(async (_url, options) => ({
        ok: true,
        json: async () => view(JSON.parse(options.body).evidence),
    }));
    vi.stubGlobal("fetch", fetcher);
    render(<ProspectivePreparationPanel apiBaseUrl="" session={session} />);
    fill();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Añadir referencia humana de preparación",
        }),
    );
    for (const [label, value] of [
        ["proveedor original (opcional)", "synthetic-provider"],
        ["instrumento original (opcional)", "US100"],
        ["timeframe original", "4h"],
        ["timestamp original (opcional)", "2026.11.18 12:00:00"],
        ["reloj original (opcional)", "MT5Native"],
        ["precisión", "ExactNativeRecord"],
        ["fuente (opcional, note: o https://)", "note:synthetic reference"],
    ]) {
        fireEvent.change(screen.getByLabelText(`Referencia 1: ${label}`), {
            target: { value },
        });
    }
    submit();
    await screen.findByRole("region", { name: "Preparación persistida" });

    const draft = JSON.parse(fetcher.mock.calls[0][1].body).evidence;
    expect(draft.sourceProviderId).toBeNull();
    expect(draft.originalSymbol).toBeNull();
    expect(draft.references[0]).toMatchObject({
        sourceProviderId: "synthetic-provider",
        originalSymbol: "US100",
        timeframe: "4h",
        originalTime: "2026.11.18 12:00:00",
        originalClockDomain: "MT5Native",
        timePrecision: "ExactNativeRecord",
        sourceReference: "note:synthetic reference",
    });
});
it("revises and reloads immutable originals with an expected hash and reported time separate from receipt", async () => {
    const history: ReturnType<typeof view>[] = [];
    const fetcher = vi.fn().mockImplementation(async (url: string, options) => {
        if (options) {
            const d = JSON.parse(options.body).evidence;
            const v = view(d, history.length + 1);
            history.push(v);
            return { ok: true, json: async () => v };
        }
        return {
            ok: true,
            json: async () =>
                url.endsWith("revision=1")
                    ? history[0]
                    : history[history.length - 1],
        };
    });
    vi.stubGlobal("fetch", fetcher);
    render(<ProspectivePreparationPanel apiBaseUrl="" session={session} />);
    fill();
    submit();
    await screen.findByRole("region", { name: "Preparación persistida" });
    fireEvent.change(
        screen.getByLabelText("Preparación: motivo de registro/corrección"),
        { target: { value: "synthetic retrospective correction" } },
    );
    fireEvent.change(
        screen.getByLabelText(
            "Preparación: hora de observación reportada por el operador (opcional)",
        ),
        { target: { value: "synthetic earlier time, no authenticated UTC" } },
    );
    submit();
    await waitFor(() =>
        expect(screen.getByText(/revisión 2 · secuencia/)).not.toBeNull(),
    );
    const body = JSON.parse(fetcher.mock.calls[1][1].body);
    expect(body.expectedRevision).toBe("a".repeat(64));
    expect(body.evidence.operatorReportedObservationTime).toContain(
        "no authenticated UTC",
    );
    fireEvent.click(
        screen.getByRole("button", { name: "Ver preparación original" }),
    );
    await waitFor(() =>
        expect(screen.getByText(/revisión 1 · secuencia/)).not.toBeNull(),
    );
    expect(
        screen.getByLabelText(
            "Preparación: hora de observación reportada por el operador (opcional)",
        ),
    ).toHaveProperty("value", "");
    fireEvent.change(
        screen.getByLabelText("Número de revisión de preparación"),
        { target: { value: "2" } },
    );
    fireEvent.click(
        screen.getByRole("button", { name: "Ver revisión de preparación" }),
    );
    await waitFor(() =>
        expect(screen.getByText(/revisión 2 · secuencia/)).not.toBeNull(),
    );
    expect(fetcher.mock.calls.at(-1)![0]).toMatch(/revision=2$/);
});
it("permits documentary corrections for terminal sessions while exposing limitations and no entry control", () => {
    render(
        <ProspectivePreparationPanel
            apiBaseUrl=""
            session={{ ...session, terminal: true }}
        />,
    );
    expect(screen.getByText(/TERMINAL: solo documentación/)).not.toBeNull();
    expect(
        screen.getByRole("button", { name: "Guardar preparación parcial" }),
    ).toHaveProperty("disabled", false);
    expect(
        screen.queryByRole("button", { name: /Registrar intento de entrada/ }),
    ).toBeNull();
    expect(
        screen.getByText(
            /Preparación documentada ≠ preparación canónicamente Passed/,
        ),
    ).not.toBeNull();
});
it("retains and retries the identical uncertain submission rather than issuing a second receipt record", async () => {
    const fetcher = vi
        .fn()
        .mockRejectedValueOnce(new Error("synthetic lost response"))
        .mockImplementation(async (_url, options) => ({
            ok: true,
            json: async () => view(JSON.parse(options.body).evidence),
        }));
    vi.stubGlobal("fetch", fetcher);
    render(<ProspectivePreparationPanel apiBaseUrl="" session={session} />);
    fill();
    submit();
    fireEvent.click(
        await screen.findByRole("button", {
            name: "Reintentar mismo envío de preparación",
        }),
    );
    await screen.findByRole("region", { name: "Preparación persistida" });
    expect(fetcher.mock.calls[1][1].body).toBe(fetcher.mock.calls[0][1].body);
});
