import { useEffect, useMemo, useRef, useState } from "react";
import { CandleChart } from "../StrategyLab";
import developmentUrl from "../context/data/june23-context.json?url";
import {
    chartCandle,
    frames,
    maxFileBytes,
    projectDevelopment,
    readOriginalPackage,
    requireValue,
    syntheticPacket,
    type Frame,
    type OriginalPackage,
} from "./originalPackage";
import {
    canPick,
    exposures,
    importReview,
    kinds,
    loadReview,
    reference,
    targets,
    type CandleReference,
    type Exposure,
    type Kind,
    type Review,
    type Target,
} from "./reviewHistory";
import {
    appendEvent,
    earliestOpen,
    exportEventReview,
    groupEvents,
    importEventReview,
    loadEventReview,
    persistEventReview,
    recordEventReveal,
    respectsLegacy,
    startEventReview,
    type EventReview,
    type EventRevision,
} from "./eventReview";
import { SessionOverlayLegend } from "./SessionOverlayLegend";
import {
    availableSessionLayers,
    sessionConfiguration,
    sessionOverlays,
} from "./sessionOverlays";
import "./IntegratedReview.css";

const kindLabels: Record<Kind, string> = {
    Reference: "Referencia observada",
    NoReference: "Sin referencia",
    Ambiguous: "Ambigua",
    InsufficientSourceEvidence: "Evidencia insuficiente",
    Skip: "Revisión omitida",
};
const exposureLabels: Record<Exposure, string> = {
    Unknown: "Exposición desconocida",
    SelfDeclaredIndependent: "Declaro no haber visto resultados",
    PreviouslyExposed: "Ya vi resultados",
};
const failure = (error: unknown) =>
    error instanceof SyntaxError
        ? "JSON inválido o truncado."
        : error instanceof Error
          ? error.message
          : "No se pudo completar la revisión.";
const now = () => new Date().toISOString();
const shortTime = (iso: string) => iso.slice(0, 16).replace("T", " ") + "Z";

function readLocal(file: File): Promise<string> {
    requireValue(
        file.size > 0 && file.size <= maxFileBytes,
        "Seleccione un JSON local de hasta 8 MiB.",
    );
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () =>
            reject(new Error("No se pudo leer el archivo local."));
        reader.readAsText(file);
    });
}

function download(text: string) {
    const url = URL.createObjectURL(
        new Blob([text], { type: "application/json" }),
    );
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "moneyway-nasdaq-integrated-human-review-v2.json";
    anchor.click();
    URL.revokeObjectURL(url);
}

