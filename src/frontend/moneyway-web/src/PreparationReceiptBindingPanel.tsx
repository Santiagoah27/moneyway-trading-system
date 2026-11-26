import { useEffect, useState } from "react";
import type { PreparationView } from "./ProspectivePreparationPanel";

type AvailableCut = { id: string; sequence: number; reportSha256: string };
type Binding = {
    associationSha256: string;
    artifacts: NonNullable<PreparationView["record"]["artifacts"]>;
    binding: {
        receiptCutId: string;
        cutSequence: number;
        cutReceiptSha256: string;
        receiptCutReportSha256: string;
        preparationRevisionSha256: string;
        preparationSourceReference: string;
        source: { company: string; server: string; nativeSymbol: string };
        receiptHostClockCandidate: string;
        preparationReceivedAtUtc: string;
        preparationRegisteredAtUtc: string;
        preparationClockSource: string;
        humanReportedObservationTime: string | null;
        preparationOriginalTime: string | null;
        preparationOriginalClockDomain: string | null;
        associationCreatedAtUtc: string;
        matchStatus: string;
        wallClockOrdering: string;
        preparationAvailabilityAtCut: string;
        standardEvaluation: string;
        diagnostics: string[];
        references: {
            localReferenceId: string;
            originalTime: string | null;
            originalClockDomain: string | null;
            status: string;
            marketEventNativeTime: { display: string; raw: number } | null;
            firstUsableHistoricalReceipt: {
                sequence: number;
                recordSha256: string;
                probeSha256: string;
            } | null;
            receiptHostClockCandidate: string | null;
        }[];
    };
};
type Index = { bindings: Binding[]; availableCuts: AvailableCut[] };

export function PreparationReceiptBindingPanel({
    apiBaseUrl,
    preparation,
}: {
    apiBaseUrl: string;
    preparation: PreparationView;
}) {
    const [open, setOpen] = useState(false);
    const { recordId, revision, evidence } = preparation.record;
    const route = `${apiBaseUrl}/api/nasdaq/supervised-sessions/${encodeURIComponent(evidence.sessionKey)}/preparation/${encodeURIComponent(recordId)}/revisions/${revision}/receipt-cuts`;
    return (
        <section aria-label="Vínculo de preparación y corte MT5">
            <button
                type="button"
                aria-expanded={open}
                onClick={() => setOpen(!open)}
            >
                {open
                    ? "Cerrar vínculos de esta revisión"
                    : "Vincular esta revisión a un corte MT5"}
            </button>
            {open && (
                <Bindings
                    key={route}
                    route={route}
                    revisionHash={preparation.revisionHash}
                />
            )}
        </section>
    );
}

function Bindings({
    route,
    revisionHash,
}: {
    route: string;
    revisionHash: string;
}) {
    const [index, setIndex] = useState<Index>();
    const [selected, setSelected] = useState("");
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    useEffect(() => {
        const controller = new AbortController();
        const options: RequestInit = {
            signal: controller.signal,
            cache: "no-store",
        };
        async function load() {
            try {
                let response = await fetch(route, options);
                if (response.status === 409) {
                    const problem = await response.json();
                    if (
                        problem.detail ===
                        "EvidenceStoreBusy_RetrySameSubmission"
                    ) {
                        await new Promise((resolve) =>
                            setTimeout(resolve, 100),
                        );
                        if (controller.signal.aborted) return;
                        response = await fetch(route, options);
                    }
                }
                if (!response.ok) throw new Error();
                const data: Index = await response.json();
                if (!controller.signal.aborted) setIndex(data);
            } catch {
                if (!controller.signal.aborted)
                    setError(
                        "No se pudieron leer los vínculos de la revisión.",
                    );
            }
        }
        void load();
        return () => controller.abort();
    }, [route]);

    const cut = index?.availableCuts.find((entry) => entry.id === selected);
    async function save(entry: AvailableCut) {
        setBusy(true);
        setError("");
        try {
            const response = await fetch(route, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-MoneyWay-Local-Capture": "1",
                },
                body: JSON.stringify({
                    preparationRevisionSha256: revisionHash,
                    receiptCutId: entry.id,
                    receiptCutReportSha256: entry.reportSha256,
                }),
            });
            if (!response.ok) {
                const problem = await response.json().catch(() => null);
                throw new Error(
                    problem?.detail ??
                        "No se pudo verificar y guardar el vínculo.",
                );
            }
            const value: Binding = await response.json();
            setIndex(
                (previous) =>
                    previous && {
                        ...previous,
                        bindings: previous.bindings.some(
                            (binding) =>
                                binding.associationSha256 ===
                                value.associationSha256,
                        )
                            ? previous.bindings
                            : [...previous.bindings, value],
                    },
            );
        } catch (failure) {
            setError(
                failure instanceof Error
                    ? failure.message
                    : "Vínculo no disponible.",
            );
        } finally {
            setBusy(false);
        }
    }

    return (
        <>
            {error && <p role="alert">{error}</p>}
            {!index && !error && (
                <p role="status">Cargando vínculos y cortes de esta sesión.</p>
            )}
            {index &&
                (index.availableCuts.length > 0 ? (
                    <>
                        <label>
                            Corte registrado de la sesión
                            <select
                                value={selected}
                                onChange={(event) =>
                                    setSelected(event.target.value)
                                }
                                disabled={busy}
                            >
                                <option value="">Selecciona un corte</option>
                                {index.availableCuts.map((entry) => (
                                    <option key={entry.id} value={entry.id}>
                                        {entry.sequence} · {entry.id}
                                    </option>
                                ))}
                            </select>
                        </label>{" "}
                        <button
                            type="button"
                            disabled={busy || !cut}
                            onClick={() => cut && void save(cut)}
                        >
                            Guardar vínculo documental
                        </button>
                    </>
                ) : (
                    <p>No hay cortes registrados para esta sesión Standard.</p>
                ))}
            {index?.bindings.map((entry) => (
                <BindingDetails key={entry.associationSha256} entry={entry} />
            ))}
        </>
    );
}

