import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NasdaqHistoricalDemo } from "./NasdaqHistoricalDemo";
import type { NasdaqHistoricalReport } from "./types/nasdaqHistoricalDemo";

const report: NasdaqHistoricalReport = {
    sessionId: "synthetic-no-trade-ui-only",
    strategyId: "moneyway-nasdaq",
    strategyVersion: "nasdaq-0.1.0-draft",
    providerId: "test-demo-provider",
    symbol: "US100",
    tradingDay: "2026-06-29",
    schemaVersion: 2,
    frameCount: 9660,
    finalVerdict: "NoTrade",
    replayStartUtc: "2026-06-21T22:01:00Z",
    replayEndUtc: "2026-06-30T21:00:00Z",
    reportSha256: "A".repeat(64),
    reportBytes: 178140291,
    validation: "SchemaChronologyAndCountChecked",
    evidenceValidation: "NotRevalidated",
    inputDiagnosticCodes: [],
    bindingDiagnosticCodes: [],
    selectedSessionLastFrame: {
        step: 8000,
        asOfUtc: "2026-06-29T16:20:00Z",
        verdict: "NoTrade",
        reason: "No qualifying preparation evidence.",
        blockingRuleId: "NQ-TIME-003",
        blockingResult: "Failed",
        hasCompleteRequiredCoverage: true,
        missingRequiredRuleIds: [],
        snapshotState: "Unavailable",
        snapshotDiagnostic: "CanonicalEligibilityUnavailable",
        factualEvaluationState: "Unavailable",
        factualRequiresHumanValidation: null,
        rules: [
            {
                ruleId: "NQ-TIME-003",
                definitionStatus: "Confirmed",
                result: "Failed",
                sequence: 5,
                isRequired: true,
                reason: "No qualifying preparation evidence.",
                evaluatedAtUtc: "2026-06-29T16:20:00Z",
                evidenceId: null,
            },
        ],
    },
};

function respond(body: unknown = report, status = 200) {
    vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue(
            new Response(JSON.stringify(body), {
                status,
                headers: { "Content-Type": "application/json" },
            }),
        ),
    );
    render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
}

afterEach(() => vi.unstubAllGlobals());

describe("Nasdaq historical demo", () => {
    it("shows loading without execution controls", () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise<Response>(() => undefined)),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        expect(screen.getByRole("status").textContent).toContain("Cargando");
        expect(screen.queryByRole("button")).toBeNull();
    });

    it("shows canonical NO_TRADE, provenance, blockers and unavailable factual operation", async () => {
        respond();
        expect(await screen.findByText(report.sessionId)).toBeTruthy();
        expect(screen.getAllByText("NO_TRADE (NoTrade)")).toHaveLength(2);
        expect(screen.getByText("2026-06-29")).toBeTruthy();
        expect(screen.getByText(/test-demo-provider \/ US100/)).toBeTruthy();
        expect(
            screen.getByText("Evaluación factual: Unavailable"),
        ).toBeTruthy();
        expect(
            screen.getByText(/No hay una operación histórica factual completa/),
        ).toBeTruthy();
        expect(screen.getByText(/SHA-256:/).textContent).toContain(
            report.reportSha256,
        );
        expect(
            screen.getByText(/Contenido de evidencias:/).textContent,
        ).toContain("NotRevalidated");
        expect(
            screen.queryByText(/PnL|R-multiple|Entry:|Take Profit:/),
        ).toBeNull();
        expect(screen.queryByRole("button")).toBeNull();
        expect(fetch).toHaveBeenCalledWith(
            "http://localhost:5080/api/nasdaq/historical-demo",
            expect.objectContaining({ cache: "no-store" }),
        );
    });

    it.each(["HumanValidationRequired", "DataUnavailable", "Wait"])(
        "preserves %s without converting it into NoTrade",
        async (status) => {
            const frame = report.selectedSessionLastFrame!;
            respond({
                ...report,
                finalVerdict: status,
                selectedSessionLastFrame: {
                    ...frame,
                    verdict: status,
                    rules: [
                        {
                            ...frame.rules[0],
                            result: status === "Wait" ? "Waiting" : status,
                        },
                    ],
                },
            });
            await screen.findByText(report.sessionId);
            expect(screen.getAllByText(status).length).toBeGreaterThanOrEqual(
                2,
            );
            expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
        },
    );

    it("shows absent required evaluations and input/binding diagnostics", async () => {
        respond({
            ...report,
            inputDiagnosticCodes: ["MissingTimeframe"],
            bindingDiagnosticCodes: ["PrerequisiteUnavailable"],
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame,
                hasCompleteRequiredCoverage: false,
                missingRequiredRuleIds: ["NQ-H4-001"],
            },
        });
        expect(
            await screen.findByText(
                /Reglas obligatorias sin evaluación: NQ-H4-001/,
            ),
        ).toBeTruthy();
        expect(
            screen.getByText(/Diagnósticos de entrada: MissingTimeframe/),
        ).toBeTruthy();
        expect(
            screen.getByText(
                /Diagnósticos de evidencia humana: PrerequisiteUnavailable/,
            ),
        ).toBeTruthy();
    });

    it("keeps an absent session frame unavailable instead of using a different day", async () => {
        respond({
            ...report,
            selectedSessionLastFrame: null,
            finalVerdict: null,
        });
        expect(
            await screen.findByText(
                /No hay un frame disponible para la sesión seleccionada/,
            ),
        ).toBeTruthy();
        expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
    });

    it("shows factual availability and human review separately from a trading verdict", async () => {
        respond({
            ...report,
            selectedSessionLastFrame: {
                ...report.selectedSessionLastFrame,
                factualEvaluationState: "Available",
                factualRequiresHumanValidation: true,
            },
        });
        expect(
            await screen.findByText("Evaluación factual: Available"),
        ).toBeTruthy();
        expect(
            screen.getByText(
                "La evaluación factual requiere validación humana.",
            ),
        ).toBeTruthy();
        expect(
            screen.queryByText(
                /No hay una operación histórica factual completa/,
            ),
        ).toBeNull();
    });

    it.each([
        [404, /Reporte histórico no disponible/],
        [422, /reporte histórico es inválido/],
        [503, /No se pudo consultar el reporte histórico/],
    ])(
        "handles HTTP %s without presenting a successful report",
        async (status, message) => {
            respond({}, status as number);
            expect((await screen.findByRole("alert")).textContent).toMatch(
                message as RegExp,
            );
            expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
        },
    );

    it("handles connection failure", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        expect((await screen.findByRole("alert")).textContent).toContain(
            "No se pudo consultar",
        );
    });
});
