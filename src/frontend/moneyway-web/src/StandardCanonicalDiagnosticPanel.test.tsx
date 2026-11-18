import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { StandardCanonicalDiagnosticPanel } from "./StandardCanonicalDiagnosticPanel";
import type { StandardDiagnosticContext } from "./StandardCanonicalDiagnosticPanel";
import { SupervisedExecutionCapture } from "./SupervisedExecutionCapture";

afterEach(() => vi.unstubAllGlobals());
it("does not link a registered package research report whose causal admission is blocked", async () => {
    const onReference = vi.fn();
    const onAssociate = vi.fn();
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                ...context,
                packageAdmission:
                    "BlockedRegisteredPackageCausalAdmission:synthetic-package",
            }),
        }),
    );
    render(
        <StandardCanonicalDiagnosticPanel
            apiBaseUrl=""
            session={context.session}
            onReference={onReference}
            onAssociate={onAssociate}
        />,
    );
    fireEvent.click(
        screen.getByRole("button", {
            name: "Cargar diagnóstico de esta sesión",
        }),
    );
    const button = await screen.findByRole("button", {
        name: "Vincular frame a evidencia documental",
    });
    expect(button).toHaveProperty("disabled", true);
    expect(onReference).toHaveBeenLastCalledWith(null);
    expect(onAssociate).not.toHaveBeenCalled();
});
const reference = {
    sessionKey: "a".repeat(64),
    reportSha256: "b".repeat(64),
    frameStep: 12,
    asOfUtc: "2026-09-08T13:32:00Z",
};
// Explicitly synthetic UI transport samples, never claimed to be authentic evaluator output.
const context = {
    reference,
    session: {
        sessionKey: reference.sessionKey,
        sessionId: "synthetic:standard",
        path: "Standard",
        status: "Open_NoTrade",
        terminal: false,
        identity: {
            tradingDay: "2026-09-08",
            providerId: { value: "synthetic-provider" },
            symbol: { value: "US100" },
        },
        events: [],
    },
    report: {
        strategyId: "moneyway-nasdaq",
        strategyVersion: "nasdaq-0.1.0-draft",
        sessionId: "synthetic:canonical-run",
        providerId: "synthetic-provider",
        symbol: "US100",
        tradingDay: "2026-09-08",
        evidenceValidation: "NotRevalidated",
        inputDiagnosticCodes: ["synthetic:availability"],
        bindingDiagnosticCodes: [],
        selectedFrame: {
            step: 12,
            asOfUtc: reference.asOfUtc,
            verdict: "HumanValidationRequired",
            reason: "synthetic:human selection absent",
            blockingRuleId: "NQ-M5-001",
            blockingResult: "HumanValidationRequired",
            missingRequiredRuleIds: ["synthetic:missing evaluation"],
            rules: [
                "Passed",
                "Waiting",
                "HumanValidationRequired",
                "DataUnavailable",
                "Failed",
            ].map((result, i) => ({
                ruleId: `synthetic-rule-${i}`,
                sequence: i,
                definitionStatus: "HumanValidationRequired",
                result,
                isRequired: true,
                reason: `synthetic reason ${result}`,
                evidenceId: `synthetic:evidence-${i}`,
                evaluatedAtUtc: reference.asOfUtc,
            })),
            tradeLifecycle: {
                sources: [{ sourceReference: "synthetic:original source" }],
            },
        },
    },
    canonicalPath: "Unresolved",
    authorization: "NotGranted_DocumentaryContextOnly_SpecificationConflict",
    specificationConflict: "0830To1130_vs_0830To1100",
    conflictProvenance: "synthetic:human-reviewed source conflict",
    correctionPath: "CandidateVersion_HumanApproval",
    sourceLimitations: "PersistedCanonicalReport_NotLive",
    dependencies: [],
} as unknown as StandardDiagnosticContext;

it("keeps the report digest pinned across a failed frame reload and retry", async () => {
    const fetcher = vi
        .fn()
        .mockResolvedValueOnce({ ok: true, json: async () => context })
        .mockResolvedValue({
            ok: false,
            status: 409,
            json: async () => ({ detail: "ReportChanged_NoSubstitution" }),
        });
    vi.stubGlobal("fetch", fetcher);
    render(
        <StandardCanonicalDiagnosticPanel
            apiBaseUrl=""
            session={context.session}
            onReference={vi.fn()}
        />,
    );
    const load = screen.getByRole("button", {
        name: "Cargar diagnóstico de esta sesión",
    });
    fireEvent.click(load);
    await screen.findByText(/Veredicto canónico:/);
    fireEvent.click(load);
    await screen.findByRole("alert");
    fireEvent.click(load);
    await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(3));
    expect(fetcher.mock.calls[1][0]).toContain(
        `reportSha256=${reference.reportSha256}`,
    );
    expect(fetcher.mock.calls[2][0]).toContain(
        `reportSha256=${reference.reportSha256}`,
    );
});

