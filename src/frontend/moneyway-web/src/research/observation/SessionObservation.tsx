import { useRef, useState } from "react";
import { SessionTimeline, formatNumber, policyLabel } from "./SessionTimeline";
import { bogotaClock, bogotaTime } from "./sessionEvidence";
import {
    maxReportBytes,
    parseSessionReport,
    type SessionReport,
} from "./sessionReport";
import "./SessionObservation.css";

function Coverage({ report }: { report: SessionReport }) {
    return (
        <details className="ob-coverage">
            <summary>Cobertura, hashes y versiones</summary>
            <p>{report.provenance}</p>
            <dl className="ob-data">
                <div>
                    <dt>Dataset</dt>
                    <dd>{report.dataset}</dd>
                </div>
                <div>
                    <dt>Identidad del replay</dt>
                    <dd>{report.runId}</dd>
                </div>
                <div>
                    <dt>
                        {report.registered
                            ? "SHA-256 del registro"
                            : "SHA-256 de la entrada documental"}
                    </dt>
                    <dd>
                        <code>{report.digest}</code>
                    </dd>
                </div>
            </dl>
            <div className="ob-table-scroll">
                <table>
                    <thead>
                        <tr>
                            <th>Escala</th>
                            <th>Velas al corte</th>
                            <th>Último cierre Bogotá</th>
                            <th>Continuidad registrada</th>
                        </tr>
                    </thead>
                    <tbody>
                        {report.coverage.map((c) => (
                            <tr key={c.timeframe}>
                                <th scope="row">{c.timeframe}</th>
                                <td>{formatNumber(c.count)}</td>
                                <td>
                                    {c.last ? bogotaTime(c.last) : "Sin cierre"}
                                </td>
                                <td>
                                    {c.complete === null
                                        ? "Sin informe registrado"
                                        : c.complete
                                          ? "Suficiente"
                                          : "Incompleta"}
                                    {c.gaps.map((g) => (
                                        <small key={g}>{g}</small>
                                    ))}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            <ul className="ob-sources">
                {report.sources.map((s) => (
                    <li key={[s.timeframe, s.raw, s.normalized].join("/")}>
                        <strong>{s.timeframe} original</strong>
                        <small>
                            Original <code>{s.raw}</code>
                        </small>
                        <small>
                            Normalizado <code>{s.normalized}</code>
                        </small>
                        {s.mapping && <small>{s.mapping}</small>}
                    </li>
                ))}
            </ul>
            <ul className="ob-versions">
                <li>
                    {report.candidate} {report.version}
                </li>
                {report.h4.some((p) => p.at) && (
                    <>
                        <li>EXP-NQ-H4-CONTEXT-V1 1.0.0</li>
                        <li>EXP-NQ-H4-MARKET-STRUCTURE-V1 1.0.0</li>
                    </>
                )}
                <li>nasdaq-0.3.0-session-candidate · H1</li>
                {report.versions.map((v) => (
                    <li key={v}>{v}</li>
                ))}
            </ul>
            <p>
                Los hashes declarados identifican las fuentes. La vista no
                autentica su publicación histórica.
            </p>
        </details>
    );
}

function h4Limitation(report: SessionReport) {
    if (report.outcome === "H4ContextUnavailable") return report.outcome;
    return report.h4.every((p) => p.reason === "PreparationCutoffNotReached")
        ? "Preparación H4 pendiente"
        : "Contexto H4 no disponible";
}

function Readiness({ report }: { report: SessionReport }) {
    const coverage = !report.registered
        ? "Sin diagnóstico de continuidad registrado"
        : report.coverage.every((c) => c.complete)
          ? "Cobertura registrada suficiente al corte"
          : "Cobertura registrada incompleta";
    const dimensions = [
        ["Cobertura multitemporal", coverage],
        [
            "Reloj y procedencia",
            report.timestampVerified
                ? "Mapeo documental verificado por el registro"
                : "Mapeo documental declarado",
        ],
        [
            "Exposición de investigación",
            report.exposure === "Development"
                ? "Development · sesión ya expuesta"
                : "Prospective · adquisición local registrada",
        ],
        [
            "Estructura H4",
            report.h4.some((p) => p.available)
                ? "Contexto experimental disponible en algunos perfiles"
                : h4Limitation(report),
        ],
        [
            "Secuencia M5/M1",
            report.counts.realignments > 0
                ? formatNumber(report.counts.realignments) +
                  " realineaciones observadas"
                : "Sin realineaciones observables",
        ],
        [
            "Referencias estructurales",
            report.counts.paired > 0
                ? formatNumber(report.counts.paired) +
                  " pares con R:R hipotético"
                : "Sin pares SL/TP suficientes",
        ],
        [
            "Validación independiente",
            "Pendiente de evidencia admisible y revisión independiente",
        ],
        ["Aprobación canónica", "Requiere aprobación humana"],
        ["Política ejecutable de entrada", "Sin resolver"],
        ["Política ejecutable de SL/TP", "Sin resolver"],
        [
            "Contrato del instrumento y riesgo",
            "Sin resolver para esta observación · sin evidencia contractual",
        ],
        [
            "Conectividad demo y supervisión",
            "La aplicación dispone de supervisión local. La conectividad demo de esta sesión no está verificada.",
        ],
    ];
    return (
        <section
            className="ob-readiness"
            aria-labelledby="ob-readiness-heading"
        >
            <h2 id="ob-readiness-heading">Admisión a Demo v0</h2>
            <p className="ob-admission">Admisión canónica bloqueada</p>
            <dl>
                {dimensions.map(([title, state]) => (
                    <div key={title}>
                        <dt>{title}</dt>
                        <dd>{state}</dd>
                    </div>
                ))}
            </dl>
            {report.blockers.length > 0 && (
                <details>
                    <summary>Bloqueos del registro</summary>
                    <ul>
                        {report.blockers.map((b) => (
                            <li key={b}>{b}</li>
                        ))}
                    </ul>
                </details>
            )}
            {(report.actions.length > 0 ||
                !report.h4.some((h) => h.available)) && (
                <div className="ob-next">
                    <h3>Evidencia pendiente</h3>
                    {report.actions.length > 0 ? (
                        <ul>
                            {report.actions.map((a) => (
                                <li key={a}>{a}</li>
                            ))}
                        </ul>
                    ) : (
                        <p>
                            Ampliar la cobertura H4 original y revisar el mapeo
                            UTC antes de repetir la preparación.
                        </p>
                    )}
                </div>
            )}
            <small>
                CanonicalAdmissionPermitted=false · OperationalSignal=false
            </small>
        </section>
    );
}

function Workbench({ report }: { report: SessionReport }) {
    const [episodeId, setEpisode] = useState(
        report.episodes.find((e) => e.chains.some((c) => c.realignment))?.id ??
            report.episodes[0]?.id,
    );
    const episode = report.episodes.find((e) => e.id === episodeId);
    const window = "[" + bogotaClock(report.window.start) + ", ";
    return (
        <>
            <section
                className="ob-overview"
                aria-labelledby="ob-overview-heading"
            >
                <div className="ob-session-heading">
                    <div>
                        <p className="ob-mode">
                            {report.exposure === "Prospective"
                                ? "Observación prospectiva registrada · replay al corte"
                                : "Replay histórico · Development expuesto"}
                        </p>
                        <h2 id="ob-overview-heading">
                            {report.instrument} · {report.day}
                        </h2>
                        <p>
                            {report.provider} · <code>{report.dataset}</code>
                        </p>
                        <small>
                            Disponibilidad documental por cierre nominal.
                            Publicación histórica sin autenticar.
                        </small>
                    </div>
                    <div>
                        <span className="ob-label">Corte en Bogotá</span>
                        <strong>{bogotaTime(report.cutoff)}</strong>
                    </div>
                </div>
                <dl className="ob-windows">
                    <div>
                        <dt>Preparación</dt>
                        <dd>{bogotaClock(report.preparation)}</dd>
                    </div>
                    <div>
                        <dt>Ventana experimental</dt>
                        <dd>
                            {window}
                            {bogotaClock(report.window.researchEnd)})
                        </dd>
                        <small>{report.windowVersion}</small>
                    </div>
                    <div>
                        <dt>Ventana canónica publicada</dt>
                        <dd>
                            {window}
                            {bogotaClock(report.window.publishedEnd)})
                        </dd>
                        <small>{report.publishedVersion}</small>
                    </div>
                </dl>
                <dl
                    className="ob-metrics"
                    aria-label="Recuentos de investigación"
                >
                    {[
                        ["Episodios", report.counts.episodes],
                        ["Tomas observadas", report.counts.takes],
                        [
                            "Confirmaciones distintas",
                            report.counts.confirmations,
                        ],
                        ["Anclas M5", report.counts.anchors],
                        [
                            "Realineaciones M1 distintas",
                            report.counts.realignments,
                        ],
                        [
                            "Asociaciones de configuración",
                            report.counts.associations,
                        ],
                        ["Cadenas completas H4 → M1", report.counts.complete],
                    ].map(([label, count]) => (
                        <div key={label}>
                            <dt>{label}</dt>
                            <dd>{formatNumber(Number(count))}</dd>
                        </div>
                    ))}
                </dl>
                <Coverage report={report} />
            </section>
            {!report.h4.some((p) => p.available) && (
                <div className="lab-warning ob-h4-warning" role="status">
                    <strong>{h4Limitation(report)}</strong>
                    <span>
                        Contexto H4 insuficiente para completar la cadena. Las
                        observaciones M5/M1 y sus referencias siguen disponibles
                        cuando constan en el informe.
                    </span>
                </div>
            )}
            <div className="ob-workbench">
                <section
                    className="ob-episodes"
                    aria-labelledby="ob-episodes-heading"
                >
                    <h2 id="ob-episodes-heading">Episodios</h2>
                    {report.episodes.length === 0 ? (
                        <p>Sin episodios observables en este corte.</p>
                    ) : (
                        <ul>
                            {report.episodes.map((e) => (
                                <li key={e.id}>
                                    <button
                                        type="button"
                                        aria-pressed={episode?.id === e.id}
                                        onClick={() => setEpisode(e.id)}
                                    >
                                        <strong>
                                            {e.category} ·{" "}
                                            {policyLabel(e.policy)}
                                        </strong>
                                        <small>
                                            {e.take?.at
                                                ? bogotaTime(e.take.at).slice(
                                                      11,
                                                  ) + " · Toma"
                                                : "Sin toma observada"}
                                        </small>
                                    </button>
                                </li>
                            ))}
                        </ul>
                    )}
                </section>
                <SessionTimeline
                    key={episode?.id ?? "no-episode"}
                    report={report}
                    episode={episode}
                />
                <Readiness report={report} />
            </div>
        </>
    );
}

export default function SessionObservation() {
    const [report, setReport] = useState<SessionReport | null>(null);
    const [fileName, setFileName] = useState("");
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);
    const selected = useRef(0);
    function open(file: File) {
        const run = ++selected.current;
        setReport(null);
        setFileName(file.name);
        setError("");
        setLoading(false);
        if (file.size === 0 || file.size > maxReportBytes) {
            setError("Selecciona un informe JSON local de hasta 64 MiB.");
            return;
        }
        setLoading(true);
        const reader = new FileReader();
        reader.onerror = () => {
            if (selected.current !== run) return;
            setLoading(false);
            setError("No se pudo leer el archivo local.");
        };
        reader.onload = () => {
            if (selected.current !== run) return;
            try {
                setReport(
                    parseSessionReport(
                        JSON.parse(
                            String(reader.result).replace(/^\uFEFF/, ""),
                        ),
                    ),
                );
            } catch (e) {
                setError(
                    e instanceof SyntaxError
                        ? "JSON inválido o truncado."
                        : e instanceof Error
                          ? e.message
                          : "Informe incompatible.",
                );
            } finally {
                setLoading(false);
            }
        };
        reader.readAsText(file, "UTF-8");
    }
    return (
        <main className="strategy-lab observation">
            <nav
                className="lab-nav ob-nav"
                aria-label="Navegación Strategy Lab"
            >
                <a href="#strategy-lab">← Strategy Lab</a>
                <span>Nasdaq · Solo lectura</span>
            </nav>
            <header className="ob-header">
                <h1>Observación supervisada Nasdaq</h1>
                <label className="ob-file">
                    Abrir informe local
                    <input
                        type="file"
                        accept=".json,application/json"
                        onChange={(e) => {
                            const file = e.currentTarget.files?.[0];
                            if (file) open(file);
                            e.currentTarget.value = "";
                        }}
                    />
                </label>
            </header>
            {fileName && <p className="ob-file-name">{fileName}</p>}
            {loading && <p role="status">Validando el informe local…</p>}
            {error && (
                <p className="lab-warning" role="alert">
                    {error}
                </p>
            )}
            {report ? (
                <Workbench key={report.runId} report={report} />
            ) : (
                !loading &&
                !error && (
                    <section
                        className="ob-empty"
                        aria-labelledby="ob-empty-heading"
                    >
                        <h2 id="ob-empty-heading">
                            Selecciona un replay de investigación
                        </h2>
                        <p>
                            Abre el archivo{" "}
                            <code>nasdaq-end-to-end-research.json</code>{" "}
                            generado por el Worker con{" "}
                            <code>--compare-end-to-end</code>. El archivo se lee
                            en este navegador.
                        </p>
                    </section>
                )
            )}
        </main>
    );
}
