import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { HistoricalEvidencePackagePanel } from "./HistoricalEvidencePackagePanel";

afterEach(() => vi.unstubAllGlobals());
const identity = {
    packageId: "synthetic-package",
    packageVersion: 1,
    sessionId: "synthetic-session",
    tradingDay: "2026-06-29",
    strategyId: "moneyway-nasdaq",
    strategyVersion: "nasdaq-0.1.0-draft",
    scenario: "Standard",
    providerId: "synthetic-provider",
    originalSymbol: "US100",
    origin: "SyntheticFixture",
    analysisReference: "synthetic different analysis feed",
};
const inspection = {
    package: {
        ...identity,
        candleProvenance: "synthetic only",
        originalClockDomain: "native timestamp unverified",
        timeMapping: "claimed offset only",
        availabilityEvidence: "not established",
        humanReviews: [],
        limitations: ["No authentic approval"],
    },
    manifestSha256: "a".repeat(64),
    state: "ValidatedDocumentaryPackage_NotCanonicallyAdmissible",
    documentaryStructureValid: true,
    sourceIntegrityVerified: true,
    canonicalReplayPermitted: false,
    admissionResult: "DataUnavailable",
    admissionBlockers: [
        "HistoricalAvailabilityNotEstablished",
        "RetrospectiveHumanReviewCannotBackdateAvailability",
    ],
    invalidReasons: [],
    artifacts: [],
    coverage: [],
    availabilityStatus: "NotEstablished",
    humanApprovalStatus: "NoIndependentPackageAdmissionApproval",
    temporalContract: "no trusted verifier",
    historicalReportScope:
        "PreviouslyGeneratedResearchReport_NotNewCausalAdmission",
    tradingWindowConflict: "1130_vs_1100",
    existingHistoricalReport: {
        reportSha256: "b".repeat(64),
        frameCount: 2,
        selectedFrame: {
            step: 1,
            asOfUtc: "2026-06-29T13:30:00Z",
            verdict: "NoTrade",
            reason: "synthetic missing preparation",
            rules: [
                {
                    ruleId: "NQ-TIME-003",
                    result: "Failed",
                    reason: "synthetic unavailable preparation",
                    evidenceId: null,
                },
            ],
        },
    },
};
function fetcher(result: unknown = inspection) {
    return vi.fn().mockImplementation(async (url: string) => {
        if (url.endsWith("/historical-packages"))
            return {
                ok: true,
                json: async () => [
                    { ...identity, manifestSha256: inspection.manifestSha256 },
                ],
            };
        if (url.endsWith("/diagnostic"))
            return {
                ok: false,
                status: 409,
                json: async () => ({
                    code: "HistoricalPackageCanonicalAdmissionUnavailable",
                }),
            };
        return { ok: true, json: async () => result };
    });
}
async function select() {
    fireEvent.click(
        screen.getByRole("button", {
            name: "Listar paquetes históricos registrados",
        }),
    );
    await screen.findByRole("option", { name: /synthetic-package/ });
    fireEvent.change(screen.getByLabelText("Paquete registrado"), {
        target: { value: identity.packageId },
    });
    fireEvent.click(
        screen.getByRole("button", {
            name: "Inspeccionar integridad y disponibilidad",
        }),
    );
    await screen.findByText(
        "ValidatedDocumentaryPackage_NotCanonicallyAdmissible",
    );
}
it("loads only registered immutable candidates and distinguishes documentary integrity from admission", async () => {
    const request = fetcher();
    vi.stubGlobal("fetch", request);
    const scope = vi.fn();
    render(<HistoricalEvidencePackagePanel apiBaseUrl="" onScope={scope} />);
    expect(request).not.toHaveBeenCalled();
    await select();
    expect(request.mock.calls[1][0]).toContain(
        `manifestSha256=${inspection.manifestSha256}`,
    );
    expect(
        screen.getByText(/NoIndependentPackageAdmissionApproval/),
    ).not.toBeNull();
    expect(
        screen.getByText(/Bloqueo: HistoricalAvailabilityNotEstablished/),
    ).not.toBeNull();
    expect(
        screen.getByText(/Veredicto histórico registrado: NoTrade/),
    ).not.toBeNull();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Usar identidad nativa para sesión documental, sin autorización",
        }),
    );
    expect(scope).toHaveBeenCalledWith(inspection.package);
    expect(request).toHaveBeenCalledTimes(2); // Selecting scope sends no opening, entry or order request.
});
it("exercises actual admission rejection rather than offering an approval checkbox", async () => {
    vi.stubGlobal("fetch", fetcher());
    render(<HistoricalEvidencePackagePanel apiBaseUrl="" onScope={vi.fn()} />);
    await select();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Comprobar bloqueo de diagnóstico admitido",
        }),
    );
    expect((await screen.findByRole("alert")).textContent).toContain(
        "HistoricalPackageCanonicalAdmissionUnavailable",
    );
    expect(screen.queryByRole("checkbox")).toBeNull();
});
it("shows invalid artifact failure without substituting a synthetic success or allowing scope linkage", async () => {
    const value = {
        ...inspection,
        state: "InvalidPackage",
        documentaryStructureValid: false,
        sourceIntegrityVerified: false,
        invalidReasons: ["ArtifactHashMismatch:raw"],
        existingHistoricalReport: null,
    };
    vi.stubGlobal("fetch", fetcher(value));
    render(<HistoricalEvidencePackagePanel apiBaseUrl="" onScope={vi.fn()} />);
    fireEvent.click(
        screen.getByRole("button", {
            name: "Listar paquetes históricos registrados",
        }),
    );
    await screen.findByRole("option", { name: /synthetic-package/ });
    fireEvent.change(screen.getByLabelText("Paquete registrado"), {
        target: { value: identity.packageId },
    });
    fireEvent.click(
        screen.getByRole("button", {
            name: "Inspeccionar integridad y disponibilidad",
        }),
    );
    expect(await screen.findByText("InvalidPackage")).not.toBeNull();
    expect(
        screen.getByRole("button", {
            name: "Usar identidad nativa para sesión documental, sin autorización",
        }),
    ).toHaveProperty("disabled", true);
    expect(
        screen.getByText(/Inválido: ArtifactHashMismatch:raw/),
    ).not.toBeNull();
    expect(screen.queryByText(/Veredicto histórico registrado/)).toBeNull();
});
it("clears stale package detail when selection changes or the registry is explicitly reloaded", async () => {
    const request = fetcher();
    vi.stubGlobal("fetch", request);
    render(<HistoricalEvidencePackagePanel apiBaseUrl="" onScope={vi.fn()} />);
    await select();
    fireEvent.click(
        screen.getByRole("button", {
            name: "Listar paquetes históricos registrados",
        }),
    );
    await waitFor(() =>
        expect(screen.getByLabelText("Paquete registrado")).toHaveProperty(
            "value",
            "",
        ),
    );
    expect(
        screen.queryByText(
            "ValidatedDocumentaryPackage_NotCanonicallyAdmissible",
        ),
    ).toBeNull();
});
