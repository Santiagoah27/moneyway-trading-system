import { useEffect, useState } from "react";
import {
    CandleChart,
    type ChartGuide,
    type ChartReference,
} from "../StrategyLab";
import { formatTime, type Envelope } from "../report";
import { compareReference } from "../blind/metrics";
import { markContextSeen } from "../contextExposure";
import {
    bogota,
    loadContext,
    coverageAt,
    cutoffOptions,
    eligibleReview,
    frames,
    loadOriginalComparison,
    originalReviews,
    visibleAt,
    type Frame,
    type ContextData,
} from "./model";
import "./ContextView.css";
import { LondonAsiaSummary } from "./LondonAsiaSummary";
import { sessionReferencesAt, sessionReport } from "./sessionLiquidity";
import { ContextualScReview } from "./ContextualScReview";

const guides: ChartGuide[] = [
    { timeUtc: "2026-06-23T13:00:00Z", label: "Preparación 08:00 Bogotá" },
    { timeUtc: "2026-06-23T13:30:00Z", label: "Inicio 08:30 Bogotá" },
    { timeUtc: "2026-06-23T16:00:00Z", label: "Fin runtime 11:00 Bogotá" },
    { timeUtc: "2026-06-23T16:30:00Z", label: "Fin instrucción 11:30 Bogotá" },
];
function ContextSession({ context }: { context: ContextData }) {
    const [frame, setFrame] = useState<Frame>("M5");
    const [cutoff, setCutoff] = useState("2026-06-23T15:00:00Z");
    const [hours, setHours] = useState<number | null>(12);
    const [selected, setSelected] = useState(0);
    const [episode, setEpisode] = useState(
        originalReviews.reviews[0].episodeId,
    );
    const [overlays, setOverlays] = useState(true);
    const [loaded, setLoaded] = useState<{
        id: string;
        report?: Envelope;
        error?: string;
    }>();
    const [exposureError, setExposureError] = useState("");
    const review = eligibleReview(episode, cutoff);
    const bars = visibleAt(context, frame, cutoff, hours);
    const coverage = coverageAt(context, frame, cutoff);
    const current = bars[selected];
    // This is a documentary snapshot loader, never a contextual detector/re-evaluation.
    useEffect(() => {
        let active = true;
        if (review)
            loadOriginalComparison(review.episodeId)
                .then((report) => {
                    if (active) setLoaded({ id: review.episodeId, report });
                })
                .catch((error: unknown) => {
                    if (active)
                        setLoaded({
                            id: review.episodeId,
                            error:
                                error instanceof Error
                                    ? error.message
                                    : "Reporte no disponible",
                        });
                });
        return () => {
            active = false;
        };
    }, [review]);
    useEffect(() => {
        try {
            markContextSeen(
                originalReviews.reviews
                    .filter(
                        (r) =>
                            Date.parse(r.label.cutoffUtc) <= Date.parse(cutoff),
                    )
                    .map((r) => r.episodeId),
            );
        } catch {
            setExposureError(
                "No se pudo registrar la exposición en este navegador. Declare material previamente visto si vuelve a V3.",
            );
        }
    }, [cutoff]);
    const report =
        review && loaded?.id === review.episodeId ? loaded.report : undefined;
    const references: ChartReference[] = [];
    const original = report?.comparison;
    let liquidity: ReturnType<typeof sessionReferencesAt>;
    let liquidityError = "";
    try {
        liquidity = sessionReferencesAt(context, cutoff);
    } catch (error) {
        liquidityError =
            error instanceof Error
                ? error.message
                : "Referencias no disponibles.";
    }
    for (const s of liquidity?.sessions ?? []) {
        if (s.coverageStatus !== "Complete" || !s.availableAtUtc) continue;
        for (const side of ["high", "low"] as const) {
            const price = s[side];
            if (price === null) continue;
            for (const c of side === "high" ? s.highCandles : s.lowCandles)
                references.push({
                    id: `${s.session}-${side}-${c.openTimeUtc}`,
                    label: `${s.session} ${side} H1 documental`,
                    price,
                    formationUtc: c.openTimeUtc,
                    knownAtUtc: s.availableAtUtc,
                    color: s.session === "Asia" ? "#71d6e8" : "#d6b3ff",
                    sourceTimeframe: "1h",
                });
        }
    }
    if (review && original && overlays) {
        for (const [i, time] of review.label.selectedOpens.entries())
            references.push({
                id: `human-${i}`,
                label: "Humano original M5",
                price: review.label.price,
                formationUtc: time,
                knownAtUtc: review.label.cutoffUtc,
                color: "#ffffff",
            });
        const groups = new Map<
            string,
            { price: number; times: string[]; ids: string[] }
        >();
        for (const result of original.results)
            if (
                result.isExperimentalReferenceConfirmed &&
                result.proposedReferencePrice !== null
            ) {
                const key = `${result.proposedReferencePrice}/${result.referenceCandles.map((c) => c.openTimeUtc).join()}`;
                const group = groups.get(key) ?? {
                    price: result.proposedReferencePrice,
                    times: result.referenceCandles.map((c) => c.openTimeUtc),
                    ids: [],
                };
                group.ids.push(result.algorithm.algorithmId.split("-")[3]);
                groups.set(key, group);
            }
        for (const [key, group] of groups)
            for (const [i, time] of group.times.entries())
                references.push({
                    id: `algorithm-${key}-${i}`,
                    label: `${group.ids.join("/")} original M5`,
                    price: group.price,
                    formationUtc: time,
                    knownAtUtc: review.label.cutoffUtc,
                    color: "#57d8ef",
                });
        if (
            original.input.initialization &&
            original.input.initialImpulsePrice !== null
        )
            references.push({
                id: "original-anchor",
                label: "Ancla del experimento M5",
                price: original.input.initialImpulsePrice,
                formationUtc: original.input.initialization.anchorOpenTimeUtc,
                knownAtUtc: review.label.cutoffUtc,
                color: "#ffb86b",
            });
    }
    const episodeGuides =
        report && review && overlays
            ? [
                  ...guides,
                  ...report.comparison.results
                      .filter((r) => r.confirmationAtUtc)
                      .slice(0, 1)
                      .map((r) => ({
                          timeUtc: r.confirmationAtUtc!,
                          label: "Confirmación algoritmo M5 (original)",
                      })),
              ]
            : guides;
    return (
        <main className="strategy-lab context-research">
            <nav className="lab-nav">
                <a href="#strategy-lab">← Strategy Lab</a>
                <span>
                    Contexto Nasdaq · <b>RESEARCH ONLY · YA EXPUESTO</b>
                </span>
                <a href="#strategy-lab-blind">Revisión V3</a>
            </nav>
            <header className="context-heading">
                <h1>La sesión en contexto</h1>
                <p>
                    US100 · {context.providerId} · OHLC originales ·
                    America/Bogota
                </p>
            </header>
            <section aria-label="Liquidez de sesiones configurada">
                <h2>Asia y London · candidata documental H1</h2>
                <p>
                    Asia [D-1 17:00, D 02:00) · London [D 02:00, D 08:00),
                    America/Bogota. Fuente exacta H1; no se sustituye por M1/M5.
                </p>
                <p>{sessionReport.provenance}</p>
                {liquidityError ? (
                    <p role="alert">{liquidityError}</p>
                ) : liquidity ? (
                    <table>
                        <thead>
                            <tr>
                                <th>Sesión / intervalo UTC</th>
                                <th>Cobertura H1</th>
                                <th>High / Low</th>
                                <th>Formación UTC</th>
                                <th>Disponibilidad documental</th>
                            </tr>
                        </thead>
                        <tbody>
                            {liquidity.sessions.map((s) => (
                                <tr key={s.session}>
                                    <td>
                                        {s.session}
                                        <small>
                                            {formatTime(s.startUtc)} →{" "}
                                            {formatTime(s.endUtc)}
                                        </small>
                                    </td>
                                    <td>
                                        {s.coverageStatus} · {s.sourceCount}/
                                        {s.expectedCount}
                                        <small>
                                            {s.missingOpensUtc.length
                                                ? `${s.missingOpensUtc.length} intervalos no observables`
                                                : "Cobertura completa"}
                                        </small>
                                    </td>
                                    <td>
                                        {s.high === null
                                            ? "No disponible"
                                            : `${s.session === "Asia" ? "AH" : "LH"} ${s.high}`}{" "}
                                        /{" "}
                                        {s.low === null
                                            ? "No disponible"
                                            : `${s.session === "Asia" ? "AL" : "LL"} ${s.low}`}
                                    </td>
                                    <td>
                                        {[
                                            ...s.highCandles,
                                            ...s.lowCandles,
                                        ].map((c, i) => (
                                            <small
                                                key={`${c.openTimeUtc}-${i}`}
                                            >
                                                {formatTime(c.openTimeUtc)}
                                            </small>
                                        ))}
                                    </td>
                                    <td>
                                        {formatTime(s.availableAtUtc)}
                                        <small>
                                            Prefijo evaluado{" "}
                                            {formatTime(liquidity.cutoffUtc)};
                                            no acredita publicación autenticada.
                                        </small>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                ) : (
                    <p>Sin prefijo documental calculado para este cutoff.</p>
                )}
                <p>
                    Extremos calculados no equivalen a liquidez relevante,
                    barrido, SC ni aprobación de entrada.
                    CanonicalAdmissionPermitted=false · OperationalSignal=false.
                </p>
            </section>
            <LondonAsiaSummary context={context} cutoff={cutoff} />
            <div className="context-controls">
                <label>
                    Sesión
                    <select value="2026-06-23" onChange={() => {}}>
                        <option value="2026-06-23">
                            23 junio 2026 · desarrollo revisado
                        </option>
                    </select>
                </label>
                <fieldset>
                    <legend>Timeframe original</legend>
                    {frames.map((f) => (
                        <button
                            key={f}
                            type="button"
                            aria-pressed={frame === f}
                            onClick={() => {
                                setFrame(f);
                                setSelected(0);
                            }}
                        >
                            {f}
                        </button>
                    ))}
                </fieldset>
                <label>
                    Cutoff compartido · Bogotá
                    <select
                        value={cutoff}
                        onChange={(e) => {
                            setCutoff(e.target.value);
                            setSelected(0);
                        }}
                    >
                        {cutoffOptions.map((t) => (
                            <option key={t} value={t}>
                                {bogota(t)} · {t.slice(11, 16)}Z
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Historia visible
                    <select
                        value={hours ?? "all"}
                        onChange={(e) => {
                            setHours(
                                e.target.value === "all"
                                    ? null
                                    : Number(e.target.value),
                            );
                            setSelected(0);
                        }}
                    >
                        {[2, 6, 12, 24].map((h) => (
                            <option key={h} value={h}>
                                Últimas {h} horas
                            </option>
                        ))}
                        <option value="all">Todo el contexto disponible</option>
                    </select>
                </label>
            </div>
            <div className="context-limitations">
                <span>
                    Asia/London: ventanas explícitas de la candidata; extremos
                    documentales sujetos a cobertura completa H1.
                </span>
                <span>
                    Ventana: instrucción 08:30–11:30 / runtime [08:30,11:00).
                    Sin resolución ni autorización.
                </span>
                <span>
                    08:00 es una referencia horaria; no acredita preparación
                    completada.
                </span>
            </div>
            <div className="context-coverage" aria-live="polite">
                <strong>
                    {frame} · {bars.length} velas en vista / {coverage.count}{" "}
                    cerradas al cutoff
                </strong>
                <span>
                    Primera:{" "}
                    {coverage.firstOpen
                        ? formatTime(coverage.firstOpen)
                        : "sin datos"}{" "}
                    · Último cierre: {formatTime(coverage.lastClose)}
                </span>
                <span>
                    {coverage.gaps.length
                        ? `${coverage.gaps.length} rango(s) sin filas fuente; ver cobertura.`
                        : "Sin huecos entre las filas cerradas seleccionadas."}{" "}
                    {coverage.pendingInterval &&
                        "Intervalo posterior todavía no completo: no se muestra como vela cerrada."}
                </span>
            </div>
            {exposureError && <p role="alert">{exposureError}</p>}
            <CandleChart
                report={{
                    input: {
                        symbol: context.symbol,
                        timeframe: frame,
                        evaluationCutoffUtc: cutoff,
                    },
                    evidenceCandles: bars,
                    results: [],
                }}
                visible={[]}
                selected={selected}
                onSelect={setSelected}
                references={references.map((r) => ({
                    ...r,
                    sourceTimeframe: "M5",
                }))}
                guides={episodeGuides}
            />
            {current ? (
                <div className="lab-inspector" aria-live="polite">
                    <div>
                        <strong>{formatTime(current.openTimeUtc)}</strong>
                        <small>
                            Bogotá {bogota(current.openTimeUtc)} · nativo{" "}
                            {current.nativeOpenTime} MT5 server
                        </small>
                        <small>
                            Cierre nominal {formatTime(current.closeTimeUtc)} ·
                            original {frame}, no agregado
                        </small>
                    </div>
                    <span>
                        O <b>{current.open}</b> · H <b>{current.high}</b> · L{" "}
                        <b>{current.low}</b> · C <b>{current.close}</b>
                    </span>
                    <button
                        type="button"
                        disabled={selected === 0}
                        onClick={() => setSelected((i) => i - 1)}
                        aria-label="Vela anterior"
                    >
                        ←
                    </button>
                    <button
                        type="button"
                        disabled={selected === bars.length - 1}
                        onClick={() => setSelected((i) => i + 1)}
                        aria-label="Vela siguiente"
                    >
                        →
                    </button>
                </div>
            ) : (
                <p role="status">
                    Sin velas cerradas en este zoom. Amplíe la historia visible.
                </p>
            )}
            <div className="context-comparison-controls">
                <label>
                    Comparación original M5
                    <select
                        value={episode}
                        onChange={(e) => setEpisode(e.target.value)}
                    >
                        {originalReviews.reviews.map((r, i) => (
                            <option key={r.episodeId} value={r.episodeId}>
                                Desarrollo {i + 1} · cutoff original{" "}
                                {r.label.cutoffUtc.slice(11, 16)}Z
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    <input
                        type="checkbox"
                        checked={overlays}
                        onChange={(e) => setOverlays(e.target.checked)}
                    />{" "}
                    Superponer referencias M5 históricas
                </label>
            </div>
            {!review ? (
                <p className="lab-warning">
                    Comparación oculta: su cutoff original es posterior al
                    cutoff compartido. No se conservan resultados ni etiquetas
                    posteriores en esta vista.
                </p>
            ) : loaded?.id === review.episodeId && loaded.error ? (
                <p role="alert">{loaded.error}</p>
            ) : !report ? (
                <p role="status">Cargando el reporte original verificado…</p>
            ) : (
                <div
                    className="context-original"
                    aria-label="Comparación original preservada"
                >
                    <strong>
                        Experimento M5 original · cutoff{" "}
                        {formatTime(review.label.cutoffUtc)}
                    </strong>
                    <p>
                        No se recalcula sobre este contexto ni sobre {frame}.
                        Las líneas siguen siendo referencias M5, no puntos
                        estructurales H4/H1.
                    </p>
                    <div className="context-facts">
                        <span>
                            Humano r1: {review.label.extreme}{" "}
                            <b>{review.label.price}</b>
                            <small>
                                Formación{" "}
                                {review.label.selectedOpens
                                    .map(formatTime)
                                    .join(", ")}
                            </small>
                            <small>
                                SelfDeclaredIndependent ·
                                IndependentRetrospectiveResearch
                            </small>
                        </span>
                        <span>
                            Ancla experimental{" "}
                            <b>{original!.input.initialImpulsePrice}</b>
                            <small>
                                {formatTime(
                                    original!.input.initialization
                                        ?.anchorOpenTimeUtc ?? null,
                                )}
                            </small>
                        </span>
                        <span>
                            Confirmación humana independiente:{" "}
                            <b>no suministrada</b>
                            <small>
                                Latencia relativa al humano: no disponible.
                            </small>
                        </span>
                    </div>
                    <div className="lab-chart-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th>Hipótesis V1</th>
                                    <th>Referencia / formación M5</th>
                                    <th>Confirmación original</th>
                                    <th>Acuerdo original</th>
                                    <th>Δ precio / absoluto</th>
                                </tr>
                            </thead>
                            <tbody>
                                {report.comparison.results.map((r) => {
                                    const comparison = compareReference(
                                        r,
                                        review.label,
                                    );
                                    return (
                                        <tr key={r.algorithm.algorithmId}>
                                            <td>
                                                {r.algorithm.algorithmId} ·{" "}
                                                {r.algorithm.algorithmVersion}
                                            </td>
                                            <td>
                                                {r.proposedReferencePrice}
                                                <small>
                                                    {r.referenceCandles
                                                        .map((c) =>
                                                            formatTime(
                                                                c.openTimeUtc,
                                                            ),
                                                        )
                                                        .join(", ")}
                                                </small>
                                            </td>
                                            <td>
                                                {formatTime(
                                                    r.confirmationAtUtc,
                                                )}
                                            </td>
                                            <td>{comparison.outcome}</td>
                                            <td>
                                                {comparison.signedDifference} /{" "}
                                                {comparison.absoluteDifference}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                    <p>
                        V1 excluye la vela confirmadora de la agregación. La
                        discrepancia del primer episodio es consistente con esa
                        política; no se cambia aquí.
                    </p>
                    {originalReviews.reviews.every(
                        (r) =>
                            Date.parse(r.label.cutoffUtc) <= Date.parse(cutoff),
                    ) ? (
                        <p>
                            Cada hipótesis tiene 1/2 acuerdos exactos en estos
                            dos episodios de desarrollo: no establece precisión
                            general ni rentabilidad.
                        </p>
                    ) : (
                        <p>
                            Resumen conjunto oculto hasta el último cutoff
                            original; no se muestran estadísticas posteriores.
                        </p>
                    )}
                </div>
            )}
            <details className="lab-details">
                <summary>Cobertura y rangos sin filas fuente</summary>
                <p>
                    Historia solicitada: {formatTime(context.requestedStartUtc)}{" "}
                    hasta {formatTime(cutoff)}. Los rangos ausentes no prueban
                    horarios de mercado ni corrupción. H4 no acredita
                    continuidad de M1.
                </p>
                <table>
                    <thead>
                        <tr>
                            <th>TF original</th>
                            <th>Cerradas al cutoff</th>
                            <th>Primer / último límite</th>
                            <th>Rangos sin filas</th>
                        </tr>
                    </thead>
                    <tbody>
                        {frames.map((f) => {
                            const c = coverageAt(context, f, cutoff);
                            return (
                                <tr key={f}>
                                    <td>{f}</td>
                                    <td>{c.count}</td>
                                    <td>
                                        {formatTime(c.firstOpen)}
                                        <br />
                                        {formatTime(c.lastClose)}
                                    </td>
                                    <td>
                                        {c.gaps.map((g) => (
                                            <div key={g.startUtc}>
                                                {formatTime(g.startUtc)} →{" "}
                                                {formatTime(g.endUtc)}
                                            </div>
                                        ))}
                                        {c.pendingInterval &&
                                            "Cola del intervalo aún no cerrada."}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </details>
            <details className="lab-details">
                <summary>
                    Procedencia, privacidad y límites de interpretación
                </summary>
                <p>{context.documentaryMapping}</p>
                <p>
                    Research Only. CanonicalAdmissionPermitted=false ·
                    OperationalSignal=false. Sin hechos canónicos, clasificación
                    automática H4, liquidez tomada, SC/IFVG, FVG, M1
                    realignment, SL/TP ni elegibilidad inferidos. Standard y WF2
                    siguen separados; los controles operativos y no-reentry
                    permanecen fuera de esta vista.
                </p>
                <p>
                    Export original SHA256: {originalReviews.sourceSha256}.{" "}
                    {originalReviews.privacy}
                </p>
                <p>
                    Las etiquetas originales no se escriben ni importan sobre
                    localStorage V3. Se registra únicamente exposición
                    contextual para no presentar futuras primeras selecciones
                    como ciegas.
                </p>
                <p>
                    La configuración candidata resuelve las fronteras
                    Asia/London. Siguen pendientes las selecciones
                    estructurales/liquidez relevantes y su validación contextual
                    humana.
                </p>
                {frames.map((f) => (
                    <p key={f}>
                        {f}: {context.series[f].rawRelativePath} · SHA256{" "}
                        {context.series[f].rawSha256}
                        <br />
                        {context.series[f].normalizedRelativePath} · SHA256{" "}
                        {context.series[f].normalizedSha256} · derived=false
                    </p>
                ))}
                {review && (
                    <p>
                        Input original {review.inputSha256}
                        <br />
                        Reporte original {review.reportSha256}
                    </p>
                )}
            </details>
        </main>
    );
}

export function ContextualResearch() {
    const [reviewMode, setReviewMode] = useState(false);
    const [state, setState] = useState<{ data?: ContextData; error?: string }>(
        {},
    );
    useEffect(() => {
        const controller = new AbortController();
        loadContext(controller.signal)
            .then((data) => {
                if (!controller.signal.aborted) setState({ data });
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted)
                    setState({
                        error:
                            error instanceof Error
                                ? error.message
                                : "Contexto no disponible",
                    });
            });
        return () => controller.abort();
    }, []);
    if (reviewMode)
        return (
            <>
                <button onClick={() => setReviewMode(false)}>
                    Volver a exploración expuesta del 23 junio
                </button>
                <ContextualScReview />
            </>
        );
    if (state.data)
        return (
            <>
                <button onClick={() => setReviewMode(true)}>
                    Revisión contextual cronológica · episodio independiente
                </button>
                <ContextSession context={state.data} />
            </>
        );
    return (
        <main className="strategy-lab">
            <a href="#strategy-lab">← Strategy Lab</a>
            {state.error ? (
                <p role="alert">{state.error}</p>
            ) : (
                <p role="status">Cargando OHLC documentales del 23 de junio…</p>
            )}
        </main>
    );
}
