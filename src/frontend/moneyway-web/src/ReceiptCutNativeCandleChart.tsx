import { useId, useRef, useState, type KeyboardEvent } from "react";
import type { Cut, Frame, Sample } from "./NasdaqReceiptCutExplorer";

const framePeriod: Record<Frame, number> = {
    H4: 14400,
    H1: 3600,
    M5: 300,
    M1: 60,
};
const preparationFrame: Record<Frame, string> = {
    H4: "4h",
    H1: "1h",
    M5: "5m",
    M1: "1m",
};
const pageSize = 48;
const priceFormatter = new Intl.NumberFormat("es-CO", {
    maximumFractionDigits: 6,
});

function reference(cut: Cut, sample: Sample) {
    return JSON.stringify(
        {
            providerId: cut.providerId,
            symbol: cut.diagnostic.expectedSource.nativeSymbol,
            timeframe: preparationFrame[sample.timeframe],
            originalTime: sample.openTimeNative.display,
            originalClockDomain: "MT5Native",
            timePrecision: "ExactNativeRecord",
            nativeOpenRaw: sample.openTimeNative.raw,
            cut: {
                id: cut.id,
                sequence: cut.diagnostic.cutReceipt.sequence,
                receiptSha256: cut.diagnostic.cutReceipt.sha256,
                reportSha256: cut.reportSha256,
            },
            firstUsableHistoricalReceipt: sample.firstUsableHistorical.receipt,
            sourcePosition: sample.firstUsableHistorical.sourcePosition,
        },
        null,
        2,
    );
}

