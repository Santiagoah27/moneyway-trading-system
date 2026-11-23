import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import App from "../../App";
import SessionObservation from "./SessionObservation";
import { parseSessionReport } from "./sessionReport";

import { artifact, at, day, digest, windowId } from "./sessionReportFixture";

async function upload(value: unknown) {
    const file = new File(
        [typeof value === "string" ? value : JSON.stringify(value)],
        "replay.json",
        { type: "application/json" },
    );
    await userEvent.upload(screen.getByLabelText("Abrir informe local"), file);
}

afterEach(() => {
    cleanup();
    window.history.replaceState(null, "", "/");
});

describe("observación supervisada Nasdaq", () => {
    it("abre informes 1.1.0 con evidencia sustentadora y rechaza una finalización sin ella", () => {
        const report = artifact({ complete: true });
        report.research.version = "1.1.0";
        report.research.runId = [
            report.research.candidateId,
            report.research.version,
            report.research.datasetId,
            at.cutoff,
        ].join("/");
        for (const chain of report.research.chains)
            Object.assign(chain.branches[0], {
                realignmentBindingVerified: true,
            });
        expect(parseSessionReport(report).counts.complete).toBe(2);

        for (const chain of report.research.chains)
            chain.branches[0].stateAtRealignment = "Invalidated";
        expect(() => parseSessionReport(report)).toThrow(
            /finalización sin contexto M5 sustentador/,
        );

        report.research.summary.completeH4ToM1Associations = 0;
        for (const chain of report.research.chains)
            Object.assign(chain.geometry[0].contextOutcomes[0], {
                outcome: "ContextInvalidatedAtRealignment",
                completedAtUtc: null,
            });
        expect(parseSessionReport(report).counts.complete).toBe(0);
    });

    it("navega desde Strategy Lab al espacio de observación local", async () => {
        window.history.replaceState(null, "", "/#strategy-lab");
        render(<App />);
        const link = await screen.findByRole("link", {
            name: /Observación supervisada Nasdaq/,
        });
        expect(link.getAttribute("href")).toBe("#strategy-lab-observation");
        await userEvent.click(link);
        expect(
            await screen.findByRole("heading", {
                name: "Observación supervisada Nasdaq",
            }),
        ).toBeDefined();
    });

    it("conserva la evidencia M5/M1 y referencias con H4 indisponible", async () => {
        render(<SessionObservation />);
        await upload(artifact());
        expect(
            await screen.findByText("Replay histórico · Development expuesto"),
        ).toBeDefined();
        expect(screen.getByRole("status").textContent).toContain(
            "H4ContextUnavailable",
        );
        expect(screen.getByText("[08:30, 11:00)")).toBeDefined();
        expect(screen.getByText("[08:30, 11:30)")).toBeDefined();
        expect(screen.getByText(windowId)).toBeDefined();
        const stage = screen.getByRole("button", {
            name: /Realineación.*08:58:00/,
        });
        expect(stage).toBeDefined();
        expect(
            screen.getAllByRole("button", { name: /Toma de liquidez/ }),
        ).toHaveLength(1);
        expect(
            screen.getByRole("button", {
                name: /Primera confirmación.*08:35:00/,
            }),
        ).toBeDefined();
        expect(
            screen.getByRole("button", {
                name: /FVG direccional posterior.*08:55:00/,
            }),
        ).toBeDefined();
        const counts = screen.getByLabelText("Recuentos de investigación");
        expect(
            within(counts).getByText("Episodios").nextElementSibling
                ?.textContent,
        ).toBe("1");
        expect(
            within(counts).getByText("Asociaciones de configuración")
                .nextElementSibling?.textContent,
        ).toBe("2");
        expect(
            screen.getByText("R:R hipotético").nextElementSibling?.textContent,
        ).toBe("2");
        expect(screen.getByText("Admisión canónica bloqueada")).toBeDefined();
        expect(
            screen.getByText("Política ejecutable de entrada")
                .nextElementSibling?.textContent,
        ).toBe("Sin resolver");
        expect(
            screen.queryByRole("button", {
                name: /enviar orden|ejecutar|comprar|vender/i,
            }),
        ).toBeNull();
        await userEvent.click(stage);
        await userEvent.click(
            screen.getByText("Inspeccionar velas originales"),
        );
        expect(
            screen.getByRole("img", { name: /Velas OHLC M1/ }),
        ).toBeDefined();
        await userEvent.click(
            screen.getByText("Configuraciones de investigación"),
        );
        const sequence = screen.getByLabelText(
            "Secuencia M5/M1",
        ) as HTMLSelectElement;
        await userEvent.selectOptions(sequence, sequence.options[1]);
        expect(
            screen.getAllByRole("button", { name: /Toma de liquidez/ }),
        ).toHaveLength(1);
    });

    it("muestra la finalización histórica antes de su invalidación y conserva confirmaciones simultáneas", async () => {
        render(<SessionObservation />);
        await upload(artifact({ complete: true, simultaneous: true }));
        const completed = await screen.findByRole("button", {
            name: /Finalización histórica.*08:58:00/,
        });
        const invalidated = screen.getByRole("button", {
            name: /Cronología del contexto.*10:10:00/,
        });
        expect(
            completed.compareDocumentPosition(invalidated) &
                Node.DOCUMENT_POSITION_FOLLOWING,
        ).toBeTruthy();
        expect(
            screen.getByText("Contexto M5 invalidado después."),
        ).toBeDefined();
        const triggerStage = screen.getByRole("button", {
            name: /Primera confirmación.*SimultaneousConfirmation/,
        });
        expect(triggerStage.textContent).toContain(
            "orden intravela indeterminado",
        );
        await userEvent.click(triggerStage);
        expect(screen.getByText("StructuralChange")).toBeDefined();
        expect(screen.getByText("IFVG", { exact: true })).toBeDefined();
    });

    it("muestra SL no protector, TP ausente y R:R no disponible", async () => {
        render(<SessionObservation />);
        await upload(artifact({ protective: false, targetAvailable: false }));
        expect(await screen.findByText("TP no disponible")).toBeDefined();
        expect(screen.getAllByText("SL no protector").length).toBeGreaterThan(
            0,
        );
        expect(
            screen.getByText("R:R hipotético").nextElementSibling?.textContent,
        ).toBe("No disponible");
    });

    it("rechaza JSON truncado y limpia la sesión cargada", async () => {
        render(<SessionObservation />);
        await upload(artifact());
        await screen.findByText("Replay histórico · Development expuesto");
        await upload("{");
        expect((await screen.findByRole("alert")).textContent).toBe(
            "JSON inválido o truncado.",
        );
        expect(
            screen.queryByText("Replay histórico · Development expuesto"),
        ).toBeNull();
    });

    it("distingue adquisición prospectiva registrada de un replay histórico sin presentarla como sesión en vivo", async () => {
        const prospective = JSON.parse(
            JSON.stringify(artifact()).replaceAll(day, "2026-11-23"),
        ) as ReturnType<typeof artifact>;
        prospective.evidenceReadiness.session.exposure = "Prospective";
        const sources = prospective.sourceProvenance.map((s) => ({
            ...s,
            firstOpenUtc: "2026-11-23T05:00:00Z",
            lastNominalCloseUtc: "2026-11-23T16:30:00Z",
            localAcquisition: {
                acquiredAtUtc: "2026-11-23T17:30:00Z",
                evidence: { sha256: digest },
            },
        }));
        render(<SessionObservation />);
        await upload({ ...prospective, sourceProvenance: sources });
        expect(
            await screen.findByText(
                "Observación prospectiva registrada · replay al corte",
            ),
        ).toBeDefined();
        expect(screen.queryByText(/en vivo|sesión actual/i)).toBeNull();
    });

    it.each([
        "2026-06-22",
        "2026-06-24",
        "2026-06-25",
        "2026-06-26",
        "2026-06-30",
        "2026-09-08",
    ])(
        "rechaza el día protegido %s aunque declare exposición de desarrollo",
        (date) => {
            const input = artifact();
            input.research.operatingDate = date;
            expect(() => parseSessionReport(input)).toThrow(/protegida/);
        },
    );

    it("rechaza esquema, procedencia, identidades antiguas y configuraciones incoherentes", () => {
        expect(() =>
            parseSessionReport({ ...artifact(), schemaVersion: 2 }),
        ).toThrow(/versión del esquema/);
        const sourceMissing = artifact();
        sourceMissing.sourceProvenance.pop();
        expect(() => parseSessionReport(sourceMissing)).toThrow(
            /cuatro escalas/,
        );
        const configurationMismatch = artifact();
        configurationMismatch.research.chains[0].geometry[0].configurationId =
            "otro";
        expect(() => parseSessionReport(configurationMismatch)).toThrow(
            /inconsistente/,
        );
        const runtimeChange = artifact();
        runtimeChange.research.window.publishedRuntime.endUtc = at.cutoff;
        expect(() => parseSessionReport(runtimeChange)).toThrow(/límites/);
        const future = artifact();
        future.research.chains[0].branches[0].fvg.invalidatedAtUtc =
            day + "T16:30:00.0000001Z";
        expect(() => parseSessionReport(future)).toThrow(/posterior al corte/);
    });
});
