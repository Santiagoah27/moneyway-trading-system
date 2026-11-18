import { useState } from "react";
import { ReportedMonetaryOutcomeFields } from "./ReportedMonetaryOutcomeFields";
import type { ReportedMonetaryOutcome } from "./ReportedMonetaryOutcomeFields";

export interface CaptureEvent {
    sourceReference: string;
    eventKind: string;
    symbol: string | null;
    direction: string | null;
    price: string | null;
    priceUnit: string | null;
    currency: string | null;
    originalTime: string | null;
    clockDomain: string | null;
    timePrecision: string;
    documentedUtc: string | null;
    timeMappingReference: string | null;
    dealId: string | null;
    positionId: string | null;
    closeMechanism: string | null;
    closeMechanismSource: string | null;
}
export interface CaptureDraft {
    sessionId: string;
    tradingDay: string;
    sourcePlatform: string;
    sourceProviderId: string | null;
    originalSymbol: string;
    direction: string | null;
    scenario: string | null;
    reviewer: string;
    sourceReference: string;
    revisionReason: string;
    entry: CaptureEvent | null;
    exit: CaptureEvent | null;
    association: string;
    associationSourceReference: string | null;
    reportedOutcome?: ReportedMonetaryOutcome | null;
}
interface CaptureView {
    record: {
        recordId: string;
        revision: number;
        submissionId: string;
        registeredAtUtc: string;
        previousRevision: string | null;
        evidence: CaptureDraft;
    };
    revisionHash: string;
    scope: string;
    integrity: string;
    artifactAvailability: string;
    originalArtifactSha256: null;
    nativeExecutionVerified: false;
    entryExitRelationshipVerified: false;
    completeSupervisedTrade: false;
    economicOutcome: null;
    missingEvidence: string[];
    documentaryLifecycle: {
        supervisedExecutionRecord: {
            recordId: string;
            revision: number;
            revisionHash: string;
        };
    } | null;
}
const emptyEvent = (entry: boolean): CaptureEvent => ({
    sourceReference: "",
    eventKind: entry ? "ReportedExecution" : "ReportedClosure",
    symbol: null,
    direction: null,
    price: null,
    priceUnit: null,
    currency: null,
    originalTime: null,
    clockDomain: null,
    timePrecision: "Unknown",
    documentedUtc: null,
    timeMappingReference: null,
    dealId: null,
    positionId: null,
    closeMechanism: null,
    closeMechanismSource: null,
});
const blank = (): CaptureDraft => ({
    sessionId: "",
    tradingDay: "",
    sourcePlatform: "",
    sourceProviderId: null,
    originalSymbol: "",
    direction: null,
    scenario: null,
    reviewer: "",
    sourceReference: "",
    revisionReason: "",
    entry: emptyEvent(true),
    exit: null,
    association: "None",
    associationSourceReference: null,
    reportedOutcome: null,
});
const optional = (s: string) => s.trim() || null;

