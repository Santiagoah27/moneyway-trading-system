import { useMemo, useRef, useState } from "react";
import { CandleChart } from "../StrategyLab";
import developmentUrl from "../context/data/june23-context.json?url";
import {
    chartCandle,
    frames,
    maxFileBytes,
    projectDevelopment,
    readOriginalPackage,
    requireValue,
    type Frame,
    type OriginalPackage,
} from "../integratedReview/originalPackage";
import {
    targets,
    type CandleReference,
} from "../integratedReview/reviewHistory";
import { atMost, utc } from "../observation/sessionEvidence";
import { maxReportBytes } from "../observation/sessionReport";
import {
    appendDecision,
    decisionKinds,
    exportComparison,
    exposureKinds,
    humanRows,
    humanVisibility,
    importComparison,
    outcomes,
    prepareComparison,
    recordDisclosure,
    relationship,
    type Comparison,
    type Decision,
    type DecisionKind,
    type Disclosure,
    type EvidenceRelationship,
} from "./comparison";

const relations: Record<EvidenceRelationship, string> = {
    ExactDocumentary: "Coincidencia documental exacta",
    PartialDocumentary: "Coincidencia documental parcial",
    DifferentEvidence: "Referencias documentales distintas",
    InsideNoReferenceScope: "Dentro del intervalo sin referencia",
    OutsideNoReferenceScope: "Fuera del intervalo sin referencia",
    InsufficientTemporalEvidence:
        "Evidencia temporal insuficiente al corte humano",
    NotComparable: "No comparable",
    HumanJudgmentUnresolved: "Juicio humano sin resolver",
};
const decisionsInSpanish: Record<DecisionKind, string> = {
    Confirmed: "Correspondencia confirmada",
    DoesNotCorrespond: "No corresponde",
    Ambiguous: "Ambigua",
    InsufficientEvidence: "Evidencia insuficiente",
};
const exposureInSpanish: Record<Disclosure["declaredExposure"], string> = {
    Unknown: "Exposición desconocida",
    PreviouslyExposed: "Ya vi resultados",
    DeclaresNoPriorResults: "Declaro no haber visto resultados",
};
const fail = (e: unknown) =>
    e instanceof Error ? e.message : "No se pudo completar la comparación.";

async function bytes(file: File, max = maxFileBytes) {
    requireValue(
        file.size > 0 && file.size <= max,
        "Tamaño de archivo local incompatible.",
    );
    // Preserve a UTF-8 BOM to verify the exact file bytes against the supplied pin.
    const buffer = await new Promise<ArrayBuffer>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result as ArrayBuffer);
        reader.onerror = () =>
            reject(new Error("No se pudo leer el archivo local."));
        reader.readAsArrayBuffer(file);
    });
    return new TextDecoder("utf-8", { fatal: true, ignoreBOM: true }).decode(
        buffer,
    );
}

function FileField({
    label,
    onFile,
}: {
    label: string;
    onFile: (file: File) => void;
}) {
    return (
        <label>
            {label}
            <input
                type="file"
                accept=".json,application/json"
                onChange={(event) => {
                    const file = event.currentTarget.files?.[0];
                    event.currentTarget.value = "";
                    if (file) onFile(file);
                }}
            />
        </label>
    );
}

function references(refs: CandleReference[]) {
    return (
        <ul>
            {refs.map((r) => (
                <li key={r.timeframe + r.openUtc}>
                    {r.timeframe} · {r.nativeOpen} · {r.openUtc} — {r.closeUtc}{" "}
                    · <code>{r.originalSha256}</code>
                </li>
            ))}
        </ul>
    );
}

