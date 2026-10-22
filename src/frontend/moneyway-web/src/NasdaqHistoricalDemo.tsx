import { useEffect, useRef, useState } from "react";
import {
    getNasdaqHistoricalReport,
    HistoricalReportError,
} from "./api/nasdaqHistoricalDemoApi";
import type {
    HistoricalRule,
    NasdaqHistoricalReport,
} from "./types/nasdaqHistoricalDemo";

function verdict(value: string | null | undefined) {
    return value === "NoTrade"
        ? "NO_TRADE (NoTrade)"
        : (value ?? "No disponible");
}

function Rules({
    rules,
    caption,
}: {
    rules: HistoricalRule[];
    caption: string;
}) {
    return (
        <div className="rules-table-wrapper">
            <table>
                <caption>{caption}</caption>
                <thead>
                    <tr>
                        <th scope="col">Regla</th>
                        <th scope="col">Estado canónico</th>
                        <th scope="col">Definición</th>
                        <th scope="col">Evidencia / motivo</th>
                    </tr>
                </thead>
                <tbody>
                    {rules.map((rule) => (
                        <tr key={rule.ruleId}>
                            <td data-label="Regla">
                                {rule.ruleId}
                                <small>
                                    {rule.isRequired ? "Required" : "Optional"}
                                </small>
                            </td>
                            <td data-label="Estado canónico">{rule.result}</td>
                            <td data-label="Definición">
                                {rule.definitionStatus}
                            </td>
                            <td data-label="Evidencia / motivo">
                                {rule.reason}
                                {rule.evidenceId && (
                                    <small>EvidenceId: {rule.evidenceId}</small>
                                )}
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

export function NasdaqHistoricalDemo({ apiBaseUrl }: { apiBaseUrl: string }) {
    return <HistoricalDemoContent key={apiBaseUrl} apiBaseUrl={apiBaseUrl} />;
}

function HistoricalDemoContent({ apiBaseUrl }: { apiBaseUrl: string }) {
    const [report, setReport] = useState<NasdaqHistoricalReport | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [hasLoaded, setHasLoaded] = useState(false);
    const [notice, setNotice] = useState<string | null>(null);
    const [inputError, setInputError] = useState<string | null>(null);
    const [stepInput, setStepInput] = useState("");
    const [selection, setSelection] = useState<{
        frameStep?: number;
        reportSha256?: string;
        revision: number;
    }>({ revision: 0 });
    const generation = useRef(0);
    function select(frameStep?: number) {
        generation.current++;
        setReport(null);
        setError(null);
        setNotice(null);
        setInputError(null);
        setSelection((current) => ({
            frameStep,
            reportSha256:
                frameStep === undefined ? undefined : report?.reportSha256,
            revision: current.revision + 1,
        }));
    }
    useEffect(() => {
        const controller = new AbortController();
        const requestGeneration = ++generation.current;
        async function load() {
            try {
                const result = await getNasdaqHistoricalReport(
                    apiBaseUrl,
                    controller.signal,
                    selection,
                );
                if (
                    !controller.signal.aborted &&
                    requestGeneration === generation.current
                ) {
                    setReport(result);
                    setHasLoaded(true);
                    setStepInput(
                        String(
                            result.selectedFrame?.step ??
                                result.selectedSessionLastFrame?.step ??
                                "",
                        ),
                    );
                }
            } catch (failure) {
                if (
                    controller.signal.aborted ||
                    requestGeneration !== generation.current
                )
                    return;
                if (
                    failure instanceof HistoricalReportError &&
                    failure.status === 409
                ) {
                    setNotice(
                        "El reporte cambió. La selección se reinició al último frame de la sesión actual.",
                    );
                    setSelection((current) => ({
                        revision: current.revision + 1,
                    }));
                    return;
                }
                setError(
                    failure instanceof HistoricalReportError &&
                        failure.code === "FrameNotInSelectedSession"
                        ? "Frame no disponible para esta sesión. Vuelve al último frame."
                        : failure instanceof HistoricalReportError &&
                            failure.status === 400
                          ? "Selección de frame inválida. Usa un Step disponible de la sesión."
                          : failure instanceof HistoricalReportError &&
                              failure.status === 404
                            ? "Reporte histórico no disponible. Debe configurarse un reporte existente en la API local."
                            : failure instanceof HistoricalReportError &&
                                failure.status === 422
                              ? "El reporte histórico es inválido, no compatible o no coincide con el hash esperado."
                              : "No se pudo consultar el reporte histórico. Comprueba la API y el acceso al archivo configurado.",
                );
            }
        }
        void load();
        return () => controller.abort();
    }, [apiBaseUrl, selection]);

    const frame = report?.selectedFrame ?? report?.selectedSessionLastFrame;
    const nav = report?.navigation;
    const blockers =
        frame?.rules.filter(
            (rule) =>
                rule.isRequired &&
                rule.result !== "Passed" &&
                rule.result !== "NotApplicable",
        ) ?? [];
    return (
        <section
            id="nasdaq-historical-demo"
            aria-labelledby="historical-heading"
        >
            <h2 id="historical-heading">Nasdaq histórico — demo supervisada</h2>
            <p>
                Revisión de evidencia histórica. No ejecuta órdenes ni opera en
                vivo.
            </p>
            {!report && !error && (
                <p role="status">Cargando reporte histórico.</p>
            )}
            {error && <p role="alert">{error}</p>}
            {notice && <p role="status">{notice}</p>}
            <button
                type="button"
                onClick={() => select()}
                disabled={!hasLoaded && !error}
            >
                Último frame
            </button>
            {report && (
                <>
                    <dl className="definition-summary">
                        <div>
                            <dt>Sesión (America/Bogota)</dt>
                            <dd>{report.tradingDay}</dd>
                        </div>
                        <div>
                            <dt>Identificador</dt>
                            <dd>{report.sessionId}</dd>
                        </div>
                        <div>
                            <dt>Estrategia / versión</dt>
                            <dd>
                                {report.strategyId} / {report.strategyVersion}
                            </dd>
                        </div>
                        <div>
                            <dt>Proveedor / instrumento</dt>
                            <dd>
                                {report.providerId} / {report.symbol}
                            </dd>
                        </div>
                        <div>
                            <dt>Frames del reporte completo</dt>
                            <dd>{report.frameCount}</dd>
                        </div>
                        <div>
                            <dt>Resultado final del reporte completo</dt>
                            <dd>{verdict(report.finalVerdict)}</dd>
                        </div>
                    </dl>
                    <p>
                        La clasificación del setup no se infiere del reporte.
                        Este caso no establece una ganancia o pérdida de un
                        trade Standard.
                    </p>
                    {frame ? (
                        <>
                            <h3>Frame seleccionado de la sesión</h3>
                            {nav && (
                                <>
                                    <p>
                                        Steps disponibles: {nav.firstStep}–
                                        {nav.lastStep} · {nav.sessionFrameCount}{" "}
                                        frames de sesión. Step es el
                                        identificador canónico de base 1 del
                                        replay completo.
                                    </p>
                                    <button
                                        type="button"
                                        disabled={nav.previousStep === null}
                                        onClick={() =>
                                            select(nav.previousStep!)
                                        }
                                    >
                                        Anterior
                                    </button>
                                    <button
                                        type="button"
                                        disabled={nav.nextStep === null}
                                        onClick={() => select(nav.nextStep!)}
                                    >
                                        Siguiente
                                    </button>
                                    <form
                                        onSubmit={(event) => {
                                            event.preventDefault();
                                            const step = Number(stepInput);
                                            if (
                                                !/^[0-9]+$/.test(stepInput) ||
                                                !Number.isSafeInteger(step) ||
                                                nav.firstStep === null ||
                                                nav.lastStep === null ||
                                                step < nav.firstStep ||
                                                step > nav.lastStep
                                            ) {
                                                setInputError(
                                                    "Selecciona un Step entero dentro de los límites disponibles.",
                                                );
                                                return;
                                            }
                                            select(step);
                                        }}
                                    >
                                        <label htmlFor="historical-frame-step">
                                            Step del frame
                                        </label>
                                        <input
                                            id="historical-frame-step"
                                            inputMode="numeric"
                                            value={stepInput}
                                            onChange={(event) =>
                                                setStepInput(event.target.value)
                                            }
                                        />
                                        <button type="submit">
                                            Ir al frame
                                        </button>
                                    </form>
                                    {inputError && (
                                        <p role="alert">{inputError}</p>
                                    )}
                                </>
                            )}
                            <p>
                                Frame {frame.step} · {frame.asOfUtc} (UTC)
                            </p>
                            <p>
                                Hora America/Bogota:{" "}
                                {new Intl.DateTimeFormat("es-CO", {
                                    timeZone: "America/Bogota",
                                    dateStyle: "short",
                                    timeStyle: "medium",
                                    hourCycle: "h23",
                                }).format(new Date(frame.asOfUtc))}
                            </p>
                            <p>
                                Resultado de ese frame:{" "}
                                <strong>{verdict(frame.verdict)}</strong>
                            </p>
                            <p>
                                {frame.reason ??
                                    "Diagnóstico canónico no disponible."}
                            </p>
                            <p>
                                Bloqueo canónico:{" "}
                                {frame.blockingRuleId ?? "No registrado"} ·{" "}
                                {frame.blockingResult ?? "No registrado"}
                            </p>
                            <p>
                                Snapshot histórico:{" "}
                                {frame.snapshotState ?? "No disponible"}
                            </p>
                            <p>
                                Evaluación factual:{" "}
                                {frame.factualEvaluationState ??
                                    "No disponible"}
                            </p>
                            {frame.factualEvaluationState !== "Available" && (
                                <p>
                                    No hay una operación histórica factual
                                    completa establecida en este frame.
                                </p>
                            )}
                            {frame.factualRequiresHumanValidation === true && (
                                <p>
                                    La evaluación factual requiere validación
                                    humana.
                                </p>
                            )}
                            {frame.snapshotDiagnostic && (
                                <p>{frame.snapshotDiagnostic}</p>
                            )}
                            <p>
                                Cobertura de reglas obligatorias:{" "}
                                {frame.hasCompleteRequiredCoverage === null
                                    ? "No disponible"
                                    : frame.hasCompleteRequiredCoverage
                                      ? "Complete"
                                      : "Incomplete"}
                            </p>
                            {frame.missingRequiredRuleIds.length > 0 && (
                                <p>
                                    Reglas obligatorias sin evaluación:{" "}
                                    {frame.missingRequiredRuleIds.join(", ")}
                                </p>
                            )}
                            {blockers.length > 0 ? (
                                <Rules
                                    rules={blockers}
                                    caption="Reglas obligatorias sin completar — frame seleccionado"
                                />
                            ) : (
                                <p>
                                    No se registran reglas obligatorias
                                    pendientes en este frame; esto no autoriza
                                    ejecución.
                                </p>
                            )}
                            <details>
                                <summary>
                                    Todas las decisiones del frame seleccionado
                                </summary>
                                <Rules
                                    rules={frame.rules}
                                    caption="Decisiones canónicas del frame seleccionado de la sesión"
                                />
                            </details>
                        </>
                    ) : (
                        <p>
                            No hay un frame disponible para la sesión
                            seleccionada. No se establece una operación
                            histórica.
                        </p>
                    )}
                    {report.inputDiagnosticCodes.length > 0 && (
                        <p>
                            Diagnósticos de entrada:{" "}
                            {report.inputDiagnosticCodes.join(", ")}
                        </p>
                    )}
                    {report.bindingDiagnosticCodes.length > 0 && (
                        <p>
                            Diagnósticos de evidencia humana:{" "}
                            {report.bindingDiagnosticCodes.join(", ")}
                        </p>
                    )}
                    <details>
                        <summary>Identidad y validación de la fuente</summary>
                        <p>SHA-256: {report.reportSha256}</p>
                        <p>
                            Tamaño: {report.reportBytes} bytes · SchemaVersion:{" "}
                            {report.schemaVersion}
                        </p>
                        <p>Validación del reporte: {report.validation}</p>
                        <p>
                            Contenido de evidencias: {report.evidenceValidation}{" "}
                            (no se vuelven a validar sus hashes internos).
                        </p>
                        <p>
                            Intervalo completo:{" "}
                            {report.replayStartUtc ?? "No disponible"} —{" "}
                            {report.replayEndUtc ?? "No disponible"}
                        </p>
                    </details>
                </>
            )}
        </section>
    );
}
