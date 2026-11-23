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
    appendLabel,
    canPick,
    exportReview,
    exposures,
    importReview,
    kinds,
    loadReview,
    newReview,
    persistReview,
    recordExternalReveal,
    reference,
    targets,
    type CandleReference,
    type Exposure,
    type Kind,
    type Review,
    type Target,
} from "./reviewHistory";
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
    anchor.download = "moneyway-nasdaq-integrated-human-review-v1.json";
    anchor.click();
    URL.revokeObjectURL(url);
}

function Workbench({
    packet,
    initial,
}: {
    packet: OriginalPackage;
    initial: Review | null;
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
    const [cursor, setCursor] = useState(0);
    const [frame, setFrame] = useState<Frame>("H4");
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

    function save(next: Review) {
        persistReview(packet, review, next);
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
            setSelected(-1);
            setCursor(index);
        });
    }

    function chooseFrame(value: Frame) {
        setFrame(value);
        setSelected(-1);
    }

    async function openReview(file: File) {
        setBusy(true);
        setError("");
        try {
            const next = await importReview(
                packet,
                review,
                await readLocal(file),
            );
            if (!active.current) return;
            save(next);
            setPicks([]);
            setNote("");
        } catch (e) {
            if (active.current) setError(failure(e));
        } finally {
            if (active.current) setBusy(false);
        }
    }

    function pick(i: number) {
        if (
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
                    Importar etiquetas JSON
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
                                void exportReview(packet, review)
                                    .then(download)
                                    .catch((e) => setError(failure(e)));
                            }}
                        >
                            Exportar etiquetas JSON
                        </button>
                    </>
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
                                        newReview(
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
                        humanReferences={picks
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
                        <legend>Anotación humana</legend>
                        <label>
                            Observación
                            <select
                                value={target}
                                onChange={(e) => {
                                    const value = e.target.value as Target;
                                    setTarget(value);
                                    setPicks([]);
                                    if (!canPick(value, frame))
                                        chooseFrame(targets[value].frames[0]);
                                }}
                            >
                                {Object.entries(targets).map(([id, v]) => (
                                    <option key={id} value={id}>
                                        {v.label}
                                    </option>
                                ))}
                            </select>
                        </label>
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
                            onClick={() =>
                                act(() => {
                                    save(
                                        appendLabel(
                                            packet,
                                            review,
                                            target,
                                            kind,
                                            picks,
                                            note,
                                            currentTime,
                                            now(),
                                        ),
                                    );
                                    setNote("");
                                    setPicks([]);
                                })
                            }
                        >
                            Guardar revisión
                        </button>
                    </fieldset>
                    <p>
                        Seleccione una vela en el gráfico para añadirla o
                        retirarla de la evidencia.
                    </p>
                    <ul aria-label="Velas seleccionadas">
                        {picks.map((p) => (
                            <li key={p.timeframe + p.openUtc}>
                                {p.timeframe} · {p.nativeOpen} · {p.openUtc}
                            </li>
                        ))}
                    </ul>
                    <h3>Historial local</h3>
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
                                        recordExternalReveal(
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
                        {review.labels
                            .filter(
                                (l) =>
                                    Date.parse(l.viewingCutoffUtc) <=
                                    Date.parse(currentTime),
                            )
                            .map((l) => (
                                <li key={l.revision}>
                                    Revisión {l.revision} ·{" "}
                                    {targets[l.observation].label} ·{" "}
                                    {kindLabels[l.kind]} ·{" "}
                                    {l.phase === "PostRevealCorrection"
                                        ? "Corrección posterior a revelación"
                                        : "Juicio previo a revelación"}{" "}
                                    · {l.note}
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
        review: Review | null;
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
            if (ordinal.current === run)
                setLoaded({ packet, review: loadReview(packet) });
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
                />
            )}
        </main>
    );
}
