import { useEffect, useState } from "react";
import type { PreparationView } from "./ProspectivePreparationPanel";

export interface PreparationArtifact {
    artifactId: string;
    attachedInRevision: number;
    byteLength: number;
    sha256: string;
    receivedAtUtc: string;
    receiptClockSource: string;
    source: {
        originalFilename: string;
        contentType: string;
        sourceProviderId: string;
        originalSymbol: string;
        sourceDescription: string;
        revisionReason: string;
        observationReference: string | null;
        originalTime: string | null;
        originalClockDomain: string | null;
        timePrecision: string;
    };
}
export function PreparationArtifactPanel({
    apiBaseUrl,
    sessionKey,
    preparation,
    onAttached,
    onLockChange,
    disabled,
}: {
    apiBaseUrl: string;
    sessionKey: string;
    preparation: PreparationView;
    onAttached: (view: PreparationView) => void;
    onLockChange: (locked: boolean) => void;
    disabled: boolean;
}) {
    const [file, setFile] = useState<File | null>(null);
    const [provider, setProvider] = useState(
        preparation.record.evidence.sourceProviderId ?? "",
    );
    const [symbol, setSymbol] = useState(
        preparation.record.evidence.originalSymbol ?? "",
    );
    const [description, setDescription] = useState("");
    const [reason, setReason] = useState("");
    const [observation, setObservation] = useState("");
    const [time, setTime] = useState("");
    const [clock, setClock] = useState("");
    const [precision, setPrecision] = useState("Unknown");
    const [pending, setPending] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState("");
    const [preview, setPreview] = useState<{
        url: string;
        filename: string;
    } | null>(null);
    useEffect(
        () => () => {
            if (preview) URL.revokeObjectURL(preview.url);
        },
        [preview],
    );
    const base = `${apiBaseUrl}/api/nasdaq/supervised-sessions/${encodeURIComponent(sessionKey)}/preparation/${preparation.record.recordId}`;
    async function upload() {
        setBusy(true);
        onLockChange(true);
        setMessage("");
        let requestPrepared = pending !== null;
        try {
            let body = pending;
            if (!body) {
                if (
                    !file ||
                    file.type !== "image/png" ||
                    file.size > 4 * 1024 * 1024 ||
                    !file.size
                )
                    throw new Error(
                        "Selecciona un PNG original de hasta 4 MiB.",
                    );
                const encoded = await new Promise<string>((resolve, reject) => {
                    const reader = new FileReader();
                    reader.onload = () =>
                        resolve(String(reader.result).split(",")[1]);
                    reader.onerror = () =>
                        reject(
                            new Error(
                                "No se pudo leer el archivo seleccionado.",
                            ),
                        );
                    reader.readAsDataURL(file);
                });
                body = JSON.stringify({
                    submissionId: crypto.randomUUID(),
                    preparationRevision: preparation.record.revision,
                    expectedRevision: preparation.revisionHash,
                    source: {
                        originalFilename: file.name,
                        contentType: file.type,
                        sourceProviderId: provider,
                        originalSymbol: symbol,
                        sourceDescription: description,
                        revisionReason: reason,
                        observationReference: observation || null,
                        originalTime: time || null,
                        originalClockDomain: clock || null,
                        timePrecision: precision,
                    },
                    bytes: encoded,
                });
                setPending(body);
                requestPrepared = true;
            }
            const response = await fetch(base + "/artifacts", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-MoneyWay-Local-Capture": "1",
                },
                body,
            });
            if (!response.ok) {
                const error = await response.json().catch(() => ({}));
                if (
                    response.status < 500 &&
                    !(
                        response.status === 409 &&
                        String(error.detail).includes("EvidenceStoreBusy")
                    )
                ) {
                    requestPrepared = false;
                    setPending(null);
                    onLockChange(false);
                }
                throw new Error(
                    `${error.code ?? response.status}: ${error.detail ?? (error.errors ? Object.values(error.errors).flat().join(" ") : "Adjunto no disponible")}`,
                );
            }
            const saved = (await response.json()) as PreparationView;
            setPending(null);
            onLockChange(false);
            onAttached(saved);
        } catch (e) {
            setMessage(e instanceof Error ? e.message : "Error de adjunto");
            if (!requestPrepared) onLockChange(false);
        } finally {
            setBusy(false);
        }
    }
    async function inspect(artifact: PreparationArtifact) {
        setBusy(true);
        onLockChange(true);
        setMessage("");
        try {
            const route = `${base}/revisions/${preparation.record.revision}/artifacts/${artifact.artifactId}`;
            const metadata = await fetch(route);
            if (!metadata.ok)
                throw new Error(`Metadatos no disponibles: ${metadata.status}`);
            const actual = (await metadata.json()) as PreparationArtifact;
            if (
                actual.sha256 !== artifact.sha256 ||
                actual.byteLength !== artifact.byteLength ||
                actual.artifactId !== artifact.artifactId
            )
                throw new Error("Identidad del adjunto no coincide.");
            const response = await fetch(route + "/content");
            if (
                !response.ok ||
                response.headers.get("content-type") !== "image/png"
            )
                throw new Error(
                    `Original no disponible o integridad inválida: ${response.status}`,
                );
            const bytes = await response.arrayBuffer();
            const hash = Array.from(
                new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)),
            )
                .map((b) => b.toString(16).padStart(2, "0"))
                .join("");
            if (
                bytes.byteLength !== actual.byteLength ||
                hash !== actual.sha256
            )
                throw new Error(
                    "Los bytes no coinciden con SHA256/tamaño del adjunto.",
                );
            setPreview({
                url: URL.createObjectURL(
                    new Blob([bytes], { type: "image/png" }),
                ),
                filename: actual.source.originalFilename,
            });
            setMessage(
                "Original recuperado; tamaño y SHA256 comprobados. Contenido de mercado no verificado.",
            );
        } catch (e) {
            setPreview(null);
            setMessage(
                e instanceof Error ? e.message : "Original no disponible",
            );
        } finally {
            setBusy(false);
            onLockChange(false);
        }
    }
    return (
        <section aria-label="Artefactos originales de preparación">
            <h5>
                Artefactos originales · revisión {preparation.record.revision}
            </h5>
            <p>
                Artefacto preservado ≠ evidencia de mercado independientemente
                verificada. Integridad SHA256 ≠ disponibilidad UTC histórica.
                Preparación documentada ≠ estrategia ready.
            </p>
            <p>
                PNG RGB/RGBA de 8 bits, sin entrelazado, hasta 4 MiB y 16
                megapíxeles. Guarda primero los cambios de observaciones: el
                adjunto se asocia a la revisión persistida y crea una nueva
                revisión. Sin OCR ni interpretación automática.
            </p>
            <ul>
                {(preparation.record.artifacts ?? []).map((a) => (
                    <li key={a.artifactId}>
                        <p>
                            {a.source.originalFilename} · {a.byteLength} bytes ·
                            SHA256 {a.sha256} · ID MoneyWay {a.artifactId} ·
                            agregado en revisión {a.attachedInRevision}.
                        </p>
                        <p>
                            {a.source.sourceProviderId}/
                            {a.source.originalSymbol} ·{" "}
                            {a.source.sourceDescription} · observación{" "}
                            {a.source.observationReference ??
                                "preparación general"}{" "}
                            · hora original{" "}
                            {a.source.originalTime ?? "desconocida"} ·{" "}
                            {a.source.originalClockDomain ??
                                "reloj desconocido"}{" "}
                            · {a.source.timePrecision}.
                        </p>
                        <p>
                            Recepción {a.receivedAtUtc} · {a.receiptClockSource}
                            . Fuente no verificada; admisión DataUnavailable.
                        </p>
                        <button
                            type="button"
                            disabled={disabled || busy || !!pending}
                            onClick={() => void inspect(a)}
                        >
                            Inspeccionar original {a.source.originalFilename}
                        </button>
                    </li>
                ))}
            </ul>
            {preview && (
                <figure>
                    <img
                        src={preview.url}
                        alt="Artefacto cargado por el operador; contenido de mercado no verificado"
                        style={{ maxWidth: "100%" }}
                    />
                    <figcaption>
                        <a href={preview.url} download={preview.filename}>
                            Descargar bytes originales {preview.filename}
                        </a>
                    </figcaption>
                </figure>
            )}
            <form
                onSubmit={(e) => {
                    e.preventDefault();
                    void upload();
                }}
            >
                <fieldset disabled={disabled || busy || !!pending}>
                    <legend>Adjuntar a nueva revisión documental</legend>
                    <label>
                        Archivo PNG original
                        <input
                            type="file"
                            accept="image/png,.png"
                            required
                            onChange={(e) =>
                                setFile(e.target.files?.[0] ?? null)
                            }
                        />
                    </label>
                    <label>
                        Proveedor original del adjunto
                        <input
                            required
                            value={provider}
                            onChange={(e) => setProvider(e.target.value)}
                        />
                    </label>
                    <label>
                        Instrumento original del adjunto
                        <input
                            required
                            value={symbol}
                            onChange={(e) => setSymbol(e.target.value)}
                        />
                    </label>
                    <label>
                        Descripción de fuente del adjunto
                        <input
                            required
                            value={description}
                            onChange={(e) => setDescription(e.target.value)}
                        />
                    </label>
                    <label>
                        Motivo de revisión con adjunto
                        <input
                            required
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                        />
                    </label>
                    <label>
                        Observación asociada
                        <select
                            value={observation}
                            onChange={(e) => setObservation(e.target.value)}
                        >
                            <option value="">Preparación general</option>
                            <option value="H4">Contexto H4</option>
                            {preparation.record.evidence.references.map((r) => (
                                <option
                                    key={r.localReferenceId}
                                    value={r.localReferenceId}
                                >
                                    {r.localReferenceId} · {r.category}/
                                    {r.role ?? "desconocido"}
                                </option>
                            ))}
                        </select>
                    </label>
                    <label>
                        Hora original del adjunto (opcional)
                        <input
                            value={time}
                            onChange={(e) => setTime(e.target.value)}
                        />
                    </label>
                    <label>
                        Dominio de reloj original del adjunto (opcional)
                        <input
                            value={clock}
                            onChange={(e) => setClock(e.target.value)}
                        />
                    </label>
                    <label>
                        Precisión temporal del adjunto
                        <select
                            value={precision}
                            onChange={(e) => setPrecision(e.target.value)}
                        >
                            {[
                                "Unknown",
                                "Approximate",
                                "SourceReportedSecond",
                                "ExactNativeRecord",
                            ].map((p) => (
                                <option key={p}>{p}</option>
                            ))}
                        </select>
                    </label>
                    <button type="submit">
                        Preservar adjunto en nueva revisión
                    </button>
                </fieldset>
            </form>
            {pending && !busy && (
                <button type="button" onClick={() => void upload()}>
                    Reintentar mismo adjunto
                </button>
            )}
            {message && <p role="status">{message}</p>}
        </section>
    );
}
