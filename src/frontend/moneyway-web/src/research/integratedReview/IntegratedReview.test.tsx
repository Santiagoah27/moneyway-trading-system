import { webcrypto } from "node:crypto";
import { readFileSync } from "node:fs";
import { fireEvent, render, screen, within } from "@testing-library/react";
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
    persistReview,
    reference,
} from "./reviewHistory";
import {
    appendEvent,
    exportEventReview,
    groupEvents,
    importEventReview,
    loadEventReview,
    startEventReview,
    validateEventReview,
    type EventDraft,
} from "./eventReview";

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

const v2 = "moneyway.nasdaq-integrated-human-review.v2";
function draft(
    p: OriginalPackage,
    observation: EventDraft["observation"],
    index = 0,
): EventDraft {
    const tf = observation === "M1Realignment" ? "M1" : "M5";
    const bar = p.sources[tf].bars[index];
    return {
        observation,
        kind: "Reference",
        references: [reference(p, tf, bar.openUtc)],
        note: "Juicio documental",
        viewingCutoffUtc: p.cutoffUtc,
        scopeStartUtc: null,
    };
}

describe("contrato de eventos V2", () => {
    it("mantiene instancias separadas incluso si comparten tipo, precio y vela", async () => {
        const p = await packet();
        let review = startEventReview(p, "QA", "Unknown", p.cutoffUtc, now);
        for (const type of [
            "LiquidityTake",
            "M5SC",
            "M5FVG",
            "M1Realignment",
        ] as const) {
            review = appendEvent(p, review, null, draft(p, type), now);
            review = appendEvent(p, review, null, draft(p, type), now);
        }
        expect(
            [...groupEvents(review.labels)].map(([id, labels]) => [
                id,
                labels.length,
            ]),
        ).toEqual(
            Array.from({ length: 8 }, (_, i) => [
                `synthetic-us100-v1/event/${i + 1}`,
                1,
            ]),
        );
        const a = review.labels[0].targetId;
        const originalB = review.labels[1];
        review = appendEvent(
            p,
            review,
            a,
            { ...draft(p, "LiquidityTake", 1), note: "Corrección de A" },
            now,
        );
        review = appendEvent(
            p,
            review,
            a,
            {
                ...draft(p, "LiquidityTake", 2),
                note: "Segunda corrección de A",
            },
            now,
        );
        expect(
            groupEvents(review.labels)
                .get(a)
                ?.map((label) => [label.revision, label.eventRevision]),
        ).toEqual([
            [1, 1],
            [9, 2],
            [10, 3],
        ]);
        expect(groupEvents(review.labels).get(originalB.targetId)).toEqual([
            originalB,
        ]);
        expect(review.labels[2]).not.toHaveProperty("antecedentTargetId");
        expect(review.operationalSignal).toBe(false);
        expect(review.canonicalAdmissionPermitted).toBe(false);
        const serialized = await exportEventReview(p, review);
        expect(await importEventReview(p, null, serialized)).toEqual(review);
        await expect(
            importEventReview(
                p,
                review,
                await exportEventReview(p, {
                    ...review,
                    labels: review.labels.slice(0, 8),
                }),
            ),
        ).rejects.toThrow(/sustituiría/);
        await expect(
            importEventReview(
                p,
                null,
                serialized.replace(/"sha256":"./, '"sha256":"Z'),
            ),
        ).rejects.toThrow();
    });

    it("rechaza identidades duplicadas, tipos contradictorios y referencias manipuladas aun con hash correcto", async () => {
        const p = await packet();
        const root = startEventReview(
            p,
            "QA",
            "PreviouslyExposed",
            p.cutoffUtc,
            now,
        );
        const first = appendEvent(
            p,
            root,
            null,
            draft(p, "LiquidityTake"),
            now,
        );
        const next = appendEvent(p, first, null, draft(p, "M5SC"), now);
        const [a, b] = next.labels;
        const cases = [
            { ...b, targetId: a.targetId, eventNumber: 1 },
            { ...b, targetId: a.targetId, eventNumber: 1, eventRevision: 2 },
            { ...b, eventRevision: 3 },
            { ...b, targetId: "canonical/setup/4" },
            {
                ...b,
                references: [
                    { ...b.references[0], originalSha256: "f".repeat(64) },
                ],
            },
            {
                ...b,
                viewingCutoffUtc: "2000-01-03T12:04:00Z",
                reviewMode: "RetrospectiveAfterLaterView",
            },
            { ...b, antecedentTargetId: a.targetId },
        ];
        for (const modified of cases)
            await expect(
                importEventReview(
                    p,
                    null,
                    await seal(v2, { ...next, labels: [a, modified] }),
                ),
            ).rejects.toThrow();
        expect(a.phase).toBe("PostRevealCorrection");
        expect(() =>
            appendEvent(p, next, a.targetId, draft(p, "M5SC"), now),
        ).toThrow(/conservar su tipo/);
    });

    it("registra una revisión retrospectiva y exige un alcance explícito para la ausencia", async () => {
        const p = await packet();
        const review = startEventReview(p, "QA", "Unknown", p.cutoffUtc, now);
        const absent: EventDraft = {
            ...draft(p, "LiquidityTake"),
            kind: "NoReference",
            references: [],
            viewingCutoffUtc: "2000-01-03T12:05:00Z",
        };
        expect(() => appendEvent(p, review, null, absent, now)).toThrow(
            /alcance explícito/,
        );
        const recorded = appendEvent(
            p,
            review,
            null,
            { ...absent, scopeStartUtc: "2000-01-03T12:00:00Z" },
            now,
        );
        expect(recorded.labels[0]).toMatchObject({
            reviewedAtUtc: now,
            maximumViewedUtc: p.cutoffUtc,
            reviewMode: "RetrospectiveAfterLaterView",
        });
        expect(() =>
            validateEventReview(p, {
                ...recorded,
                labels: [
                    { ...recorded.labels[0], reviewMode: "AtViewingFrontier" },
                ],
            }),
        ).toThrow(/datos posteriores/);
        for (const kind of [
            "Ambiguous",
            "InsufficientSourceEvidence",
            "Skip",
        ] as const) {
            expect(
                appendEvent(
                    p,
                    recorded,
                    null,
                    { ...absent, kind },
                    now,
                ).labels.at(-1)?.references,
            ).toEqual([]);
        }
    });
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

async function note(text: string, action = "Guardar evento") {
    const input = screen.getByLabelText("Evidencia o motivo");
    await userEvent.clear(input);
    await userEvent.type(input, text);
    await userEvent.click(screen.getByRole("button", { name: action }));
}

async function newEvent() {
    await userEvent.click(screen.getByRole("button", { name: "Nuevo evento" }));
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
        await newEvent();
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
        expect(loadEventReview(p)?.labels[0]).toMatchObject({
            targetId: "synthetic-us100-v1/event/1",
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
        expect(loadEventReview(p)?.maximumViewedUtc).toBe(
            "2000-01-03T20:00:00.000Z",
        );
    });

    it("guarda ausencia y ambigüedad, persiste al recargar y conserva el juicio tras exposición externa", async () => {
        const view = render(<IntegratedReview />);
        await openCase();
        await begin("Unknown");
        await newEvent();
        await userEvent.selectOptions(
            screen.getByLabelText("Clasificación"),
            "NoReference",
        );
        await userEvent.selectOptions(
            screen.getByLabelText("Inicio del alcance"),
            "2000-01-03T12:00:00.000Z",
        );
        await note("Sin referencia observable.");
        await userEvent.click(
            screen.getByRole("button", { name: /Registrar que vi resultados/ }),
        );
        await userEvent.click(
            screen.getByRole("button", { name: "Editar evento seleccionado" }),
        );
        await userEvent.selectOptions(
            screen.getByLabelText("Clasificación"),
            "Ambiguous",
        );
        await note("Dos lecturas posibles.", "Guardar corrección");
        const p = await packet();
        const saved = loadEventReview(p)!;
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
        await userEvent.selectOptions(
            screen.getByLabelText("Evento registrado"),
            saved.labels[0].targetId,
        );
        expect(screen.getByText(/Sin referencia observable/)).toBeDefined();
        expect(screen.getByText(/Dos lecturas posibles/)).toBeDefined();
    });

    it("mantiene archivo y almacenamiento V1 en solo lectura y rechaza su migración implícita", async () => {
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
            screen.getByLabelText("Importar etiquetas JSON V2"),
            new File([exportText], "review.json", { type: "application/json" }),
        );
        expect((await screen.findByRole("alert")).textContent).toMatch(
            /Esquema de revisión/,
        );
        expect(loadEventReview(p)).toBeNull();
        await userEvent.click(screen.getByText("Historial V1 · solo lectura"));
        await userEvent.upload(
            screen.getByLabelText("Consultar archivo de etiquetas V1"),
            new File([exportText], "review.json", { type: "application/json" }),
        );
        await screen.findByText(/Cobertura insuficiente/);
        expect(loadReview(p)).toBeNull();
        persistReview(p, null, first);
        const legacyKey = Object.keys(localStorage).find((key) =>
            key.startsWith("moneyway.nasdaq-integrated-human-review.v1:"),
        )!;
        const originalBytes = localStorage.getItem(legacyKey);
        await userEvent.click(
            screen.getByRole("button", {
                name: "Iniciar revisión V2 con la declaración V1",
            }),
        );
        expect(loadEventReview(p)).toMatchObject({
            labels: [],
            exposure: "PreviouslyExposed",
            maximumViewedUtc: p.cutoffUtc,
            reveal: { afterRevision: 0 },
        });
        expect(loadReview(p)).toEqual(first);
        expect(localStorage.getItem(legacyKey)).toBe(originalBytes);
    });

    it("restaura un archivo V2 y distingue dos eventos del mismo tipo en el selector", async () => {
        const p = await packet();
        const empty = startEventReview(p, "QA", "Unknown", p.cutoffUtc, now);
        const a = appendEvent(
            p,
            empty,
            null,
            { ...draft(p, "LiquidityTake"), note: "Toma original A" },
            now,
        );
        const both = appendEvent(
            p,
            a,
            null,
            { ...draft(p, "LiquidityTake"), note: "Toma original B" },
            now,
        );
        render(<IntegratedReview />);
        await openCase();
        await userEvent.upload(
            screen.getByLabelText("Importar etiquetas JSON V2"),
            new File([await exportEventReview(p, both)], "review-v2.json", {
                type: "application/json",
            }),
        );
        await screen.findByRole("button", { name: "Nuevo evento" });
        await userEvent.click(
            screen.getByRole("button", { name: "Corte final" }),
        );
        const select = screen.getByLabelText("Evento registrado");
        await userEvent.selectOptions(select, both.labels[0].targetId);
        expect(screen.getByText(/Toma original A/)).toBeDefined();
        expect(screen.queryByText(/Toma original B/)).toBeNull();
        await userEvent.click(
            screen.getByRole("button", { name: "Editar evento seleccionado" }),
        );
        await note("Toma A corregida", "Guardar corrección");
        await userEvent.selectOptions(select, both.labels[1].targetId);
        expect(screen.getByText(/Toma original B/)).toBeDefined();
        expect(screen.queryByText(/Toma A corregida/)).toBeNull();
        expect(
            loadEventReview(p)?.labels.map((label) => [
                label.eventNumber,
                label.eventRevision,
            ]),
        ).toEqual([
            [1, 1],
            [2, 1],
            [1, 2],
        ]);
    });

    it("recorre el Development fijado por la interfaz y corrige A sin modificar B", async () => {
        const original = readFileSync(
            "src/research/context/data/june23-context.json",
            "utf8",
        );
        const fetchSource = vi.fn(async () => ({
            ok: true,
            text: async () => original,
        }));
        vi.stubGlobal("fetch", fetchSource);
        render(<IntegratedReview />);
        fireEvent.click(
            screen.getByRole("button", {
                name: "Abrir Development del 23 de junio",
            }),
        );
        await screen.findByRole("heading", {
            name: /2026-06-23.*Development expuesto/,
        });
        fireEvent.change(screen.getByLabelText("Revisor"), {
            target: { value: "QA Development" },
        });
        fireEvent.change(screen.getByLabelText("Exposición declarada"), {
            target: { value: "PreviouslyExposed" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Iniciar revisión" }),
        );
        fireEvent.click(screen.getByRole("button", { name: "Corte final" }));
        for (const frame of frames) {
            fireEvent.change(screen.getByLabelText("Temporalidad"), {
                target: { value: frame },
            });
            expect(
                screen.getByRole("img", { name: "Velas OHLC " + frame }),
            ).toBeDefined();
        }
        for (const name of ["Toma A Development", "Toma B Development"]) {
            fireEvent.click(
                screen.getByRole("button", { name: "Nuevo evento" }),
            );
            fireEvent.change(screen.getByLabelText("Observación"), {
                target: { value: "LiquidityTake" },
            });
            fireEvent.click(
                screen
                    .getAllByRole("button", { name: /Inspeccionar vela/ })
                    .at(-1)!,
            );
            fireEvent.change(screen.getByLabelText("Evidencia o motivo"), {
                target: { value: name },
            });
            fireEvent.click(
                screen.getByRole("button", { name: "Guardar evento" }),
            );
        }
        const p = await projectDevelopment(original);
        const [first, second] = loadEventReview(p)!.labels;
        fireEvent.change(screen.getByLabelText("Evento registrado"), {
            target: { value: first.targetId },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Editar evento seleccionado" }),
        );
        fireEvent.change(screen.getByLabelText("Evidencia o motivo"), {
            target: { value: "Corrección A Development" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Guardar corrección" }),
        );
        expect(loadEventReview(p)).toMatchObject({
            sessionExposure: "Development",
            exposure: "PreviouslyExposed",
            labels: [
                first,
                second,
                {
                    targetId: first.targetId,
                    eventRevision: 2,
                    note: "Corrección A Development",
                },
            ],
        });
        expect(fetchSource).toHaveBeenCalledExactlyOnceWith(
            expect.stringContaining("june23-context.json"),
        );
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
            screen.queryByRole("button", { name: "Nuevo evento" }),
        ).toBeNull();
    });
});
