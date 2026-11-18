import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { SupervisedExecutionCapture } from "./SupervisedExecutionCapture";
import type { CaptureDraft } from "./SupervisedExecutionCapture";

const id = "11111111-1111-4111-8111-111111111111";
const hash = "a".repeat(64);
function view(evidence: CaptureDraft, revision = 1) {
    return {
        record: {
            recordId: id,
            revision,
            evidence,
            registeredAtUtc: "2026-11-17T20:00:00Z",
            submissionId: "synthetic:submission",
            previousRevision: revision > 1 ? hash : null,
        },
        revisionHash: hash,
        scope: "LocalOperatorAssertion_NotHistoricalReplayInput",
        integrity: "RegisteredPayloadSha256_NotOriginalArtifactHash",
        artifactAvailability: "NotInspected_ReferenceOnly",
        originalArtifactSha256: null,
        nativeExecutionVerified: false,
        entryExitRelationshipVerified: false,
        completeSupervisedTrade: false,
        economicOutcome: null,
        missingEvidence: [
            "ExitMissing",
            "NativeExecutionAndRelationshipUnverified",
        ],
        documentaryLifecycle: null,
    };
}
function fill() {
    for (const [name, value] of [
        ["Sesión de evidencia", "synthetic:session"],
        ["Fecha de sesión", "2026-09-08"],
        ["Plataforma original", "synthetic:platform"],
        ["Revisor / declarante", "synthetic:reviewer"],
        [
            "Fuente general (note: o https://)",
            "note:synthetic manual observation",
        ],
        ["Motivo de registro o corrección", "synthetic:initial registration"],
        ["Entrada: Fuente (note: o https://)", "note:synthetic opening row"],
    ])
        fireEvent.change(screen.getByLabelText(name), { target: { value } });
    fireEvent.change(screen.getByLabelText("Instrumento original"), {
        target: { value: "USTEC" },
    });
}
function submit() {
    fireEvent.submit(
        screen
            .getByRole("button", {
                name: /Guardar evidencia parcial|Guardar nueva revisión|Reintentar mismo envío/,
            })
            .closest("form")!,
    );
}
afterEach(() => vi.unstubAllGlobals());
it("adds a source-reported outcome to an existing revision and retrieves it without calculating or verifying PnL", async () => {
    let stored: ReturnType<typeof view>;
    const fetcher = vi.fn().mockImplementation(async (url: string, options) => {
        if (options) {
            const body = JSON.parse(options.body);
            stored = view(body.evidence, url.endsWith("/revisions") ? 2 : 1);
        }
        return new Response(JSON.stringify(stored), { status: 200 });
    });
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fill();
    submit();
    await screen.findByRole("region", { name: "Evidencia persistida" });
    fireEvent.click(
        screen.getByLabelText("Registrar resultado monetario reportado"),
    );
    fireEvent.change(
        screen.getByLabelText("Resultado: Fuente (note: o https://)"),
        { target: { value: "note:synthetic reported statement" } },
    );
    fireEvent.change(
        screen.getByLabelText(
            "Resultado: Importe original (opcional, decimal exacto)",
        ),
        { target: { value: "+125.00000000000000000000001" } },
    );
    fireEvent.change(
        screen.getByLabelText("Resultado: Moneda original (opcional)"),
        { target: { value: "USD" } },
    );
    fireEvent.change(
        screen.getByLabelText("Resultado: significado del importe"),
        { target: { value: "SignedResult" } },
    );
    fireEvent.change(screen.getByLabelText("Resultado: bruto/neto"), {
        target: { value: "Net" },
    });
    fireEvent.change(screen.getByLabelText("Motivo de registro o corrección"), {
        target: { value: "synthetic:add reported outcome" },
    });
    fireEvent.click(
        screen.getByRole("button", { name: "Añadir costo reportado" }),
    );
    fireEvent.change(screen.getByLabelText("Costo 1: Categoría original"), {
        target: { value: "synthetic:fee" },
    });
    fireEvent.change(
        screen.getByLabelText("Costo 1: Fuente (note: o https://)"),
        { target: { value: "note:synthetic fee statement" } },
    );
    fireEvent.change(screen.getByLabelText("Costo 1: Importe original"), {
        target: { value: "0.00" },
    });
    fireEvent.change(screen.getByLabelText("Costo 1: Moneda original"), {
        target: { value: "EUR" },
    });
    submit();
    const outcome = await screen.findByRole("region", {
        name: "Resultado monetario persistido",
    });
    expect(outcome.textContent).toContain("+125.00000000000000000000001");
    expect(outcome.textContent).toContain("USD");
    expect(outcome.textContent).toContain("0.00 EUR");
    expect(outcome.textContent).toContain("P&L calculado por MoneyWay");
    expect(outcome.textContent).toContain("Operación incompleta");
    const body = JSON.parse(
        fetcher.mock.calls.find((c) => String(c[0]).endsWith("/revisions"))![1]
            .body,
    );
    expect(body.evidence.reportedOutcome.executionRecordId).toBe(id);
    expect(body.evidence.reportedOutcome.executionEvidenceRevision).toBe(1);
    expect(body.evidence.reportedOutcome.executionEvidenceRevisionHash).toBe(
        hash,
    );
    expect(body.evidence.reportedOutcome.amount).toBe(
        "+125.00000000000000000000001",
    );
    expect(body.evidence.reportedOutcome.costs[0].amount).toBe("0.00");
    fireEvent.click(screen.getByRole("button", { name: "Recuperar por ID" }));
    await screen.findByText(/Registro persistido recuperado/);
    expect(
        (
            screen.getByLabelText(
                "Resultado: Importe original (opcional, decimal exacto)",
            ) as HTMLInputElement
        ).value,
    ).toBe("+125.00000000000000000000001");
});
it("preserves narrative-only outcome with absent amount, currency and costs", async () => {
    const fetcher = vi
        .fn()
        .mockImplementation(
            async (_url, options) =>
                new Response(
                    JSON.stringify(view(JSON.parse(options.body).evidence)),
                    { status: 200 },
                ),
        );
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fill();
    submit();
    await screen.findByRole("region", { name: "Evidencia persistida" });
    fireEvent.click(
        screen.getByLabelText("Registrar resultado monetario reportado"),
    );
    fireEvent.change(
        screen.getByLabelText("Resultado: Fuente (note: o https://)"),
        { target: { value: "note:synthetic narrative" } },
    );
    fireEvent.change(
        screen.getByLabelText(
            "Resultado: Narrativa original, sin inferencia numérica",
        ),
        { target: { value: "synthetic:protected stop around 1:1" } },
    );
    fireEvent.change(screen.getByLabelText("Motivo de registro o corrección"), {
        target: { value: "synthetic:narrative only" },
    });
    submit();
    const panel = await screen.findByRole("region", {
        name: "Resultado monetario persistido",
    });
    expect(panel.textContent).toContain("Importe original: No reportado");
    expect(panel.textContent).toContain("Moneda: Desconocida");
    expect(panel.textContent).toContain(
        "Costos desconocidos; no equivalen a cero",
    );
    expect(panel.textContent).toContain("synthetic:protected stop around 1:1");
    const d = JSON.parse(fetcher.mock.calls.at(-1)![1].body).evidence
        .reportedOutcome;
    expect(d.amount).toBeNull();
    expect(d.currency).toBeNull();
    expect(d.costs).toBeNull();
    expect(d.basis).toBe("Unspecified");
});
it("captures an entry-only record without inventing identifiers, prices or trusted availability", async () => {
    const fetcher = vi
        .fn()
        .mockImplementation(
            async (_url, options) =>
                new Response(
                    JSON.stringify(view(JSON.parse(options.body).evidence)),
                    { status: 201 },
                ),
        );
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="http://localhost:5080" />);
    fill();
    submit();
    const panel = await screen.findByRole("region", {
        name: "Evidencia persistida",
    });
    expect(panel.textContent).toContain(id);
    expect(panel.textContent).toContain("Verificación nativa: No");
    expect(panel.textContent).toContain("Operación completa: No");
    const request = JSON.parse(fetcher.mock.calls[0][1].body);
    expect(request.evidence.entry.price).toBeNull();
    expect(request.evidence.entry.dealId).toBeNull();
    expect(request.evidence.exit).toBeNull();
    expect(request.evidence).not.toHaveProperty("trusted");
    expect(request.evidence).not.toHaveProperty("historicalAvailableAtUtc");
    expect(fetcher.mock.calls[0][1].headers["X-MoneyWay-Local-Capture"]).toBe(
        "1",
    );
});
it("preserves decimal text, approximate exit and explicit human association", async () => {
    const fetcher = vi
        .fn()
        .mockImplementation(
            async (_url, options) =>
                new Response(
                    JSON.stringify(view(JSON.parse(options.body).evidence)),
                    { status: 201 },
                ),
        );
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fill();
    fireEvent.change(screen.getByLabelText("Entrada: precio observado"), {
        target: { value: "29604.00000000000000000000001" },
    });
    fireEvent.change(screen.getByLabelText("Entrada: unidad de precio"), {
        target: { value: "NativeInstrumentPrice" },
    });
    fireEvent.click(screen.getByLabelText("Registrar salida reportada"));
    fireEvent.change(
        screen.getByLabelText("Salida: Fuente (note: o https://)"),
        { target: { value: "note:synthetic approximate exit" } },
    );
    fireEvent.change(screen.getByLabelText("Salida: precisión"), {
        target: { value: "Approximate" },
    });
    fireEvent.change(screen.getByLabelText("Salida: Hora original mostrada"), {
        target: { value: "around 08:37" },
    });
    fireEvent.change(screen.getByLabelText("Asociación entrada/salida"), {
        target: { value: "HumanReported" },
    });
    fireEvent.change(screen.getByLabelText("Fuente explícita del vínculo"), {
        target: { value: "note:synthetic operator association" },
    });
    submit();
    await screen.findByRole("region", { name: "Evidencia persistida" });
    const d = JSON.parse(fetcher.mock.calls[0][1].body).evidence;
    expect(d.entry.price).toBe("29604.00000000000000000000001");
    expect(d.exit.symbol).toBeNull();
    expect(d.exit.timePrecision).toBe("Approximate");
    expect(d.association).toBe("HumanReported");
});
it("supports an exit-only partial record and shows API validation errors", async () => {
    const fetcher = vi.fn().mockResolvedValue(
        new Response(
            JSON.stringify({
                code: "InvalidEvidence",
                errors: {
                    evidence: ["Source reference must be note: or HTTPS"],
                },
            }),
            { status: 400 },
        ),
    );
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fill();
    fireEvent.click(screen.getByLabelText("Registrar entrada observada"));
    fireEvent.click(screen.getByLabelText("Registrar salida reportada"));
    fireEvent.change(
        screen.getByLabelText("Salida: Fuente (note: o https://)"),
        { target: { value: "file:///C:/unavailable" } },
    );
    submit();
    expect((await screen.findByRole("status")).textContent).toContain(
        "Source reference must be note: or HTTPS",
    );
    expect(JSON.parse(fetcher.mock.calls[0][1].body).evidence.entry).toBeNull();
    expect(
        screen.queryByRole("region", { name: "Evidencia persistida" }),
    ).toBeNull();
});
it("retries an uncertain save with the identical submission and payload", async () => {
    let attempt = 0;
    const fetcher = vi.fn().mockImplementation(async (_url, options) => {
        if (attempt++ === 0)
            throw new Error("synthetic:connection interrupted");
        return new Response(
            JSON.stringify(view(JSON.parse(options.body).evidence)),
            { status: 201 },
        );
    });
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fill();
    submit();
    await screen.findByRole("button", { name: "Reintentar mismo envío" });
    fireEvent.click(
        screen.getByRole("button", { name: "Reintentar mismo envío" }),
    );
    await screen.findByRole("region", { name: "Evidencia persistida" });
    expect(fetcher.mock.calls[0][1].body).toBe(fetcher.mock.calls[1][1].body);
});
it("retrieves saved evidence, adds a revision with optimistic identity, and lists session records", async () => {
    let stored: ReturnType<typeof view>;
    const fetcher = vi.fn().mockImplementation(async (url: string, options) => {
        if (options) {
            const body = JSON.parse(options.body);
            stored = view(body.evidence, url.endsWith("/revisions") ? 2 : 1);
            return new Response(JSON.stringify(stored), { status: 200 });
        }
        return new Response(
            JSON.stringify(url.includes("?sessionId=") ? [stored] : stored),
            { status: 200 },
        );
    });
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fill();
    submit();
    await screen.findByRole("region", { name: "Evidencia persistida" });
    fireEvent.click(screen.getByRole("button", { name: "Recuperar por ID" }));
    await screen.findByText(/Registro persistido recuperado/);
    fireEvent.change(screen.getByLabelText("Motivo de registro o corrección"), {
        target: { value: "synthetic:add source correction" },
    });
    submit();
    await waitFor(() =>
        expect(
            screen.getByRole("region", { name: "Evidencia persistida" })
                .textContent,
        ).toContain("Revisión: 2"),
    );
    const revision = fetcher.mock.calls.find((c) =>
        String(c[0]).endsWith("/revisions"),
    );
    expect(JSON.parse(revision![1].body).expectedRevision).toBe(hash);
    fireEvent.click(
        screen.getByRole("button", { name: "Listar registros de sesión" }),
    );
    await screen.findByRole("button", {
        name: new RegExp(`${id} · revisión 2`),
    });
});