function download(text: string) {
    const url = URL.createObjectURL(
        new Blob([text], { type: "application/json" }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = "moneyway-nasdaq-evidence-comparison-v1.json";
    a.click();
    URL.revokeObjectURL(url);
}

function ReviewComparison({
    comparison,
    packet,
}: {
    comparison: Comparison;
    packet: OriginalPackage;
}) {
    const [disclosure, setDisclosure] = useState<Disclosure | null>(null);
    const [reviewer, setReviewer] = useState("");
    const [exposure, setExposure] = useState<
        Disclosure["declaredExposure"] | ""
    >("");
    const [history, setHistory] = useState<Decision[]>([]);
    const [left, setLeft] = useState("");
    const [right, setRight] = useState("");
    const [kind, setKind] = useState<DecisionKind>("Ambiguous");
    const [note, setNote] = useState("");
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const [frame, setFrame] = useState<Frame>("M5");
    const [cursor, setCursor] = useState(-1);
    const humans = useMemo(() => humanRows(comparison), [comparison]);
    const summary = useMemo(
        () => (disclosure ? outcomes(comparison, history) : null),
        [comparison, disclosure, history],
    );
    const human = humans.find((h) => h.selected.targetId === left);
    const candidates = human?.inScope
        ? comparison.candidates.filter(
              (e) => e.type === human.selected.observation,
          )
        : [];
    const candidate = candidates.find((c) => c.id === right);
    const relation =
        human && candidate ? relationship(human.selected, candidate) : null;
    const canDisplayCandidate =
        candidate?.at &&
        human &&
        atMost(candidate.at, human.selected.viewingCutoffUtc);
    const visibleAtJudgment = ({ at }: { at: string | null }) =>
        !!(at && human && atMost(at, human.selected.viewingCutoffUtc));
    const bars =
        human &&
        packet.sources[frame].bars
            .filter((b) => atMost(b.closeUtc, human.selected.viewingCutoffUtc))
            .slice(-80);
    const key = JSON.stringify([
        "moneyway.nasdaq.comparison-disclosure.v1",
        packet.sessionId,
        packet.operatingDate,
        comparison.review.reviewerId,
    ]);

    function act(fn: () => void) {
        setError("");
        try {
            fn();
        } catch (e) {
            setError(fail(e));
        }
    }

    function reveal() {
        act(() => {
            requireValue(
                exposure,
                "Declare la exposición para abrir resultados.",
            );
            const now = new Date().toISOString();
            const prior = localStorage.getItem(key);
            const d = recordDisclosure(
                comparison,
                reviewer,
                exposure,
                now,
                prior === null ? now : utc(prior, "primera apertura"),
            );
            if (prior === null) localStorage.setItem(key, d.firstOpenedAtUtc);
            setDisclosure(d);
        });
    }

    async function restore(file: File, current: Disclosure) {
        setBusy(true);
        setError("");
        try {
            const saved = await importComparison(comparison, await bytes(file));
            requireValue(
                saved.disclosure.reviewerId === current.reviewerId &&
                    JSON.stringify(history) ===
                        JSON.stringify(
                            saved.decisions.slice(0, history.length),
                        ),
                "El archivo no preserva el revisor y el historial actual.",
            );
            const first = atMost(
                saved.disclosure.firstOpenedAtUtc,
                current.firstOpenedAtUtc,
            )
                ? saved.disclosure.firstOpenedAtUtc
                : current.firstOpenedAtUtc;
            localStorage.setItem(key, first);
            setDisclosure({ ...saved.disclosure, firstOpenedAtUtc: first });
            setHistory(saved.decisions);
        } catch (e) {
            setError(fail(e));
        } finally {
            setBusy(false);
        }
    }

    return (
        <>
            <p>
                Insumos verificados · {packet.operatingDate} · {packet.exposure}{" "}
                · {packet.cutoffUtc}
            </p>
            <p>
                Exposición declarada en las etiquetas:{" "}
                {comparison.review.exposure} ·{" "}
                {comparison.review.reveal
                    ? "revelación registrada tras revisión " +
                      comparison.review.reveal.afterRevision
                    : "sin marcador de revelación en el archivo"}
            </p>
            {error && (
                <p role="alert" className="lab-warning">
                    {error}
                </p>
            )}
            {!disclosure || !summary ? (
                <fieldset>
                    <legend>
                        Registrar exposición antes de abrir resultados
                    </legend>
                    <label>
                        Revisor de comparación{" "}
                        <input
                            value={reviewer}
                            onChange={(e) => setReviewer(e.target.value)}
                        />
                    </label>{" "}
                    <label>
                        Exposición al informe{" "}
                        <select
                            value={exposure}
                            onChange={(e) =>
                                setExposure(e.target.value as typeof exposure)
                            }
                        >
                            <option value="">Seleccione</option>
                            {exposureKinds.map((e) => (
                                <option key={e} value={e}>
                                    {exposureInSpanish[e]}
                                </option>
                            ))}
                        </select>
                    </label>{" "}
                    <button type="button" onClick={reveal}>
                        Abrir comparación retrospectiva
                    </button>
                </fieldset>
            ) : (
                <>
                    <p role="status">
                        Comparación retrospectiva · reloj y disponibilidad
                        histórica documentales · validación independiente no
                        evaluada.
                    </p>
                    <details>
                        <summary>Identidades y alcance</summary>
                        <p>{comparison.reportRunId}</p>
                        <p>
                            SHA-256 informe{" "}
                            <code>{comparison.manifest.reportSha256}</code> ·
                            manifiesto <code>{comparison.manifestSha256}</code>
                        </p>
                        <p>
                            Configuración{" "}
                            {comparison.manifest.configurationId ??
                                "todas las configuraciones informadas"}
                        </p>
                        <p>
                            {comparison.manifest.observationTypes
                                .map((t) => targets[t].label)
                                .join(" · ")}
                        </p>
                        <p>
                            Primera apertura registrada{" "}
                            {disclosure.firstOpenedAtUtc} · revisor{" "}
                            {disclosure.reviewerId}
                        </p>
                    </details>
                    <h2>Conteos del alcance seleccionado</h2>
                    <ul>
                        <li>
                            Eventos humanos en alcance{" "}
                            {summary.scopedHumanEvents}/{summary.humanEvents} ·{" "}
                            {summary.humanRevisions} revisiones
                        </li>
                        <li>
                            Eventos experimentales en alcance{" "}
                            {summary.scopedCandidateEvents}/
                            {summary.candidateEvents} ·{" "}
                            {summary.candidateConfigurationAssociations}{" "}
                            asociaciones con configuraciones
                        </li>
                        <li>
                            Instancias con evidencia comparable{" "}
                            {summary.comparableHumanEvents}/
                            {summary.scopedHumanEvents} humanas y{" "}
                            {summary.comparableCandidateEvents}/
                            {summary.scopedCandidateEvents} experimentales
                        </li>
                        <li>
                            Coincidencias documentales exactas{" "}
                            {summary.exactPairs}/{summary.comparablePairs} pares
                            comparables
                        </li>
                        <li>
                            Decisiones guardadas por par {summary.decisions} ·
                            confirmadas {summary.confirmed} · discrepancias{" "}
                            {summary.discrepancies} · ambiguas{" "}
                            {summary.ambiguous} · insuficientes{" "}
                            {summary.insufficient}
                        </li>
                        <li>
                            Pares con evidencia temporal insuficiente{" "}
                            {summary.temporalInsufficiencyPairs}
                        </li>
                        <li>
                            Sin asociación {summary.humanUnassociated} humanos y{" "}
                            {summary.candidateUnassociated} experimentales
                        </li>
                        <li>
                            Fuera del universo comparable{" "}
                            {summary.humansOutsideUniverse} humanos y{" "}
                            {summary.candidatesOutsideUniverse} experimentales
                        </li>
                    </ul>
                    <fieldset disabled={busy}>
                        <legend>Inspeccionar y asociar evidencia</legend>
                        <label>
                            Evento humano{" "}
                            <select
                                value={left}
                                onChange={(e) => {
                                    setLeft(e.target.value);
                                    setRight("");
                                    setCursor(-1);
                                }}
                            >
                                <option value="">Seleccione</option>
                                {humans.map((h) => (
                                    <option
                                        key={h.selected.targetId}
                                        value={h.selected.targetId}
                                    >
                                        {h.selected.targetId} ·{" "}
                                        {targets[h.selected.observation].label}
                                    </option>
                                ))}
                            </select>
                        </label>{" "}
                        <label>
                            Evento experimental{" "}
                            <select
                                value={right}
                                onChange={(e) => setRight(e.target.value)}
                            >
                                <option value="">Seleccione</option>
                                {candidates.map((c) => (
                                    <option key={c.id} value={c.id}>
                                        {human &&
                                        c.at &&
                                        atMost(
                                            c.at,
                                            human.selected.viewingCutoffUtc,
                                        )
                                            ? c.observations.find(
                                                  visibleAtJudgment,
                                              )?.id +
                                              " · " +
                                              c.description
                                            : "Evidencia experimental fuera del corte humano"}
                                    </option>
                                ))}
                            </select>
                        </label>
                        {human && (
                            <section aria-label="Evidencia humana">
                                <h3>Juicio humano</h3>
                                <p>
                                    {human.selected.kind} · revisión{" "}
                                    {human.selected.revision} ·{" "}
                                    {humanVisibility(
                                        human.selected,
                                        disclosure,
                                    )}
                                </p>
                                <p>
                                    {human.selected.note} ·{" "}
                                    {human.selected.reviewMode} · corte{" "}
                                    {human.selected.viewingCutoffUtc}
                                </p>
                                {human.selected.scopeStartUtc && (
                                    <p>
                                        Intervalo sin referencia desde{" "}
                                        {human.selected.scopeStartUtc}
                                    </p>
                                )}
                                <p>
                                    Última revisión previa al marcador en la
                                    exportación:{" "}
                                    {human.preReveal?.revision ?? "ninguna"}
                                </p>
                                {references(human.selected.references)}
                                <details>
                                    <summary>Revisiones originales</summary>
                                    <ol>
                                        {human.history.map((r) => (
                                            <li key={r.revision}>
                                                Revisión {r.revision} · {r.kind}{" "}
                                                · {r.phase} · {r.reviewedAtUtc}
                                            </li>
                                        ))}
                                    </ol>
                                </details>
                                {(!human.inScope ||
                                    !candidates.length ||
                                    human.selected.kind === "Skip") && (
                                    <p>
                                        No comparable ·{" "}
                                        {human.selected.kind === "Skip"
                                            ? "revisión omitida"
                                            : !human.inScope
                                              ? "tipo fuera del manifiesto"
                                              : human.selected.observation ===
                                                      "H4Context" &&
                                                  comparison
                                                      .h4UnavailableReasons
                                                      .length
                                                ? "contexto H4 indisponible · " +
                                                  comparison.h4UnavailableReasons.join(
                                                      ", ",
                                                  )
                                                : "sin observación experimental del tipo en este alcance"}
                                        .
                                    </p>
                                )}
                            </section>
                        )}
                        {candidate && relation && (
                            <section aria-label="Evidencia experimental">
                                <h3>Observación experimental</h3>
                                <p>{relations[relation]}</p>
                                {canDisplayCandidate && (
                                    <>
                                        <p>
                                            {candidate.description} ·{" "}
                                            {candidate.at}
                                        </p>
                                        {references(candidate.references)}
                                    </>
                                )}
                                {canDisplayCandidate && (
                                    <details>
                                        <summary>
                                            Identidades reportadas y
                                            configuraciones
                                        </summary>
                                        <ul>
                                            {candidate.observations
                                                .filter(visibleAtJudgment)
                                                .map(({ id }) => (
                                                    <li key={id}>{id}</li>
                                                ))}
                                        </ul>
                                        <ul>
                                            {candidate.configurations
                                                .filter(visibleAtJudgment)
                                                .map(({ id }) => (
                                                    <li key={id}>{id}</li>
                                                ))}
                                        </ul>
                                    </details>
                                )}
                                <label>
                                    Decisión de asociación{" "}
                                    <select
                                        value={kind}
                                        onChange={(e) =>
                                            setKind(
                                                e.target.value as DecisionKind,
                                            )
                                        }
                                    >
                                        {decisionKinds.map((v) => (
                                            <option key={v} value={v}>
                                                {decisionsInSpanish[v]}
                                            </option>
                                        ))}
                                    </select>
                                </label>{" "}
                                <label>
                                    Justificación{" "}
                                    <textarea
                                        value={note}
                                        maxLength={2000}
                                        onChange={(e) =>
                                            setNote(e.target.value)
                                        }
                                    />
                                </label>{" "}
                                <button
                                    type="button"
                                    onClick={() =>
                                        act(() => {
                                            setHistory([
                                                ...history,
                                                appendDecision(
                                                    comparison,
                                                    disclosure,
                                                    history,
                                                    left,
                                                    right,
                                                    kind,
                                                    note,
                                                    new Date().toISOString(),
                                                ),
                                            ]);
                                            setNote("");
                                        })
                                    }
                                >
                                    Registrar decisión
                                </button>
                            </section>
                        )}
                    </fieldset>
                    {human && bars && (
                        <section aria-label="Velas fuente del juicio">
                            <label>
                                Temporalidad fuente{" "}
                                <select
                                    value={frame}
                                    onChange={(e) => {
                                        setFrame(e.target.value as Frame);
                                        setCursor(-1);
                                    }}
                                >
                                    {frames.map((f) => (
                                        <option key={f}>{f}</option>
                                    ))}
                                </select>
                            </label>
                            <CandleChart
                                report={{
                                    input: {
                                        symbol: packet.instrument,
                                        timeframe: frame,
                                        evaluationCutoffUtc:
                                            human.selected.viewingCutoffUtc,
                                    },
                                    evidenceCandles: bars.map((bar) =>
                                        chartCandle(frame, bar),
                                    ),
                                    results: [],
                                }}
                                originalOnly
                                visible={[]}
                                selected={cursor}
                                onSelect={setCursor}
                                humanReferences={human.selected.references
                                    .filter((r) => r.timeframe === frame)
                                    .map((r) => r.openUtc)}
                            />
                        </section>
                    )}
                    <details>
                        <summary>Historial de decisiones</summary>
                        <ol>
                            {history.map((d) => (
                                <li key={d.revision}>
                                    {decisionsInSpanish[d.kind]} · {d.targetId}{" "}
                                    · {d.candidateEventId} · {d.reviewerId} ·{" "}
                                    {d.recordedAtUtc} · {d.note}
                                </li>
                            ))}
                        </ol>
                    </details>
                    <fieldset disabled={busy}>
                        <button
                            type="button"
                            onClick={() =>
                                void exportComparison(
                                    comparison,
                                    disclosure,
                                    history,
                                )
                                    .then(download)
                                    .catch((e) => setError(fail(e)))
                            }
                        >
                            Exportar comparación JSON
                        </button>{" "}
                        <FileField
                            label="Importar comparación JSON"
                            onFile={(file) => void restore(file, disclosure)}
                        />
                    </fieldset>
                </>
            )}
        </>
    );
}

export default function EvidenceComparison() {
    const [packet, setPacket] = useState<OriginalPackage | null>(null);
    const [inputs, setInputs] = useState<{
        review?: File;
        report?: File;
        manifest?: File;
    }>({});
    const [ready, setReady] = useState<Comparison | null>(null);
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const epoch = useRef(0);
    async function openPacket(load: () => Promise<OriginalPackage>) {
        const ticket = ++epoch.current;
        setError("");
        setReady(null);
        setPacket(null);
        setBusy(true);
        try {
            const result = await load();
            if (ticket === epoch.current) setPacket(result);
        } catch (e) {
            if (ticket === epoch.current) setError(fail(e));
        } finally {
            if (ticket === epoch.current) setBusy(false);
        }
    }
    async function verify() {
        const ticket = ++epoch.current;
        setError("");
        setReady(null);
        setBusy(true);
        try {
            requireValue(
                packet && inputs.review && inputs.report && inputs.manifest,
                "Se requieren las cuatro entradas.",
            );
            const [review, report, manifest] = await Promise.all([
                bytes(inputs.review),
                bytes(inputs.report, maxReportBytes),
                bytes(inputs.manifest),
            ]);
            const result = await prepareComparison(
                packet,
                review,
                report,
                manifest,
            );
            if (ticket === epoch.current) setReady(result);
        } catch (e) {
            if (ticket === epoch.current) setError(fail(e));
        } finally {
            if (ticket === epoch.current) setBusy(false);
        }
    }
    return (
        <main className="strategy-lab observation">
            <nav className="lab-nav">
                <a href="#strategy-lab">← Strategy Lab</a>
            </nav>
            <h1>Comparación de evidencia Nasdaq</h1>
            <p>
                Consulta retrospectiva para Development expuesto o paquetes
                sintéticos. Requiere una declaración de exposición antes de
                abrir resultados.
            </p>
            <fieldset disabled={busy}>
                <legend>Entradas locales</legend>
                <button
                    type="button"
                    onClick={() =>
                        void openPacket(async () => {
                            const response = await fetch(developmentUrl);
                            requireValue(
                                response.ok,
                                "La fuente Development no está disponible.",
                            );
                            return projectDevelopment(await response.text());
                        })
                    }
                >
                    Abrir paquete original Development
                </button>
                <FileField
                    label="Paquete original sintético"
                    onFile={(file) =>
                        void openPacket(async () =>
                            readOriginalPackage(await bytes(file)),
                        )
                    }
                />
                {packet && (
                    <p>
                        Paquete {packet.sessionId} ·{" "}
                        <code>{packet.sha256}</code>
                    </p>
                )}
                {(["review", "report", "manifest"] as const).map((key, i) => (
                    <div key={key}>
                        <FileField
                            label={
                                [
                                    "Etiquetas humanas V2",
                                    "Informe integrado 1.1.0",
                                    "Manifiesto de comparación",
                                ][i]
                            }
                            onFile={(file) => {
                                setReady(null);
                                setError("");
                                setInputs({ ...inputs, [key]: file });
                            }}
                        />
                        {inputs[key] && <span> {inputs[key].name}</span>}
                    </div>
                ))}
                <button type="button" onClick={() => void verify()}>
                    Verificar insumos
                </button>
            </fieldset>
            {busy && <p role="status">Verificando los archivos locales…</p>}
            {error && (
                <p role="alert" className="lab-warning">
                    {error}
                </p>
            )}
            {ready && packet && (
                <ReviewComparison
                    key={ready.manifestSha256}
                    comparison={ready}
                    packet={packet}
                />
            )}
        </main>
    );
}