export function ReceiptCutNativeCandleChart({
    cut,
    frame,
}: {
    cut: Cut;
    frame: Frame;
}) {
    const [page, setPage] = useState(0);
    const [selection, setSelection] = useState<number>();
    const [copyState, setCopyState] = useState<{
        nativeOpen: number;
        status: "copied" | "fallback";
    }>();
    const candleNodes = useRef<(SVGGElement | null)[]>([]);
    const warningId = useId();
    const samples = cut.diagnostic.samples
        .filter(
            (sample) =>
                sample.timeframe === frame &&
                sample.firstUsableHistorical.usable &&
                sample.firstUsableHistorical.sourcePosition > 0 &&
                sample.firstUsableHistorical.receipt.sequence <=
                    cut.diagnostic.cutReceipt.sequence,
        )
        .sort((a, b) => a.openTimeNative.raw - b.openTimeNative.raw);

    const offset = page * pageSize;
    const visible = samples.slice(offset, offset + pageSize);
    const selected = visible.find(
        (sample) => sample.openTimeNative.raw === selection,
    );
    const pages = Math.ceil(samples.length / pageSize);
    function pick(sample: Sample) {
        setSelection(sample.openTimeNative.raw);
        setCopyState(undefined);
    }
    function movePage(next: number) {
        setPage(next);
        setSelection(undefined);
        setCopyState(undefined);
    }
    function key(event: KeyboardEvent<SVGGElement>, index: number) {
        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            pick(visible[index]);
        } else if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
            event.preventDefault();
            const next = index + (event.key === "ArrowLeft" ? -1 : 1);
            if (visible[next]) {
                pick(visible[next]);
                candleNodes.current[next]?.focus();
            }
        }
    }
    async function copy(sample: Sample) {
        try {
            await navigator.clipboard.writeText(reference(cut, sample));
            setCopyState({
                nativeOpen: sample.openTimeNative.raw,
                status: "copied",
            });
        } catch {
            setCopyState({
                nativeOpen: sample.openTimeNative.raw,
                status: "fallback",
            });
        }
    }

    let units = 0;
    const points = visible.map((sample, index) => {
        const previous = samples[offset + index - 1];
        const gapSeconds = previous
            ? sample.openTimeNative.raw - previous.openTimeNative.raw
            : framePeriod[frame];
        const gap =
            gapSeconds !== framePeriod[frame]
                ? { previous, seconds: gapSeconds }
                : null;
        if (gap) units += 0.9;
        const value = sample.firstUsableHistorical.values;
        return {
            sample,
            gap,
            unit: units++,
            open: Number(value.open),
            close: Number(value.close),
            high: Number(value.high),
            low: Number(value.low),
        };
    });
    const low = Math.min(...points.map((bar) => bar.low));
    const high = Math.max(...points.map((bar) => bar.high));
    const top = 16;
    const bottom = 216;
    const y = (price: number) =>
        high === low
            ? (top + bottom) / 2
            : bottom - ((price - low) / (high - low)) * (bottom - top);
    const step = 690 / units;
    const x = (unit: number) => 84 + step * (unit + 0.5);
    const width = Math.min(24, step * 0.56);
    const ticks =
        high === low
            ? [low]
            : [
                  low,
                  low + (high - low) / 4,
                  (low + high) / 2,
                  high - (high - low) / 4,
                  high,
              ];
    const nativeLabels = new Set([
        0,
        Math.floor((points.length - 1) / 2),
        points.length - 1,
    ]);
    return (
        <section
            className="receipt-native-chart"
            aria-label={`Gráfico nativo ${frame} del corte ${cut.diagnostic.cutReceipt.sequence}`}
        >
            <p id={warningId}>Hora nativa MT5; conversión UTC no verificada</p>
            {points.length === 0 ? (
                <p>No hay velas históricas utilizables en este marco.</p>
            ) : (
                <>
                    <div className="receipt-native-chart-scroll">
                        <svg
                            viewBox="0 0 790 252"
                            role="group"
                            aria-label={`Velas OHLC ${frame}`}
                            aria-describedby={warningId}
                        >
                            {ticks.map((price) => (
                                <g key={price} aria-hidden="true">
                                    <line
                                        x1="80"
                                        x2="774"
                                        y1={y(price)}
                                        y2={y(price)}
                                        className="receipt-native-grid"
                                    />
                                    <text
                                        x="74"
                                        y={y(price)}
                                        textAnchor="end"
                                        dominantBaseline="middle"
                                        className="receipt-native-axis"
                                    >
                                        {priceFormatter.format(price)}
                                    </text>
                                </g>
                            ))}
                            {points.map(
                                (
                                    {
                                        sample,
                                        gap,
                                        unit,
                                        open,
                                        close,
                                        high: maximum,
                                        low: minimum,
                                    },
                                    index,
                                ) => {
                                    const center = x(unit);
                                    const active = sample === selected;
                                    const position = sample.openTimeNative;
                                    return (
                                        <g key={position.raw}>
                                            {gap && (
                                                <line
                                                    x1={center - step * 0.95}
                                                    x2={center - step * 0.95}
                                                    y1={top}
                                                    y2={bottom}
                                                    className="receipt-native-gap"
                                                    aria-hidden="true"
                                                />
                                            )}
                                            <g
                                                role="button"
                                                tabIndex={
                                                    active ||
                                                    (!selected && index === 0)
                                                        ? 0
                                                        : -1
                                                }
                                                aria-label={`Vela ${frame} ${position.display}`}
                                                aria-pressed={active}
                                                ref={(node) => {
                                                    candleNodes.current[index] =
                                                        node;
                                                }}
                                                className={`receipt-native-bar ${close < open ? "down" : "up"}`}
                                                data-native-open={position.raw}
                                                onClick={() => pick(sample)}
                                                onKeyDown={(event) =>
                                                    key(event, index)
                                                }
                                            >
                                                <rect
                                                    className="receipt-native-hit"
                                                    x={center - step * 0.46}
                                                    y={top - 3}
                                                    width={step * 0.92}
                                                    height={bottom - top + 6}
                                                />
                                                <line
                                                    className="receipt-native-wick"
                                                    x1={center}
                                                    x2={center}
                                                    y1={y(maximum)}
                                                    y2={y(minimum)}
                                                />
                                                <rect
                                                    className="receipt-native-body"
                                                    x={center - width / 2}
                                                    width={width}
                                                    y={Math.min(
                                                        y(open),
                                                        y(close),
                                                    )}
                                                    height={Math.max(
                                                        1,
                                                        Math.abs(
                                                            y(open) - y(close),
                                                        ),
                                                    )}
                                                />
                                            </g>
                                            {nativeLabels.has(index) && (
                                                <text
                                                    x={center}
                                                    y="236"
                                                    textAnchor={
                                                        index === 0
                                                            ? "start"
                                                            : index ===
                                                                points.length -
                                                                    1
                                                              ? "end"
                                                              : "middle"
                                                    }
                                                    className="receipt-native-axis"
                                                    aria-hidden="true"
                                                >
                                                    {position.display.slice(
                                                        5,
                                                        16,
                                                    )}
                                                </text>
                                            )}
                                        </g>
                                    );
                                },
                            )}
                        </svg>
                    </div>
                    <div className="receipt-native-nav">
                        <button
                            type="button"
                            disabled={page === 0}
                            onClick={() => movePage(page - 1)}
                        >
                            Velas anteriores
                        </button>
                        <span aria-live="polite">
                            {offset + 1}–{offset + points.length} de{" "}
                            {samples.length} velas {frame}
                        </span>
                        <button
                            type="button"
                            disabled={page + 1 >= pages}
                            onClick={() => movePage(page + 1)}
                        >
                            Velas siguientes
                        </button>
                    </div>
                    {points.some((point) => point.gap) && (
                        <ul aria-label="Discontinuidades visibles">
                            {points.map(
                                ({ sample, gap }) =>
                                    gap && (
                                        <li key={sample.openTimeNative.raw}>
                                            Discontinuidad {frame} ·{" "}
                                            {
                                                gap.previous.openTimeNative
                                                    .display
                                            }{" "}
                                            → {sample.openTimeNative.display} ·{" "}
                                            {gap.seconds} s nativos entre
                                            aperturas
                                        </li>
                                    ),
                            )}
                        </ul>
                    )}
                    {selected && (
                        <CandleDetail
                            sample={selected}
                            providerId={cut.providerId}
                            onCopy={() => void copy(selected)}
                        />
                    )}
                    {selected &&
                        copyState?.nativeOpen === selected.openTimeNative.raw &&
                        copyState.status === "copied" && (
                            <p role="status">Referencia nativa copiada.</p>
                        )}
                    {selected &&
                        copyState?.nativeOpen === selected.openTimeNative.raw &&
                        copyState.status === "fallback" && (
                            <label>
                                Referencia nativa para copiar
                                <textarea
                                    readOnly
                                    rows={9}
                                    value={reference(cut, selected)}
                                    onFocus={(event) =>
                                        event.currentTarget.select()
                                    }
                                />
                            </label>
                        )}
                </>
            )}
        </section>
    );
}

