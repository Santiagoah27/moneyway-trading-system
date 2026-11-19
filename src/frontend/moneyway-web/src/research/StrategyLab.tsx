import { useEffect, useState } from "react";
import { episodes, loadEpisode } from "./episodes";
import {
    algorithmIds,
    formatTime,
    type Candle,
    type Envelope,
    type Report,
    type Context,
} from "./report";
import "./StrategyLab.css";
import nativeRows from "./reports/june29-native-source.tsv?raw";

const colors = ["#57d8ef", "#bda0ff", "#ffb86b", "#ee8dbb"];
const names = [
    "Candle Color",
    "Price Structure",
    "Combined Strict",
    "Combined Flexible",
];
const letter = (id: string) => id.split("-")[3];
const number = (v: number | null) =>
    v === null
        ? "—"
        : v.toLocaleString("en-US", {
              maximumFractionDigits: 20,
              useGrouping: false,
          });

export type ChartReference = {
    id: string;
    label: string;
    price: number;
    formationUtc: string;
    knownAtUtc: string;
    color: string;
    sourceTimeframe?: string;
};
export type ChartGuide = { timeUtc: string; label: string };
type ChartContext = Pick<Context, "symbol" | "evaluationCutoffUtc"> &
    Partial<
        Pick<
            Context,
            "timeframe" | "direction" | "initialization" | "initialImpulsePrice"
        >
    >;

