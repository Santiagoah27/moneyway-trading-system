import { useState } from "react";
import type { NasdaqHistoricalReport } from "./types/nasdaqHistoricalDemo";
import type { SupervisedSessionState } from "./SupervisedSessionPanel";

export interface CanonicalDiagnosticReference {
    sessionKey: string;
    reportSha256: string;
    frameStep: number;
    asOfUtc: string;
}
export interface StandardDiagnosticContext {
    reference: CanonicalDiagnosticReference;
    session: SupervisedSessionState;
    report: NasdaqHistoricalReport;
    canonicalPath: string;
    authorization: string;
    specificationConflict: string;
    conflictProvenance: string;
    correctionPath: string;
    sourceLimitations: string;
    dependencies: {
        ruleId: string;
        prerequisites: string[];
        alternativePrerequisite: string | null;
        requiresLatestPrerequisitePass: boolean;
    }[];
}
export function StandardCanonicalDiagnosticPanel({
    apiBaseUrl,
    session,
    savedReference,
    onReference,
    onAssociate,
}: {
    apiBaseUrl: string;
    session: SupervisedSessionState;
    savedReference?: CanonicalDiagnosticReference | null;
    onReference: (reference: CanonicalDiagnosticReference | null) => void;
    onAssociate?: (context: StandardDiagnosticContext) => void;
}) {
    const [context, setContext] = useState<StandardDiagnosticContext | null>(
        null,
    );
    const [step, setStep] = useState("");
    const [busy, setBusy] = useState(false);
    const [pinnedHash, setPinnedHash] = useState<string | null>(null);
    const [error, setError] = useState("");
    async function load(reference?: CanonicalDiagnosticReference) {
        setBusy(true);
        setError("");
        setContext(null);
        onReference(null);
        try {
            const parameters = new URLSearchParams();
            if (reference) {
                parameters.set("frameStep", String(reference.frameStep));
                parameters.set("reportSha256", reference.reportSha256);
            } else {
                if (step) parameters.set("frameStep", step);
                if (pinnedHash) parameters.set("reportSha256", pinnedHash);
            }
            const response = await fetch(
                `${apiBaseUrl}/api/nasdaq/supervised-sessions/${encodeURIComponent(session.sessionKey)}/diagnostic?${parameters}`,
            );
            if (!response.ok) {
                const problem = await response.json().catch(() => ({}));
                throw new Error(
                    `${problem.code ?? response.status}: ${problem.detail ?? "Diagnóstico canónico no disponible para esta sesión; no se sustituye otro reporte."}`,
                );
            }
            const value: StandardDiagnosticContext = await response.json();
            if (reference && value.reference.asOfUtc !== reference.asOfUtc)
                throw new Error("DiagnosticFrameTimestampMismatch");
            setContext(value);
            setPinnedHash(value.reference.reportSha256);
            onReference(value.reference);
        } catch (e) {
            setError(e instanceof Error ? e.message : "DataUnavailable");
        } finally {
            setBusy(false);
        }
    }
    const frame = context?.report.selectedFrame;
    return (
        <section aria-label="Diagnóstico canónico Standard">
            <h4>Standard · diagnóstico histórico → registro supervisado</h4>
            <p>
                Sesión seleccionada: {session.sessionId} ·{" "}
                {session.identity.tradingDay} America/Bogota ·{" "}
                {session.identity.providerId.value}/
                {session.identity.symbol.value}. Estado operativo:{" "}
                {session.terminal ? "TERMINAL · SIN REENTRADA" : session.status}
                .
            </p>
            <p role="note">
                Discrepancia de especificación: instrucción humana 08:30–11:30
                frente a runtime [08:30,11:00) America/Bogota. Los resultados
                conservan el cutoff implementado. Autorización no verificada;
                requiere versión candidata, validación independiente y
                aprobación humana.
            </p>
            <label>
                Frame Step canónico (vacío: último disponible)
                <input
                    value={step}
                    onChange={(e) => setStep(e.target.value)}
                    inputMode="numeric"
                    disabled={busy}
                />
            </label>
            <button type="button" disabled={busy} onClick={() => void load()}>
                Cargar diagnóstico de esta sesión
            </button>
            {savedReference && (
                <button
                    type="button"
                    disabled={
                        busy || savedReference.sessionKey !== session.sessionKey
                    }
                    onClick={() => void load(savedReference)}
                >
                    Recuperar frame vinculado al registro
                </button>
            )}
            {error && <p role="alert">DataUnavailable · {error}</p>}
            {context && frame && (
                <>
                    <p>
                        {context.report.strategyId} /{" "}
                        {context.report.strategyVersion} · Ruta canónica:{" "}
                        {context.canonicalPath} · Reporte/run:{" "}
                        {context.report.sessionId ?? "Identificado por SHA256"}{" "}
                        · SHA256 {context.reference.reportSha256} · Step{" "}
                        {frame.step} · AsOfUtc {frame.asOfUtc}.
                    </p>
                    <p>
                        Veredicto canónico: <strong>{frame.verdict}</strong> ·{" "}
                        {frame.reason} · Bloqueo:{" "}
                        {frame.blockingRuleId ?? "No indicado"} /{" "}
                        {frame.blockingResult ?? "No indicado"}.
                    </p>
                    <p>
                        Evaluaciones requeridas ausentes:{" "}
                        {frame.missingRequiredRuleIds.join(", ") ||
                            "Ninguna ausencia declarada; esto no implica Passed"}
                        .
                    </p>
                    <p>
                        {context.authorization} · {context.sourceLimitations}
                    </p>
                    <p>
                        {context.conflictProvenance} · {context.correctionPath}
                    </p>
                    <div className="rules-table-wrapper">
                        <table>
                            <thead>
                                <tr>
                                    <th>Secuencia / regla</th>
                                    <th>Requerida / definición</th>
                                    <th>Resultado real</th>
                                    <th>Motivo / evidencia</th>
                                    <th>Dependencias declaradas</th>
                                </tr>
                            </thead>
                            <tbody>
                                {frame.rules.map((rule) => {
                                    const dependency =
                                        context.dependencies.find(
                                            (d) => d.ruleId === rule.ruleId,
                                        );
                                    return (
                                        <tr key={rule.ruleId}>
                                            <td data-label="Secuencia / regla">
                                                {rule.sequence} · {rule.ruleId}
                                            </td>
                                            <td data-label="Requerida / definición">
                                                {rule.isRequired ? "Sí" : "No"}{" "}
                                                · {rule.definitionStatus}
                                            </td>
                                            <td data-label="Resultado real">
                                                {rule.result}
                                            </td>
                                            <td data-label="Motivo / evidencia">
                                                {rule.reason}
                                                <br />
                                                {rule.evidenceId ??
                                                    "Sin identificador de evidencia"}{" "}
                                                · {rule.evaluatedAtUtc}
                                            </td>
                                            <td data-label="Dependencias declaradas">
                                                {dependency
                                                    ? `${dependency.prerequisites.join(" AND ")}${dependency.alternativePrerequisite ? ` OR ${dependency.alternativePrerequisite}` : ""}; latest prerequisite pass: ${dependency.requiresLatestPrerequisitePass}`
                                                    : "Sin dependencia declarada"}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                    <p>
                        Las dependencias son metadatos de esa versión; no se
                        recalcula su cumplimiento desde esta tabla.
                    </p>
                    <details>
                        <summary>Procedencia disponible del frame</summary>
                        <pre>
                            {JSON.stringify(
                                {
                                    sources:
                                        frame.tradeLifecycle?.sources ?? [],
                                    inputDiagnosticCodes:
                                        context.report.inputDiagnosticCodes,
                                    bindingDiagnosticCodes:
                                        context.report.bindingDiagnosticCodes,
                                    evidenceValidation:
                                        context.report.evidenceValidation,
                                },
                                null,
                                2,
                            )}
                        </pre>
                    </details>
                    <button
                        type="button"
                        disabled={busy || !onAssociate}
                        onClick={() => onAssociate?.(context)}
                    >
                        Vincular frame a evidencia documental
                    </button>
                    <a href="#supervised-documentary-form">
                        Ir al formulario de evidencia supervisada
                    </a>
                    <p>
                        Se conserva una referencia al diagnóstico, sin
                        aprobación de entrada ni orden. Una sesión terminal
                        permite documentos y correcciones, pero no nuevos
                        intentos operativos.
                    </p>
                </>
            )}
        </section>
    );
}