function Workbench({
    packet,
    initial,
    initialLegacy,
}: {
    packet: OriginalPackage;
    initial: EventReview | null;
    initialLegacy: Review | null;
}) {
    const times = useMemo(
        () =>
            Array.from(
                new Set([
                    ...frames.flatMap((tf) =>
                        packet.sources[tf].bars.map((b) =>
                            Date.parse(b.closeUtc),
                        ),
                    ),
                    Date.parse(packet.cutoffUtc),
                ]),
            )
                .sort((a, b) => a - b)
                .map((t) => new Date(t).toISOString()),
        [packet],
    );
    const [review, setReview] = useState(initial);
    const [legacy, setLegacy] = useState(initialLegacy);
    const [eventId, setEventId] = useState("");
    const [revision, setRevision] = useState(0);
    const [editor, setEditor] = useState<"new" | "edit" | null>(null);
    const [scopeStart, setScopeStart] = useState("");
    const [cursor, setCursor] = useState(0);
    const [frame, setFrame] = useState<Frame>("H4");
    const [showSessionBands, setShowSessionBands] = useState(true);
    const [showSessionLevels, setShowSessionLevels] = useState(true);
    const [selected, setSelected] = useState(-1);
    const [target, setTarget] = useState<Target>("H4Context");
    const [kind, setKind] = useState<Kind>("Reference");
    const [note, setNote] = useState("");
    const [picks, setPicks] = useState<CandleReference[]>([]);
    const [reviewer, setReviewer] = useState("");
    const [exposure, setExposure] = useState<Exposure | "">("");
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const active = useRef(true);
    useEffect(() => {
        active.current = true;
        return () => {
            active.current = false;
        };
    }, []);
    const currentTime = times[cursor];
    const sessions = useMemo(
        () => sessionOverlays(packet, currentTime),
        [packet, currentTime],
    );
    const visibleRevisions = (review?.labels ?? []).filter(
        (l) => Date.parse(l.viewingCutoffUtc) <= Date.parse(currentTime),
    );
    const events = groupEvents(visibleRevisions);
    const history = events.get(eventId) ?? [];
    const inspected =
        history.find((label) => label.revision === revision) ?? history.at(-1);
    const visible = packet.sources[frame].bars.filter(
        (b) => Date.parse(b.closeUtc) <= Date.parse(currentTime),
    );
    const bars = visible.slice(-80);
    const guide = bars[selected];
    const gapCount = visible.filter(
        (b, i) =>
            i > 0 &&
            Date.parse(visible[i - 1].closeUtc) < Date.parse(b.openUtc),
    ).length;

    function save(next: EventReview) {
        persistEventReview(packet, review, next, legacy);
        setReview(next);
    }

    function act(fn: () => void) {
        setError("");
        try {
            fn();
        } catch (e) {
            setError(failure(e));
        }
    }

    function navigate(index: number) {
        act(() => {
            if (
                review &&
                Date.parse(times[index]) > Date.parse(review.maximumViewedUtc)
            )
                save({ ...review, maximumViewedUtc: times[index] });
            setPicks((old) =>
                old.filter(
                    (b) => Date.parse(b.closeUtc) <= Date.parse(times[index]),
                ),
            );
            if (Date.parse(scopeStart) >= Date.parse(times[index]))
                setScopeStart("");
            setSelected(-1);
            setCursor(index);
        });
    }

    function chooseFrame(value: Frame) {
        setFrame(value);
        setSelected(-1);
    }

    async function openReview(file: File, historical = false) {
        setBusy(true);
        setError("");
        try {
            const text = await readLocal(file);
            if (historical) {
                const next = await importReview(packet, legacy, text);
                if (!active.current) return;
                requireValue(
                    !review || respectsLegacy(review, next),
                    "La revisión V1 no coincide con la exposición o el corte ya registrado en V2.",
                );
                setLegacy(next);
                return;
            }
            const next = await importEventReview(packet, review, text);
            if (!active.current) return;
            save(next);
            setEventId("");
            setRevision(0);
            setEditor(null);
        } catch (e) {
            if (active.current) setError(failure(e));
        } finally {
            if (active.current) setBusy(false);
        }
    }

    function pick(i: number) {
        if (
            busy ||
            !editor ||
            !canPick(target, frame) ||
            (kind !== "Reference" && kind !== "Ambiguous")
        )
            return;
        const ref = reference(packet, frame, bars[i].openUtc);
        setPicks((old) =>
            old.some((b) => b.timeframe === frame && b.openUtc === ref.openUtc)
                ? old.filter(
                      (b) => b.timeframe !== frame || b.openUtc !== ref.openUtc,
                  )
                : [...old, ref],
        );
    }

    function openEditor(existing?: EventRevision) {
        setEditor(existing ? "edit" : "new");
        setTarget(existing?.observation ?? "H4Context");
        setKind(existing?.kind ?? "Reference");
        setNote(existing?.note ?? "");
        setScopeStart(existing?.scopeStartUtc ?? "");
        setPicks(existing?.references ?? []);
        if (existing && !canPick(existing.observation, frame))
            chooseFrame(targets[existing.observation].frames[0]);
    }

    function inspect(label: EventRevision) {
        setRevision(label.revision);
        setEditor(null);
        const ref = label.references[0];
        if (ref) chooseFrame(ref.timeframe);
    }

    return (
        <section aria-label="Sesión para revisión">
            <h2>
                {packet.operatingDate} · US100 ·{" "}
                {packet.exposure === "Synthetic"
                    ? "Sintética"
                    : "Development expuesto"}
            </h2>
            <p>
                Corte máximo {shortTime(packet.cutoffUtc)} · Disponibilidad
                histórica UTC sin autenticar.
            </p>
            <details>
                <summary>Procedencia y hashes</summary>
                <p>
                    Paquete <code>{packet.sha256}</code>
                </p>
                <p>
                    Fuente de la proyección{" "}
                    <code>{packet.provenanceSha256}</code>
                </p>
                <ul>
                    {frames.map((tf) => (
                        <li key={tf}>
                            {tf} · {packet.sources[tf].mapping} ·{" "}
                            <code>{packet.sources[tf].originalSha256}</code>
                        </li>
                    ))}
                </ul>
            </details>
            <fieldset className="integrated-controls" disabled={busy}>
                <label>
                    Importar etiquetas JSON V2
                    <input
                        type="file"
                        accept=".json,application/json"
                        onChange={(e) => {
                            const file = e.currentTarget.files?.[0];
                            e.currentTarget.value = "";
                            if (file) void openReview(file);
                        }}
                    />
                </label>
                {review ? (
                    <>
                        <p>
                            Revisor {review.reviewerId} ·{" "}
                            {exposureLabels[review.exposure]}
                        </p>
                        <button
                            type="button"
                            onClick={() => {
                                void exportEventReview(packet, review)
                                    .then(download)
                                    .catch((e) => setError(failure(e)));
                            }}
                        >
                            Exportar etiquetas JSON V2
                        </button>
                    </>
                ) : legacy ? (
                    <button
                        type="button"
                        onClick={() =>
                            act(() => {
                                save(
                                    startEventReview(
                                        packet,
                                        legacy.reviewerId,
                                        legacy.exposure,
                                        currentTime,
                                        now(),
                                        legacy,
                                    ),
                                );
                            })
                        }
                    >
                        Iniciar revisión V2 con la declaración V1
                    </button>
                ) : (
                    <>
                        <label>
                            Revisor
                            <input
                                value={reviewer}
                                maxLength={120}
                                onChange={(e) => setReviewer(e.target.value)}
                            />
                        </label>
                        <label>
                            Exposición declarada
                            <select
                                value={exposure}
                                onChange={(e) =>
                                    setExposure(e.target.value as Exposure | "")
                                }
                            >
                                <option value="">Seleccione</option>
                                {exposures.map((v) => (
                                    <option key={v} value={v}>
                                        {exposureLabels[v]}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <button
                            type="button"
                            onClick={() =>
                                act(() => {
                                    requireValue(
                                        exposure,
                                        "Declare su exposición antes de revisar.",
                                    );
                                    save(
                                        startEventReview(
                                            packet,
                                            reviewer,
                                            exposure,
                                            currentTime,
                                            now(),
                                        ),
                                    );
                                })
                            }
                        >
                            Iniciar revisión
                        </button>
                    </>
                )}
            </fieldset>
            <details>
                <summary>Historial V1 · solo lectura</summary>
                <p>
                    Las anotaciones V1 conservan su identidad y orden
                    originales.
                </p>
                <label>
                    Consultar archivo de etiquetas V1
                    <input
                        type="file"
                        accept=".json,application/json"
                        disabled={busy}
                        onChange={(e) => {
                            const file = e.currentTarget.files?.[0];
                            e.currentTarget.value = "";
                            if (file) void openReview(file, true);
                        }}
                    />
                </label>
                {legacy && (
                    <>
                        <p>
                            {legacy.reviewerId} ·{" "}
                            {exposureLabels[legacy.exposure]}
                        </p>
                        <ol>
                            {legacy.labels
                                .filter(
                                    (l) =>
                                        !review ||
                                        Date.parse(l.viewingCutoffUtc) <=
                                            Date.parse(currentTime),
                                )
                                .map((l) => (
                                    <li key={l.revision}>
                                        {l.targetId} · Revisión {l.revision} ·{" "}
                                        {kindLabels[l.kind]} · {l.reviewedAtUtc}{" "}
                                        · {l.note}
                                        <ul>
                                            {l.references.map((ref) => (
                                                <li
                                                    key={
                                                        ref.timeframe +
                                                        ref.openUtc
                                                    }
                                                >
                                                    {ref.timeframe} ·{" "}
                                                    {ref.nativeOpen} ·{" "}
                                                    {ref.openUtc}
                                                </li>
                                            ))}
                                        </ul>
                                    </li>
                                ))}
                        </ol>
                    </>
                )}
            </details>
            {error && (
                <p role="alert" className="lab-warning">
                    {error}
                </p>
            )}
            {review && (
                <>
                    <fieldset disabled={busy} className="integrated-controls">
                        <legend>Navegación cronológica</legend>
                        <label>
                            Temporalidad
                            <select
                                value={frame}
                                onChange={(e) =>
                                    chooseFrame(e.target.value as Frame)
                                }
                            >
                                {frames.map((tf) => (
                                    <option key={tf}>{tf}</option>
                                ))}
                            </select>
                        </label>
                        <label>
                            Posición temporal
                            <input
                                type="range"
                                min={0}
                                max={times.length - 1}
                                value={cursor}
                                aria-valuetext={shortTime(currentTime)}
                                onChange={(e) =>
                                    navigate(e.target.valueAsNumber)
                                }
                            />
                        </label>
                        <button
                            type="button"
                            disabled={cursor === 0}
                            onClick={() => navigate(cursor - 1)}
                        >
                            Anterior
                        </button>
                        <button
                            type="button"
                            disabled={cursor === times.length - 1}
                            onClick={() => navigate(cursor + 1)}
                        >
                            Siguiente
                        </button>
                        <button
                            type="button"
                            disabled={cursor === times.length - 1}
                            onClick={() => navigate(times.length - 1)}
                        >
                            Corte final
                        </button>
                    </fieldset>
                    <p>
                        Corte visible {shortTime(currentTime)} ·{" "}
                        {visible.length} velas cerradas · {gapCount} brechas
                        entre velas.
                        {visible.length > bars.length &&
                            " Se muestran las últimas 80 del corte."}
                    </p>
                    {packet.sources[frame].mapping === "Unresolved" && (
                        <p role="status">
                            Mapeo UTC sin verificar para {frame}. No hay velas
                            temporales utilizables en esta escala.
                        </p>
                    )}
                    {sessions.length > 0 && (
                        <SessionOverlayLegend
                            sessions={sessions}
                            bands={showSessionBands}
                            levels={showSessionLevels}
                            h4={frame === "H4"}
                            onBands={setShowSessionBands}
                            onLevels={setShowSessionLevels}
                        />
                    )}
                    <CandleChart
                        report={{
                            input: {
                                symbol: "US100",
                                timeframe: frame,
                                evaluationCutoffUtc: currentTime,
                            },
                            evidenceCandles: bars.map((b) =>
                                chartCandle(frame, b),
                            ),
                            results: [],
                        }}
                        visible={[]}
                        selected={selected}
                        onSelect={setSelected}
                        onPick={pick}
                        originalOnly
                        documentarySessions={{
                            sessions,
                            bands: showSessionBands && frame !== "H4",
                            levels: showSessionLevels,
                        }}
                        humanReferences={(editor
                            ? picks
                            : (inspected?.references ?? [])
                        )
                            .filter((b) => b.timeframe === frame)
                            .map((b) => b.openUtc)}
                    />
                    {guide && (
                        <p>
                            Vela {frame} · nativa {guide.nativeOpen} · apertura{" "}
                            {guide.openUtc} · cierre {guide.closeUtc}
                            {" · "}O {guide.open} H {guide.high} L {guide.low} C{" "}
                            {guide.close}
                        </p>
                    )}
                    <fieldset disabled={busy} className="integrated-controls">
                        <legend>Eventos registrados</legend>
                        <button type="button" onClick={() => openEditor()}>
                            Nuevo evento
                        </button>
                        <label>
                            Evento registrado
                            <select
                                value={inspected ? eventId : ""}
                                onChange={(e) => {
                                    const id = e.currentTarget.value;
                                    setEventId(id);
                                    setRevision(0);
                                    setEditor(null);
                                    const selectedRef = events.get(id)?.at(-1)
                                        ?.references[0];
                                    if (selectedRef)
                                        chooseFrame(selectedRef.timeframe);
                                }}
                            >
                                <option value="">Seleccione un evento</option>
                                {[...events.entries()].map(([id, labels]) => {
                                    const last = labels.at(-1)!;
                                    return (
                                        <option key={id} value={id}>
                                            Evento {last.eventNumber} ·{" "}
                                            {targets[last.observation].label}
                                        </option>
                                    );
                                })}
                            </select>
                        </label>
                        <button
                            type="button"
                            disabled={!inspected}
                            onClick={() => openEditor(inspected)}
                        >
                            Editar evento seleccionado
                        </button>
                    </fieldset>
                    {editor && (
                        <fieldset
                            disabled={busy}
                            className="integrated-controls"
                        >
                            <legend>
                                {editor === "new"
                                    ? "Nuevo evento"
                                    : "Corregir evento seleccionado"}
                            </legend>
                            <label>
                                Observación
                                <select
                                    value={target}
                                    disabled={editor === "edit"}
                                    onChange={(e) => {
                                        const value = e.target.value as Target;
                                        setTarget(value);
                                        setPicks([]);
                                        if (!canPick(value, frame))
                                            chooseFrame(
                                                targets[value].frames[0],
                                            );
                                    }}
                                >
                                    {Object.entries(targets).map(([id, v]) => (
                                        <option key={id} value={id}>
                                            {v.label}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            {kind === "NoReference" && (
                                <label>
                                    Inicio del alcance
                                    <select
                                        value={scopeStart}
                                        onChange={(e) =>
                                            setScopeStart(e.target.value)
                                        }
                                    >
                                        <option value="">
                                            Seleccione el inicio
                                        </option>
                                        {[
                                            ...new Set([
                                                new Date(
                                                    earliestOpen(packet),
                                                ).toISOString(),
                                                ...times.slice(0, cursor),
                                            ]),
                                        ]
                                            .filter(
                                                (t) =>
                                                    Date.parse(t) <
                                                    Date.parse(currentTime),
                                            )
                                            .map((t) => (
                                                <option key={t} value={t}>
                                                    {shortTime(t)}
                                                </option>
                                            ))}
                                    </select>
                                </label>
                            )}
                            <label>
                                Clasificación
                                <select
                                    value={kind}
                                    onChange={(e) => {
                                        const value = e.target.value as Kind;
                                        setKind(value);
                                        if (
                                            value !== "Reference" &&
                                            value !== "Ambiguous"
                                        )
                                            setPicks([]);
                                    }}
                                >
                                    {kinds.map((k) => (
                                        <option key={k} value={k}>
                                            {kindLabels[k]}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <label>
                                Evidencia o motivo
                                <textarea
                                    value={note}
                                    maxLength={2000}
                                    onChange={(e) => setNote(e.target.value)}
                                />
                            </label>
                            <button
                                type="button"
                                disabled={editor === "edit" && !inspected}
                                onClick={() =>
                                    act(() => {
                                        const available =
                                            availableSessionLayers(
                                                packet,
                                                frame,
                                                currentTime,
                                            );
                                        const bands =
                                            showSessionBands && available.bands;
                                        const levels =
                                            showSessionLevels &&
                                            available.levels;
                                        const next = appendEvent(
                                            packet,
                                            review,
                                            editor === "edit" ? eventId : null,
                                            {
                                                observation: target,
                                                kind,
                                                references: picks,
                                                note,
                                                viewingCutoffUtc: currentTime,
                                                ...(bands || levels
                                                    ? {
                                                          sessionOverlay: {
                                                              configurationId:
                                                                  sessionConfiguration,
                                                              frame,
                                                              bands,
                                                              levels,
                                                          },
                                                      }
                                                    : {}),
                                                scopeStartUtc:
                                                    kind === "NoReference"
                                                        ? scopeStart
                                                        : null,
                                            },
                                            now(),
                                        );
                                        save(next);
                                        const latest = next.labels.at(-1)!;
                                        setEventId(latest.targetId);
                                        inspect(latest);
                                        setNote("");
                                        setPicks([]);
                                    })
                                }
                            >
                                {editor === "new"
                                    ? "Guardar evento"
                                    : "Guardar corrección"}
                            </button>
                            <button
                                type="button"
                                onClick={() => setEditor(null)}
                            >
                                Cancelar
                            </button>
                        </fieldset>
                    )}
                    {editor && (
                        <p>
                            Seleccione una vela en el gráfico para añadirla o
                            retirarla de la evidencia.
                        </p>
                    )}
                    <ul aria-label="Velas seleccionadas">
                        {(editor ? picks : (inspected?.references ?? [])).map(
                            (p) => (
                                <li key={p.timeframe + p.openUtc}>
                                    {p.timeframe} · {p.nativeOpen} · {p.openUtc}
                                </li>
                            ),
                        )}
                    </ul>
                    <h3>Historial de revisiones</h3>
                    {inspected && (
                        <p>
                            Identidad <code>{eventId}</code>
                        </p>
                    )}
                    {review.reveal ? (
                        <p>
                            Exposición externa registrada tras la revisión{" "}
                            {review.reveal.afterRevision}.
                        </p>
                    ) : (
                        <button
                            type="button"
                            disabled={busy}
                            onClick={() =>
                                act(() => {
                                    save(
                                        recordEventReveal(
                                            packet,
                                            review,
                                            now(),
                                        ),
                                    );
                                })
                            }
                        >
                            Registrar que vi resultados fuera de esta vista
                        </button>
                    )}
                    <ol>
                        {history.map((l) => (
                            <li key={l.revision}>
                                <button
                                    type="button"
                                    onClick={() => inspect(l)}
                                    aria-pressed={
                                        inspected?.revision === l.revision
                                    }
                                >
                                    Revisión {l.eventRevision} del evento
                                </button>
                                {" · Registro "}
                                {l.revision} · {kindLabels[l.kind]} ·{" "}
                                {l.phase === "PostRevealCorrection"
                                    ? "Corrección posterior a revelación"
                                    : "Juicio previo a revelación"}{" "}
                                · {l.note} · {l.reviewedAtUtc}
                                {l.sessionOverlay && (
                                    <>
                                        {" · "}Guías visibles al guardar (
                                        {l.sessionOverlay.frame}
                                        {l.sessionOverlay.bands
                                            ? ", bandas"
                                            : ""}
                                        {l.sessionOverlay.levels
                                            ? ", niveles"
                                            : ""}
                                        )
                                    </>
                                )}
                                {l.reviewMode ===
                                    "RetrospectiveAfterLaterView" && (
                                    <>
                                        {" "}
                                        · Retrospectiva tras ver hasta{" "}
                                        {shortTime(l.maximumViewedUtc)}
                                    </>
                                )}
                                {l.scopeStartUtc && (
                                    <>
                                        {" "}
                                        · Alcance {shortTime(
                                            l.scopeStartUtc,
                                        )} a {shortTime(l.viewingCutoffUtc)}
                                    </>
                                )}
                            </li>
                        ))}
                    </ol>
                </>
            )}
        </section>
    );
}

export default function IntegratedReview() {
    const [loaded, setLoaded] = useState<{
        packet: OriginalPackage;
        review: EventReview | null;
        legacy: Review | null;
    } | null>(null);
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);
    const ordinal = useRef(0);
    async function load(read: () => Promise<OriginalPackage>) {
        const run = ++ordinal.current;
        setLoaded(null);
        setError("");
        setLoading(true);
        try {
            const packet = await read();
            if (ordinal.current === run) {
                const review = loadEventReview(packet);
                const legacy = loadReview(packet);
                requireValue(
                    !review || respectsLegacy(review, legacy),
                    "Los historiales V1 y V2 tienen datos de exposición incompatibles.",
                );
                setLoaded({ packet, review, legacy });
            }
        } catch (e) {
            if (ordinal.current === run) setError(failure(e));
        } finally {
            if (ordinal.current === run) setLoading(false);
        }
    }
    return (
        <main className="strategy-lab integrated-review">
            <nav className="lab-nav" aria-label="Navegación Strategy Lab">
                <a href="#strategy-lab">← Strategy Lab</a>
            </nav>
            <h1>Revisión humana ciega Nasdaq</h1>
            <p>
                Etiquetas de investigación sobre velas originales. Las
                declaraciones de exposición y las fechas del dispositivo quedan
                registradas como evidencia local.
            </p>
            <div className="integrated-controls">
                <button
                    type="button"
                    onClick={() =>
                        void load(async () =>
                            readOriginalPackage(await syntheticPacket()),
                        )
                    }
                >
                    Abrir caso sintético
                </button>
                <button
                    type="button"
                    onClick={() =>
                        void load(async () => {
                            const response = await fetch(developmentUrl);
                            requireValue(
                                response.ok,
                                "La fuente Development no está disponible.",
                            );
                            return projectDevelopment(await response.text());
                        })
                    }
                >
                    Abrir Development del 23 de junio
                </button>
                <label>
                    Abrir paquete original local
                    <input
                        type="file"
                        accept=".json,application/json"
                        onChange={(e) => {
                            const file = e.currentTarget.files?.[0];
                            e.currentTarget.value = "";
                            if (file)
                                void load(async () =>
                                    readOriginalPackage(await readLocal(file)),
                                );
                        }}
                    />
                </label>
            </div>
            {loading && <p role="status">Verificando fuente local…</p>}
            {error && (
                <p role="alert" className="lab-warning">
                    {error}
                </p>
            )}
            {loaded && (
                <Workbench
                    key={loaded.packet.sha256}
                    packet={loaded.packet}
                    initial={loaded.review}
                    initialLegacy={loaded.legacy}
                />
            )}
        </main>
    );
}
