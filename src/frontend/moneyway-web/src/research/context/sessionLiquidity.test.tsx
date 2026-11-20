import { webcrypto } from "node:crypto";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import rawContext from "./data/june23-context.json";
import rawText from "./data/june23-context.json?raw";
import { ContextualResearch } from "./ContextView";
import { loadContext, type ContextData } from "./model";
import {
    readSessionReport,
    sessionReferencesAt,
    sessionReport,
} from "./sessionLiquidity";

const context = rawContext as unknown as ContextData;
beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("crypto", webcrypto);
    vi.stubGlobal(
        "fetch",
        vi.fn(async () => new Response(rawText, { status: 200 })),
    );
});
afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
});

it.each([
    ["2026-06-23T06:59:00Z", false, false],
    ["2026-06-23T07:00:00Z", true, false],
    ["2026-06-23T12:59:00Z", true, false],
    ["2026-06-23T13:00:00Z", true, true],
])("gates completed documentary prices at %s", (cutoff, asia, london) => {
    const r = sessionReferencesAt(context, cutoff)!;
    expect(r.sessions[0].high !== null).toBe(asia);
    expect(r.sessions[1].high !== null).toBe(london);
    for (const s of r.sessions)
        for (const c of [...s.highCandles, ...s.lowCandles])
            expect(Date.parse(c.closeTimeUtc)).toBeLessThanOrEqual(
                Date.parse(cutoff),
            );
});
it("reads actual exact-H1 references and original price-source identities without TS extrema calculation", () => {
    const [asia, london] = sessionReferencesAt(
        context,
        "2026-06-23T13:00:00Z",
    )!.sessions;
    expect([asia.high, asia.low, london.high, london.low]).toEqual([
        30463.9, 29715.2, 29818.6, 29472.7,
    ]);
    expect([asia.sourceCount, london.sourceCount]).toEqual([9, 6]);
    expect(asia.highCandles.every((c) => c.timeframe.unit === "Hour")).toBe(
        true,
    );
    expect(sessionReport.canonicalAdmissionPermitted).toBe(false);
    expect(sessionReport.operationalSignal).toBe(false);
});
it.each(["version", "instrument", "provenance", "future", "partial", "scope"])(
    "fails closed on incompatible %s",
    (issue) => {
        const report = structuredClone(sessionReport);
        const frame = report.frames.at(-1)!;
        const s = frame.sessions[0];
        if (issue === "version")
            report.candidateVersion = "nasdaq-0.2.0-sc-candidate";
        if (issue === "instrument") s.instrument = "USTEC";
        if (issue === "provenance")
            report.sourceProvenance.rawSha256 = "0".repeat(64);
        if (issue === "future") s.endUtc = "2026-06-24T07:00:00Z";
        if (issue === "partial") s.sourceCount = 8;
        if (issue === "scope")
            report.canonicalAdmissionPermitted = true as false;
        expect(() => readSessionReport(context, report)).toThrow(
            "no disponibles",
        );
    },
);
it("fails closed if fetched source bytes differ from the report's original input binding", async () => {
    vi.stubGlobal(
        "fetch",
        vi.fn(
            async () =>
                new Response(JSON.stringify(rawContext), { status: 200 }),
        ),
    );
    await expect(loadContext()).rejects.toThrow("hash fuente incompatible");
});
it("never accepts a finalized reference for an incomplete window", () => {
    const report = structuredClone(sessionReport);
    report.frames[0].sessions[0].high = 30463.9;
    expect(() => readSessionReport(context, report)).toThrow();
    expect(() =>
        sessionReferencesAt(context, "2026-06-24T13:00:00Z"),
    ).toThrow();
    expect(JSON.stringify(sessionReport)).not.toMatch(
        /2026-06-25T|2026-06-30T/,
    );
});
it("changes the existing shared cutoff and removes future London prices and overlays", async () => {
    const { container } = render(<ContextualResearch />);
    const panel = await screen.findByRole("region", {
        name: "Liquidez de sesiones configurada",
    });
    expect(within(panel).getByText(/LH 29818.6/)).toBeTruthy();
    await userEvent.selectOptions(
        screen.getByLabelText("Cutoff compartido · Bogotá"),
        "2026-06-23T12:59:00Z",
    );
    expect(within(panel).queryByText(/LH 29818.6/)).toBeNull();
    expect(within(panel).getByText(/AH 30463.9/)).toBeTruthy();
    expect(
        container.querySelector('[data-context-reference^="London-"]'),
    ).toBeNull();
    await userEvent.selectOptions(
        screen.getByLabelText("Cutoff compartido · Bogotá"),
        "2026-06-23T06:59:00Z",
    );
    expect(within(panel).queryByText(/AH 30463.9/)).toBeNull();
    expect(
        container.querySelector('[data-context-reference^="Asia-"]'),
    ).toBeNull();
    await userEvent.selectOptions(
        screen.getByLabelText("Cutoff compartido · Bogotá"),
        "2026-06-23T13:00:00Z",
    );
    expect(within(panel).getByText(/LH 29818.6/)).toBeTruthy();
    expect(screen.getByText(/Comparación oculta/)).toBeTruthy();
});
