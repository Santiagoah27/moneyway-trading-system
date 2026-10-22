import { act, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

// Synthetic transport fixtures: explicit timestamps, never an ordinal-to-time formula.
function navigable(step = 8000): NasdaqHistoricalReport {
    const steps = [7998, 7999, 8000];
    const timestamps = [
        "2026-06-29T13:29:00Z",
        "2026-06-29T13:30:00Z",
        "2026-06-29T16:20:00Z",
    ];
    const index = steps.indexOf(step);
    const frame = report.selectedSessionLastFrame!;
    return {
        ...report,
        selectedSessionLastFrame: null,
        selectedFrame: {
            ...frame,
            step,
            asOfUtc: timestamps[index],
            verdict: index === 0 ? "Wait" : "NoTrade",
            reason: `source frame ${step}`,
            rules: [
                {
                    ...frame.rules[0],
                    reason: `source rule ${step}`,
                    result: index === 0 ? "Waiting" : "Failed",
                },
            ],
        },
        navigation: {
            firstStep: 7998,
            lastStep: 8000,
            selectedStep: step,
            previousStep: steps[index - 1] ?? null,
            nextStep: steps[index + 1] ?? null,
            sessionFrameCount: 3,
        },
    };
}

function jsonResponse(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
    });
}

function navigationFetch() {
    const mock = vi.fn((url: string) =>
        Promise.resolve(
            jsonResponse(
                navigable(
                    Number(new URL(url).searchParams.get("frameStep") ?? 8000),
                ),
            ),
        ),
    );
    vi.stubGlobal("fetch", mock);
    return mock;
}