export function CandleChart({
    report,
    visible,
    selected,
    onSelect,
    onPick,
    humanReferences = [],
    humanConfirmation,
    references = [],
    guides = [],
}: {
    report: {
        input: ChartContext;
        evidenceCandles: Candle[];
        results: Report["results"];
    };
    visible: string[];
    selected: number;
    onSelect: (i: number) => void;
    onPick?: (i: number) => void;
    humanReferences?: string[];
    humanConfirmation?: string | null;
    references?: ChartReference[];
    guides?: ChartGuide[];
}) {
    const bars = report.evidenceCandles;
    if (!bars.length)
        return <p role="status">Sin velas visibles en esta selección.</p>;
    const first = Date.parse(bars[0].openTimeUtc);
    const last = Math.max(
        Date.parse(report.input.evaluationCutoffUtc),
        Date.parse(bars[bars.length - 1].closeTimeUtc),
    );
    const eligibleReferences = references.filter(
        (r) =>
            Date.parse(r.formationUtc) <=
                Date.parse(report.input.evaluationCutoffUtc) &&
            Date.parse(r.knownAtUtc) <=
                Date.parse(report.input.evaluationCutoffUtc),
    );
    const min = Math.min(
        ...bars.map((b) => b.low),
        ...eligibleReferences.map((r) => r.price),
    );
    const max = Math.max(
        ...bars.map((b) => b.high),
        ...eligibleReferences.map((r) => r.price),
    );
    const padding = Math.max((max - min) * 0.19, Math.abs(max) * 0.0001, 0.01);
    const floor = min - padding;
    const ceiling = max + padding;
    const x = (t: string | number) =>
        38 +
        (((typeof t === "number" ? t : Date.parse(t)) - first) /
            Math.max(last - first, 300000)) *
            1040;
    const y = (p: number) => 410 - ((p - floor) / (ceiling - floor)) * 360;
    const center = (b: Candle) =>
        x((Date.parse(b.openTimeUtc) + Date.parse(b.closeTimeUtc)) / 2);
    const spacing =
        ((Date.parse(bars[0].closeTimeUtc) - Date.parse(bars[0].openTimeUtc)) /
            Math.max(last - first, 300000)) *
        1040;
    const width = Math.max(0.25, Math.min(36, spacing * 0.58));
    const hitWidth = Math.max(
        0.25,
        Math.min(spacing * 0.96, Math.max(12, width * 2)),
    );
    const active = report.results.filter((r) =>
        visible.includes(r.algorithm.algorithmId),
    );
    const levels = new Map<number, string[]>();
    for (const row of active)
        if (row.proposedReferencePrice !== null)
            levels.set(row.proposedReferencePrice, [
                ...(levels.get(row.proposedReferencePrice) ?? []),
                letter(row.algorithm.algorithmId),
            ]);
    const anchor = bars.find(
        (b) => b.openTimeUtc === report.input.initialization?.anchorOpenTimeUtc,
    );
    return (
        <div
            className="lab-chart-scroll"
            tabIndex={0}
            aria-label="Gráfico desplazable horizontalmente"
        >
            <svg
                className="lab-chart"
                viewBox="0 0 1200 474"
                role="img"
                aria-label={`Velas OHLC ${report.input.timeframe ?? "M5"} y referencias experimentales`}
            >
                <title>
                    OHLC de {report.input.symbol}; horas UTC. Referencias
                    retrospectivas, no señales.
                </title>
                {[0, 1, 2, 3, 4].map((i) => {
                    const p = floor + ((ceiling - floor) * i) / 4;
                    return (
                        <g key={i}>
                            <line
                                x1={38}
                                x2={1078}
                                y1={y(p)}
                                y2={y(p)}
                                className="lab-grid"
                            />
                            <text x={1090} y={y(p) + 4} className="lab-axis">
                                {p.toFixed(2)}
                            </text>
                        </g>
                    );
                })}
                {bars.map((bar, i) => (
                    <g key={bar.openTimeUtc} data-candle={bar.openTimeUtc}>
                        {i === selected && (
                            <rect
                                x={center(bar) - width}
                                y={30}
                                width={width * 2}
                                height={390}
                                className="lab-selection"
                            />
                        )}
                        <line
                            data-testid="wick"
                            x1={center(bar)}
                            x2={center(bar)}
                            y1={y(bar.high)}
                            y2={y(bar.low)}
                            stroke={
                                bar.close >= bar.open ? "#58d7b1" : "#f18482"
                            }
                        />
                        <rect
                            data-testid="body"
                            x={center(bar) - width / 2}
                            y={y(Math.max(bar.open, bar.close))}
                            width={width}
                            height={Math.max(
                                1,
                                Math.abs(y(bar.open) - y(bar.close)),
                            )}
                            fill={bar.close >= bar.open ? "#58d7b1" : "#f18482"}
                        />
                        <rect
                            x={center(bar) - hitWidth / 2}
                            y={30}
                            width={hitWidth}
                            height={390}
                            fill="transparent"
                            tabIndex={0}
                            role="button"
                            aria-label={`Inspeccionar vela ${formatTime(bar.openTimeUtc)}`}
                            onFocus={() => onSelect(i)}
                            onMouseEnter={() => onSelect(i)}
                            onClick={() => {
                                onSelect(i);
                                onPick?.(i);
                            }}
                            onKeyDown={(e) => {
                                if (e.key === "Enter" || e.key === " ") {
                                    e.preventDefault();
                                    onSelect(i);
                                    onPick?.(i);
                                }
                                if (e.key === "ArrowRight") {
                                    e.preventDefault();
                                    onSelect(
                                        Math.min(bars.length - 1, selected + 1),
                                    );
                                }
                                if (e.key === "ArrowLeft") {
                                    e.preventDefault();
                                    onSelect(Math.max(0, selected - 1));
                                }
                            }}
                        >
                            <title>
                                {formatTime(bar.openTimeUtc)} · O {bar.open} H{" "}
                                {bar.high} L {bar.low} C {bar.close}
                            </title>
                        </rect>
                    </g>
                ))}
                {anchor && (
                    <g className="lab-overlay" data-anchor={anchor.openTimeUtc}>
                        <line
                            x1={center(anchor)}
                            x2={center(anchor)}
                            y1={20}
                            y2={420}
                            stroke="#e0e8f3"
                            strokeDasharray="3 6"
                        />
                        <text x={center(anchor) + 5} y={19} fill="#e0e8f3">
                            Ancla{" "}
                            {number(report.input.initialImpulsePrice ?? null)}
                        </text>
                    </g>
                )}
                {bars
                    .filter((b) => humanReferences.includes(b.openTimeUtc))
                    .map((b) => (
                        <g
                            key={b.openTimeUtc}
                            className="lab-overlay"
                            data-human-reference={b.openTimeUtc}
                        >
                            <circle
                                cx={center(b)}
                                cy={y(
                                    report.input.direction === "Ascending"
                                        ? b.low
                                        : b.high,
                                )}
                                r={10}
                                fill="none"
                                stroke="#ffffff"
                                strokeWidth={2.5}
                            />
                            <text
                                x={center(b) + 13}
                                y={
                                    y(
                                        report.input.direction === "Ascending"
                                            ? b.low
                                            : b.high,
                                    ) + 4
                                }
                                fill="#ffffff"
                            >
                                H
                            </text>
                        </g>
                    ))}
                {humanConfirmation &&
                    bars
                        .filter((b) => b.openTimeUtc === humanConfirmation)
                        .map((b) => (
                            <g
                                key={b.openTimeUtc}
                                className="lab-overlay"
                                data-human-confirmation={b.openTimeUtc}
                            >
                                <line
                                    x1={x(b.closeTimeUtc)}
                                    x2={x(b.closeTimeUtc)}
                                    y1={35}
                                    y2={420}
                                    stroke="#ffffff"
                                    strokeDasharray="2 5"
                                />
                                <text
                                    x={x(b.closeTimeUtc) + 5}
                                    y={40}
                                    fill="#ffffff"
                                >
                                    H confirmación
                                </text>
                            </g>
                        ))}
                {eligibleReferences.map((r, i) => (
                    <g
                        key={r.id}
                        className="lab-overlay"
                        data-context-reference={r.id}
                    >
                        <line
                            x1={38}
                            x2={1078}
                            y1={y(r.price)}
                            y2={y(r.price)}
                            stroke={r.color}
                            strokeDasharray="4 5"
                        />
                        <text
                            x={43}
                            y={y(r.price) - 7 - (i % 2) * 13}
                            fill={r.color}
                        >
                            {r.label} · {number(r.price)}
                        </text>
                        {Date.parse(r.formationUtc) >= first && (
                            <circle
                                data-formation-utc={r.formationUtc}
                                cx={
                                    r.sourceTimeframe ===
                                        report.input.timeframe &&
                                    bars.some(
                                        (b) =>
                                            Date.parse(b.openTimeUtc) ===
                                            Date.parse(r.formationUtc),
                                    )
                                        ? center(
                                              bars.find(
                                                  (b) =>
                                                      Date.parse(
                                                          b.openTimeUtc,
                                                      ) ===
                                                      Date.parse(
                                                          r.formationUtc,
                                                      ),
                                              )!,
                                          )
                                        : x(r.formationUtc)
                                }
                                cy={y(r.price)}
                                r={6}
                                fill="none"
                                stroke={r.color}
                            >
                                <title>
                                    Vela fuente{" "}
                                    {r.sourceTimeframe ?? "original"} abierta{" "}
                                    {formatTime(r.formationUtc)}; instante
                                    intra-vela no establecido. Referencia
                                    visible en esta investigación desde{" "}
                                    {formatTime(r.knownAtUtc)}
                                </title>
                            </circle>
                        )}
                    </g>
                ))}
                {guides
                    .filter(
                        (g) =>
                            Date.parse(g.timeUtc) >= first &&
                            Date.parse(g.timeUtc) <=
                                Date.parse(report.input.evaluationCutoffUtc),
                    )
                    .map((g) => (
                        <g
                            key={g.label}
                            className="lab-overlay"
                            data-time-guide={g.timeUtc}
                        >
                            <line
                                x1={x(g.timeUtc)}
                                x2={x(g.timeUtc)}
                                y1={30}
                                y2={420}
                                stroke="#c8c0a0"
                                strokeDasharray="2 6"
                            />
                            <text
                                x={x(g.timeUtc) + 4}
                                y={45}
                                transform={`rotate(90 ${x(g.timeUtc) + 4} 45)`}
                                fill="#c8c0a0"
                            >
                                {g.label}
                            </text>
                        </g>
                    ))}
                {[...levels].map(([price, ids]) => (
                    <g
                        key={price}
                        className="lab-overlay"
                        data-reference-price={price}
                    >
                        <line
                            x1={38}
                            x2={1078}
                            y1={y(price)}
                            y2={y(price)}
                            stroke="#94a6bc"
                            strokeDasharray="5 6"
                        />
                        <text x={43} y={y(price) - 7} fill="#e5edf8">
                            {ids.join(" / ")} · {number(price)} · retrospectiva
                        </text>
                    </g>
                ))}
                {active.map((row) => {
                    const id = row.algorithm.algorithmId;
                    const index = algorithmIds.indexOf(id);
                    const color = colors[index];
                    return (
                        <g
                            key={id}
                            className="lab-overlay"
                            data-overlay={id}
                            fill={color}
                            stroke={color}
                        >
                            {row.pullbackStart && (
                                <g
                                    data-marker="start"
                                    data-open={row.pullbackStart.openTimeUtc}
                                >
                                    <path
                                        d={`M ${center(row.pullbackStart)} ${y(row.pullbackStart.high) - 10 - index * 14} l -5 -7 h 10 Z`}
                                    />
                                    <text
                                        x={center(row.pullbackStart) + 8}
                                        y={
                                            y(row.pullbackStart.high) -
                                            11 -
                                            index * 14
                                        }
                                        stroke="none"
                                    >
                                        {letter(id)} inicio
                                    </text>
                                </g>
                            )}
                            {row.referenceCandles.map((bar) => (
                                <circle
                                    key={bar.openTimeUtc}
                                    data-marker="reference"
                                    data-open={bar.openTimeUtc}
                                    cx={center(bar)}
                                    cy={y(row.proposedReferencePrice!)}
                                    r={5 + index * 3}
                                    fill="none"
                                    strokeWidth={1.8}
                                >
                                    <title>
                                        {letter(id)} referencia formada{" "}
                                        {formatTime(bar.openTimeUtc)};
                                        confirmada{" "}
                                        {formatTime(row.confirmationAtUtc)}
                                    </title>
                                </circle>
                            ))}
                            {row.confirmationAtUtc &&
                                row.continuationCandle && (
                                    <g
                                        data-marker="confirmation"
                                        data-open={
                                            row.continuationCandle.openTimeUtc
                                        }
                                        data-known-at={row.confirmationAtUtc}
                                    >
                                        <rect
                                            x={x(row.confirmationAtUtc) - 4}
                                            y={
                                                y(row.continuationCandle.low) +
                                                8 +
                                                index * 14
                                            }
                                            width={8}
                                            height={8}
                                        />
                                        <title>
                                            {letter(id)} conocida al cierre{" "}
                                            {formatTime(row.confirmationAtUtc)}
                                        </title>
                                    </g>
                                )}
                        </g>
                    );
                })}
                <line
                    x1={x(report.input.evaluationCutoffUtc)}
                    x2={x(report.input.evaluationCutoffUtc)}
                    y1={28}
                    y2={423}
                    stroke="#e4c779"
                    strokeDasharray="6 5"
                />
                <text
                    x={x(report.input.evaluationCutoffUtc) - 5}
                    y={452}
                    textAnchor="end"
                    fill="#e4c779"
                >
                    Cutoff {report.input.evaluationCutoffUtc.slice(11, 16)} UTC
                </text>
                {[0, 1, 2, 3].map((i) => {
                    const t = first + ((last - first) * i) / 4;
                    return (
                        <text key={i} x={x(t)} y={443} className="lab-axis">
                            {new Date(t).toISOString().slice(11, 16)}
                        </text>
                    );
                })}
            </svg>
        </div>
    );
}

