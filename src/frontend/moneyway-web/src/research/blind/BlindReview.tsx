import { useEffect, useState } from "react";
import { CandleChart } from "../StrategyLab";
import { algorithmIds, formatTime, type Envelope } from "../report";
import {
    catalog,
    charts,
    episodeById,
    loadRevealedReport,
    type Episode,
    type Partition,
} from "./catalog";
import {
    commitLabel,
    decodeState,
    emptyDraft,
    exportResearch,
    independentLabel,
    persist,
    reveal,
    selection,
    storageKey,
    type Draft,
    type History,
    type LabelKind,
    type ReviewState,
} from "./labels";
import { compareReference, rate, summarize } from "./metrics";
import "./BlindReview.css";

function download(text: string) {
    const url = URL.createObjectURL(
        new Blob([text], { type: "application/json" }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = "moneyway-m5-research-labels-v1.json";
    a.click();
    URL.revokeObjectURL(url);
}
const percent = (n: number, d: number) => {
    const r = rate(n, d);
    return r
        ? `${n}/${d} (${r.percent.toFixed(1)}%)`
        : "No disponible (0 casos)";
};

function Summary({
    state,
    reports,
}: {
    state: ReviewState;
    reports: Record<string, Envelope>;
}) {
    const [includeOther, setIncludeOther] = useState(false);
    const groups = summarize(state, reports, includeOther);
    return (
        <details className="lab-details">
            <summary>Resumen descriptivo por partición y exposición</summary>
            <label>
                <input
                    type="checkbox"
                    checked={includeOther}
                    onChange={(e) => setIncludeOther(e.target.checked)}
                />{" "}
                Mostrar también sintéticos y previamente expuestos, en grupos
                separados
            </label>
            <p>
                Una observación por episodio. Se usa la última revisión anterior
                a la primera revelación; las correcciones posteriores no mejoran
                las métricas. Acuerdo humano no demuestra rentabilidad.
            </p>
            {!groups.length && (
                <p>
                    Sin etiquetas auténticas independientes comparables. No se
                    inventan porcentajes.
                </p>
            )}
            {groups.map((g) => (
                <div key={g.key}>
                    <h3>
                        {g.partition} · {g.origin} · {g.exposure}
                    </h3>
                    <p>
                        Revisados: {g.reviewed} · Elegibles: {g.eligibleLabels}{" "}
                        · Ambiguos: {g.ambiguity} · Omitidos: {g.skipped} ·
                        Pendientes de revelar/cargar: {g.pendingReveal}
                    </p>
                    <div className="lab-chart-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th>Algoritmo</th>
                                    <th>
                                        Cobertura (detecciones / elegibles
                                        comparados)
                                    </th>
                                    <th>Acuerdo exacto condicional</th>
                                    <th>Precio</th>
                                    <th>Identidad de velas</th>
                                    <th>Empates</th>
                                    <th>
                                        Ref. incorrectas / ambigüedad de empates
                                    </th>
                                    <th>Falsas / omitidas</th>
                                    <th>Abstenciones / sin confirmar</th>
                                    <th>Acuerdo sin referencia</th>
                                </tr>
                            </thead>
                            <tbody>
                                {g.algorithms.map((a) => (
                                    <tr key={a.id}>
                                        <td>{a.id}</td>
                                        <td>
                                            {percent(
                                                a.detections,
                                                a.comparedEligible,
                                            )}
                                        </td>
                                        <td>
                                            {percent(
                                                a.exactReferences,
                                                a.comparableReferences,
                                            )}
                                        </td>
                                        <td>
                                            {percent(
                                                a.priceAgreements,
                                                a.comparableReferences,
                                            )}
                                        </td>
                                        <td>
                                            {percent(
                                                a.candleAgreements,
                                                a.comparableReferences,
                                            )}
                                        </td>
                                        <td>
                                            {percent(
                                                a.equalExtremeAgreements,
                                                a.comparableTies,
                                            )}
                                        </td>
                                        <td>
                                            {a.incorrectReferences} /{" "}
                                            {a.tieAmbiguities}
                                        </td>
                                        <td>
                                            {a.falseDetections} /{" "}
                                            {a.missedDetections}
                                        </td>
                                        <td>
                                            {a.abstentions} / {a.unconfirmed}
                                        </td>
                                        <td>{a.correctNoReference}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            ))}
        </details>
    );
}

function EpisodeReview({
    episode: e,
    history,
    reviewer,
    reports,
    save,
    next,
    hasNext,
}: {
    episode: Episode;
    history?: History;
    reviewer: string;
    reports: Record<string, Envelope>;
    save: (fn: (s: ReviewState) => ReviewState) => boolean;
    next: () => void;
    hasNext: boolean;
}) {
    const original = history?.revisions[0];
    const latest = history?.revisions.at(-1);
    const [draft, setDraft] = useState<Draft>(
        latest
            ? {
                  kind: latest.kind,
                  selectedOpens: latest.selectedOpens,
                  confirmationOpenUtc: latest.confirmationOpenUtc,
              }
            : emptyDraft(),
    );
    const [editing, setEditing] = useState(!history);
    const [selected, setSelected] = useState(0);
    const [confirmMode, setConfirmMode] = useState(false);
    const [known, setKnown] = useState(
        Boolean(
            e.priorExposure === "PreviouslyExposed" ||
            history?.exposure === "PreviouslyExposed",
        ),
    );
    const [message, setMessage] = useState("");
    const [visible, setVisible] = useState(algorithmIds);
    const chart = charts[e.id];
    const bar = chart.evidenceCandles[selected];
    const shownLabel = editing ? draft : latest!;
    const envelope = history?.revealedAtDeviceUtc ? reports[e.id] : undefined;
    const comparisonLabel = history ? independentLabel(history) : undefined;
    const picked =
        shownLabel.kind === "Reference" && shownLabel.selectedOpens.length
            ? selection(e, shownLabel.selectedOpens)
            : null;
    function choose(index: number) {
        if (!editing) return;
        const candle = chart.evidenceCandles[index];
        setMessage("");
        if (confirmMode) {
            setDraft((d) => ({
                ...d,
                confirmationOpenUtc: candle.openTimeUtc,
            }));
            setConfirmMode(false);
            return;
        }
        if (Date.parse(candle.openTimeUtc) <= Date.parse(e.anchorOpenUtc)) {
            setMessage("Seleccione la referencia después del ancla explícita.");
            return;
        }
        const price = e.direction === "Ascending" ? candle.low : candle.high;
        let opens = [candle.openTimeUtc];
        if (
            draft.kind === "Reference" &&
            draft.selectedOpens.length &&
            selection(e, draft.selectedOpens).price === price
        )
            opens = draft.selectedOpens.includes(candle.openTimeUtc)
                ? draft.selectedOpens.filter((t) => t !== candle.openTimeUtc)
                : [...draft.selectedOpens, candle.openTimeUtc];
        setDraft({
            kind: "Reference",
            selectedOpens: opens,
            confirmationOpenUtc: null,
        });
    }
    return (
        <>
            <div className="blind-steps" aria-label="Progreso de revisión">
                <b>1 Seleccionar</b>
                <b className={history ? "done" : ""}>2 Confirmar</b>
                <b className={envelope ? "done" : ""}>3 Revelar</b>
                <button
                    type="button"
                    disabled={!history || !hasNext}
                    onClick={next}
                >
                    {hasNext ? "Siguiente episodio →" : "Fin de partición"}
                </button>
            </div>
            <div className="lab-episode-header">
                <div>
                    <span className="lab-kicker">
                        {e.partition} · {e.dataOrigin} · Research Only
                    </span>
                    <h2>
                        {e.symbol} · M5{" "}
                        <span>
                            {e.direction} · {e.providerId}
                        </span>
                    </h2>
                </div>
                <div className="lab-cutoff">
                    {formatTime(e.startUtc)}
                    <br />
                    Cutoff {formatTime(e.cutoffUtc)}
                </div>
            </div>
            <div className="lab-warning">
                {known
                    ? "PreviouslyExposed: no es una observación ciega independiente."
                    : history?.revealedAtDeviceUtc
                      ? "Resultados ya revelados: toda nueva selección será una corrección posterior."
                      : "Revisión retrospectiva independiente autodeclarada: algoritmos ocultos hasta confirmar y revelar."}{" "}
                {e.dataOrigin === "Synthetic"
                    ? "Fixture sintético, sin actividad de mercado auténtica."
                    : "UTC documental de junio; sin disponibilidad histórica autenticada."}{" "}
                CanonicalAdmissionPermitted=false · OperationalSignal=false.
            </div>
            {envelope && (
                <fieldset className="lab-controls">
                    <legend>Overlays después de revelar</legend>
                    {algorithmIds.map((id) => (
                        <label key={id}>
                            <input
                                type="checkbox"
                                checked={visible.includes(id)}
                                onChange={(event) =>
                                    setVisible(
                                        event.target.checked
                                            ? [...visible, id]
                                            : visible.filter((v) => v !== id),
                                    )
                                }
                            />
                            {id.split("-")[3]}
                        </label>
                    ))}
                </fieldset>
            )}
            <CandleChart
                report={{
                    ...chart,
                    results: envelope?.comparison.results ?? [],
                }}
                visible={envelope ? visible : []}
                selected={selected}
                onSelect={setSelected}
                onPick={choose}
                humanReferences={
                    shownLabel.kind === "Reference"
                        ? shownLabel.selectedOpens
                        : []
                }
                humanConfirmation={shownLabel.confirmationOpenUtc}
            />
            <div className="lab-inspector" aria-live="polite">
                <strong>{formatTime(bar.openTimeUtc)}</strong>
                <span>
                    O {bar.open} · H {bar.high} · L {bar.low} · C {bar.close}
                </span>
                <small>
                    Cierre {formatTime(bar.closeTimeUtc)} · H blanco: selección
                    humana, no recomendación.
                </small>
                <button
                    type="button"
                    disabled={selected === 0}
                    onClick={() => setSelected((i) => i - 1)}
                >
                    ← Inspeccionar
                </button>
                <button
                    type="button"
                    disabled={selected === chart.evidenceCandles.length - 1}
                    onClick={() => setSelected((i) => i + 1)}
                >
                    Inspeccionar →
                </button>
            </div>
            <div className="blind-selection">
                <fieldset disabled={!editing}>
                    <legend>Juicio humano de investigación</legend>
                    <div className="blind-options">
                        {(
                            [
                                ["Reference", "Referencia válida"],
                                ["NoReference", "Sin referencia válida"],
                                [
                                    "Ambiguous",
                                    "Ambiguo / contexto insuficiente",
                                ],
                                ["Skip", "Omitir para revisar luego"],
                            ] as [LabelKind, string][]
                        ).map(([kind, label]) => (
                            <label key={kind}>
                                <input
                                    type="radio"
                                    name="human-kind"
                                    value={kind}
                                    checked={draft.kind === kind}
                                    onChange={() => {
                                        setDraft({ ...emptyDraft(), kind });
                                        setConfirmMode(false);
                                    }}
                                />
                                {label}
                            </label>
                        ))}
                    </div>
                    {draft.kind === "Reference" && (
                        <>
                            <p>
                                Haga clic o pulse Enter sobre una vela posterior
                                al ancla. Seleccione varias solo si comparten el
                                mismo{" "}
                                {e.direction === "Ascending" ? "Low" : "High"};
                                no se preseleccionan extremos.
                            </p>
                            <button
                                type="button"
                                onClick={() => setConfirmMode((v) => !v)}
                                aria-pressed={confirmMode}
                            >
                                {confirmMode
                                    ? "Seleccione la vela de confirmación…"
                                    : "Marcar confirmación independiente (opcional)"}
                            </button>
                            <button
                                type="button"
                                disabled={!draft.confirmationOpenUtc}
                                onClick={() =>
                                    setDraft((d) => ({
                                        ...d,
                                        confirmationOpenUtc: null,
                                    }))
                                }
                            >
                                Quitar confirmación
                            </button>
                        </>
                    )}
                    <label className="blind-known">
                        <input
                            type="checkbox"
                            checked={known}
                            disabled={
                                e.priorExposure === "PreviouslyExposed" ||
                                history?.exposure === "PreviouslyExposed"
                            }
                            onChange={(event) => setKnown(event.target.checked)}
                        />
                        Ya conocía este episodio o sus resultados
                    </label>
                </fieldset>
                <div className="blind-pick" aria-live="polite">
                    <strong>
                        {picked
                            ? `${picked.extreme} ${picked.price} · ${picked.chosen.length} vela(s)`
                            : shownLabel.kind === "Reference"
                              ? "Seleccione una vela; no se sugiere ninguna."
                              : shownLabel.kind}
                    </strong>
                    {shownLabel.selectedOpens.map((t) => (
                        <span key={t}>{formatTime(t)}</span>
                    ))}
                    <span>
                        Confirmación humana:{" "}
                        {shownLabel.confirmationOpenUtc
                            ? formatTime(
                                  chart.evidenceCandles.find(
                                      (b) =>
                                          b.openTimeUtc ===
                                          shownLabel.confirmationOpenUtc,
                                  )!.closeTimeUtc,
                              )
                            : "no disponible; no se deriva del algoritmo"}
                    </span>
                </div>
                {message && <p role="alert">{message}</p>}
                {editing ? (
                    <button
                        className="blind-primary"
                        type="button"
                        disabled={
                            !reviewer.trim() ||
                            (draft.kind === "Reference" &&
                                !draft.selectedOpens.length)
                        }
                        onClick={() => {
                            if (
                                save((s) =>
                                    commitLabel(
                                        s,
                                        e.id,
                                        draft,
                                        reviewer,
                                        known,
                                        new Date().toISOString(),
                                    ),
                                )
                            ) {
                                setEditing(false);
                                setMessage("");
                            }
                        }}
                    >
                        Confirmar selección
                        {history?.revealedAtDeviceUtc
                            ? " · corrección post-revelación"
                            : ""}
                    </button>
                ) : (
                    <button type="button" onClick={() => setEditing(true)}>
                        {history?.revealedAtDeviceUtc
                            ? "Registrar corrección (conservar original)"
                            : "Revisar selección antes de revelar"}
                    </button>
                )}
                {history && !history.revealedAtDeviceUtc && !editing && (
                    <button
                        className="blind-primary"
                        type="button"
                        onClick={() =>
                            save((s) =>
                                reveal(s, e.id, new Date().toISOString()),
                            )
                        }
                    >
                        Revelar algoritmos
                    </button>
                )}
                {history?.revealedAtDeviceUtc && !envelope && (
                    <p role="status">
                        Revelación registrada; cargando reporte verificado…
                    </p>
                )}
            </div>
            {envelope && comparisonLabel && (
                <section
                    className="blind-comparison"
                    aria-label="Comparación después de revelar"
                >
                    <h3>
                        Comparación con revisión {comparisonLabel.revision} ·{" "}
                        {history!.exposure}
                    </h3>
                    <div className="lab-chart-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th>Algoritmo / versión</th>
                                    <th>Estado / referencia</th>
                                    <th>Formación</th>
                                    <th>Conocida al cierre</th>
                                    <th>Acuerdo con humano</th>
                                    <th>Δ precio / absoluto</th>
                                    <th>Δ confirmación humana</th>
                                </tr>
                            </thead>
                            <tbody>
                                {envelope.comparison.results.map((r) => {
                                    const a = compareReference(
                                        r,
                                        comparisonLabel,
                                    );
                                    return (
                                        <tr key={r.algorithm.algorithmId}>
                                            <td>
                                                {r.algorithm.algorithmId} /{" "}
                                                {r.algorithm.algorithmVersion}
                                            </td>
                                            <td>
                                                {r.status}
                                                <br />
                                                {r.proposedReferencePrice ??
                                                    "—"}
                                            </td>
                                            <td>
                                                {r.referenceCandles.map((c) => (
                                                    <div key={c.openTimeUtc}>
                                                        {formatTime(
                                                            c.openTimeUtc,
                                                        )}
                                                    </div>
                                                ))}
                                            </td>
                                            <td>
                                                {formatTime(
                                                    r.confirmationAtUtc,
                                                )}
                                            </td>
                                            <td>
                                                {a.outcome}
                                                <br />
                                                Precio:{" "}
                                                {String(
                                                    a.priceAgreement ??
                                                        "no comparable",
                                                )}{" "}
                                                · Velas:{" "}
                                                {String(
                                                    a.candleAgreement ??
                                                        "no comparable",
                                                )}
                                            </td>
                                            <td>
                                                {a.signedDifference ?? "—"} /{" "}
                                                {a.absoluteDifference ?? "—"}
                                            </td>
                                            <td>
                                                {a.latencyMilliseconds === null
                                                    ? "No disponible"
                                                    : `${a.latencyMilliseconds / 1000} s`}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                    <details className="lab-details">
                        <summary>
                            Hipótesis y motivos originales del runner
                        </summary>
                        {envelope.comparison.results.map((r) => (
                            <p key={r.algorithm.algorithmId}>
                                {r.algorithm.algorithmId}:{" "}
                                {r.algorithm.startCondition}. {r.reason}
                            </p>
                        ))}
                    </details>
                    <p>
                        Formación ≠ confirmación. Una referencia retrospectiva
                        no era necesariamente conocida en su vela. Sin ganador
                        ni P&amp;L.
                    </p>
                </section>
            )}
            {history && (
                <details className="lab-details">
                    <summary>
                        Original y revisiones · {history.revisions.length}{" "}
                        registro(s)
                    </summary>
                    <p>
                        Original: {original!.kind} · revisión{" "}
                        {original!.revision} · {original!.reviewer} ·{" "}
                        {original!.classification}. Identidad humana local sin
                        autenticación.
                    </p>
                    {history.revisions.map((l) => (
                        <p key={l.revision}>
                            r{l.revision} · {l.kind} · {l.price ?? "sin precio"}{" "}
                            · {l.classification} · dispositivo{" "}
                            {l.reviewedAtDeviceUtc} · cutoff {l.cutoffUtc}
                        </p>
                    ))}
                </details>
            )}
            <details className="lab-details">
                <summary>Procedencia, ancla y limitaciones</summary>
                <p>{chart.input.provenance}</p>
                <p>
                    Ancla explícita: {formatTime(e.anchorOpenUtc)} ·{" "}
                    {e.anchorPrice}. Dirección y primera vela son supuestos de
                    investigación, no una referencia validada del mentor.
                </p>
                <p>
                    Contenido input {e.inputSha256}; reporte {e.reportSha256}.
                    Originales nativos:{" "}
                    {e.nativeOpenIdentities.join(", ") || "sintético"}.
                </p>
                <p>
                    No se muestran velas posteriores al cutoff. Inspeccionar no
                    cambia la evaluación. El extremo de confirmación queda
                    excluido de V1 aunque su mecha exceda la referencia:
                    limitación experimental, no corregida aquí.
                </p>
            </details>
        </>
    );
}

export function BlindReview() {
    const [saved, setSaved] = useState(() => {
        let raw: string | null = null;
        try {
            raw = localStorage.getItem(storageKey);
            return { raw, state: decodeState(raw), error: "" };
        } catch (error) {
            return {
                raw,
                state: undefined,
                error:
                    error instanceof Error
                        ? error.message
                        : "Almacenamiento local no disponible",
            };
        }
    });
    const [reviewer, setReviewer] = useState(saved.state?.reviewer ?? "");
    const [partition, setPartition] = useState<Partition>("Development");
    const [index, setIndex] = useState(0);
    const [reports, setReports] = useState<Record<string, Envelope>>({});
    const available = catalog.episodes.filter((e) => e.partition === partition);
    const episode = available[index] ?? available[0];
    useEffect(() => {
        let active = true;
        const ids = Object.keys(saved.state?.records ?? {}).filter(
            (id) => saved.state?.records[id].revealedAtDeviceUtc,
        );
        Promise.all(
            ids.map(
                async (id) =>
                    [id, await loadRevealedReport(episodeById(id))] as const,
            ),
        )
            .then((entries) => {
                if (active) setReports(Object.fromEntries(entries));
            })
            .catch((error: unknown) => {
                if (active)
                    setSaved((s) => ({
                        ...s,
                        error:
                            error instanceof Error
                                ? error.message
                                : "Reporte no disponible",
                    }));
            });
        return () => {
            active = false;
        };
    }, [saved.state]);
    function save(fn: (s: ReviewState) => ReviewState) {
        try {
            if (!saved.state) return false;
            const state = fn(saved.state);
            const raw = persist(localStorage, saved.raw, state);
            setSaved({ state, raw, error: "" });
            return true;
        } catch (error) {
            setSaved((s) => ({
                ...s,
                error:
                    error instanceof Error
                        ? error.message
                        : "No se pudo guardar",
            }));
            return false;
        }
    }
    return (
        <main className="strategy-lab blind-review">
            <nav className="lab-nav">
                <a href="#strategy-lab">← Exploración V2 (no ciega)</a>
                <span>
                    Strategy Lab · revisión ciega <b>RESEARCH ONLY</b>
                </span>
                <button
                    type="button"
                    onClick={() =>
                        download(
                            saved.state
                                ? exportResearch(saved.state)
                                : (saved.raw ?? "Registro no accesible"),
                        )
                    }
                >
                    Exportar etiquetas e historial
                </button>
            </nav>
            <header className="blind-heading">
                <div>
                    <p className="lab-kicker">
                        {catalog.datasetId} · versión {catalog.datasetVersion}
                    </p>
                    <h1>Seleccionar → Confirmar → Revelar</h1>
                    <p>
                        Etiquetas humanas de investigación, sin autoridad
                        operativa.
                    </p>
                </div>
                <label>
                    Revisor local
                    <input
                        value={reviewer}
                        maxLength={120}
                        onChange={(e) => setReviewer(e.target.value)}
                        placeholder="Su nombre; se conserva al confirmar"
                    />
                </label>
            </header>
            <div className="blind-navigation">
                <label>
                    Partición
                    <select
                        value={partition}
                        onChange={(e) => {
                            setPartition(e.target.value as Partition);
                            setIndex(0);
                        }}
                    >
                        <option value="Development">Desarrollo</option>
                        <option value="Validation">
                            Validación (V1 sin cambios)
                        </option>
                        <option value="PreviouslyExposed">
                            Ya expuesto · V2
                        </option>
                        <option value="SoftwareAcceptance">
                            Aceptación sintética · no métricas auténticas
                        </option>
                    </select>
                </label>
                <label>
                    Episodio {index + 1} / {available.length}
                    <select
                        value={episode.id}
                        onChange={(e) =>
                            setIndex(
                                available.findIndex(
                                    (v) => v.id === e.target.value,
                                ),
                            )
                        }
                    >
                        {available.map((e, i) => (
                            <option key={e.id} value={e.id}>
                                {i + 1}. {e.startUtc.slice(0, 16)} ·{" "}
                                {e.direction}
                            </option>
                        ))}
                    </select>
                </label>
                <span>
                    Etiquetas guardadas:{" "}
                    {available.filter((e) => saved.state?.records[e.id]).length}{" "}
                    / {available.length}
                </span>
            </div>
            <p className="blind-notice">
                Dos episodios finales reservados fuera del frontend, sin
                resultados calculados. Separación cronológica sin solapamiento,
                pero evidencia insuficiente para generalización. Solo OHLC hasta
                el cutoff; ninguna etiqueta viene del consenso.
            </p>
            {saved.error && <p role="alert">{saved.error}</p>}
            {saved.state && (
                <EpisodeReview
                    key={episode.id}
                    episode={episode}
                    history={saved.state.records[episode.id]}
                    reviewer={reviewer}
                    reports={reports}
                    save={save}
                    hasNext={index < available.length - 1}
                    next={() =>
                        setIndex((i) => Math.min(i + 1, available.length - 1))
                    }
                />
            )}
            {saved.state?.records[episode.id]?.revealedAtDeviceUtc && (
                <Summary state={saved.state} reports={reports} />
            )}
            <p className="blind-notice">
                Persistencia solo en este navegador/origen (incluido el puerto).
                Puede perderse, editarse o borrarse; no está autenticada ni es
                inmutable. Exporte el historial al finalizar. La fecha del
                dispositivo no autentica la disponibilidad histórica.
            </p>
        </main>
    );
}
