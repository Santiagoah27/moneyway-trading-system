import { useState } from "react";
import type { SupervisedSessionState } from "./SupervisedSessionPanel";
import {
    PreparationArtifactPanel,
    type PreparationArtifact,
} from "./PreparationArtifactPanel";

interface ReferenceReport {
    localReferenceId: string;
    category: string;
    nativeReferenceId: string | null;
    role: string | null;
    timeframe: string | null;
    originalSymbol: string | null;
    sourceProviderId: string | null;
    price: string | null;
    lowerPrice: string | null;
    upperPrice: string | null;
    priceUnit: string | null;
    sourceReference: string | null;
    originalTime: string | null;
    originalClockDomain: string | null;
    timePrecision: string;
    humanValidationStatus: string;
}
export interface PreparationDraft {
    sessionKey: string;
    reviewer: string;
    sourceReference: string;
    revisionReason: string;
    originalObservationId: string | null;
    sourceProviderId: string | null;
    originalSymbol: string | null;
    direction: string | null;
    h4Context: string | null;
    artifactReference: string | null;
    originalTime: string | null;
    originalClockDomain: string | null;
    timePrecision: string;
    operatorReportedObservationTime: string | null;
    h4ReviewReportedComplete: boolean;
    liquidityReviewReportedComplete: boolean;
    humanValidationStatus: string;
    references: ReferenceReport[];
}
export interface PreparationView {
    record: {
        recordId: string;
        revision: number;
        submissionSequence: number;
        submissionId: string;
        receivedAtUtc: string;
        registeredAtUtc: string;
        receiptClockSource: string;
        submissionContext: string;
        evidence: PreparationDraft;
        artifacts?: PreparationArtifact[];
        identity: {
            strategyId: { value: string };
            strategyVersion: { value: string };
        };
    };
    revisionHash: string;
    documentaryStatus: string;
    missingDocumentaryInputs: string[];
    admissionBlockers: string[];
    selectorContract: string;
    h4SelectorContract: string;
    selectorResult: string;
    preparationRuleResult: string | null;
    strategyVerdict: string | null;
    admissibleCompletionObservationCount: number;
    admissionResult: string;
    canonicalAdmissionPermitted: false;
    receiptMeaning: string;
    originalSourceIndependentlyVerified: false;
    utcAuthenticated: false;
}
const nullable = (text: string) => (text === "" ? null : text);
function blank(sessionKey: string): PreparationDraft {
    return {
        sessionKey,
        reviewer: "",
        sourceReference: "",
        revisionReason: "",
        originalObservationId: null,
        sourceProviderId: null,
        originalSymbol: null,
        direction: null,
        h4Context: null,
        artifactReference: null,
        originalTime: null,
        originalClockDomain: null,
        timePrecision: "Unknown",
        operatorReportedObservationTime: null,
        h4ReviewReportedComplete: false,
        liquidityReviewReportedComplete: false,
        humanValidationStatus: "human_validation_required",
        references: [],
    };
}
const reviews = [
    "human_validation_required",
    "confirmed",
    "candidate",
    "context_specific",
    "visual_only",
    "unresolved",
    "rejected_ai_inference",
];
const precisions = [
    "Unknown",
    "Approximate",
    "SourceReportedSecond",
    "ExactNativeRecord",
];
export function ProspectivePreparationPanel({
    apiBaseUrl,
    session,
}: {
    apiBaseUrl: string;
    session: SupervisedSessionState;
}) {
    const [draft, setDraft] = useState(() => blank(session.sessionKey));
    const [saved, setSaved] = useState<PreparationView | null>(null);
    const [records, setRecords] = useState<PreparationView[]>([]);
    const [lookup, setLookup] = useState("");
    const [revisionToRead, setRevisionToRead] = useState("1");
    const [message, setMessage] = useState("");
    const [working, setBusy] = useState(false);
    const [artifactLocked, setArtifactLocked] = useState(false);
    const busy = working || artifactLocked;
    const [pending, setPending] = useState<{
        path: string;
        body: unknown;
    } | null>(null);
    async function request<T>(path: string, body?: unknown): Promise<T> {
        const response = await fetch(
            `${apiBaseUrl}/api/nasdaq/supervised-sessions/${encodeURIComponent(session.sessionKey)}/preparation${path}`,
            body
                ? {
                      method: "POST",
                      headers: {
                          "Content-Type": "application/json",
                          "X-MoneyWay-Local-Capture": "1",
                      },
                      body: JSON.stringify(body),
                  }
                : undefined,
        );
        if (!response.ok) {
            if (response.status < 500) setPending(null);
            const p = await response.json().catch(() => ({}));
            throw new Error(
                `${p.code ?? response.status}: ${p.detail ?? (p.errors ? Object.values(p.errors).flat().join(" ") : "Preparación no disponible")}`,
            );
        }
        return response.json() as Promise<T>;
    }
    async function work(f: () => Promise<void>) {
        setBusy(true);
        setMessage("");
        try {
            await f();
        } catch (e) {
            setMessage(e instanceof Error ? e.message : "Error de preparación");
        } finally {
            setBusy(false);
        }
    }
    function accept(value: PreparationView) {
        setSaved(value);
        setDraft({ ...value.record.evidence, revisionReason: "" });
        setLookup(value.record.recordId);
    }
    async function save() {
        await work(async () => {
            const operation = pending ?? {
                path: saved ? `/${saved.record.recordId}/revisions` : "",
                body: {
                    submissionId: crypto.randomUUID(),
                    evidence: draft,
                    ...(saved ? { expectedRevision: saved.revisionHash } : {}),
                },
            };
            setPending(operation);
            const value = await request<PreparationView>(
                operation.path,
                operation.body,
            );
            accept(value);
            setPending(null);
            setMessage(
                "Preparación documental guardada y releída. No equivale a Passed, Ready ni disponibilidad UTC autenticada.",
            );
        });
    }
    function field(
        label: string,
        key: keyof PreparationDraft,
        required = false,
    ) {
        return (
            <label>
                {label}
                <input
                    value={
                        typeof draft[key] === "string" ? String(draft[key]) : ""
                    }
                    required={required}
                    onChange={(e) =>
                        setDraft({
                            ...draft,
                            [key]: required
                                ? e.target.value
                                : nullable(e.target.value),
                        })
                    }
                />
            </label>
        );
    }
    function referenceField(
        index: number,
        label: string,
        key: keyof ReferenceReport,
    ) {
        const ref = draft.references[index];
        return (
            <label>
                Referencia {index + 1}: {label}
                <input
                    value={ref[key] ?? ""}
                    onChange={(e) =>
                        updateReference(index, key, nullable(e.target.value))
                    }
                />
            </label>
        );
    }
    function updateReference(
        index: number,
        key: keyof ReferenceReport,
        value: string | null,
    ) {
        setDraft({
            ...draft,
            references: draft.references.map((r, i) =>
                i === index ? { ...r, [key]: value } : r,
            ),
        });
    }
    return (
        <section aria-label="Preparación prospectiva Standard">
            <h4>Preparación prospectiva Standard · reporte humano</h4>
            <p>
                Sesión {session.sessionId} · {session.identity.tradingDay}{" "}
                America/Bogota · {session.identity.providerId.value}/
                {session.identity.symbol.value} ·{" "}
                {session.terminal
                    ? "TERMINAL: solo documentación/correcciones, sin nuevas entradas"
                    : session.status}
                .
            </p>
            <p>
                Preparación documentada ≠ preparación canónicamente Passed.
                Evidencia capturada ≠ disponibilidad UTC autenticada. Sesión
                desbloqueada ≠ estrategia ready.
            </p>
            <p>
                Ventana de preparación existente: [08:00,08:30) Bogotá. Se
                conserva el conflicto entre instrucción 11:30 y cutoff runtime
                11:00; no se decide ni modifica aquí.
            </p>
            <button
                type="button"
                disabled={busy || !!pending}
                onClick={() =>
                    void work(async () => {
                        const status = await request<{
                            status: string;
                            records: PreparationView[];
                        }>("");
                        setRecords(status.records);
                        setMessage(status.status);
                    })
                }
            >
                Consultar preparación guardada de esta sesión
            </button>
            <ul>
                {records.map((r) => (
                    <li key={r.record.recordId}>
                        <button
                            type="button"
                            disabled={busy || !!pending}
                            onClick={() => accept(r)}
                        >
                            {r.record.recordId} · preparación revisión{" "}
                            {r.record.revision} · {r.documentaryStatus}
                        </button>
                    </li>
                ))}
            </ul>
            <fieldset disabled={busy || !!pending}>
                <legend>Recuperación de preparación inmutable</legend>
                <label>
                    Número de revisión de preparación
                    <input
                        type="number"
                        min="1"
                        max="100"
                        value={revisionToRead}
                        onChange={(e) => setRevisionToRead(e.target.value)}
                    />
                </label>
                <button
                    type="button"
                    disabled={!lookup || !revisionToRead}
                    onClick={() =>
                        void work(async () =>
                            accept(
                                await request(
                                    `/${encodeURIComponent(lookup)}?revision=${encodeURIComponent(revisionToRead)}`,
                                ),
                            ),
                        )
                    }
                >
                    Ver revisión de preparación
                </button>
                <label>
                    ID MoneyWay de preparación
                    <input
                        value={lookup}
                        onChange={(e) => setLookup(e.target.value)}
                    />
                </label>
                <button
                    type="button"
                    disabled={!lookup}
                    onClick={() =>
                        void work(async () =>
                            accept(
                                await request(`/${encodeURIComponent(lookup)}`),
                            ),
                        )
                    }
                >
                    Recuperar preparación por ID
                </button>
                <button
                    type="button"
                    disabled={!lookup}
                    onClick={() =>
                        void work(async () =>
                            accept(
                                await request(
                                    `/${encodeURIComponent(lookup)}?revision=1`,
                                ),
                            ),
                        )
                    }
                >
                    Ver preparación original
                </button>
                <button
                    type="button"
                    onClick={() => {
                        setSaved(null);
                        setDraft(blank(session.sessionKey));
                        setMessage("");
                        setLookup("");
                    }}
                >
                    Nueva preparación documental
                </button>
            </fieldset>
            <form
                onSubmit={(e) => {
                    e.preventDefault();
                    void save();
                }}
            >
                <fieldset disabled={busy || !!pending}>
                    <legend>
                        {saved
                            ? `Corrección de preparación · revisión ${saved.record.revision}`
                            : "Nueva observación de preparación parcial"}
                    </legend>
                    <div className="capture-grid">
                        {field(
                            "Preparación: revisor declarante",
                            "reviewer",
                            true,
                        )}
                        {field(
                            "Preparación: fuente general (note: o https://)",
                            "sourceReference",
                            true,
                        )}
                        {field(
                            "Preparación: motivo de registro/corrección",
                            "revisionReason",
                            true,
                        )}
                        {field(
                            "Preparación: ID original de observación (opcional)",
                            "originalObservationId",
                        )}
                        {field(
                            "Preparación: proveedor original observado (opcional)",
                            "sourceProviderId",
                        )}
                        {field(
                            "Preparación: instrumento original observado (opcional)",
                            "originalSymbol",
                        )}
                        <label>
                            Preparación: dirección H4 humana
                            <select
                                value={draft.direction ?? ""}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        direction: nullable(e.target.value),
                                    })
                                }
                            >
                                <option value="">Sin selección</option>
                                {["Unresolved", "Buy", "Sell"].map((v) => (
                                    <option key={v}>{v}</option>
                                ))}
                            </select>
                        </label>
                        <label>
                            Preparación: contexto H4 humano
                            <select
                                value={draft.h4Context ?? ""}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        h4Context: nullable(e.target.value),
                                    })
                                }
                            >
                                <option value="">Sin selección</option>
                                {[
                                    "Unresolved",
                                    "Breakout",
                                    "Wickfill",
                                    "Fakeout",
                                ].map((v) => (
                                    <option key={v}>{v}</option>
                                ))}
                            </select>
                        </label>
                        {field(
                            "Preparación: referencia al artefacto (opcional, note: o https://)",
                            "artifactReference",
                        )}
                        {field(
                            "Preparación: timestamp original mostrado (opcional)",
                            "originalTime",
                        )}
                        {field(
                            "Preparación: reloj/zona original (opcional)",
                            "originalClockDomain",
                        )}
                        {field(
                            "Preparación: hora de observación reportada por el operador (opcional)",
                            "operatorReportedObservationTime",
                        )}
                        <label>
                            Preparación: precisión original
                            <select
                                value={draft.timePrecision}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        timePrecision: e.target.value,
                                    })
                                }
                            >
                                {precisions.map((v) => (
                                    <option key={v}>{v}</option>
                                ))}
                            </select>
                        </label>
                        <label>
                            Preparación: validación humana declarada
                            <select
                                value={draft.humanValidationStatus}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        humanValidationStatus: e.target.value,
                                    })
                                }
                            >
                                {reviews.map((v) => (
                                    <option key={v}>{v}</option>
                                ))}
                            </select>
                        </label>
                    </div>
                    <label>
                        <input
                            type="checkbox"
                            checked={draft.h4ReviewReportedComplete}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    h4ReviewReportedComplete: e.target.checked,
                                })
                            }
                        />
                        Reporto haber completado la revisión H4; no autentica
                        sus fuentes
                    </label>
                    <label>
                        <input
                            type="checkbox"
                            checked={draft.liquidityReviewReportedComplete}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    liquidityReviewReportedComplete:
                                        e.target.checked,
                                })
                            }
                        />
                        Reporto haber completado la revisión de liquidez; no
                        concede Passed
                    </label>
                    <button
                        type="button"
                        onClick={() =>
                            setDraft({
                                ...draft,
                                sourceProviderId:
                                    session.identity.providerId.value,
                                originalSymbol: session.identity.symbol.value,
                            })
                        }
                    >
                        Usar identidad fuente declarada de la sesión
                    </button>
                    {draft.references.map((r, i) => (
                        <fieldset key={r.localReferenceId}>
                            <legend>
                                Referencia documental {i + 1} ·{" "}
                                {r.localReferenceId} (ID local, no nativo)
                            </legend>
                            <label>
                                Referencia {i + 1}: categoría
                                <select
                                    value={r.category}
                                    onChange={(e) =>
                                        updateReference(
                                            i,
                                            "category",
                                            e.target.value,
                                        )
                                    }
                                >
                                    <option>Structure</option>
                                    <option>Liquidity</option>
                                </select>
                            </label>
                            <label>
                                Referencia {i + 1}: rol humano
                                <select
                                    value={r.role ?? ""}
                                    onChange={(e) =>
                                        updateReference(
                                            i,
                                            "role",
                                            nullable(e.target.value),
                                        )
                                    }
                                >
                                    <option value="">Sin selección</option>
                                    {[
                                        "HH",
                                        "HL",
                                        "LH",
                                        "LL",
                                        "AsiaHigh",
                                        "AsiaLow",
                                        "LondonHigh",
                                        "LondonLow",
                                        "PreviousDayHigh",
                                        "PreviousDayLow",
                                    ].map((v) => (
                                        <option key={v}>{v}</option>
                                    ))}
                                </select>
                            </label>
                            <label>
                                Referencia {i + 1}: timeframe original
                                <select
                                    value={r.timeframe ?? ""}
                                    onChange={(e) =>
                                        updateReference(
                                            i,
                                            "timeframe",
                                            nullable(e.target.value),
                                        )
                                    }
                                >
                                    <option value="">Desconocido</option>
                                    <option>4h</option>
                                    <option>1h</option>
                                </select>
                            </label>
                            {referenceField(
                                i,
                                "ID nativo original (opcional)",
                                "nativeReferenceId",
                            )}
                            {referenceField(
                                i,
                                "precio original escalar (opcional)",
                                "price",
                            )}
                            {referenceField(
                                i,
                                "límite inferior original de zona (opcional)",
                                "lowerPrice",
                            )}
                            {referenceField(
                                i,
                                "límite superior original de zona (opcional)",
                                "upperPrice",
                            )}
                            <label>
                                Referencia {i + 1}: unidad original
                                <select
                                    value={r.priceUnit ?? ""}
                                    onChange={(e) =>
                                        updateReference(
                                            i,
                                            "priceUnit",
                                            nullable(e.target.value),
                                        )
                                    }
                                >
                                    <option value="">No indicada</option>
                                    <option>NativeInstrumentPrice</option>
                                </select>
                            </label>
                            {referenceField(
                                i,
                                "fuente (opcional, note: o https://)",
                                "sourceReference",
                            )}
                            {referenceField(
                                i,
                                "timestamp original (opcional)",
                                "originalTime",
                            )}
                            {referenceField(
                                i,
                                "reloj original (opcional)",
                                "originalClockDomain",
                            )}
                            <label>
                                Referencia {i + 1}: precisión
                                <select
                                    value={r.timePrecision}
                                    onChange={(e) =>
                                        updateReference(
                                            i,
                                            "timePrecision",
                                            e.target.value,
                                        )
                                    }
                                >
                                    {precisions.map((v) => (
                                        <option key={v}>{v}</option>
                                    ))}
                                </select>
                            </label>
                            <label>
                                Referencia {i + 1}: validación humana
                                <select
                                    value={r.humanValidationStatus}
                                    onChange={(e) =>
                                        updateReference(
                                            i,
                                            "humanValidationStatus",
                                            e.target.value,
                                        )
                                    }
                                >
                                    {reviews.map((v) => (
                                        <option key={v}>{v}</option>
                                    ))}
                                </select>
                            </label>
                            <button
                                type="button"
                                onClick={() =>
                                    setDraft({
                                        ...draft,
                                        references: draft.references.filter(
                                            (_, index) => index !== i,
                                        ),
                                    })
                                }
                            >
                                Quitar referencia documental {i + 1} del
                                borrador
                            </button>
                        </fieldset>
                    ))}
                    <button
                        type="button"
                        disabled={draft.references.length >= 32}
                        onClick={() =>
                            setDraft({
                                ...draft,
                                references: [
                                    ...draft.references,
                                    {
                                        localReferenceId: `documentary-${crypto.randomUUID()}`,
                                        category: "Structure",
                                        nativeReferenceId: null,
                                        role: null,
                                        timeframe: null,
                                        originalSymbol: null,
                                        sourceProviderId: null,
                                        price: null,
                                        lowerPrice: null,
                                        upperPrice: null,
                                        priceUnit: null,
                                        sourceReference: null,
                                        originalTime: null,
                                        originalClockDomain: null,
                                        timePrecision: "Unknown",
                                        humanValidationStatus:
                                            "human_validation_required",
                                    },
                                ],
                            })
                        }
                    >
                        Añadir referencia humana de preparación
                    </button>
                    <button type="submit">
                        {saved
                            ? "Guardar nueva revisión de preparación"
                            : "Guardar preparación parcial"}
                    </button>
                </fieldset>
            </form>
            {pending && !busy && (
                <button type="button" onClick={() => void save()}>
                    Reintentar mismo envío de preparación
                </button>
            )}
            {saved && (
                <section aria-label="Preparación persistida">
                    <h5>{saved.documentaryStatus}</h5>
                    <PreparationArtifactPanel
                        key={`${saved.record.recordId}-${saved.record.revision}`}
                        apiBaseUrl={apiBaseUrl}
                        sessionKey={session.sessionKey}
                        preparation={saved}
                        onAttached={accept}
                        onLockChange={setArtifactLocked}
                        disabled={working || !!pending}
                    />
                    <p>
                        {saved.record.identity.strategyId.value}/
                        {saved.record.identity.strategyVersion.value} · ID
                        MoneyWay {saved.record.recordId} · revisión{" "}
                        {saved.record.revision} · secuencia de preparación{" "}
                        {saved.record.submissionSequence} · SHA256{" "}
                        {saved.revisionHash}.
                    </p>
                    <p>
                        Recepción de solicitud parseada:{" "}
                        {saved.record.receivedAtUtc} · registro:{" "}
                        {saved.record.registeredAtUtc} ·{" "}
                        {saved.record.receiptClockSource} ·{" "}
                        {saved.record.submissionContext}.
                    </p>
                    <p>
                        {saved.receiptMeaning} · Fuente original no verificada;
                        UTC no autenticado.
                    </p>
                    <p>
                        Admisión: {saved.admissionResult} ·{" "}
                        {saved.selectorContract} / {saved.h4SelectorContract} ·{" "}
                        {saved.selectorResult} · observaciones de finalización
                        admisibles: {saved.admissibleCompletionObservationCount}
                        .
                    </p>
                    <p>
                        Resultado de regla:{" "}
                        {saved.preparationRuleResult ?? "No evaluado"} ·
                        Veredicto: {saved.strategyVerdict ?? "No calculado"}.
                        Completar documentación no crea una señal.
                    </p>
                    <ul>
                        {saved.missingDocumentaryInputs.map((v) => (
                            <li key={v}>{v}</li>
                        ))}
                        {saved.admissionBlockers.map((v) => (
                            <li key={v}>{v}</li>
                        ))}
                    </ul>
                    <details>
                        <summary>
                            Originales de esta revisión de preparación
                        </summary>
                        <pre>
                            {JSON.stringify(saved.record.evidence, null, 2)}
                        </pre>
                    </details>
                </section>
            )}
            <p>
                Los diagnósticos y el registro de ejecución siguen en los
                paneles existentes de esta sesión; no se insertan estos
                documentos en frames históricos ni se habilitan entradas.
            </p>
            {message && <p role="status">{message}</p>}
        </section>
    );
}
