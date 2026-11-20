import { render, screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import raw from "./data/june23-context.json";
import type { ContextData } from "./model";
import {
    interactionReport,
    londonAsiaAt,
    readInteractionReport,
} from "./londonAsiaInteraction";
import { LondonAsiaSummary } from "./LondonAsiaSummary";

const data = raw as unknown as ContextData;
it.each([
    ["06:59", "AsiaUnavailable", 0],
    ["07:00", "Partial", 0],
    ["08:00", "Partial", 1],
    ["12:59", "Partial", 5],
    ["13:00", "Complete", 6],
])(
    "consumes only backend observations visible at %sZ",
    (time, status, count) => {
        const r = londonAsiaAt(data, `2026-06-23T${time}:00Z`)!;
        expect(r.status).toBe(status);
        expect(r.observations).toHaveLength(count);
        expect(r.completedClassification).toBe(
            status === "Complete" ? "AsiaLowOnlyPenetrated" : null,
        );
        for (const o of r.observations)
            expect(Date.parse(o.candle.closeTimeUtc)).toBeLessThanOrEqual(
                Date.parse(r.cutoffUtc),
            );
    },
);
it("preserves actual first lower penetration and close independently, descriptive magnitudes and provenance", () => {
    const r = londonAsiaAt(data, "2026-06-23T13:00:00Z")!;
    expect(r.completedLowerMagnitude).toBe(242.5);
    expect(r.completedUpperMagnitude).toBe(0);
    expect(r.firstLowerPenetration!.candle.low).toBe(29653.8);
    expect(r.firstLowerCloseBeyond!.candle.close).toBe(29672.6);
    expect(Date.parse(r.firstLowerPenetration!.candle.openTimeUtc)).toBe(
        Date.parse("2026-06-23T07:00:00Z"),
    );
    expect(r.firstUpperPenetration).toBeNull();
    expect(r.firstUpperExactContact).toBeNull();
    expect(r.canonicalAdmissionPermitted).toBe(false);
    expect(r.operationalSignal).toBe(false);
});
it.each([
    "source",
    "instrument",
    "timeframe",
    "scope",
    "future",
    "premature",
    "first",
    "close-identity",
    "category",
])("fails closed for %s corruption", (issue) => {
    const report = structuredClone(interactionReport);
    const r = report.frames.at(-1)!.interaction;
    if (issue === "source") report.inputSha256 = "0".repeat(64);
    if (issue === "instrument") r.instrument = "USTEC";
    if (issue === "timeframe") r.sourceTimeframe = "5m";
    if (issue === "scope") r.operationalSignal = true as false;
    if (issue === "future")
        r.observations[0].candle.closeTimeUtc = "2026-06-24T08:00:00Z";
    if (issue === "premature")
        report.frames[0].interaction.completedClassification =
            "NoAsiaExtremePenetrated";
    if (issue === "first") r.firstLowerPenetration!.candle.low = 1;
    if (issue === "close-identity")
        r.observations[0].candle.closeTimeUtc = "2026-06-23T07:30:00Z";
    if (issue === "category") r.completedClassification = "SELL";
    expect(() => readInteractionReport(data, report)).toThrow("no disponible");
});
it("removes final classification and later observed magnitudes when the existing view moves backward", () => {
    const view = render(
        <LondonAsiaSummary context={data} cutoff="2026-06-23T13:00:00Z" />,
    );
    const panel = screen.getByRole("region", {
        name: "Interacción London–Asia documental",
    });
    expect(within(panel).getByText(/AsiaLowOnlyPenetrated/)).toBeTruthy();
    expect(within(panel).getByText("242.5")).toBeTruthy();
    view.rerender(
        <LondonAsiaSummary context={data} cutoff="2026-06-23T08:00:00Z" />,
    );
    expect(within(panel).queryByText(/AsiaLowOnlyPenetrated/)).toBeNull();
    expect(within(panel).queryByText("242.5")).toBeNull();
    expect(within(panel).getByText("61.4")).toBeTruthy();
    view.rerender(
        <LondonAsiaSummary context={data} cutoff="2026-06-23T07:00:00Z" />,
    );
    expect(within(panel).queryByText("61.4")).toBeNull();
    expect(within(panel).queryByText(/29653.8/)).toBeNull();
    expect(within(panel).getByText(/Sin dirección BUY\/SELL/)).toBeTruthy();
});
it("keeps reserved datasets absent and unsupported cutoffs unavailable", () => {
    expect(() => londonAsiaAt(data, "2026-06-24T13:00:00Z")).toThrow();
    expect(JSON.stringify(interactionReport)).not.toMatch(
        /2026-06-25T|2026-06-30T/,
    );
});