function CandleDetail({
    sample,
    providerId,
    onCopy,
}: {
    sample: Sample;
    providerId: string;
    onCopy: () => void;
}) {
    const { values, receipt, sourcePosition } = sample.firstUsableHistorical;
    return (
        <div
            className="receipt-native-detail"
            aria-label="Evidencia de la vela seleccionada"
        >
            <dl className="definition-summary">
                <div>
                    <dt>Vela {sample.timeframe}</dt>
                    <dd>
                        {sample.openTimeNative.display} · raw{" "}
                        {sample.openTimeNative.raw}
                    </dd>
                </div>
                {(["open", "high", "low", "close"] as const).map((field) => (
                    <div key={field}>
                        <dt>{field[0].toUpperCase() + field.slice(1)}</dt>
                        <dd>{values[field]}</dd>
                    </div>
                ))}
                <div>
                    <dt>Primer recibo histórico utilizable</dt>
                    <dd>
                        {receipt.sequence} · posición {sourcePosition}
                    </dd>
                </div>
                <div>
                    <dt>SHA-256 del recibo</dt>
                    <dd>
                        <code>{receipt.recordSha256}</code>
                    </dd>
                </div>
                <div>
                    <dt>SHA-256 de la sonda</dt>
                    <dd>
                        <code>{receipt.probeSha256}</code>
                    </dd>
                </div>
            </dl>
            {sample.spansInterruption && (
                <p>La muestra atraviesa una interrupción.</p>
            )}
            {sample.laterHistoricalRevisions > 0 && (
                <p>
                    Revisiones históricas posteriores:{" "}
                    {sample.laterHistoricalRevisions}. Se conserva la primera
                    observación utilizable.
                </p>
            )}
            <button type="button" onClick={onCopy} disabled={!providerId}>
                Copiar referencia nativa
            </button>
        </div>
    );
}