describe("Historical frame navigation", () => {
    it("starts at last, uses actual neighbors, respects boundaries and returns to last", async () => {
        const mock = navigationFetch();
        const user = userEvent.setup();
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        expect(
            screen
                .getByRole("button", { name: "Siguiente" })
                .getAttribute("disabled"),
        ).not.toBeNull();
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source frame 7999");
        expect(mock.mock.calls.at(-1)![0]).toContain("frameStep=7999");
        expect(mock.mock.calls.at(-1)![0]).toContain(
            `reportSha256=${report.reportSha256}`,
        );
        await user.click(screen.getByRole("button", { name: "Siguiente" }));
        await screen.findByText("source frame 8000");
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source frame 7999");
        await user.click(screen.getByRole("button", { name: "Anterior" }));
        await screen.findByText("source frame 7998");
        expect(
            screen
                .getByRole("button", { name: "Anterior" })
                .getAttribute("disabled"),
        ).not.toBeNull();
        expect(screen.getByText("Wait")).toBeTruthy();
        expect(screen.getByText("NO_TRADE (NoTrade)")).toBeTruthy(); // Complete-report result remains separate.
        expect(screen.getByText(/Hora America\/Bogota:/).textContent).toMatch(
            /\b0?8:29:00/,
        );
        expect(screen.getByText(/Frame 7998/).textContent).toContain(
            "2026-06-29T13:29:00Z",
        );
        expect(screen.queryByText("source rule 8000")).toBeNull();
        await user.click(screen.getByRole("button", { name: "Último frame" }));
        await screen.findByText("source frame 8000");
        expect(mock.mock.calls.at(-1)![0]).toBe(
            "http://localhost:5080/api/nasdaq/historical-demo",
        );
    });

    it("supports direct selection and rejects invalid input without requesting a frame", async () => {
        const mock = navigationFetch();
        const user = userEvent.setup();
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        const input = screen.getByLabelText("Step del frame");
        await user.clear(input);
        await user.type(input, "7999");
        await user.click(screen.getByRole("button", { name: "Ir al frame" }));
        await screen.findByText("source frame 7999");
        await user.clear(screen.getByLabelText("Step del frame"));
        await user.type(screen.getByLabelText("Step del frame"), "0");
        await user.click(screen.getByRole("button", { name: "Ir al frame" }));
        expect((await screen.findByRole("alert")).textContent).toContain(
            "Step entero",
        );
        expect(mock).toHaveBeenCalledTimes(2);
        expect(screen.getByText("source frame 7999")).toBeTruthy();
    });

    it("hides previous evidence while loading and handles an unavailable frame", async () => {
        let complete!: (response: Response) => void;
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(navigable()))
                .mockImplementationOnce(
                    () =>
                        new Promise<Response>((resolve) => {
                            complete = resolve;
                        }),
                ),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
        expect(screen.getByText("Cargando reporte histórico.")).toBeTruthy();
        expect(screen.queryByText("source frame 8000")).toBeNull();
        complete(jsonResponse({ code: "FrameNotInSelectedSession" }, 404));
        expect((await screen.findByRole("alert")).textContent).toContain(
            "Frame no disponible para esta sesión",
        );
        expect(screen.queryByText("NO_TRADE (NoTrade)")).toBeNull();
    });

    it("resets a selection when the configured report changes", async () => {
        const replacement = {
            ...navigable(),
            sessionId: "replacement-session",
            reportSha256: "B".repeat(64),
        };
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(navigable()))
                .mockResolvedValueOnce(
                    jsonResponse({ code: "ReportChanged" }, 409),
                )
                .mockResolvedValueOnce(jsonResponse(replacement)),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
        expect(await screen.findByText("replacement-session")).toBeTruthy();
        expect(screen.getByText(/El reporte cambió/)).toBeTruthy();
        expect(fetch).toHaveBeenLastCalledWith(
            "http://localhost:5080/api/nasdaq/historical-demo",
            expect.anything(),
        );
    });

    it("ignores a late response after API/session replacement even if fetch ignores abort", async () => {
        let stale!: (response: Response) => void;
        const mock = vi
            .fn()
            .mockImplementationOnce(
                () =>
                    new Promise<Response>((resolve) => {
                        stale = resolve;
                    }),
            )
            .mockResolvedValueOnce(
                jsonResponse({ ...navigable(7998), sessionId: "new-session" }),
            );
        vi.stubGlobal("fetch", mock);
        const view = render(
            <NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />,
        );
        view.rerender(
            <NasdaqHistoricalDemo apiBaseUrl="http://localhost:5090" />,
        );
        await screen.findByText("new-session");
        await act(async () => stale(jsonResponse(navigable())));
        expect(screen.getByText("source frame 7998")).toBeTruthy();
        expect(screen.queryByText("source frame 8000")).toBeNull();
        expect(mock.mock.calls[1][0]).toBe(
            "http://localhost:5090/api/nasdaq/historical-demo",
        );
    });

    it("ignores an older frame response after return-to-last supersedes it", async () => {
        let stale!: (response: Response) => void;
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(navigable()))
                .mockImplementationOnce(
                    () =>
                        new Promise<Response>((resolve) => {
                            stale = resolve;
                        }),
                )
                .mockResolvedValueOnce(jsonResponse(navigable())),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        await screen.findByText("source frame 8000");
        fireEvent.click(screen.getByRole("button", { name: "Anterior" }));
        expect(screen.getByText("Cargando reporte histórico.")).toBeTruthy();
        fireEvent.click(screen.getByRole("button", { name: "Último frame" }));
        await screen.findByText("source frame 8000");
        await act(async () => stale(jsonResponse(navigable(7999))));
        expect(screen.getByText("source frame 8000")).toBeTruthy();
        expect(screen.queryByText("source frame 7999")).toBeNull();
    });
});

describe("Nasdaq historical demo", () => {
    it("shows loading without execution controls", () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(() => new Promise<Response>(() => undefined)),
        );
        render(<NasdaqHistoricalDemo apiBaseUrl="http://localhost:5080" />);
        expect(screen.getByRole("status").textContent).toContain("Cargando");
        expect(
            screen
                .getByRole("button", { name: "Último frame" })
                .getAttribute("disabled"),
        ).not.toBeNull();
        expect(
            screen.queryByRole("button", { name: /run|ejecutar/i }),
        ).toBeNull();
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
        expect(
            screen.queryByRole("button", { name: /run|ejecutar/i }),
        ).toBeNull();
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
