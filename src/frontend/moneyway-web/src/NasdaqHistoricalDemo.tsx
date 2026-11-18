import { useEffect, useRef, useState } from "react";
import {
    getNasdaqHistoricalReport,
    HistoricalReportError,
} from "./api/nasdaqHistoricalDemoApi";
import type {
    HistoricalRule,
    HistoricalTradeLifecycle,
    NasdaqHistoricalReport,
    SupervisedHistoricalTradeSnapshot,
} from "./types/nasdaqHistoricalDemo";

function SupervisedSnapshot({
    snapshot,
}: {
    snapshot: SupervisedHistoricalTradeSnapshot;
}) {
    const entries = snapshot.documentaryEntry?.observations ?? [];
    const labels: Record<string, string> = {
        Entry: "Entrada observada",
        EntryEligibility: "Elegibilidad en la hora original",
        StopLoss: "Stop Loss",
        Risk: "Riesgo",
        TakeProfit: "Take Profit",
        Execution: "Ejecución",
        DocumentedExit: "Salida",
        EconomicOutcome: "Resultado",
    };
    function value(stage: string) {
        switch (stage) {
            case "Entry":
                return (
                    entries
                        .map(
                            (o) =>
                                `${o.evidence.sourceSymbol ?? "Símbolo desconocido"}: ${o.evidence.sourceEntryPriceText ?? "Precio desconocido"} · ${o.evidence.originalTime ?? "Hora desconocida"} · ${o.evidence.timePrecision}`,
                        )
                        .join("; ") || "Sin entrada documentada"
                );
            case "EntryEligibility":
                return (
                    entries
                        .map((o) =>
                            o.canonicalAtEntry
                                ? `${o.canonicalAtEntry.asOfUtc} · ${o.canonicalAtEntry.verdict ?? "Sin veredicto"}`
                                : "Sin frame original exacto",
                        )
                        .join("; ") || "No establecida"
                );
            case "StopLoss":
                return snapshot.stopLoss
                    ? `${snapshot.stopLoss.stopPriceText ?? snapshot.stopLoss.stopPrice} · ${snapshot.stopLoss.referenceId} · ${snapshot.stopLoss.protectivePriceText ?? snapshot.stopLoss.protectivePrice}`
                    : "Falta SL nativo";
            case "Risk":
                return snapshot.risk
                    ? `${snapshot.risk.reportedMaximumLossText} / ${snapshot.risk.accountBasisAmountText} ${snapshot.risk.exposure.currency} · Proporción: ${snapshot.risk.riskRatioText ?? "Fuera del rango decimal"}`
                    : "Falta exposición documentada";
            case "TakeProfit":
                return snapshot.takeProfit
                    ? `${snapshot.takeProfit.takeProfitPriceText} · ${snapshot.takeProfit.referenceDescription} · ${snapshot.takeProfit.symbol}`
                    : "Falta selección humana de TP";
            case "Execution":
                return (
                    entries
                        .map(
                            (o) =>
                                `${o.nativeExecutionStatus} · ${o.evidence.executionReference ?? "Falta deal nativo"}`,
                        )
                        .join("; ") || "Sin registro nativo"
                );
            case "DocumentedExit":
                return "Sin salida WF2 integrada";
            case "EconomicOutcome":
                return "No implementado";
            default:
                return "No disponible";
        }
    }
    function source(stage: string) {
        switch (stage) {
            case "StopLoss":
                return snapshot.stopLoss
                    ? `${snapshot.stopLoss.source.sourceReference}; ${snapshot.stopLoss.protectiveSource.sourceReference}`
                    : "Sin fuente nativa";
            case "Risk":
                return snapshot.risk
                    ? `${snapshot.risk.source.sourceReference}; ${snapshot.risk.exposure.calculationSourceReference}`
                    : "Sin fuente de exposición";
            case "TakeProfit":
                return snapshot.takeProfit
                    ? `${snapshot.takeProfit.source.sourceReference}; ${snapshot.takeProfit.referenceSource.sourceReference}`
                    : "Sin fuente de TP";
            case "Entry":
            case "EntryEligibility":
            case "Execution":
                return (
                    entries.map((o) => o.evidence.sourceReference).join("; ") ||
                    "Sin fuente de entrada"
                );
            default:
                return "Pendiente de implementación y evidencia compatible";
        }
    }
    return (
        <section aria-label="Snapshot histórico supervisado WF2">
            <h4>Snapshot histórico supervisado · WF2 SELL · Incompleto</h4>
            <p>
                Contexto nativo: {snapshot.session.providerId.value} /{" "}
                {snapshot.session.symbol.value} · Frame {snapshot.frameStep} ·{" "}
                {snapshot.asOfUtc} · Veredicto canónico:{" "}
                {snapshot.canonicalVerdict ?? "Sin veredicto"}.
            </p>
            <p>
                Correspondencia de instrumentos:{" "}
                {snapshot.instrumentCorrespondence}. Parámetros documentales
                completos:{" "}
                {snapshot.documentedParameterSetAvailable ? "Sí" : "No"}.
            </p>
            <p>
                Entrada humana observada no equivale a entrada autorizada. Regla
                Passed no equivale a operación ejecutada. Autorización:{" "}
                {snapshot.entryAuthorization}.
            </p>
            <p>
                Verificación nativa independiente:{" "}
                {snapshot.nativeExecutionIndependentlyVerified ? "Sí" : "No"}.
                Clasificación: {snapshot.failureClassification}.
            </p>
            <div className="rules-table-wrapper">
                <table>
                    <caption>Componentes de la operación supervisada</caption>
                    <thead>
                        <tr>
                            <th>Componente</th>
                            <th>Estado / regla</th>
                            <th>Valores y procedencia</th>
                            <th>Requisitos</th>
                        </tr>
                    </thead>
                    <tbody>
                        {snapshot.components.map((c) => (
                            <tr key={c.stage}>
                                <th scope="row">
                                    {labels[c.stage] ?? c.stage}
                                </th>
                                <td>
                                    {c.status} ·{" "}
                                    {c.ruleId ?? "Sin regla de ejecución"} ·{" "}
                                    {c.ruleResult ?? "Sin evaluación"}
                                </td>
                                <td>
                                    {value(c.stage)}
                                    <p>Fuente: {source(c.stage)}</p>
                                </td>
                                <td>{c.reason}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            <p>
                Evidencia pendiente: {snapshot.missingEvidence.join(", ")}. La
                disponibilidad de parámetros no completa ejecución, salida ni
                resultado.
            </p>
        </section>
    );
}

function TradeLifecycle({ trade }: { trade: HistoricalTradeLifecycle }) {
    const risk = trade.scenario2RiskExposure;
    const target = trade.scenario2TakeProfit;
    const humanEntry = trade.historicalEntry;
    const labels: Record<string, string> = {
        PreEntry: "Preentrada",
        Entry: "Entrada observada",
        StopLoss: "Stop Loss",
        Risk: "Riesgo",
        TakeProfit: "Take Profit",
        DocumentedExit: "Salida documentada",
        EconomicOutcome: "Resultado económico",
        SessionAttempts: "Intentos y reentradas",
    };
    return (
        <section aria-label="Ciclo de la operación histórica">
            <h3>Ciclo de la operación histórica</h3>
            <p>
                Rama: {trade.path} · Alcance: {trade.evidenceScope}
            </p>
            <p>
                La sesión supervisada completa sigue pendiente. Clasificación:{" "}
                {trade.failureClassification}.
            </p>
            {trade.supervisedSnapshot && (
                <SupervisedSnapshot snapshot={trade.supervisedSnapshot} />
            )}
            <div className="rules-table-wrapper">
                <table>
                    <caption>
                        Evidencia y dependencias de la operación en este frame
                    </caption>
                    <thead>
                        <tr>
                            <th scope="col">Etapa</th>
                            <th scope="col">Disponibilidad</th>
                            <th scope="col">Regla canónica</th>
                            <th scope="col">Detalle</th>
                        </tr>
                    </thead>
                    <tbody>
                        {trade.stages.map((stage) => (
                            <tr key={stage.stage}>
                                <th scope="row">
                                    {labels[stage.stage] ?? stage.stage}
                                </th>
                                <td>{stage.status}</td>
                                <td>
                                    {stage.ruleId ?? "Sin regla de ejecución"} ·{" "}
                                    {stage.ruleResult ?? "Sin evaluación"}
                                </td>
                                <td>{stage.reason}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            {humanEntry && (
                <section aria-label="Evidencia histórica de entrada humana">
                    <h4>
                        Entrada humana observada · {humanEntry.selectionStatus}
                    </h4>
                    <p>
                        Una ejecución histórica observada no equivale a una
                        entrada autorizada por la estrategia. Autorización:{" "}
                        {humanEntry.entryAuthorization}. Clasificación:{" "}
                        {humanEntry.failureClassification}.
                    </p>
                    <p>
                        Verificación nativa independiente:{" "}
                        {humanEntry.nativeExecutionIndependentlyVerified
                            ? "Sí"
                            : "No"}
                        .
                    </p>
                    {humanEntry.observations.map((item) => (
                        <article key={item.evidence.reviewId}>
                            <p>
                                {item.evidence.reviewId} ·{" "}
                                {item.evidence.eventKind} ·{" "}
                                {item.nativeExecutionStatus}.
                            </p>
                            <p>
                                Instrumento original:{" "}
                                {item.evidence.sourcePlatform} /{" "}
                                {item.evidence.sourceProviderId ??
                                    "Proveedor desconocido"}{" "}
                                /{" "}
                                {item.evidence.sourceSymbol ??
                                    "Símbolo desconocido"}
                                .
                            </p>
                            <p>
                                Hora original:{" "}
                                {item.evidence.originalTime ?? "No documentada"}{" "}
                                · Precisión: {item.evidence.timePrecision}.
                            </p>
                            <p>
                                Intervalo UTC documentado:{" "}
                                {item.evidence.earliestEventUtc ??
                                    "Sin conversión acreditada"}{" "}
                                —{" "}
                                {item.evidence.latestEventUtc ?? "Desconocido"}.
                            </p>
                            <p>
                                Fuente temporal:{" "}
                                {item.evidence.timeMappingReference ??
                                    "No acreditada"}
                                .
                            </p>
                            <p>
                                Precio del instrumento original:{" "}
                                {item.evidence.sourceEntryPriceText ??
                                    "No documentado"}{" "}
                                {item.evidence.sourcePriceUnit ??
                                    "Unidad no documentada"}
                                .
                            </p>
                            <p>
                                Referencia de ejecución:{" "}
                                {item.evidence.executionReference ??
                                    "Falta registro nativo de ejecución"}
                                .
                            </p>
                            <p>
                                Disponibilidad histórica:{" "}
                                {item.evidence.historicalAvailableAtUtc ??
                                    "Desconocida; revisión retrospectiva no admitida en el replay"}{" "}
                                · {item.availability}.
                            </p>
                            <p>
                                Revisión documentada:{" "}
                                {item.evidence.reviewCreatedOn ??
                                    "Fecha no documentada"}{" "}
                                · Validación humana:{" "}
                                {item.evidence.humanValidationIdentity}.
                            </p>
                            <p>Fuente: {item.evidence.sourceReference}.</p>
                            {item.canonicalAtEntry ? (
                                <div>
                                    <p>
                                        Estado canónico original: frame{" "}
                                        {item.canonicalAtEntry.step} ·{" "}
                                        {item.canonicalAtEntry.asOfUtc} ·{" "}
                                        {item.canonicalAtEntry.verdict ??
                                            "Sin veredicto"}
                                        .
                                    </p>
                                    <ul>
                                        {item.canonicalAtEntry.rules.map(
                                            (r) => (
                                                <li key={r.ruleId}>
                                                    {r.ruleId}: {r.result}
                                                    {r.isRequired
                                                        ? " · Required"
                                                        : ""}
                                                </li>
                                            ),
                                        )}
                                    </ul>
                                </div>
                            ) : (
                                <p>
                                    No hay un frame exacto acreditado para la
                                    hora de entrada; no se elige otro.
                                </p>
                            )}
                            <p>Limitaciones: {item.issues.join(", ")}.</p>
                        </article>
                    ))}
                    <p>
                        Los estados actuales de SL, riesgo y TP aparecen en la
                        tabla. Una confirmación posterior no retroautoriza la
                        entrada original.
                    </p>
                </section>
            )}
            {trade.scenario2StopLoss && (
                <div>
                    <p>
                        SL canónico Scenario 2:{" "}
                        {trade.scenario2StopLoss.stopPrice}. Referencia
                        protectora: {trade.scenario2StopLoss.referenceId} ·{" "}
                        {trade.scenario2StopLoss.protectivePrice}.
                    </p>
                    <p>
                        Selección efectiva:{" "}
                        {trade.scenario2StopLoss.effectiveAtUtc} · Disponible:{" "}
                        {trade.scenario2StopLoss.observedAtUtc}.
                    </p>
                    <p>
                        Fuente SL:{" "}
                        {trade.scenario2StopLoss.source.sourceReference}
                    </p>
                    <p>
                        Fuente protectora:{" "}
                        {
                            trade.scenario2StopLoss.protectiveSource
                                .sourceReference
                        }
                    </p>
                    <p>
                        La selección de SL conserva su propia procedencia.
                        Entrada, riesgo y TP mantienen sus requisitos
                        independientes.
                    </p>
                </div>
            )}
            {risk && (
                <section aria-label="Riesgo documentado Scenario 2">
                    <h4>NQ-RISK-001 · Scenario 2</h4>
                    <p>SL canónico recibido: {risk.stopPriceText}.</p>
                    <p>
                        Base documentada ({risk.exposure.accountBasis}):{" "}
                        {risk.accountBasisAmountText} {risk.exposure.currency}.
                        Pérdida máxima planificada:{" "}
                        {risk.reportedMaximumLossText} {risk.exposure.currency}.
                    </p>
                    <p>
                        Comparación canónica:{" "}
                        {risk.isWithinLimit ? "Passed" : "Failed"} · Límite
                        inclusivo: {risk.limit}. Proporción:{" "}
                        {risk.riskRatioText ?? "Fuera del rango decimal"}.
                    </p>
                    <p>
                        Entrada planificada: {risk.plannedEntryPriceText} ·
                        Costes: {risk.exposure.costTreatment}. Cantidad
                        documentada: {risk.quantityText ?? "No suministrada"}{" "}
                        {risk.exposure.quantityUnit ?? ""}.
                    </p>
                    <p>
                        Evaluación efectiva: {risk.effectiveAtUtc} · Disponible:{" "}
                        {risk.observedAtUtc}.
                    </p>
                    <p>
                        Fuente: {risk.source.sourceReference} · Cálculo:{" "}
                        {risk.exposure.calculationSourceReference}.
                    </p>
                    <p>
                        La exposición planificada no acredita una ejecución ni
                        un resultado económico.
                    </p>
                </section>
            )}
            {target && (
                <section aria-label="TP documentado Scenario 2">
                    <h4>NQ-TP-001 · Scenario 2 SELL · Passed</h4>
                    <p>
                        Instrumento: {target.providerId} / {target.symbol} ·
                        Dirección: {target.direction}.
                    </p>
                    <p>
                        Referencia humana: {target.referenceKind} /{" "}
                        {target.referenceDescription}. Precio de referencia:{" "}
                        {target.targetReferencePriceText} · TP exacto:{" "}
                        {target.takeProfitPriceText}.
                    </p>
                    <p>
                        Unidad: {target.priceUnit}; precio nativo, unidad
                        monetaria no documentada.
                    </p>
                    <p>
                        Selección efectiva: {target.effectiveAtUtc} ·
                        Disponible: {target.observedAtUtc}.
                    </p>
                    <p>
                        Fuente de selección: {target.source.sourceReference} ·
                        Fuente del nivel:{" "}
                        {target.referenceSource.sourceReference}.
                    </p>
                    <p>
                        Identidad del nivel (SHA256): {target.referenceSha256}.
                    </p>
                    <p>
                        La selección humana conserva el nivel original. Entrada,
                        riesgo, ejecución, salida y resultado mantienen sus
                        requisitos independientes.
                    </p>
                </section>
            )}
            <dl>
                <dt>Entrada documentada</dt>
                <dd>
                    {trade.entry
                        ? `${trade.entry.price} · ${trade.entry.effectiveAtUtc}`
                        : trade.evidenceScope ===
                            "PublishedArtifactsWithoutDetails"
                          ? "Registrada en el informe original; detalle pendiente de proyección"
                          : "Falta evidencia de entrada en el snapshot canónico"}
                </dd>
                <dt>SL del snapshot histórico</dt>
                <dd>{trade.stopPrice ?? "No disponible"}</dd>
                <dt>TP del snapshot histórico</dt>
                <dd>{trade.takeProfitPrice ?? "No disponible"}</dd>
                <dt>Base / pérdida máxima del snapshot histórico</dt>
                <dd>
                    {trade.accountBasisAmount ?? "No disponible"} /{" "}
                    {trade.reportedMaximumLoss ?? "No disponible"}{" "}
                    {trade.riskCurrency ?? ""}
                </dd>
                <dt>Proporción de riesgo del snapshot histórico</dt>
                <dd>{trade.riskRatio ?? "No disponible"}</dd>
                <dt>Salida documentada</dt>
                <dd>
                    {trade.exit
                        ? `${trade.exit.price} · ${trade.exit.effectiveAtUtc} · ${trade.exitScope ?? "Alcance no documentado"}`
                        : trade.evidenceScope ===
                            "PublishedArtifactsWithoutDetails"
                          ? "Consulte la evidencia de salida en el informe original"
                          : "Falta ejecución de salida causalmente documentada"}
                </dd>
            </dl>
            {trade.documentedExitReason && (
                <p>Motivo literal de salida: {trade.documentedExitReason}</p>
            )}
            <p>
                Evaluaciones Failed:{" "}
                {trade.failedRuleIds.join(", ") ||
                    "Ninguna registrada en este frame"}
                . La clasificación de incumplimiento o pérdida válida requiere
                evidencia de ejecución y cumplimiento.
            </p>
            <details>
                <summary>
                    Validaciones humanas y procedencia visible (
                    {trade.sources.length})
                </summary>
                <ul>
                    {trade.sources.map((source, i) => (
                        <li key={`${source.sourceReferenceSha256}-${i}`}>
                            {source.inputType} · {source.observedAtUtc}
                            <p>
                                {source.sourceReference}
                                {source.truncated
                                    ? "… (texto completo en el informe original)"
                                    : ""}
                            </p>
                            <small>
                                SHA256 de la referencia:{" "}
                                {source.sourceReferenceSha256}
                            </small>
                        </li>
                    ))}
                </ul>
                {trade.omittedSources > 0 && (
                    <p>
                        {trade.omittedSources} referencias adicionales
                        conservadas en el informe original.
                    </p>
                )}
            </details>
        </section>
    );
}

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
    const bounded = report?.reportScope === "BoundedCanonicalDiagnostic";
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
                            <dd>
                                {report.sessionId ??
                                    "No disponible en la fuente"}
                            </dd>
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
                            <dt>Alcance del reporte</dt>
                            <dd>
                                {bounded
                                    ? "Diagnóstico canónico acotado"
                                    : "Reporte histórico completo"}
                            </dd>
                        </div>
                        <div>
                            <dt>
                                {bounded
                                    ? "Frames exportados de la ventana"
                                    : "Frames del reporte completo"}
                            </dt>
                            <dd>{report.frameCount}</dd>
                        </div>
                        {!bounded && (
                            <div>
                                <dt>Resultado final del reporte completo</dt>
                                <dd>{verdict(report.finalVerdict)}</dd>
                            </div>
                        )}
                        {bounded && (
                            <>
                                <div>
                                    <dt>Frames del prefijo evaluado</dt>
                                    <dd>
                                        {report.evaluatedPrefixFrameCount ??
                                            "No disponible"}
                                    </dd>
                                </div>
                                <div>
                                    <dt>
                                        Intervalo del prefijo evaluado (UTC)
                                    </dt>
                                    <dd>
                                        {report.evaluatedPrefixStartUtc ??
                                            "No disponible"}{" "}
                                        —{" "}
                                        {report.evaluatedPrefixEndUtc ??
                                            "No disponible"}
                                    </dd>
                                </div>
                            </>
                        )}
                    </dl>
                    {bounded ? (
                        <p>
                            Ventana de diagnóstico de preentrada. No se aporta
                            un resultado final de sesión ni una operación
                            histórica completa.
                        </p>
                    ) : (
                        <p>
                            La clasificación del setup no se infiere del
                            reporte. Este caso no establece una ganancia o
                            pérdida de un trade Standard.
                        </p>
                    )}
                    {frame ? (
                        <>
                            <h3>Frame seleccionado de la sesión</h3>
                            {nav && (
                                <>
                                    <p>
                                        Steps disponibles: {nav.firstStep}–
                                        {nav.lastStep} · {nav.sessionFrameCount}{" "}
                                        {bounded
                                            ? "frames exportados. Step es el"
                                            : "frames de sesión. Step es el"}{" "}
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
                            {frame.tradeLifecycle && (
                                <TradeLifecycle trade={frame.tradeLifecycle} />
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
                        {report.schema && <p>Schema: {report.schema}</p>}
                        {report.canonicalRuntimeCommit && (
                            <p>
                                Runtime canónico:{" "}
                                {report.canonicalRuntimeCommit}
                            </p>
                        )}
                        {!!report.sourceRecordIds?.length && (
                            <p>
                                Identificadores documentados hasta el frame
                                seleccionado:{" "}
                                {report.sourceRecordIds.join(", ")}
                            </p>
                        )}
                        <p>Validación del reporte: {report.validation}</p>
                        <p>
                            Contenido de evidencias: {report.evidenceValidation}{" "}
                            (no se vuelven a validar sus hashes internos).
                        </p>
                        <p>
                            {bounded
                                ? "Intervalo exportado (UTC): "
                                : "Intervalo completo: "}
                            {report.replayStartUtc ?? "No disponible"} —{" "}
                            {report.replayEndUtc ?? "No disponible"}
                        </p>
                    </details>
                </>
            )}
        </section>
    );
}