function EventFields({
    title,
    event,
    onChange,
    entry,
}: {
    title: string;
    event: CaptureEvent;
    onChange: (e: CaptureEvent) => void;
    entry: boolean;
}) {
    function input(label: string, field: keyof CaptureEvent, required = false) {
        return (
            <label>
                {title}: {label}
                <input
                    required={required}
                    value={event[field] ?? ""}
                    maxLength={2048}
                    onChange={(e) =>
                        onChange({
                            ...event,
                            [field]: required
                                ? e.target.value
                                : optional(e.target.value),
                        })
                    }
                />
            </label>
        );
    }
    return (
        <fieldset>
            <legend>{title}</legend>
            <div className="capture-grid">
                {input("Fuente (note: o https://)", "sourceReference", true)}
                <label>
                    {title}: evento
                    <select
                        value={event.eventKind}
                        onChange={(e) =>
                            onChange({ ...event, eventKind: e.target.value })
                        }
                    >
                        {(entry
                            ? [
                                  "ReportedExecution",
                                  "PositionOpeningField",
                                  "OpenPositionObserved",
                                  "OrderPlacement",
                              ]
                            : [
                                  "ReportedClosure",
                                  "PositionClosingField",
                                  "ClosedPositionObserved",
                                  "ReportedStopProtection",
                              ]
                        ).map((s) => (
                            <option key={s}>{s}</option>
                        ))}
                    </select>
                </label>
                <label>
                    {title}: instrumento
                    <select
                        value={event.symbol ?? ""}
                        onChange={(e) =>
                            onChange({
                                ...event,
                                symbol: optional(e.target.value),
                            })
                        }
                    >
                        <option value="">
                            {entry
                                ? "Usar instrumento original declarado"
                                : "Desconocido"}
                        </option>
                        <option>US100</option>
                        <option>USTEC</option>
                    </select>
                </label>
                <label>
                    {title}: precio observado
                    <input
                        inputMode="decimal"
                        value={event.price ?? ""}
                        onChange={(e) =>
                            onChange({
                                ...event,
                                price: optional(e.target.value),
                                priceUnit: e.target.value.trim()
                                    ? event.priceUnit
                                    : null,
                            })
                        }
                    />
                </label>
                <label>
                    {title}: unidad de precio
                    <select
                        value={event.priceUnit ?? ""}
                        required={event.price !== null}
                        onChange={(e) =>
                            onChange({
                                ...event,
                                priceUnit: optional(e.target.value),
                            })
                        }
                    >
                        <option value="">Sin unidad documentada</option>
                        <option>NativeInstrumentPrice</option>
                    </select>
                </label>
                {input("Moneda, si consta", "currency")}
                {input("Hora original mostrada", "originalTime")}
                {input("Zona horaria / reloj original", "clockDomain")}
                <label>
                    {title}: precisión
                    <select
                        value={event.timePrecision}
                        onChange={(e) =>
                            onChange({
                                ...event,
                                timePrecision: e.target.value,
                            })
                        }
                    >
                        <option value="Unknown">Desconocida</option>
                        <option value="Approximate">Aproximada</option>
                        <option value="SourceReportedSecond">
                            Segundos según fuente
                        </option>
                        <option value="ExactNativeRecord">
                            Exacta según registro declarado; sin verificar
                        </option>
                    </select>
                </label>
                {input("UTC documental opcional (con Z)", "documentedUtc")}
                {input(
                    "Fuente de interpretación UTC (note: o https://)",
                    "timeMappingReference",
                )}
                {input("Deal ID original, si consta", "dealId")}
                {input("Position ID original, si consta", "positionId")}
                {!entry && (
                    <>
                        {input(
                            "Mecanismo de cierre documentado",
                            "closeMechanism",
                        )}
                        {input(
                            "Fuente del mecanismo (note: o https://)",
                            "closeMechanismSource",
                        )}
                    </>
                )}
            </div>
        </fieldset>
    );
}

