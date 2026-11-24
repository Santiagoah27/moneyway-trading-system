import { webcrypto } from "node:crypto";
import { readFileSync } from "node:fs";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { projectDevelopment } from "./originalPackage";
import { sessionOverlays } from "./sessionOverlays";

const at = (hhmm: string) => "2026-06-23T" + hhmm + ":00Z";
const packet = () =>
    projectDevelopment(
        readFileSync("src/research/context/data/june23-context.json", "utf8"),
    );

beforeEach(() => {
    vi.stubGlobal("crypto", webcrypto);
});
afterEach(() => {
    vi.unstubAllGlobals();
});

describe("sesiones documentales H1", () => {
    it("calcula los extremos desde las velas originales y conserva su procedencia", async () => {
        const p = await packet();
        const [asia, london] = sessionOverlays(p, at("16:30"));
        expect(asia).toMatchObject({
            startUtc: "2026-06-22T22:00:00.000Z",
            endUtc: "2026-06-23T07:00:00.000Z",
            observed: 9,
            status: "Complete",
        });
        expect(london).toMatchObject({
            startUtc: asia.endUtc,
            endUtc: "2026-06-23T13:00:00.000Z",
            observed: 6,
            status: "Complete",
        });
        const levels = [...asia.levels, ...london.levels];
        expect(levels.map(({ code, price }) => [code, price])).toEqual([
            ["AH", 30463.9],
            ["AL", 29715.2],
            ["LH", 29818.6],
            ["LL", 29472.7],
        ]);
        expect(
            levels.map(({ sources }) => sources.map((b) => b.openUtc)),
        ).toEqual(
            ["2026-06-22T22:00:00Z", at("06:00"), at("07:00"), at("12:00")].map(
                (open) => [
                    p.sources.H1.bars.find(
                        (b) => Date.parse(b.openUtc) === Date.parse(open),
                    )!.openUtc,
                ],
            ),
        );
        expect(london).toMatchObject({
            provider: p.provider,
            instrument: "US100",
            operatingDate: p.operatingDate,
            sourceSha256: p.sources.H1.originalSha256,
            cutoffUtc: at("16:30"),
            timeframe: "H1",
        });
    });

    it.each([
        ["06:59", [0, 0]],
        ["07:00", [2, 0]],
        ["12:59", [2, 0]],
        ["13:00", [2, 2]],
    ])("corte %s solo publica sesiones completas", async (time, counts) => {
        const p = await packet();
        const cutoff = at(time);
        const before = sessionOverlays(p, cutoff);
        expect(before.map((s) => s.levels.length)).toEqual(counts);
        p.sources.H1.bars = p.sources.H1.bars.map((b) =>
            Date.parse(b.closeUtc) > Date.parse(cutoff)
                ? { ...b, high: b.high + 1000, low: b.low - 1000 }
                : b,
        );
        expect(sessionOverlays(p, cutoff)).toEqual(before);
    });

    it("señala brechas, velas de otra duración y mapeo no utilizable", async () => {
        const p = await packet();
        const source = p.sources.H1;
        source.bars = source.bars
            .filter((b) => Date.parse(b.openUtc) !== Date.parse(at("00:00")))
            .map((b) =>
                Date.parse(b.openUtc) === Date.parse(at("07:00"))
                    ? { ...b, closeUtc: at("07:30") }
                    : b,
            );
        const windows = sessionOverlays(p, at("16:30"));
        expect(windows.map((s) => s.status)).toEqual([
            "Incomplete",
            "Incomplete",
        ]);
        expect(windows.flatMap((s) => s.levels)).toEqual([]);
        source.mapping = "Unresolved";
        expect(sessionOverlays(p, at("16:30")).map((s) => s.status)).toEqual([
            "MappingUnavailable",
            "MappingUnavailable",
        ]);
    });

    it("retiene todas las velas originales empatadas en un extremo", async () => {
        const p = await packet();
        p.sources.H1.bars = p.sources.H1.bars.map((b) =>
            Date.parse(b.openUtc) === Date.parse(at("08:00"))
                ? { ...b, high: 29818.6, low: 29472.7 }
                : b,
        );
        const london = sessionOverlays(p, at("16:30"))[1];
        expect(
            london.levels.map(({ sources }) =>
                sources.map((s) => s.openUtc).map(Date.parse),
            ),
        ).toEqual([
            [at("07:00"), at("08:00")].map(Date.parse),
            [at("08:00"), at("12:00")].map(Date.parse),
        ]);
    });

    it.each(["2026-11-23", "2026-11-24"])(
        "no proyecta paquetes reservados %s",
        async (day) => {
            const p = await packet();
            expect(
                sessionOverlays({ ...p, operatingDate: day }, at("16:30")),
            ).toEqual([]);
            expect(
                sessionOverlays(
                    { ...p, provenanceSha256: "0".repeat(64) },
                    at("16:30"),
                ),
            ).toEqual([]);
        },
    );
});
