import { useEffect, useState } from "react";
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
    const [report, setReport] = useState<NasdaqHistoricalReport | null>(null);
    const [error, setError] = useState<string | null>(null);
    useEffect(() => {
        const controller = new AbortController();
        async function load() {
            try {
                const result = await getNasdaqHistoricalReport(
                    apiBaseUrl,
                    controller.signal,
                );
                if (!controller.signal.aborted) setReport(result);
            } catch (failure) {
                if (controller.signal.aborted) return;
                setError(
                    failure instanceof HistoricalReportError &&
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
    }, [apiBaseUrl]);

    const frame = report?.selectedSessionLastFrame;
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
                            <h3>Último frame de la sesión seleccionada</h3>
                            <p>
                                Frame {frame.step} · {frame.asOfUtc} (UTC)
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
                                    caption="Reglas obligatorias sin completar — último frame"
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
                                    Todas las decisiones del último frame
                                </summary>
                                <Rules
                                    rules={frame.rules}
                                    caption="Decisiones canónicas del último frame de la sesión"
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
