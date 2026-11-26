import {
    cleanup,
    fireEvent,
    render,
    screen,
    within,
} from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import type { Cut, Sample } from "./NasdaqReceiptCutExplorer";
import { ReceiptCutNativeCandleChart } from "./ReceiptCutNativeCandleChart";

const opening = { raw: 1795561200, display: "2026.11.24 23:00:00" };
const sample: Sample = {
    timeframe: "H1",
    openTimeNative: opening,
    localReceiptClockLabel: null,
    laterHistoricalRevisions: 1,
    spansInterruption: true,
    firstUsableHistorical: {
        usable: true,
        sourcePosition: 1,
        receipt: {
            sequence: 4911,
            recordSha256: "a".repeat(64),
            probeSha256: "b".repeat(64),
        },
        values: {
            open: "26194.9000000000014552",
            high: "26298.9000000000014552",
            low: "26118.4000000000014552",
            close: "26129.7999999999992725",
        },
    },
};
const cut = {
    id: "nov25_5094",
    reportSha256: "c".repeat(64),
    providerId: "oanda-global-markets-mt5-demo",
    diagnostic: {
        expectedSource: { nativeSymbol: "US100" },
        cutReceipt: { sequence: 5094, sha256: "d".repeat(64) },
        samples: [
            {
                ...sample,
                openTimeNative: {
                    raw: opening.raw + 7200,
                    display: "2026.11.25 01:00:00",
                },
            },
            sample,
            { ...sample, timeframe: "H4" },
            {
                ...sample,
                openTimeNative: {
                    raw: opening.raw + 10800,
                    display: "2026.11.25 02:00:00",
                },
                firstUsableHistorical: {
                    ...sample.firstUsableHistorical,
                    sourcePosition: 0,
                },
            },
            {
                ...sample,
                openTimeNative: {
                    raw: opening.raw + 14400,
                    display: "2026.11.25 03:00:00",
                },
                firstUsableHistorical: {
                    ...sample.firstUsableHistorical,
                    receipt: {
                        ...sample.firstUsableHistorical.receipt,
                        sequence: 5095,
                    },
                },
            },
            {
                ...sample,
                openTimeNative: {
                    raw: opening.raw + 18000,
                    display: "2026.11.25 04:00:00",
                },
                firstUsableHistorical: {
                    ...sample.firstUsableHistorical,
                    usable: false,
                },
            },
        ],
    },
} as unknown as Cut;

afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
});

it("plots only usable bounded samples in native order and keeps the observed gap", () => {
    const parse = vi.spyOn(Date, "parse");
    render(<ReceiptCutNativeCandleChart cut={cut} frame="H1" />);
    const group = screen.getByRole("group", { name: "Velas OHLC H1" });
    const bars = within(group).getAllByRole("button");
    expect(bars.map((bar) => bar.getAttribute("data-native-open"))).toEqual([
        String(opening.raw),
        String(opening.raw + 7200),
    ]);
    expect(bars[0].querySelector("line")).toBeTruthy();
    expect(
        Number(
            bars[0]
                .querySelector(".receipt-native-body")
                ?.getAttribute("height"),
        ),
    ).toBeGreaterThan(0);
    expect(screen.getByText(/7200 s nativos entre aperturas/)).toBeTruthy();
    expect(
        screen.getByText("Hora nativa MT5; conversión UTC no verificada"),
    ).toBeTruthy();
    expect(parse).not.toHaveBeenCalled();
});

it("shows exact OHLC and copies provenance without submitting evidence", async () => {
    const writeText = vi
        .fn<(text: string) => Promise<void>>()
        .mockResolvedValue(undefined);
    const previous = Object.getOwnPropertyDescriptor(navigator, "clipboard");
    Object.defineProperty(navigator, "clipboard", {
        configurable: true,
        value: { writeText },
    });
    try {
        render(<ReceiptCutNativeCandleChart cut={cut} frame="H1" />);
        fireEvent.keyDown(
            screen.getByRole("button", { name: `Vela H1 ${opening.display}` }),
            { key: "Enter" },
        );
        const detail = screen.getByLabelText(
            "Evidencia de la vela seleccionada",
        );
        Object.values(sample.firstUsableHistorical.values).forEach((value) =>
            expect(within(detail).getByText(value)).toBeTruthy(),
        );
        expect(within(detail).getByText(/4911 · posición 1/)).toBeTruthy();
        expect(within(detail).getByText("a".repeat(64))).toBeTruthy();
        expect(within(detail).getByText("b".repeat(64))).toBeTruthy();
        expect(
            within(detail).getByText(/atraviesa una interrupción/),
        ).toBeTruthy();
        expect(
            within(detail).getByText(/Revisiones históricas posteriores: 1/),
        ).toBeTruthy();
        fireEvent.click(
            within(detail).getByRole("button", {
                name: "Copiar referencia nativa",
            }),
        );
        await screen.findByRole("status");
        const [json] = writeText.mock.calls[0];
        expect(JSON.parse(json)).toEqual({
            providerId: cut.providerId,
            symbol: "US100",
            timeframe: "1h",
            originalTime: opening.display,
            originalClockDomain: "MT5Native",
            timePrecision: "ExactNativeRecord",
            nativeOpenRaw: opening.raw,
            cut: {
                id: cut.id,
                sequence: 5094,
                receiptSha256: cut.diagnostic.cutReceipt.sha256,
                reportSha256: cut.reportSha256,
            },
            firstUsableHistoricalReceipt: sample.firstUsableHistorical.receipt,
            sourcePosition: 1,
        });
        expect(
            screen.queryByRole("button", {
                name: /guardar|aprobar|veredicto|orden|entrada/i,
            }),
        ).toBeNull();
    } finally {
        if (previous) Object.defineProperty(navigator, "clipboard", previous);
        else Reflect.deleteProperty(navigator, "clipboard");
    }
});
