import { webcrypto } from "node:crypto";
import { readFileSync } from "node:fs";
import {
    fireEvent,
    render,
    screen,
    waitFor,
    within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "../../App";
import IntegratedReview from "./IntegratedReview";
import {
    coverage,
    frames,
    projectDevelopment,
    readOriginalPackage,
    seal,
    sha256,
    syntheticPacket,
    unseal,
    type OriginalPackage,
} from "./originalPackage";
import {
    appendLabel,
    exportReview,
    importReview,
    loadReview,
    newReview,
    reference,
} from "./reviewHistory";

const packageSchema = "moneyway.nasdaq-blind-original.v1";
const now = "2026-11-23T20:00:00Z";
type Payload = Omit<OriginalPackage, "sha256">;
async function packet() {
    return readOriginalPackage(await syntheticPacket());
}
async function payload() {
    return (await unseal(await syntheticPacket(), packageSchema))
        .value as Payload;
}

beforeEach(() => {
    vi.stubGlobal("crypto", webcrypto);
    localStorage.clear();
});
afterEach(() => {
    window.history.replaceState(null, "", "/");
    vi.unstubAllGlobals();
    localStorage.clear();
});

async function openCase() {
    await userEvent.click(
        screen.getByRole("button", { name: "Abrir caso sintético" }),
    );
    await screen.findByRole("heading", { name: /US100 · Sintética/ });
}

async function begin(exposure = "SelfDeclaredIndependent") {
    await userEvent.type(screen.getByLabelText("Revisor"), "Revisor QA");
    await userEvent.selectOptions(
        screen.getByLabelText("Exposición declarada"),
        exposure,
    );
    await userEvent.click(
        screen.getByRole("button", { name: "Iniciar revisión" }),
    );
}

async function note(text: string) {
    await userEvent.type(screen.getByLabelText("Evidencia o motivo"), text);
    await userEvent.click(
        screen.getByRole("button", { name: "Guardar revisión" }),
    );
}

describe("paquete de velas originales", () => {
    it("proyecta solo la fuente Development fijada y verifica su integridad", async () => {
        const text = readFileSync(
            "src/research/context/data/june23-context.json",
            "utf8",
        );
        const projected = await projectDevelopment(text);
        expect(projected.exposure).toBe("Development");
        expect(projected.cutoffUtc).toBe("2026-06-23T16:30:00Z");
        expect(
            frames.map((tf) => projected.sources[tf].coverage.count),
        ).toEqual([10, 41, 498, 2490]);
        expect(projected.sources.H4.originalSha256).toMatch(/^[0-9a-f]{64}$/);
        expect(projected.sources.M1.bars[0].nativeOpen).toMatch(/^2026\./);
        expect(JSON.stringify(projected)).not.toMatch(
            /candidate|rewardRisk|configuration|signal":true|outcome/i,
        );
        await expect(projectDevelopment(text + " ")).rejects.toThrow(/SHA-256/);
    });

    it.each(["2026-06-25", "2026-06-30", "2026-11-23", "2026-11-24"])(
        "bloquea la fecha protegida %s incluso si se presenta como sintética",
        async (day) => {
            const value = {
                ...(await payload()),
                operatingDate: day,
                cutoffUtc: day + "T16:30:00Z",
            };
            await expect(
                readOriginalPackage(await seal(packageSchema, value)),
            ).rejects.toThrow(/particiones protegidas/);
        },
    );

    it("rechaza velas futuras, identidades incompatibles y cualquier campo calculado", async () => {
        const value = await payload();
        const variants = [
            [
                { ...value, cutoffUtc: "2000-01-03T12:00:30Z" },
                /posterior al corte/,
            ],
            [{ ...value, instrument: "USTEC" }, /Identidad US100/],
            [
                { ...value, outcome: "H4ContextUnavailable" },
                /Campos incompatibles/,
            ],
            [{ ...value, operationalSignal: true }, /Identidad US100/],
        ] as const;
        for (const [v, message] of variants)
            await expect(
                readOriginalPackage(await seal(packageSchema, v)),
            ).rejects.toThrow(message);
        value.sources.M1.originalSha256 = "0".repeat(64);
        await expect(
            readOriginalPackage(await seal(packageSchema, value)),
        ).rejects.toThrow(/fuente M1/);
        const envelope = JSON.parse(await syntheticPacket());
        envelope.payload += " ";
        await expect(
            readOriginalPackage(JSON.stringify(envelope)),
        ).rejects.toThrow(/SHA-256/);
    });
});

describe("revisión humana integrada", () => {
    it("abre desde Strategy Lab y exige declaración explícita", async () => {
        window.history.replaceState(null, "", "/#strategy-lab");
        render(<App />);
        await userEvent.click(
            screen.getByRole("link", { name: /Revisión humana ciega Nasdaq/ }),
        );
        await screen.findByRole("heading", {
            name: "Revisión humana ciega Nasdaq",
        });
        await openCase();
        await userEvent.click(
            screen.getByRole("button", { name: "Iniciar revisión" }),
        );
        expect(screen.getByRole("alert").textContent).toMatch(
            /Declare su exposición/,
        );
        expect(screen.queryByRole("img")).toBeNull();
        expect(
            screen.queryByRole("button", { name: /revelar|orden|operar/i }),
        ).toBeNull();
    });

    it("navega cuatro escalas, oculta el futuro y guarda la identidad exacta de una vela", async () => {
        render(<IntegratedReview />);
        await openCase();
        await begin();
        await userEvent.selectOptions(
            screen.getByLabelText("Temporalidad"),
            "M1",
        );
        expect(
            screen.getAllByRole("button", { name: /Inspeccionar vela/ }),
        ).toHaveLength(1);
        await userEvent.click(
            screen.getByRole("button", { name: "Siguiente" }),
        );
        expect(
            screen.getAllByRole("button", { name: /Inspeccionar vela/ }),
        ).toHaveLength(2);
        await userEvent.click(
            screen.getByRole("button", { name: "Corte final" }),
        );
        for (const tf of frames) {
            await userEvent.selectOptions(
                screen.getByLabelText("Temporalidad"),
                tf,
            );
            expect(
                screen.getByRole("img", { name: "Velas OHLC " + tf }),
            ).toBeDefined();
        }
        await userEvent.selectOptions(
            screen.getByLabelText("Observación"),
            "M1Realignment",
        );
        await userEvent.click(
            screen
                .getAllByRole("button", { name: /Inspeccionar vela/ })
                .at(-1)!,
        );
        expect(
            within(screen.getByLabelText("Velas seleccionadas")).getByText(
                /12:35/,
            ),
        ).toBeDefined();
        await note("El revisor identifica esta vela.");
        const p = await packet();
        expect(loadReview(p)?.labels[0]).toMatchObject({
            targetId: "synthetic-us100-v1/M1Realignment",
            revision: 1,
            phase: "PreRevealJudgment",
            references: [reference(p, "M1", "2000-01-03T12:35:00.000Z")],
        });
        fireEvent.change(screen.getByLabelText("Posición temporal"), {
            target: { value: "0" },
        });
        expect(
            screen.getAllByRole("button", { name: /Inspeccionar vela/ }),
        ).toHaveLength(1);
        expect(screen.queryByText(/identifica esta vela/)).toBeNull();
        expect(loadReview(p)?.maximumViewedUtc).toBe(
            "2000-01-03T20:00:00.000Z",
        );
    });

    it("guarda ausencia y ambigüedad, persiste al recargar y conserva el juicio tras exposición externa", async () => {
        const view = render(<IntegratedReview />);
        await openCase();
        await begin("Unknown");
        await userEvent.selectOptions(
            screen.getByLabelText("Clasificación"),
            "NoReference",
        );
        await note("Sin referencia observable.");
        await userEvent.click(
            screen.getByRole("button", { name: /Registrar que vi resultados/ }),
        );
        await userEvent.selectOptions(
            screen.getByLabelText("Clasificación"),
            "Ambiguous",
        );
        await note("Dos lecturas posibles.");
        const p = await packet();
        const saved = loadReview(p)!;
        expect(saved.exposure).toBe("Unknown");
        expect(saved.reveal?.afterRevision).toBe(1);
        expect(
            saved.labels.map(({ revision, kind, phase }) => [
                revision,
                kind,
                phase,
            ]),
        ).toEqual([
            [1, "NoReference", "PreRevealJudgment"],
            [2, "Ambiguous", "PostRevealCorrection"],
        ]);
        expect(saved.canonicalAdmissionPermitted).toBe(false);
        expect(saved.operationalSignal).toBe(false);
        view.unmount();
        render(<IntegratedReview />);
        await openCase();
        expect(screen.getByText(/Sin referencia observable/)).toBeDefined();
        expect(screen.getByText(/Dos lecturas posibles/)).toBeDefined();
    });

    it("verifica exportación, importa etiquetas locales y rechaza reemplazar revisiones previas", async () => {
        const p = await packet();
        const first = appendLabel(
            p,
            newReview(p, "QA", "PreviouslyExposed", p.cutoffUtc, now),
            "H4Context",
            "InsufficientSourceEvidence",
            [],
            "Cobertura insuficiente",
            p.cutoffUtc,
            now,
        );
        const exportText = await exportReview(p, first);
        expect(await importReview(p, null, exportText)).toEqual(first);
        const second = appendLabel(
            p,
            first,
            "H1Liquidity",
            "Skip",
            [],
            "Omitida por el revisor",
            p.cutoffUtc,
            now,
        );
        await expect(importReview(p, second, exportText)).rejects.toThrow(
            /sustituiría/,
        );
        await expect(
            importReview(p, first, await exportReview(p, second)),
        ).resolves.toEqual(second);
        await expect(
            importReview(
                p,
                null,
                exportText.replace(/"sha256":"./, '"sha256":"Z'),
            ),
        ).rejects.toThrow();
        render(<IntegratedReview />);
        await openCase();
        await userEvent.upload(
            screen.getByLabelText("Importar etiquetas JSON"),
            new File([exportText], "review.json", { type: "application/json" }),
        );
        await waitFor(() => expect(loadReview(p)).toEqual(first));
        expect(screen.getByText(/Ya vi resultados/)).toBeDefined();
    });

    it("informa mapeo sin verificar y limpia la vista si se abre un informe incompatible", async () => {
        const value = await payload();
        value.sources.H4 = {
            bars: [],
            mapping: "Unresolved",
            coverage: coverage([]),
            originalSha256: await sha256("[]"),
        };
        value.provenanceSha256 = await sha256(JSON.stringify(value.sources));
        const upload = screen.getByLabelText;
        render(<IntegratedReview />);
        await userEvent.upload(
            upload("Abrir paquete original local"),
            new File([await seal(packageSchema, value)], "original.json", {
                type: "application/json",
            }),
        );
        await screen.findByRole("heading", { name: /US100 · Sintética/ });
        await begin("Unknown");
        expect(
            screen.getByText(/Mapeo UTC sin verificar para H4/),
        ).toBeDefined();
        expect(screen.queryByRole("img")).toBeNull();
        await userEvent.upload(
            upload("Abrir paquete original local"),
            new File(
                ['{"research":{"candidateId":"EXP-NQ-END-TO-END-MTF-V1"}}'],
                "report.json",
                { type: "application/json" },
            ),
        );
        expect((await screen.findByRole("alert")).textContent).toMatch(
            /incompatibles/,
        );
        expect(
            screen.queryByRole("button", { name: "Guardar revisión" }),
        ).toBeNull();
    });
});