export function ResearchViewer({ envelope }: { envelope: Envelope }) {
    const report = envelope.comparison;
    const [visible, setVisible] = useState(algorithmIds);
    const [selected, setSelected] = useState(0);
    const bar = report.evidenceCandles[selected];
    return (
        <>
            <div className="lab-episode-header">
                <div>
                    <span className="lab-kicker">
                        {report.input.providerId} · M5 ·{" "}
                        {report.input.direction}
                    </span>
                    <h2>
                        {report.input.symbol}{" "}
                        <span>
                            {report.input.dataOrigin === "Synthetic"
                                ? "Fixture sintético"
                                : "Histórico auténtico · documental"}
                        </span>
                    </h2>
                </div>
                <div className="lab-cutoff">
                    {report.evidenceCandles[0]?.openTimeUtc.slice(0, 10)}
                    <br />
                    {report.evidenceCandles[0]?.openTimeUtc.slice(
                        11,
                        16,
                    )} — {report.input.evaluationCutoffUtc.slice(11, 16)} UTC
                </div>
            </div>
            <div className="lab-warning">
                Research Only · Hipótesis MoneyWay, no reglas confirmadas del
                mentor. Disponibilidad histórica no autenticada. Sin señales ni
                admisión canónica.
                {report.input.dataOrigin === "RetrospectiveDocumentary" &&
                    " UTC mostrado = mapeo documental de junio; no acredita publicación ni disponibilidad histórica."}
            </div>
            <fieldset className="lab-controls">
                <legend>Overlays · mostrar / ocultar</legend>
                {algorithmIds.map((id, i) => (
                    <label key={id} style={{ borderColor: colors[i] }}>
                        <input
                            type="checkbox"
                            checked={visible.includes(id)}
                            onChange={(e) =>
                                setVisible(
                                    e.target.checked
                                        ? [...visible, id]
                                        : visible.filter((v) => v !== id),
                                )
                            }
                        />{" "}
                        <strong style={{ color: colors[i] }}>
                            {letter(id)}
                        </strong>{" "}
                        {names[i]}
                    </label>
                ))}
            </fieldset>
            <CandleChart
                report={report}
                visible={visible}
                selected={selected}
                onSelect={setSelected}
            />
            <div className="lab-legend">
                △ Inicio del retroceso · ○ Referencia y extremos empatados · ■
                Cierre confirmador · Línea dorada: cutoff fijo
            </div>
            {bar && (
                <div className="lab-inspector" aria-live="polite">
                    <div>
                        <strong>{formatTime(bar.openTimeUtc)}</strong>
                        <small>Cierre: {formatTime(bar.closeTimeUtc)}</small>
                    </div>
                    <div className="lab-ohlc">
                        {(
                            [
                                ["O", bar.open],
                                ["H", bar.high],
                                ["L", bar.low],
                                ["C", bar.close],
                            ] as const
                        ).map(([k, v]) => (
                            <span key={k}>
                                {k} <b>{number(v)}</b>
                            </span>
                        ))}
                    </div>
                    <button
                        type="button"
                        disabled={selected === 0}
                        onClick={() => setSelected(selected - 1)}
                        aria-label="Vela anterior"
                    >
                        ←
                    </button>
                    <button
                        type="button"
                        disabled={selected >= report.evidenceCandles.length - 1}
                        onClick={() => setSelected(selected + 1)}
                        aria-label="Vela siguiente"
                    >
                        →
                    </button>
                </div>
            )}
            <p className="lab-temporal">
                Las referencias se dibujan sobre su vela de formación. Solo
                quedan confirmadas en el cierre posterior indicado abajo.
                Inspeccionar una vela no cambia el cutoff ni simula replay
                causal.
            </p>
            <div className="lab-results">
                {report.results.map((row) => {
                    const i = algorithmIds.indexOf(row.algorithm.algorithmId);
                    return (
                        <article
                            key={row.algorithm.algorithmId}
                            style={{ borderTopColor: colors[i] }}
                        >
                            <h3>
                                <span style={{ color: colors[i] }}>
                                    {letter(row.algorithm.algorithmId)}
                                </span>{" "}
                                {names[i]}{" "}
                                <small>v{row.algorithm.algorithmVersion}</small>
                            </h3>
                            <p className="lab-price">
                                {number(row.proposedReferencePrice)}{" "}
                                <small>
                                    {row.isExperimentalReferenceConfirmed
                                        ? "referencia experimental"
                                        : row.proposedReferencePrice !== null
                                          ? "provisional"
                                          : "sin referencia"}
                                </small>
                            </p>
                            <span className="lab-state">{row.status}</span>
                            <dl>
                                <dt>Inicio (apertura)</dt>
                                <dd>
                                    {row.pullbackStart
                                        ? formatTime(
                                              row.pullbackStart.openTimeUtc,
                                          )
                                        : "No establecido"}
                                </dd>
                                <dt>Formación / extremos empatados</dt>
                                <dd>
                                    {row.referenceCandles.length
                                        ? row.referenceCandles.map((c) => (
                                              <div key={c.openTimeUtc}>
                                                  {formatTime(c.openTimeUtc)}
                                              </div>
                                          ))
                                        : "Ninguno"}
                                </dd>
                                <dt>Conocida al cierre</dt>
                                <dd>{formatTime(row.confirmationAtUtc)}</dd>
                            </dl>
                            <p>{row.reason}</p>
                            <details>
                                <summary>Hipótesis exacta</summary>
                                {row.algorithm.startCondition}
                            </details>
                        </article>
                    );
                })}
            </div>
            <details className="lab-details" open>
                <summary>
                    Acuerdos y discrepancias ·{" "}
                    {report.allAlgorithmsAgree
                        ? "coincidencia total (puede ser abstención)"
                        : "resultados diferentes"}
                </summary>
                <div className="lab-pairs">
                    {report.comparisons.map((pair) => (
                        <div key={pair.leftAlgorithm + pair.rightAlgorithm}>
                            <b>
                                {letter(pair.leftAlgorithm)} /{" "}
                                {letter(pair.rightAlgorithm)}
                            </b>
                            <span>
                                {pair.fullAgreement
                                    ? "Coincidencia total"
                                    : "Discrepancia"}
                            </span>
                            <small>
                                Referencia / confirmación:{" "}
                                {pair.confirmedReferenceAgreement === null
                                    ? "no comparables sin confirmar"
                                    : pair.confirmedReferenceAgreement
                                      ? "coinciden"
                                      : "difieren"}
                            </small>
                            <small>
                                Δ confirmación:{" "}
                                {pair.confirmationLatencyDifference ?? "—"} · Δ
                                inicio a confirmación:{" "}
                                {pair.pullbackLatencyDifference ?? "—"}
                            </small>
                            <details>
                                <summary>Detalle del runner</summary>
                                {pair.explanation}
                            </details>
                        </div>
                    ))}
                </div>
            </details>
            <details className="lab-details">
                <summary>Procedencia, ancla y contrato experimental</summary>
                <p>{report.input.seriesId}</p>
                <p>{report.input.provenance}</p>
                <p>
                    Ancla:{" "}
                    {report.input.initialization?.referenceId ?? "ausente"} ·{" "}
                    {report.input.initialization &&
                        formatTime(
                            report.input.initialization.anchorOpenTimeUtc,
                        )}{" "}
                    · precio {number(report.input.initialImpulsePrice)}
                </p>
                <p>{report.input.initialization?.sourceReference}</p>
                <p>
                    Disponibilidad declarada de investigación:{" "}
                    {formatTime(
                        report.input.initialization?.availableAtUtc ?? null,
                    )}
                    . No acredita disponibilidad histórica.
                </p>
                <code>SHA256 input: {envelope.inputSha256}</code>
                <p>{envelope.inputHashMeaning}</p>
                <ul>
                    {report.sharedAssumptions.map((a) => (
                        <li key={a}>{a}</li>
                    ))}
                </ul>
                <p>
                    CanonicalAdmissionPermitted = false · OperationalSignal =
                    false. Sin ganador, promoción ni validación económica.
                </p>
            </details>
            <details className="lab-details">
                <summary>
                    Tabla OHLC accesible · datos originales del reporte
                </summary>
                <div className="lab-chart-scroll">
                    <table>
                        <thead>
                            <tr>
                                <th>Apertura UTC</th>
                                <th>Cierre UTC</th>
                                <th>O</th>
                                <th>H</th>
                                <th>L</th>
                                <th>C</th>
                            </tr>
                        </thead>
                        <tbody>
                            {report.evidenceCandles.map((c) => (
                                <tr key={c.openTimeUtc}>
                                    <td>{formatTime(c.openTimeUtc)}</td>
                                    <td>{formatTime(c.closeTimeUtc)}</td>
                                    <td>{number(c.open)}</td>
                                    <td>{number(c.high)}</td>
                                    <td>{number(c.low)}</td>
                                    <td>{number(c.close)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </details>
        </>
    );
}

export function StrategyLab() {
    const [episode, setEpisode] = useState("synthetic-v1");
    const [loaded, setLoaded] = useState<{
        id: string;
        envelope?: Envelope;
        error?: string;
    }>();
    useEffect(() => {
        let current = true;
        loadEpisode(episode)
            .then((envelope) => {
                if (current) setLoaded({ id: episode, envelope });
            })
            .catch((error: unknown) => {
                if (current)
                    setLoaded({
                        id: episode,
                        error:
                            error instanceof Error
                                ? error.message
                                : "Reporte no disponible",
                    });
            });
        return () => {
            current = false;
        };
    }, [episode]);
    return (
        <main className="strategy-lab">
            <nav className="lab-nav">
                <a href="#">MoneyWay / Inicio</a>
                <a href="#strategy-lab-blind">
                    Revisión ciega multi-episodio →
                </a>
                <a href="#strategy-lab-context">Contexto Nasdaq →</a>
                <span>
                    Strategy Lab <b>EXPERIMENTAL</b>
                </span>
            </nav>
            <header className="lab-heading">
                <div>
                    <p className="lab-kicker">M5 SWING COMPARISON · V2</p>
                    <h1>
                        Una secuencia.
                        <br />
                        <span>Cuatro hipótesis.</span>
                    </h1>
                    <p>
                        Inspeccione dónde coinciden y dónde cambian las
                        referencias.
                    </p>
                </div>
                <label>
                    Episodio
                    <select
                        value={episode}
                        onChange={(e) => setEpisode(e.target.value)}
                    >
                        {episodes.map((e) => (
                            <option key={e.id} value={e.id}>
                                {e.label}
                            </option>
                        ))}
                    </select>
                </label>
            </header>
            {loaded?.id !== episode ? (
                <p role="status">Cargando reporte experimental…</p>
            ) : loaded.error ? (
                <p role="alert">{loaded.error}</p>
            ) : loaded.envelope ? (
                <ResearchViewer key={episode} envelope={loaded.envelope} />
            ) : (
                <p role="alert">Reporte no disponible.</p>
            )}
            {loaded?.id === episode &&
                loaded.envelope &&
                episode === "june29-v1" && (
                    <details className="lab-details">
                        <summary>
                            Reloj nativo original · MT5 server · filas retenidas
                        </summary>
                        <p>
                            Origen: OANDA Global Markets MT5 demo US100. Mapeo
                            documental de junio: UTC nominal = servidor − 3 h;
                            no autentica disponibilidad histórica. Sin
                            equivalencia de precios con TradingView ni USTEC.
                        </p>
                        <p>
                            Raw SHA256:{" "}
                            <code>
                                4f468275b5644d4298ba0c085802f04574c765f7dfa662df1ee99456eb9fcf46
                            </code>
                            <br />
                            Normalizado SHA256:{" "}
                            <code>
                                0133f473592a004535bac89437c96b1c369a753205792459c4e4e12c1835c21a
                            </code>
                        </p>
                        <div className="lab-chart-scroll">
                            <pre>{nativeRows}</pre>
                        </div>
                    </details>
                )}
        </main>
    );
}