it("shows every actual transported status/reason, missing evidence and conflict without synthesizing approval", async () => {
    const onReference = vi.fn();
    const onAssociate = vi.fn();
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue({ ok: true, json: async () => context }),
    );
    render(
        <StandardCanonicalDiagnosticPanel
            apiBaseUrl=""
            session={context.session}
            onReference={onReference}
            onAssociate={onAssociate}
        />,
    );
    fireEvent.click(
        screen.getByRole("button", {
            name: "Cargar diagnóstico de esta sesión",
        }),
    );
    await screen.findByText(/synthetic reason DataUnavailable/);
    for (const result of [
        "Passed",
        "Waiting",
        "HumanValidationRequired",
        "DataUnavailable",
        "Failed",
    ])
        expect(
            screen.getByText(new RegExp(`synthetic reason ${result}`)),
        ).not.toBeNull();
    expect(screen.getByText(/synthetic:missing evaluation/)).not.toBeNull();
    expect(screen.getByText(/11:30 frente a runtime/)).not.toBeNull();
    expect(
        screen.getByText(/NotGranted_DocumentaryContextOnly/),
    ).not.toBeNull();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Vincular frame a evidencia documental",
        }),
    );
    expect(onAssociate).toHaveBeenCalledWith(context);
    expect(onReference).toHaveBeenLastCalledWith(reference);
});
it("reloads the immutable saved digest and fails explicitly when report identity changed", async () => {
    const fetcher = vi.fn().mockResolvedValue({
        ok: false,
        status: 409,
        json: async () => ({
            code: "EvidenceConflict",
            detail: "ReportChanged_NoSubstitution",
        }),
    });
    vi.stubGlobal("fetch", fetcher);
    const onReference = vi.fn();
    render(
        <StandardCanonicalDiagnosticPanel
            apiBaseUrl=""
            session={context.session}
            savedReference={reference}
            onReference={onReference}
        />,
    );
    fireEvent.click(
        screen.getByRole("button", {
            name: "Recuperar frame vinculado al registro",
        }),
    );
    expect((await screen.findByRole("alert")).textContent).toContain(
        "ReportChanged_NoSubstitution",
    );
    expect(fetcher.mock.calls[0][0]).toContain(
        `frameStep=12&reportSha256=${reference.reportSha256}`,
    );
    expect(
        screen.queryByRole("button", {
            name: "Vincular frame a evidencia documental",
        }),
    ).toBeNull();
    expect(onReference).toHaveBeenLastCalledWith(null);
});
it("permits diagnostic/documentary viewing for a terminal session without creating operational actions", async () => {
    const fetcher = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
            ...context,
            session: { ...context.session, terminal: true },
        }),
    });
    vi.stubGlobal("fetch", fetcher);
    render(
        <StandardCanonicalDiagnosticPanel
            apiBaseUrl=""
            session={{ ...context.session, terminal: true }}
            onReference={vi.fn()}
            onAssociate={vi.fn()}
        />,
    );
    expect(screen.getByText(/TERMINAL · SIN REENTRADA/)).not.toBeNull();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Cargar diagnóstico de esta sesión",
        }),
    );
    expect(
        await screen.findByRole("button", {
            name: "Vincular frame a evidencia documental",
        }),
    ).toHaveProperty("disabled", false);
    expect(fetcher.mock.calls[0]).toHaveLength(1); // read-only request, no operational POST or order
});
it("connects selected session/frame to the existing documentary form without filling prices or approval", async () => {
    const fetcher = vi.fn().mockImplementation(async (url: string) => ({
        ok: true,
        json: async () =>
            url.includes("/diagnostic") ? context : context.session,
    }));
    vi.stubGlobal("fetch", fetcher);
    render(<SupervisedExecutionCapture apiBaseUrl="" />);
    fireEvent.click(
        screen.getByRole("button", {
            name: "Abrir o recuperar ámbito de sesión",
        }),
    );
    fireEvent.click(
        await screen.findByRole("button", {
            name: "Cargar diagnóstico de esta sesión",
        }),
    );
    fireEvent.click(
        await screen.findByRole("button", {
            name: "Vincular frame a evidencia documental",
        }),
    );
    await waitFor(() =>
        expect(screen.getByLabelText("Sesión de evidencia")).toHaveProperty(
            "value",
            "synthetic:standard",
        ),
    );
    expect(screen.getByLabelText("Instrumento original")).toHaveProperty(
        "value",
        "US100",
    );
    expect(
        screen.getByText(/Contexto documental canónico:/).textContent,
    ).toContain(reference.reportSha256);
    expect(screen.getByLabelText("Entrada: precio observado")).toHaveProperty(
        "value",
        "",
    );
    expect(
        screen.getByLabelText(/Confirmo que MoneyWay ejecutó/),
    ).toHaveProperty("checked", false);
    expect(fetcher).toHaveBeenCalledTimes(2); // linking only edits a draft; no automatic capture/attempt
});
