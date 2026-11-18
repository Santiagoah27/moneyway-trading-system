import { useState } from "react";
import type { NasdaqHistoricalReport } from "./types/nasdaqHistoricalDemo";

export interface HistoricalPackageIdentity {
    packageId: string;
    packageVersion: number;
    sessionId: string;
    tradingDay: string;
    strategyId: string;
    strategyVersion: string;
    scenario: string;
    providerId: string;
    originalSymbol: string;
    origin: string;
    analysisReference: string;
}
interface Inspection {
    package: HistoricalPackageIdentity & {
        candleProvenance: string;
        originalClockDomain: string;
        timeMapping: string;
        availabilityEvidence: string;
        humanReviews: unknown[];
        limitations: string[];
    };
    manifestSha256: string;
    state: string;
    documentaryStructureValid: boolean;
    sourceIntegrityVerified: boolean;
    canonicalReplayPermitted: false;
    admissionResult: string;
    admissionBlockers: string[];
    invalidReasons: string[];
    availabilityStatus: string;
    humanApprovalStatus: string;
    temporalContract: string;
    historicalReportScope: string;
    tradingWindowConflict: string;
    artifacts: {
        id: string;
        kind: string;
        root: string;
        relativePath: string;
        expectedSha256: string;
        actualSha256: string | null;
        integrityVerified: boolean;
        status: string;
    }[];
    coverage: {
        artifactId: string;
        timeframe: string;
        candleCount: number;
        firstOpenUtc: string;
        lastCloseUtc: string;
        unclassifiedDiscontinuities: number;
        candlesClosingAfterSessionDay: number;
        timestampMeaning: string;
    }[];
    existingHistoricalReport: NasdaqHistoricalReport | null;
}
export function HistoricalEvidencePackagePanel({
    apiBaseUrl,
    onScope,
}: {
    apiBaseUrl: string;
    onScope: (identity: HistoricalPackageIdentity) => void;
}) {
    const [list, setList] = useState<
        { packageId: string; packageVersion: number; manifestSha256: string }[]
    >([]);
    const [selected, setSelected] = useState("");
    const [frame, setFrame] = useState("");
    const [value, setValue] = useState<Inspection | null>(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    async function request<T>(suffix: string): Promise<T> {
        const response = await fetch(
            `${apiBaseUrl}/api/nasdaq/historical-packages${suffix}`,
        );
        if (!response.ok) {
            const p = await response.json().catch(() => ({}));
            throw new Error(
                `${p.code ?? response.status}: ${p.detail ?? "Paquete o admisión no disponible"}`,
            );
        }
        return response.json() as Promise<T>;
    }
    async function run(work: () => Promise<void>) {
        setBusy(true);
        setError("");
        try {
            await work();
        } catch (e) {
            setError(e instanceof Error ? e.message : "DataUnavailable");
        } finally {
            setBusy(false);
        }
    }
    const previous = value?.existingHistoricalReport?.selectedFrame;
    return (
        <section aria-label="Paquete histórico Standard">
            <h4>Paquete histórico Standard · procedencia y admisión causal</h4>
            <p>
                Selecciona un candidato registrado por el servidor. Integridad,
                revisión humana, disponibilidad histórica, reporte previo y
                admisión canónica son estados distintos. No genera señales
                actuales ni órdenes.
            </p>
            <button
                type="button"
                disabled={busy}
                onClick={() =>
                    void run(async () => {
                        setList(await request(""));
                        setSelected("");
                        setValue(null);
                    })
                }
            >
                Listar paquetes históricos registrados
            </button>
            <label>
                Paquete registrado
                <select
                    value={selected}
                    disabled={busy}
                    onChange={(e) => {
                        setSelected(e.target.value);
                        setValue(null);
                        setError("");
                    }}
                >
                    <option value="">Seleccionar</option>
                    {list.map((p) => (
                        <option key={p.packageId} value={p.packageId}>
                            {p.packageId} · v{p.packageVersion}
                        </option>
                    ))}
                </select>
            </label>
            <label>
                Frame de reporte previo (vacío: último de sesión)
                <input
                    value={frame}
                    disabled={busy}
                    onChange={(e) => setFrame(e.target.value)}
                    inputMode="numeric"
                />
            </label>
            <button
                type="button"
                disabled={busy || !selected}
                onClick={() => {
                    setValue(null);
                    void run(async () =>
                        setValue(
                            await request(
                                `/${encodeURIComponent(selected)}?manifestSha256=${encodeURIComponent(list.find((p) => p.packageId === selected)?.manifestSha256 ?? "")}${frame ? `&frameStep=${encodeURIComponent(frame)}` : ""}`,
                            ),
                        ),
                    );
                }}
            >
                Inspeccionar integridad y disponibilidad
            </button>
            {error && <p role="alert">DataUnavailable · {error}</p>}
            {value && (
                <>
                    <p>
                        {value.package.packageId} · v
                        {value.package.packageVersion} · {value.package.origin}{" "}
                        · Manifiesto SHA256 {value.manifestSha256}.
                    </p>
                    <p>
                        {value.package.strategyId}/
                        {value.package.strategyVersion} ·{" "}
                        {value.package.scenario} · {value.package.tradingDay}{" "}
                        America/Bogota · Fuente nativa{" "}
                        {value.package.providerId}/
                        {value.package.originalSymbol}.
                    </p>
                    <p>
                        Referencia de análisis separada:{" "}
                        {value.package.analysisReference}. No se equiparan feeds
                        ni precios.
                    </p>
                    <p>
                        Estado: <strong>{value.state}</strong>. Integridad de
                        fuentes:{" "}
                        {value.sourceIntegrityVerified
                            ? "Hashes coinciden; no autentica al broker"
                            : "No verificada"}
                        . Replay canónico permitido: <strong>No</strong>.
                        Resultado de admisión: {value.admissionResult}.
                    </p>
                    <p>
                        {value.availabilityStatus} · {value.temporalContract}
                    </p>
                    <p>{value.humanApprovalStatus}</p>
                    <p>
                        {value.package.originalClockDomain} ·{" "}
                        {value.package.timeMapping} ·{" "}
                        {value.package.candleProvenance} ·{" "}
                        {value.package.availabilityEvidence}
                    </p>
                    <p>Conflicto horario: {value.tradingWindowConflict}</p>
                    <ul>
                        {value.invalidReasons.map((c, i) => (
                            <li key={`invalid-${i}`}>Inválido: {c}</li>
                        ))}
                        {value.admissionBlockers.map((c, i) => (
                            <li key={`blocked-${i}`}>Bloqueo: {c}</li>
                        ))}
                    </ul>
                    <div className="rules-table-wrapper">
                        <table>
                            <thead>
                                <tr>
                                    <th>Artefacto</th>
                                    <th>SHA256 esperado / inspeccionado</th>
                                    <th>Estado</th>
                                </tr>
                            </thead>
                            <tbody>
                                {value.artifacts.map((a, i) => (
                                    <tr key={`${a.id}-${i}`}>
                                        <td data-label="Artefacto">
                                            {a.id} · {a.kind} · {a.root}/
                                            {a.relativePath}
                                        </td>
                                        <td data-label="SHA256">
                                            {a.expectedSha256}
                                            <br />
                                            {a.actualSha256 ?? "No disponible"}
                                        </td>
                                        <td data-label="Estado">{a.status}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                    <ul>
                        {value.coverage.map((c) => (
                            <li key={c.artifactId}>
                                {c.timeframe}: {c.candleCount} velas ·{" "}
                                {c.firstOpenUtc}–{c.lastCloseUtc} ·
                                discontinuidades sin clasificar:{" "}
                                {c.unclassifiedDiscontinuities} · velas
                                posteriores al día:{" "}
                                {c.candlesClosingAfterSessionDay} ·{" "}
                                {c.timestampMeaning}
                            </li>
                        ))}
                    </ul>
                    <p>
                        Las discontinuidades no prueban corrupción ni un horario
                        de mercado. Los cierres y timestamps no prueban
                        publicación, captura o disponibilidad.
                    </p>
                    <details>
                        <summary>Revisiones y límites originales</summary>
                        <pre>
                            {JSON.stringify(
                                {
                                    humanReviews: value.package.humanReviews,
                                    limitations: value.package.limitations,
                                },
                                null,
                                2,
                            )}
                        </pre>
                    </details>
                    <button
                        type="button"
                        disabled={
                            busy ||
                            !value.documentaryStructureValid ||
                            !value.sourceIntegrityVerified
                        }
                        onClick={() => onScope(value.package)}
                    >
                        Usar identidad nativa para sesión documental, sin
                        autorización
                    </button>
                    <button
                        type="button"
                        disabled={busy}
                        onClick={() =>
                            void run(async () => {
                                await request(
                                    `/${encodeURIComponent(selected)}/diagnostic`,
                                );
                            })
                        }
                    >
                        Comprobar bloqueo de diagnóstico admitido
                    </button>
                    <p>
                        Sin referencia de diagnóstico admisible: no se vincula
                        este paquete a un intento operativo. La sesión terminal
                        conserva su bloqueo; inspección y documentos no la
                        reabren ni la cierran.
                    </p>
                    {previous && (
                        <section aria-label="Reporte histórico previo de investigación">
                            <h5>
                                Reporte previo · solo investigación · no nueva
                                admisión
                            </h5>
                            <p>
                                {value.historicalReportScope} · SHA256{" "}
                                {value.existingHistoricalReport?.reportSha256} ·
                                frames{" "}
                                {value.existingHistoricalReport?.frameCount} ·
                                Step {previous.step} · AsOfUtc{" "}
                                {previous.asOfUtc} · Veredicto histórico
                                registrado: {previous.verdict} ·{" "}
                                {previous.reason}
                            </p>
                            <ul>
                                {previous.rules.map((r) => (
                                    <li key={r.ruleId}>
                                        {r.ruleId}: {r.result} · {r.reason} ·{" "}
                                        {r.evidenceId ??
                                            "Sin identificador de evidencia"}
                                    </li>
                                ))}
                            </ul>
                            <p>
                                No se recalcula ni se convierte ese resultado
                                previo en Ready actual, ejecución o P&amp;L.
                            </p>
                        </section>
                    )}
                </>
            )}
        </section>
    );
}