function BindingDetails({ entry }: { entry: Binding }) {
    const value = entry.binding;
    return (
        <article
            aria-label={`Preparación vinculada al corte ${value.cutSequence}`}
        >
            <h6>Preparación vinculada al corte {value.cutSequence}</h6>
            <p>
                {value.source.company} · {value.source.server} ·{" "}
                {value.source.nativeSymbol} · {value.matchStatus}
            </p>
            <dl className="definition-summary">
                <div>
                    <dt>Etiqueta de recepción del corte</dt>
                    <dd>{value.receiptHostClockCandidate}</dd>
                </div>
                <div>
                    <dt>Preparación recibida</dt>
                    <dd>{value.preparationReceivedAtUtc}</dd>
                </div>
                <div>
                    <dt>Preparación registrada</dt>
                    <dd>
                        {value.preparationRegisteredAtUtc} ·{" "}
                        {value.preparationClockSource}
                    </dd>
                </div>
                <div>
                    <dt>Tiempo original de preparación</dt>
                    <dd>
                        {value.preparationOriginalTime ?? "Sin informar"} ·{" "}
                        {value.preparationOriginalClockDomain ??
                            "Dominio sin informar"}
                    </dd>
                </div>
                <div>
                    <dt>Observación declarada por la persona</dt>
                    <dd>
                        {value.humanReportedObservationTime ?? "Sin informar"}
                    </dd>
                </div>
                <div>
                    <dt>Asociación creada</dt>
                    <dd>{value.associationCreatedAtUtc}</dd>
                </div>
            </dl>
            <p>
                Disponibilidad humana al corte:{" "}
                {value.preparationAvailabilityAtCut}.{" "}
                {value.wallClockOrdering ===
                "PreparationRegistrationAfterCutLabel"
                    ? "El registro de preparación es posterior según las etiquetas locales."
                    : "El orden de las etiquetas locales queda sin autenticar."}
            </p>
            <p>
                NQ-TIME-003 y NQ-H4-001: {value.standardEvaluation}. Hace falta
                una evaluación canónica independiente.
            </p>
            <details>
                <summary>Fuentes, hashes y correspondencia de velas</summary>
                <ul>
                    <li>
                        Preparación {value.preparationSourceReference} ·{" "}
                        {value.preparationRevisionSha256}
                    </li>
                    <li>
                        Reporte {value.receiptCutId} ·{" "}
                        {value.receiptCutReportSha256}
                    </li>
                    <li>Recibo de corte · {value.cutReceiptSha256}</li>
                    <li>Vínculo · {entry.associationSha256}</li>
                    {entry.artifacts.map((artifact) => (
                        <li key={artifact.artifactId}>
                            {artifact.source.originalFilename} ·{" "}
                            {artifact.sha256} ·{" "}
                            {artifact.source.originalTime ??
                                "Tiempo original sin informar"}{" "}
                            ·{" "}
                            {artifact.source.originalClockDomain ??
                                "Dominio sin informar"}
                        </li>
                    ))}
                </ul>
                <ul>
                    {value.references.map((reference) => (
                        <li key={reference.localReferenceId}>
                            {reference.localReferenceId} · {reference.status} ·{" "}
                            {reference.originalTime ?? "Tiempo sin informar"} ·{" "}
                            {reference.originalClockDomain ??
                                "Dominio sin informar"}
                            {reference.marketEventNativeTime && (
                                <>
                                    {" "}
                                    · apertura MT5 nativa{" "}
                                    {reference.marketEventNativeTime.display}
                                </>
                            )}
                            {reference.firstUsableHistoricalReceipt && (
                                <>
                                    {" "}
                                    · primer recibo histórico utilizable{" "}
                                    {
                                        reference.firstUsableHistoricalReceipt
                                            .sequence
                                    }{" "}
                                    ·{" "}
                                    {
                                        reference.firstUsableHistoricalReceipt
                                            .recordSha256
                                    }{" "}
                                    · sonda{" "}
                                    {
                                        reference.firstUsableHistoricalReceipt
                                            .probeSha256
                                    }
                                </>
                            )}
                            {reference.receiptHostClockCandidate && (
                                <>
                                    {" "}
                                    · etiqueta del host{" "}
                                    {reference.receiptHostClockCandidate}
                                </>
                            )}
                        </li>
                    ))}
                    {value.diagnostics.map((diagnostic) => (
                        <li key={diagnostic}>{diagnostic}</li>
                    ))}
                </ul>
            </details>
        </article>
    );
}
