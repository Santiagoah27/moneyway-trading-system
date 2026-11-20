import { useEffect, useState } from "react";
import { CandleChart } from "../StrategyLab";
import { formatTime } from "../report";
import {
    bogota,
    closedAt,
    coverageAt,
    frames,
    type ContextData,
    type Frame,
} from "./model";
import { sessionReferencesAt, type SessionReport } from "./sessionLiquidity";
import {
    blank,
    commit,
    decode,
    digest,
    exportReview,
    first,
    loadReviewContext,
    manifest,
    persist,
    prep,
    referenceEligibility,
    reveal,
    reviewKey,
    visit,
    type Draft,
    type Kind,
    type Review,
    type Stage,
} from "./contextualReview";
import {
    compare,
    denominators,
    loadComparison,
    type Comparison,
} from "./contextualComparison";

type Resources = {
    context: ContextData;
    sessions: SessionReport;
    interaction: {
        contextId: string;
        inputSha256: string;
        frames: {
            cutoffUtc: string;
            interaction: {
                completedClassification: string | null;
                status: string;
            };
        }[];
    };
};
async function resources(): Promise<Resources> {
    const context = await loadReviewContext();
    const sessionText = (
        await import("./data/contextual-validation/sessions.json?raw")
    ).default;
    const interactionText = (
        await import("./data/contextual-validation/interaction.json?raw")
    ).default;
    if (
        (await digest(sessionText)) !== manifest.reportHashes.sessions ||
        (await digest(interactionText)) !== manifest.reportHashes.interaction
    )
        throw new Error("Referencias documentales incompatibles.");
    const sessions = JSON.parse(sessionText) as SessionReport;
    const interaction = JSON.parse(interactionText) as Resources["interaction"];
    if (
        sessions.inputSha256 !== manifest.inputSha256 ||
        interaction.inputSha256 !== manifest.inputSha256 ||
        interaction.contextId !== context.id
    )
        throw new Error("Identidad de fuente incompatible.");
    sessionReferencesAt(context, prep, sessions);
    return { context, sessions, interaction };
}
function download(text: string) {
    const url = URL.createObjectURL(
        new Blob([text], { type: "application/json" }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = "moneyway-contextual-sc-labels-v1.json";
    a.click();
    URL.revokeObjectURL(url);
}
const labels: Record<Stage, string> = {
    A: "A · Preparación",
    B: "B · Interacción de liquidez",
    C: "C · Referencia estructural",
    D: "D · Structural Change",
};
function ReviewSession({ context, sessions, interaction }: Resources) {
    const [saved, setSaved] = useState(() => {
        let raw: string | null = null;
        try {
            raw = localStorage.getItem(reviewKey);
            return { raw, state: decode(raw, context), error: "" };
        } catch (e) {
            return { raw, state: undefined, error: String(e) };
        }
    });
    const [reviewer, setReviewer] = useState(
        saved.state?.labels.at(-1)?.reviewer ?? "",
    );
    const [cutoff, setCutoff] = useState(saved.state?.maximumSeen ?? prep);
    const [frame, setFrame] = useState<Frame>("H4");
    const [selected, setSelected] = useState(0);
    const [draft, setDraft] = useState<Draft>(
        blank(
            !saved.state || !first(saved.state, "A")
                ? "A"
                : !first(saved.state, "B")
                  ? "C"
                  : "D",
        ),
    );
    const [report, setReport] = useState<Comparison>();
    const state = saved.state;
    useEffect(() => {
        let active = true;
        if (state?.revealedRevision !== null && state)
            loadComparison(state)
                .then((r) => {
                    if (active) setReport(r);
                })
                .catch((e) => {
                    if (active) setSaved((s) => ({ ...s, error: String(e) }));
                });
        return () => {
            active = false;
        };
    }, [state]);
    function save(fn: (s: Review) => Review) {
        try {
            if (!state) return false;
            const next = fn(state);
            const raw = persist(localStorage, saved.raw, next, context);
            setSaved({ state: next, raw, error: "" });
            return true;
        } catch (e) {
            setSaved((s) => ({ ...s, error: String(e) }));
            return false;
        }
    }
    const bars = closedAt(context, frame, cutoff);
    const current = bars[selected];
    const selectedM5 = closedAt(context, "M5", cutoff).find(
        (c) => c.openTimeUtc === draft.candleOpen,
    );
    const liquidity = sessionReferencesAt(context, cutoff, sessions);
    const references =
        liquidity?.sessions
            .filter((s) => s.coverageStatus === "Complete")
            .flatMap((s) => [
                { id: s.session === "Asia" ? "AH" : "LH", price: s.high! },
                { id: s.session === "Asia" ? "AL" : "LL", price: s.low! },
            ]) ?? [];
    const inter = interaction.frames.findLast(
        (f) => Date.parse(f.cutoffUtc) <= Date.parse(cutoff),
    )?.interaction;
    const atEvent = selectedM5
        ? sessionReferencesAt(context, selectedM5.openTimeUtc, sessions)
        : undefined;
    const objective =
        draft.stage === "B" && selectedM5
            ? draft.references
                  .map((id) => {
                      const s = atEvent?.sessions.find(
                          (s) =>
                              s.session ===
                              (id.startsWith("A") ? "Asia" : "London"),
                      );
                      const high = id.endsWith("H");
                      const price = high ? s?.high : s?.low;
                      return `${id}: ${s?.coverageStatus !== "Complete" || price === null || price === undefined ? "referencia no disponible antes de la vela" : String(high ? selectedM5.high > price : selectedM5.low < price)}`;
                  })
                  .join(" · ")
            : "Seleccione interacción y referencia";
    const canReveal =
        state && ["A", "B", "C", "D"].every((s) => first(state, s as Stage));
    const showReport =
        report &&
        state?.revealedRevision !== null &&
        Date.parse(cutoff) >= Date.parse(report.comparison.tracking.cutoffUtc);
    const rows = showReport && state ? compare(state, report) : null;
    const stats = rows ? denominators(rows) : null;
    let issue = "";
    if (!state)
        issue =
            "Registro no disponible: exporte el contenido local antes de investigar.";
    else if (!reviewer.trim()) issue = "Indique el nombre del revisor.";
    else if (
        draft.kind === "Selected" &&
        draft.stage === "A" &&
        !draft.references.length
    )
        issue = "Seleccione liquidez o una alternativa explícita.";
    else if (
        draft.kind === "Selected" &&
        draft.stage !== "A" &&
        !draft.candleOpen
    )
        issue = "Seleccione una vela cerrada directamente en el gráfico M5.";
    else if (
        draft.stage === "B" &&
        draft.kind === "Selected" &&
        draft.references.length !== 1
    )
        issue = "Seleccione una sola referencia para esta interacción.";
    else if (draft.stage === "B" && !first(state, "C"))
        issue =
            "Guarde C primero; una selección después de ver el evento es retrospectiva.";
    else if (draft.stage === "D" && !first(state, "B"))
        issue = "Confirme B antes de D.";
    else if (draft.stage !== "A" && !first(state, "A"))
        issue = "Confirme A a las 08:00 Bogotá.";
    const coverage = coverageAt(context, frame, cutoff);
    return (
        <main className="strategy-lab context-research">
            <h1>Revisión contextual cronológica · RESEARCH ONLY</h1>
            <p>
                {context.tradingDay} · {context.providerId} · {context.symbol} ·
                America/Bogota. Selección congelada por cobertura, sin
                resultados. Sin etiquetas humanas precargadas.
            </p>
            <p>
                Cuatro H4 cerradas a preparación; historia previa corta.
                Asia/London H1 completas. M1 original opcional. UTC documental
                no autenticado. Ventana instruida 08:30–11:30 / runtime
                [08:30,11:00), sin corrección.
            </p>
            <label>
                Revisor local{" "}
                <input
                    value={reviewer}
                    maxLength={120}
                    onChange={(e) => setReviewer(e.target.value)}
                />
            </label>
            <label>
                <input
                    type="checkbox"
                    checked={state?.outsideExposure ?? false}
                    disabled={state?.outsideExposure || !state}
                    onChange={() =>
                        save((s) => ({ ...s, outsideExposure: true }))
                    }
                />
                Ya vi esta sesión o recomendaciones fuera de esta revisión
                (declaración irreversible en este registro)
            </label>
            <p>
                Independencia autodeclarada. El navegador contiene archivos
                completos: DevTools, otras rutas y borrar almacenamiento pueden
                eludir ocultación. Sin autenticación ni garantía de ceguera.
            </p>
            <div className="context-controls">
                <fieldset>
                    <legend>Timeframe original</legend>
                    {frames.map((f) => (
                        <button
                            type="button"
                            key={f}
                            aria-pressed={f === frame}
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
                    Cutoff de revisión · Bogotá{" "}
                    <select
                        value={cutoff}
                        onChange={(e) => {
                            if (
                                save((s) => visit(s, e.target.value, context))
                            ) {
                                setCutoff(e.target.value);
                                setSelected(0);
                                const visible = closedAt(
                                    context,
                                    "M5",
                                    e.target.value,
                                );
                                setDraft((d) => ({
                                    ...d,
                                    candleOpen: visible.some(
                                        (c) => c.openTimeUtc === d.candleOpen,
                                    )
                                        ? d.candleOpen
                                        : null,
                                    confirmationOpen: visible.some(
                                        (c) =>
                                            c.openTimeUtc ===
                                            d.confirmationOpen,
                                    )
                                        ? d.confirmationOpen
                                        : null,
                                }));
                            }
                        }}
                        disabled={!state || !first(state, "A")}
                    >
                        {Array.from(
                            new Set([
                                prep,
                                ...context.series.M5.candles
                                    .filter(
                                        (c) =>
                                            Date.parse(c.closeTimeUtc) >
                                            Date.parse(prep),
                                    )
                                    .map((c) => c.closeTimeUtc),
                            ]),
                        ).map((t) => (
                            <option key={t} value={t}>
                                {bogota(t)} · {t.slice(11, 16)}Z
                            </option>
                        ))}
                    </select>
                </label>
            </div>
            <p>
                {frame}: {bars.length} velas cerradas · {coverage.gaps.length}{" "}
                rangos sin filas fuente. Volver atrás no borra la exposición
                máxima {formatTime(state?.maximumSeen ?? prep)}.
            </p>
            <p>
                Referencias objetivas H1:{" "}
                {references.map((r) => `${r.id} ${r.price}`).join(" · ")}.
                Interacción London–Asia:{" "}
                {inter?.completedClassification ??
                    inter?.status ??
                    "No disponible"}
                . No son recomendaciones de relevancia.
            </p>
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
                onPick={(i) => {
                    setSelected(i);
                    if (frame === "M5" && draft.stage !== "A")
                        setDraft((d) => ({
                            ...d,
                            candleOpen: bars[i].openTimeUtc,
                        }));
                }}
            />
            {current && (
                <p>
                    Vela {formatTime(current.openTimeUtc)} / cierre{" "}
                    {formatTime(current.closeTimeUtc)} · nativo{" "}
                    {current.nativeOpenTime} · O {current.open} H {current.high}{" "}
                    L {current.low} C {current.close}
                </p>
            )}
            <section aria-label="Etiquetado contextual">
                <h2>{labels[draft.stage]}</h2>
                <p>
                    A confirma contexto sin dirección obligatoria; C guarda un
                    High/Low antes de exponer el evento; B registra su
                    relevancia; D selecciona SC o ausencia/ambigüedad. Puede
                    guardar C antes de B. Si ya vio el evento, C no contará como
                    pre-evento independiente.
                </p>
                <div>
                    {(["A", "C", "B", "D"] as Stage[]).map((s) => (
                        <button key={s} onClick={() => setDraft(blank(s))}>
                            {labels[s]}
                        </button>
                    ))}
                </div>
                <label>
                    Observación{" "}
                    <select
                        value={draft.kind}
                        onChange={(e) =>
                            setDraft((d) => ({
                                ...d,
                                kind: e.target.value as Kind,
                            }))
                        }
                    >
                        <option value="Selected">Seleccionar</option>
                        <option value="None">
                            Ninguna referencia / interacción / SC observado
                        </option>
                        <option value="Ambiguous">Ambigua</option>
                        <option value="Insufficient">
                            Datos insuficientes
                        </option>
                        <option value="Skip">Omitir</option>
                        {draft.stage === "D" && (
                            <option value="SameCandle">
                                Secuencia ambigua en la misma vela
                            </option>
                        )}
                    </select>
                </label>
                {(draft.stage === "A" || draft.stage === "B") && (
                    <fieldset>
                        <legend>Liquidez evaluada por el humano</legend>
                        {references.map((r) => (
                            <label key={r.id}>
                                <input
                                    type="checkbox"
                                    checked={draft.references.includes(r.id)}
                                    onChange={(e) =>
                                        setDraft((d) => ({
                                            ...d,
                                            references: e.target.checked
                                                ? [...d.references, r.id]
                                                : d.references.filter(
                                                      (x) => x !== r.id,
                                                  ),
                                        }))
                                    }
                                />
                                {r.id} {r.price}
                            </label>
                        ))}
                    </fieldset>
                )}
                {draft.stage === "B" && (
                    <>
                        <p>
                            Penetración OHLC estricta objetiva, referencia
                            disponible al inicio: {objective}. Historial
                            anterior visible al mover el cutoff; una penetración
                            repetida no implica un nuevo sweep válido.
                        </p>
                        <label>
                            <input
                                type="checkbox"
                                checked={draft.relevant}
                                onChange={(e) =>
                                    setDraft((d) => ({
                                        ...d,
                                        relevant: e.target.checked,
                                    }))
                                }
                            />
                            Considero relevante esta interacción (juicio humano
                            separado de penetración OHLC)
                        </label>
                    </>
                )}
                {draft.stage === "C" && (
                    <>
                        <label>
                            Coordenada original{" "}
                            <select
                                value={draft.side}
                                onChange={(e) =>
                                    setDraft((d) => ({
                                        ...d,
                                        side: e.target.value as "High" | "Low",
                                    }))
                                }
                            >
                                <option>High</option>
                                <option>Low</option>
                            </select>
                        </label>
                        <button
                            disabled={frame !== "M5" || !current}
                            onClick={() =>
                                setDraft((d) => ({
                                    ...d,
                                    confirmationOpen: current.openTimeUtc,
                                }))
                            }
                        >
                            Usar vela inspeccionada como evidencia humana de
                            confirmación
                        </button>
                        <button
                            onClick={() =>
                                setDraft((d) => ({
                                    ...d,
                                    confirmationOpen: null,
                                }))
                            }
                        >
                            Sin evidencia de confirmación
                        </button>
                    </>
                )}
                <p>
                    Selección: {draft.candleOpen ?? "ninguna"}; confirmación
                    humana: {draft.confirmationOpen ?? "no suministrada"}.
                    Precio e identidad se derivan del OHLC original; la
                    formación no se trata como disponibilidad confirmada.
                </p>
                <label>
                    Contexto/razón opcional (incluya H4 si lo evaluó)
                    <input
                        value={draft.note}
                        maxLength={1000}
                        onChange={(e) =>
                            setDraft((d) => ({ ...d, note: e.target.value }))
                        }
                    />
                </label>
                {issue && <p role="status">{issue}</p>}
                <button
                    disabled={Boolean(issue)}
                    onClick={() => {
                        if (
                            save((s) =>
                                commit(
                                    s,
                                    draft,
                                    context,
                                    cutoff,
                                    reviewer,
                                    new Date().toISOString(),
                                ),
                            )
                        )
                            setDraft(
                                blank(
                                    draft.stage === "A"
                                        ? "C"
                                        : draft.stage === "C"
                                          ? "B"
                                          : draft.stage === "B"
                                            ? "D"
                                            : "D",
                                ),
                            );
                    }}
                >
                    Confirmar observación · conservar original
                </button>
            </section>
            <p>
                Referencia C:{" "}
                {state ? referenceEligibility(state) : "sin registro"}.
                Revisiones tras exposición conservan el original; correcciones
                posteriores a revelar no recalculan la comparación original.
            </p>
            <button
                disabled={
                    !canReveal ||
                    Date.parse(cutoff) < Date.parse(context.maximumCutoffUtc)
                }
                onClick={() => save(reveal)}
            >
                E · Revelar comparación congelada
            </button>
            {!canReveal ? (
                <p>Para revelar confirme A, C, B y D.</p>
            ) : (
                Date.parse(cutoff) < Date.parse(context.maximumCutoffUtc) && (
                    <p>
                        Avance al cierre del episodio para revelar el reporte
                        completo sin exponer datos posteriores al cutoff.
                    </p>
                )
            )}
            {rows && stats && (
                <section aria-label="Comparación contextual revelada">
                    <h2>Comparación por dimensión · sin ganador</h2>
                    <p>
                        Sesiones: {stats.sessions}; combinaciones:{" "}
                        {stats.combinations}. Denominadores: liquidez{" "}
                        {stats.liquidity}, evento {stats.event}, precio{" "}
                        {stats.price}, vela estructural {stats.candle},
                        disponibilidad {stats.availability}, SC {stats.sc},
                        cierre SC {stats.confirmation}. Abstenciones{" "}
                        {stats.abstentions}. Denominadores por dimensión;
                        combinaciones no son sesiones independientes. Acuerdos
                        nulos no son errores. Diferencias de tiempo en
                        milisegundos; ninguna exactitud global ni P&amp;L.
                    </p>
                    <div className="lab-chart-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th>Hipótesis / política / variante</th>
                                    <th>Liquidez / evento</th>
                                    <th>Precio / vela / Δ disponibilidad</th>
                                    <th>SC / Δ cierre</th>
                                    <th>Estado / comparación</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows.map((r) => (
                                    <tr key={r.id}>
                                        <td>{r.id}</td>
                                        <td>
                                            {String(r.liquidityAgreement)} /{" "}
                                            {String(r.eventAgreement)}
                                        </td>
                                        <td>
                                            {String(r.priceAgreement)} /{" "}
                                            {String(r.candleAgreement)} /{" "}
                                            {r.availabilityDeltaMs ??
                                                "no comparable"}
                                        </td>
                                        <td>
                                            {String(r.scAgreement)} /{" "}
                                            {r.confirmationDeltaMs ??
                                                "no comparable"}
                                        </td>
                                        <td>
                                            {r.status} · {r.detection} ·
                                            referencia {r.referenceDetection} ·
                                            misma vela {String(r.sameCandle)}
                                            <small>{r.reason}</small>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </section>
            )}
            {state && (
                <details>
                    <summary>
                        Originales y revisiones ({state.labels.length})
                    </summary>
                    {state.labels
                        .filter(
                            (l) => Date.parse(l.cutoff) <= Date.parse(cutoff),
                        )
                        .map((l) => (
                            <p key={l.revision}>
                                r{l.revision} · {labels[l.stage]} · {l.kind} ·{" "}
                                {l.price ?? "sin precio estructural"} ·{" "}
                                {l.classification} · cutoff {l.cutoff} ·{" "}
                                {l.note}
                            </p>
                        ))}
                </details>
            )}
            <button
                onClick={() =>
                    download(
                        state
                            ? exportReview(state, context)
                            : (saved.raw ?? "Registro no disponible"),
                    )
                }
            >
                Exportar revisión e historial
            </button>
            {saved.error && <p role="alert">{saved.error}</p>}
            <p>
                CanonicalAdmissionPermitted=false · OperationalSignal=false. Sin
                órdenes, promociones ni autorización de entrada. La misma vela y
                posible refinamiento M1 quedan abiertos. Datos/etiquetas locales
                no autentican disponibilidad histórica.
            </p>
        </main>
    );
}
export function ContextualScReview() {
    const [loaded, setLoaded] = useState<{ data?: Resources; error?: string }>(
        {},
    );
    useEffect(() => {
        let active = true;
        resources()
            .then((data) => {
                if (active) setLoaded({ data });
            })
            .catch((e) => {
                if (active) setLoaded({ error: String(e) });
            });
        return () => {
            active = false;
        };
    }, []);
    return loaded.data ? (
        <ReviewSession {...loaded.data} />
    ) : (
        <p role={loaded.error ? "alert" : "status"}>
            {loaded.error ?? "Cargando episodio congelado…"}
        </p>
    );
}