export function SupervisedExecutionCapture({
    apiBaseUrl,
}: {
    apiBaseUrl: string;
}) {
    const [draft, setDraft] = useState<CaptureDraft>(blank);
    const [saved, setSaved] = useState<CaptureView | null>(null);
    const [items, setItems] = useState<CaptureView[]>([]);
    const [lookup, setLookup] = useState("");
    const [message, setMessage] = useState("");
    const [busy, setBusy] = useState(false);
    const [pending, setPending] = useState<{
        submissionId: string;
        evidence: CaptureDraft;
        expectedRevision?: string;
    } | null>(null);
    async function request<T>(path: string, body?: unknown): Promise<T> {
        const response = await fetch(
            `${apiBaseUrl}/api/nasdaq/execution-evidence${path}`,
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
            const p = await response.json().catch(() => ({}));
            if (response.status < 500) setPending(null);
            throw new Error(
                `${p.code ?? response.status}: ${p.detail ?? (p.errors ? Object.values(p.errors).flat().join(" ") : "No se pudo guardar o recuperar. Recarga la revisión antes de corregir.")}`,
            );
        }
        return (await response.json()) as T;
    }
    async function operation(work: () => Promise<void>) {
        setBusy(true);
        setMessage("");
        try {
            await work();
        } catch (e) {
            setMessage(e instanceof Error ? e.message : "Error de captura");
        } finally {
            setBusy(false);
        }
    }
    async function load(id: string, revision?: number) {
        await operation(async () => {
            const value = await request<CaptureView>(
                `/${encodeURIComponent(id)}${revision ? `?revision=${revision}` : ""}`,
            );
            setSaved(value);
            setDraft({ ...value.record.evidence, revisionReason: "" });
            setPending(null);
            setLookup(id);
            setMessage(
                "Registro persistido recuperado. Las correcciones crean una revisión nueva.",
            );
        });
    }
    async function save() {
        await operation(async () => {
            const body = pending ?? {
                submissionId: crypto.randomUUID(),
                evidence: draft,
                ...(saved ? { expectedRevision: saved.revisionHash } : {}),
            };
            setPending(body);
            const result = await request<CaptureView>(
                saved ? `/${saved.record.recordId}/revisions` : "",
                body,
            );
            setSaved(result);
            setDraft({ ...result.record.evidence, revisionReason: "" });
            setLookup(result.record.recordId);
            setPending(null);
            setMessage(
                "Evidencia guardada y releída desde almacenamiento local. Registro documental incompleto.",
            );
        });
    }
    function text(label: string, field: keyof CaptureDraft, type = "text") {
        return (
            <label>
                {label}
                <input
                    type={type}
                    required
                    value={String(draft[field] ?? "")}
                    maxLength={2048}
                    onChange={(e) =>
                        setDraft({ ...draft, [field]: e.target.value })
                    }
                />
            </label>
        );
    }
    return (
        <section
            className="execution-capture"
            aria-label="Captura supervisada de evidencia"
        >
            <h3>Registro supervisado de evidencia de ejecución</h3>
            <p>
                Declaración manual con procedencia. No envía órdenes ni verifica
                ejecuciones. No modifica el replay, SL, riesgo o TP. Usa note:
                para una afirmación o https:// para una referencia; los
                artefactos no se descargan ni inspeccionan.
            </p>
            <button
                type="button"
                disabled={busy}
                onClick={() => {
                    setDraft(blank());
                    setSaved(null);
                    setPending(null);
                    setMessage("");
                }}
            >
                Nuevo registro
            </button>
            <form
                onSubmit={(e) => {
                    e.preventDefault();
                    void save();
                }}
            >
                <fieldset disabled={busy || pending !== null}>
                    <legend>
                        {saved
                            ? `Corregir registro · revisión ${saved.record.revision}`
                            : "Nuevo registro documental"}
                    </legend>
                    <div className="capture-grid">
                        {text("Sesión de evidencia", "sessionId")}
                        {text("Fecha de sesión", "tradingDay", "date")}
                        {text("Plataforma original", "sourcePlatform")}
                        <label>
                            Proveedor original (opcional)
                            <input
                                value={draft.sourceProviderId ?? ""}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        sourceProviderId: optional(
                                            e.target.value,
                                        ),
                                    })
                                }
                            />
                        </label>
                        <label>
                            Instrumento original
                            <select
                                required
                                value={draft.originalSymbol}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        originalSymbol: e.target.value,
                                    })
                                }
                            >
                                <option value="">Seleccionar</option>
                                <option>USTEC</option>
                                <option>US100</option>
                            </select>
                        </label>
                        <label>
                            Dirección
                            <select
                                value={draft.direction ?? ""}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        direction: optional(e.target.value),
                                        scenario:
                                            e.target.value === "Sell"
                                                ? draft.scenario
                                                : null,
                                    })
                                }
                            >
                                <option value="">Desconocida</option>
                                <option value="Sell">SELL</option>
                                <option value="Buy">BUY</option>
                            </select>
                        </label>
                        <label>
                            Escenario declarado
                            <select
                                value={draft.scenario ?? ""}
                                onChange={(e) =>
                                    setDraft({
                                        ...draft,
                                        scenario: optional(e.target.value),
                                    })
                                }
                            >
                                <option value="">Sin clasificación</option>
                                {draft.direction === "Sell" && (
                                    <option>WickfillScenario2</option>
                                )}
                            </select>
                        </label>
                        {text("Revisor / declarante", "reviewer")}
                        {text(
                            "Fuente general (note: o https://)",
                            "sourceReference",
                        )}
                        {text(
                            "Motivo de registro o corrección",
                            "revisionReason",
                        )}
                    </div>
                    <label>
                        <input
                            type="checkbox"
                            checked={draft.entry !== null}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    entry: e.target.checked
                                        ? emptyEvent(true)
                                        : null,
                                    association: "None",
                                    associationSourceReference: null,
                                })
                            }
                        />
                        Registrar entrada observada
                    </label>
                    {draft.entry && (
                        <EventFields
                            title="Entrada"
                            event={draft.entry}
                            entry
                            onChange={(entry) => setDraft({ ...draft, entry })}
                        />
                    )}
                    <label>
                        <input
                            type="checkbox"
                            checked={draft.exit !== null}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    exit: e.target.checked
                                        ? emptyEvent(false)
                                        : null,
                                    association: "None",
                                    associationSourceReference: null,
                                })
                            }
                        />
                        Registrar salida reportada
                    </label>
                    {draft.exit && (
                        <EventFields
                            title="Salida"
                            event={draft.exit}
                            entry={false}
                            onChange={(exit) => setDraft({ ...draft, exit })}
                        />
                    )}
                    <label>
                        Asociación entrada/salida
                        <select
                            value={draft.association}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    association: e.target.value,
                                })
                            }
                        >
                            <option value="None">No establecida</option>
                            <option value="HumanReported">
                                Afirmada por el humano
                            </option>
                            <option value="DocumentarySupported">
                                Afirmada con fuente documental
                            </option>
                        </select>
                    </label>
                    <label>
                        Fuente explícita del vínculo
                        <input
                            required={draft.association !== "None"}
                            value={draft.associationSourceReference ?? ""}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    associationSourceReference: optional(
                                        e.target.value,
                                    ),
                                })
                            }
                        />
                    </label>
                    <label>
                        <input
                            type="checkbox"
                            disabled={!saved}
                            checked={!!draft.reportedOutcome}
                            onChange={(e) =>
                                setDraft({
                                    ...draft,
                                    reportedOutcome:
                                        e.target.checked && saved
                                            ? {
                                                  executionRecordId:
                                                      saved.record.recordId,
                                                  executionEvidenceRevision:
                                                      saved.record.revision,
                                                  executionEvidenceRevisionHash:
                                                      saved.revisionHash,
                                                  sourceReference: "",
                                                  reportedBy: draft.reviewer,
                                                  provenance: "HumanReported",
                                                  sourceSymbol: null,
                                                  amount: null,
                                                  currency: null,
                                                  amountMeaning: "Unspecified",
                                                  basis: "Unspecified",
                                                  originalMeaningStatement:
                                                      null,
                                                  narrative: null,
                                                  sourceEventDescription: null,
                                                  association: "RecordOnly",
                                                  associationSourceReference:
                                                      null,
                                                  dealReference: null,
                                                  positionReference: null,
                                                  originalTime: null,
                                                  clockDomain: null,
                                                  timePrecision: "Unknown",
                                                  costs: null,
                                              }
                                            : null,
                                })
                            }
                        />
                        Registrar resultado monetario reportado
                    </label>
                    {!saved && (
                        <p>
                            Guarda o recupera un registro antes de añadir un
                            resultado reportado.
                        </p>
                    )}
                    {draft.reportedOutcome && (
                        <ReportedMonetaryOutcomeFields
                            value={draft.reportedOutcome}
                            onChange={(reportedOutcome) =>
                                setDraft({ ...draft, reportedOutcome })
                            }
                        />
                    )}
                </fieldset>
                <button disabled={busy} type="submit">
                    {pending
                        ? "Reintentar mismo envío"
                        : saved
                          ? "Guardar nueva revisión"
                          : "Guardar evidencia parcial"}
                </button>
            </form>
            {pending && !busy && (
                <p>
                    El estado del envío puede ser incierto. Reintenta el mismo
                    envío o busca registros de la sesión antes de iniciar otro.
                </p>
            )}
            {message && <p role="status">{message}</p>}
            <div className="capture-grid">
                <label>
                    MoneyWay record ID
                    <input
                        value={lookup}
                        onChange={(e) => setLookup(e.target.value)}
                    />
                </label>
                <button
                    disabled={busy || !lookup}
                    onClick={() => void load(lookup)}
                >
                    Recuperar por ID
                </button>
                <button
                    disabled={busy || !draft.sessionId}
                    onClick={() =>
                        void operation(async () =>
                            setItems(
                                await request<CaptureView[]>(
                                    `?sessionId=${encodeURIComponent(draft.sessionId)}`,
                                ),
                            ),
                        )
                    }
                >
                    Listar registros de sesión
                </button>
            </div>
            <ul>
                {items.map((v) => (
                    <li key={v.record.recordId}>
                        <button
                            disabled={busy}
                            onClick={() => void load(v.record.recordId)}
                        >
                            {v.record.recordId} · revisión {v.record.revision} ·{" "}
                            {v.record.evidence.originalSymbol}
                        </button>
                    </li>
                ))}
            </ul>
            {saved && (
                <section aria-label="Evidencia persistida">
                    <h4>Registro documental incompleto</h4>
                    <p>
                        MoneyWay ID: {saved.record.recordId} · Revisión:{" "}
                        {saved.record.revision}. Este ID no es un deal del
                        broker.
                    </p>
                    <p>
                        Registro local: {saved.record.registeredAtUtc}.
                        Disponibilidad histórica: no establecida. Hash de
                        revisión: {saved.revisionHash} · {saved.integrity}.
                        Artefacto: {saved.artifactAvailability}.
                    </p>
                    <p>
                        Verificación nativa: No. Vínculo verificado: No.
                        Operación completa: No. Resultado económico: no
                        calculado.
                    </p>
                    <p>
                        Asociación declarada:{" "}
                        {saved.record.evidence.association}. Requisitos
                        pendientes: {saved.missingEvidence.join(", ")}.
                    </p>
                    {saved.record.evidence.reportedOutcome && (
                        <section aria-label="Resultado monetario persistido">
                            <h5>
                                Resultado reportado · Sin verificación
                                independiente
                            </h5>
                            <p>
                                Resultado reportado ≠ P&amp;L calculado por
                                MoneyWay. Resultado reportado ≠ resultado
                                verificado por el broker.
                            </p>
                            <p>
                                Importe original:{" "}
                                {saved.record.evidence.reportedOutcome.amount ??
                                    "No reportado"}{" "}
                                · Moneda:{" "}
                                {saved.record.evidence.reportedOutcome
                                    .currency ?? "Desconocida"}{" "}
                                · Bruto/neto:{" "}
                                {saved.record.evidence.reportedOutcome.basis} ·
                                Significado:{" "}
                                {
                                    saved.record.evidence.reportedOutcome
                                        .amountMeaning
                                }
                                .
                            </p>
                            <p>
                                Instrumento:{" "}
                                {saved.record.evidence.reportedOutcome
                                    .sourceSymbol ?? "No establecido"}{" "}
                                · Fuente:{" "}
                                {
                                    saved.record.evidence.reportedOutcome
                                        .sourceReference
                                }{" "}
                                · Reportado por:{" "}
                                {
                                    saved.record.evidence.reportedOutcome
                                        .reportedBy
                                }{" "}
                                ·{" "}
                                {
                                    saved.record.evidence.reportedOutcome
                                        .provenance
                                }
                                .
                            </p>
                            <p>
                                Revisión de evidencia vinculada:{" "}
                                {
                                    saved.record.evidence.reportedOutcome
                                        .executionEvidenceRevision
                                }{" "}
                                · Asociación declarada:{" "}
                                {
                                    saved.record.evidence.reportedOutcome
                                        .association
                                }
                                . No verifica el vínculo de ejecuciones.
                            </p>
                            <p>
                                Narrativa:{" "}
                                {saved.record.evidence.reportedOutcome
                                    .narrative ?? "No documentada"}
                                . Significado original:{" "}
                                {saved.record.evidence.reportedOutcome
                                    .originalMeaningStatement ??
                                    "No establecido"}
                                .
                            </p>
                            {(saved.record.evidence.reportedOutcome.costs ?? [])
                                .length === 0 ? (
                                <p>Costos desconocidos; no equivalen a cero.</p>
                            ) : (
                                <ul>
                                    {saved.record.evidence.reportedOutcome.costs!.map(
                                        (cost, i) => (
                                            <li key={i}>
                                                {cost.category}:{" "}
                                                {cost.amount ??
                                                    "Importe desconocido"}{" "}
                                                {cost.currency ??
                                                    "Moneda desconocida"}{" "}
                                                · {cost.sourceReference} ·
                                                Signo:{" "}
                                                {cost.originalSignMeaning ??
                                                    "No establecido"}
                                            </li>
                                        ),
                                    )}
                                </ul>
                            )}
                            <p>
                                No se suman costos ni convierten monedas. No
                                establece validez estratégica, cierre de sesión
                                o permiso de reentrada. Operación incompleta.
                            </p>
                        </section>
                    )}
                    <details>
                        <summary>
                            Valores originales y procedencia registrados
                        </summary>
                        <pre>
                            {JSON.stringify(saved.record.evidence, null, 2)}
                        </pre>
                    </details>
                    {saved.documentaryLifecycle && (
                        <p>
                            Referencia documental separada:{" "}
                            NotGrantedByDiagnostic. Elegibilidad canónica
                            original: no establecida por esta captura.
                        </p>
                    )}
                    {Array.from({ length: saved.record.revision }, (_, i) => (
                        <button
                            key={i}
                            disabled={busy}
                            onClick={() =>
                                void load(saved.record.recordId, i + 1)
                            }
                        >
                            Ver revisión {i + 1}
                        </button>
                    ))}
                </section>
            )}
        </section>
    );
}
