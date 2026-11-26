import { useEffect, useState } from "react";
import "./NasdaqReceiptCutExplorer.css";

const frames = ["H4", "H1", "M5", "M1"] as const;
type Frame = (typeof frames)[number];
type NativeOpen = { raw: number; display: string };
type RegisteredCut = { id: string; sequence: number; reportSha256: string };
type Sample = {
    timeframe: Frame;
    openTimeNative: NativeOpen;
    localReceiptClockLabel: string | null;
    laterHistoricalRevisions: number;
    spansInterruption: boolean;
    firstUsableHistorical: {
        sourcePosition: number;
        receipt: {
            sequence: number;
            recordSha256: string;
            probeSha256: string;
        };
        values: { open: string; high: string; low: string; close: string };
    };
};
type Diagnostic = {
    expectedSource: {
        company: string;
        server: string;
        nativeSymbol: string;
        demo: boolean;
    };
    directoryScope: string;
    anchor: { sequence: number; recordSha256: string } | null;
    cutReceipt: { sequence: number; sha256: string };
    cutLocalReceiptClockLabel: string | null;
    prefixManifestSha256: string;
    integrity: string;
    observationContinuity: string;
    standardEvaluation: string;
    standardEvaluationReason: string;
    nativeToUtcMapping: string;
    providerFinalization: string;
    marketAvailabilityUtc: string;
    humanReviewStatus: string;
    canonicalReady: false;
    canonicalAdmissionPermitted: false;
    operationalSignal: false;
    samples: Sample[];
    exclusions: {
        timeframe: Frame;
        openTimeNative: NativeOpen;
        reason: string;
    }[];
    nativeGaps: {
        timeframe: Frame;
        earlierOpen: NativeOpen;
        laterOpen: NativeOpen;
        observedDeltaSeconds: number;
    }[];
    timeframes: {
        timeframe: Frame;
        headRegressions: number;
        nonPeriodHeadAdvancements: number;
        skippedFrames: number;
    }[];
    notices: { sequence: number; reason: string }[];
    diagnostics: string[];
    excludedHumanReviews: {
        claimedReceiptSequence: number;
        reference: string;
    }[];
};
type Cut = { id: string; reportSha256: string; diagnostic: Diagnostic };

function issue(code: string) {
    switch (code) {
        case "NoAdmissibleClosedUtcSeriesOrVerifiedTemporalBasisForStandard":
            return "Falta una serie de velas cerradas con base temporal UTC verificable.";
        case "NoVerifiedReviewBoundToReceiptCut":
            return "Sin revisión humana verificable vinculada al corte.";
        case "position_zero_only":
            return "Solo observada en posición 0";
        case "no_usable_historical_observation":
            return "Sin observación histórica utilizable";
        case "DiscontinuityOrAcquisitionIssueObserved":
            return "Se observó una discontinuidad o incidencia de adquisición.";
        case "ContinuityNotEstablishedByReceiptChain":
            return "La cadena de recibos no acredita continuidad de adquisición.";
        default:
            return code;
    }
}

async function read<T>(url: string, signal: AbortSignal): Promise<T> {
    const response = await fetch(url, { signal, cache: "no-store" });
    if (!response.ok) {
        if (response.status === 422)
            throw new Error(
                "El reporte no pasó la verificación de identidad o integridad.",
            );
        if (response.status === 404)
            throw new Error("El corte configurado no está disponible.");
        throw new Error("No se pudo consultar la API local de cortes.");
    }
    return response.json();
}

export function NasdaqReceiptCutExplorer({
    apiBaseUrl,
}: {
    apiBaseUrl: string;
}) {
    const [open, setOpen] = useState(false);
    return (
        <section className="receipt-cut" aria-labelledby="receipt-cut-heading">
            <h3 id="receipt-cut-heading">Cortes de recibos MT5</h3>
            <button
                type="button"
                aria-expanded={open}
                onClick={() => setOpen(!open)}
            >
                {open
                    ? "Cerrar explorador de cortes"
                    : "Explorar cortes de recibos"}
            </button>
            {open && (
                <Explorer
                    key={apiBaseUrl}
                    url={`${apiBaseUrl}/api/nasdaq/historical-demo/receipt-cuts`}
                />
            )}
        </section>
    );
}

function Explorer({ url }: { url: string }) {
    const [registry, setRegistry] = useState<RegisteredCut[]>();
    const [error, setError] = useState<string>();
    const [primary, setPrimary] = useState("");
    const [comparison, setComparison] = useState("");
    const selected = registry?.find((cut) => cut.id === primary);
    const compared = registry?.find((cut) => cut.id === comparison);
    const first = useCut(url, selected);
    const second = useCut(url, compared);
    useEffect(() => {
        const controller = new AbortController();
        void read<RegisteredCut[]>(url, controller.signal)
            .then((entries) => {
                if (controller.signal.aborted) return;
                setRegistry(entries);
                setPrimary(entries[0]?.id ?? "");
            })
            .catch(() => {
                if (!controller.signal.aborted)
                    setError(
                        "No se pudo cargar el registro de cortes de la API local.",
                    );
            });
        return () => controller.abort();
    }, [url]);
    if (error) return <p role="alert">{error}</p>;
    if (!registry) return <p role="status">Cargando cortes configurados.</p>;
    if (registry.length === 0)
        return <p>No hay cortes registrados en la API local.</p>;
    return (
        <>
            <p>
                Las coordenadas nativas MT5 no son UTC autenticada. Las horas
                del host son etiquetas locales de observación.
            </p>
            <div className="receipt-cut-select">
                <label>
                    Corte de recibos
                    <select
                        value={primary}
                        onChange={(event) => {
                            const id = event.target.value;
                            setPrimary(id);
                            if (id === comparison) setComparison("");
                        }}
                    >
                        {registry.map((cut) => (
                            <option key={cut.id} value={cut.id}>
                                {cut.sequence} · {cut.id}
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Comparar con
                    <select
                        value={comparison}
                        onChange={(event) => setComparison(event.target.value)}
                    >
                        <option value="">Sin comparación</option>
                        {registry
                            .filter((cut) => cut.id !== primary)
                            .map((cut) => (
                                <option key={cut.id} value={cut.id}>
                                    {cut.sequence} · {cut.id}
                                </option>
                            ))}
                    </select>
                </label>
            </div>
            {first?.error && <p role="alert">{first.error}</p>}
            {second?.error && <p role="alert">{second.error}</p>}
            {(!first || (compared && !second)) && (
                <p role="status">Verificando el corte de recibos.</p>
            )}
            {first?.cut && second?.cut && (
                <Comparison
                    first={first.cut.diagnostic}
                    second={second.cut.diagnostic}
                />
            )}
            {first?.cut && <CutDetails key={first.cut.id} cut={first.cut} />}
            {second?.cut && <CutDetails key={second.cut.id} cut={second.cut} />}
        </>
    );
}

function useCut(url: string, selected: RegisteredCut | undefined) {
    const [result, setResult] = useState<{
        selection: RegisteredCut;
        cut?: Cut;
        error?: string;
    }>();
    useEffect(() => {
        if (!selected) return;
        const controller = new AbortController();
        async function load(selection: RegisteredCut) {
            try {
                const cut = await read<Cut>(
                    `${url}/${encodeURIComponent(selection.id)}`,
                    controller.signal,
                );
                if (
                    cut.id !== selection.id ||
                    cut.reportSha256 !== selection.reportSha256 ||
                    cut.diagnostic.cutReceipt.sequence !== selection.sequence
                )
                    throw new Error(
                        "El corte cambió. Cierra y vuelve a abrir el explorador para recargar el registro.",
                    );
                if (!controller.signal.aborted) setResult({ selection, cut });
            } catch (failure) {
                if (!controller.signal.aborted)
                    setResult({
                        selection,
                        error:
                            failure instanceof Error
                                ? failure.message
                                : "Corte no disponible.",
                    });
            }
        }
        void load(selected);
        return () => controller.abort();
    }, [selected, url]);
    return result?.selection === selected ? result : undefined;
}

function sampleKey(sample: Sample) {
    return `${sample.timeframe}:${sample.openTimeNative.raw}`;
}

function Comparison({
    first,
    second,
}: {
    first: Diagnostic;
    second: Diagnostic;
}) {
    const [earlier, later] =
        first.cutReceipt.sequence < second.cutReceipt.sequence
            ? [first, second]
            : [second, first];
    if (
        earlier.directoryScope !== later.directoryScope ||
        earlier.anchor?.recordSha256 !== later.anchor?.recordSha256 ||
        earlier.cutReceipt.sequence === later.cutReceipt.sequence
    )
        return (
            <p role="status">
                Los cortes requieren un mismo directorio de evidencia, la misma
                ancla y secuencias distintas para comparar.
            </p>
        );
    const old = new Map(
        earlier.samples.map((sample) => [sampleKey(sample), sample]),
    );
    const current = new Map(
        later.samples.map((sample) => [sampleKey(sample), sample]),
    );
    const retained = earlier.samples.filter((sample) => {
        const other = current.get(sampleKey(sample));
        return (
            other &&
            JSON.stringify(other.firstUsableHistorical) ===
                JSON.stringify(sample.firstUsableHistorical)
        );
    }).length;
    return (
        <div className="receipt-cut-comparison">
            <h4>
                Comparación de cortes {earlier.cutReceipt.sequence} y{" "}
                {later.cutReceipt.sequence}
            </h4>
            <p>
                {retained} primeras observaciones conservadas.{" "}
                {earlier.samples.length - retained} ausentes o distintas en el
                corte posterior.
            </p>
            <div className="receipt-cut-table">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Marco</th>
                            <th scope="col">Corte anterior</th>
                            <th scope="col">Corte posterior</th>
                            <th scope="col">Muestras añadidas</th>
                        </tr>
                    </thead>
                    <tbody>
                        {frames.map((frame) => (
                            <tr key={frame}>
                                <th scope="row">{frame}</th>
                                <td>
                                    {
                                        earlier.samples.filter(
                                            (sample) =>
                                                sample.timeframe === frame,
                                        ).length
                                    }
                                </td>
                                <td>
                                    {
                                        later.samples.filter(
                                            (sample) =>
                                                sample.timeframe === frame,
                                        ).length
                                    }
                                </td>
                                <td>
                                    {
                                        later.samples.filter(
                                            (sample) =>
                                                sample.timeframe === frame &&
                                                !old.has(sampleKey(sample)),
                                        ).length
                                    }
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}

function CutDetails({ cut }: { cut: Cut }) {
    const [frame, setFrame] = useState<Frame>("H4");
    const data = cut.diagnostic;
    const samples = data.samples.filter((sample) => sample.timeframe === frame);
    const oldestReceipt = (items: Sample[]) =>
        items.reduce<Sample | undefined>(
            (first, item) =>
                !first ||
                item.firstUsableHistorical.receipt.sequence <
                    first.firstUsableHistorical.receipt.sequence ||
                (item.firstUsableHistorical.receipt.sequence ===
                    first.firstUsableHistorical.receipt.sequence &&
                    item.openTimeNative.raw < first.openTimeNative.raw)
                    ? item
                    : first,
            undefined,
        );
    return (
        <article
            className="receipt-cut-detail"
            aria-label={`Corte ${data.cutReceipt.sequence}`}
        >
            <h4>Corte {data.cutReceipt.sequence}</h4>
            <dl className="definition-summary">
                <div>
                    <dt>Fuente</dt>
                    <dd>
                        {data.expectedSource.company} ·{" "}
                        {data.expectedSource.server} ·{" "}
                        {data.expectedSource.nativeSymbol} · Demo
                    </dd>
                </div>
                <div>
                    <dt>SHA-256 del recibo</dt>
                    <dd>
                        <code>{data.cutReceipt.sha256}</code>
                    </dd>
                </div>
                <div>
                    <dt>SHA-256 del reporte</dt>
                    <dd>
                        <code>{cut.reportSha256}</code>
                    </dd>
                </div>
                <div>
                    <dt>Recepción según el host</dt>
                    <dd>{data.cutLocalReceiptClockLabel ?? "No registrada"}</dd>
                </div>
                <div>
                    <dt>Integridad</dt>
                    <dd>{data.integrity}</dd>
                </div>
                <div>
                    <dt>Continuidad</dt>
                    <dd>{issue(data.observationContinuity)}</dd>
                </div>
                <div>
                    <dt>Evaluación Standard</dt>
                    <dd>
                        <code>{data.standardEvaluation}</code> ·{" "}
                        {issue(data.standardEvaluationReason)}
                    </dd>
                </div>
                <div>
                    <dt>Evidencia temporal y humana</dt>
                    <dd>
                        Mapeo UTC {data.nativeToUtcMapping} · Cierre del
                        proveedor {data.providerFinalization} · Disponibilidad
                        UTC {data.marketAvailabilityUtc}.{" "}
                        {issue(data.humanReviewStatus)}
                    </dd>
                </div>
            </dl>
            <p>
                <code>CanonicalReady={String(data.canonicalReady)}</code> ·{" "}
                <code>
                    CanonicalAdmissionPermitted=
                    {String(data.canonicalAdmissionPermitted)}
                </code>{" "}
                ·{" "}
                <code>OperationalSignal={String(data.operationalSignal)}</code>
            </p>
            <div className="receipt-cut-table">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Marco</th>
                            <th scope="col">Muestras utilizables</th>
                            <th scope="col">Primera observación utilizable</th>
                        </tr>
                    </thead>
                    <tbody>
                        {frames.map((timeframe) => {
                            const items = data.samples.filter(
                                (sample) => sample.timeframe === timeframe,
                            );
                            const first = oldestReceipt(items);
                            return (
                                <tr key={timeframe}>
                                    <th scope="row">{timeframe}</th>
                                    <td>{items.length}</td>
                                    <td>
                                        {first
                                            ? `Recibo ${first.firstUsableHistorical.receipt.sequence} · ${first.openTimeNative.display} (nativa)`
                                            : "Sin muestra"}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
            <details>
                <summary>
                    Discontinuidades y exclusiones ({data.exclusions.length})
                </summary>
                <ul>
                    {data.nativeGaps.map((gap) => (
                        <li key={`${gap.timeframe}:${gap.earlierOpen.raw}`}>
                            {gap.timeframe} · {gap.earlierOpen.display} →{" "}
                            {gap.laterOpen.display} · {gap.observedDeltaSeconds}{" "}
                            s nativos
                        </li>
                    ))}
                    {data.timeframes
                        .filter(
                            (item) =>
                                item.headRegressions ||
                                item.nonPeriodHeadAdvancements ||
                                item.skippedFrames,
                        )
                        .map((item) => (
                            <li key={item.timeframe}>
                                {item.timeframe} · {item.headRegressions}{" "}
                                regresiones, {item.nonPeriodHeadAdvancements}{" "}
                                avances fuera de periodo, {item.skippedFrames}{" "}
                                capturas de marco omitidas
                            </li>
                        ))}
                    {data.notices.map((notice, index) => (
                        <li key={index}>
                            Recibo {notice.sequence} · {issue(notice.reason)}
                        </li>
                    ))}
                    {data.diagnostics.map((reason) => (
                        <li key={reason}>{issue(reason)}</li>
                    ))}
                    {data.exclusions.map((item) => (
                        <li
                            key={`${item.timeframe}:${item.openTimeNative.raw}`}
                        >
                            {item.timeframe} · {item.openTimeNative.display} ·{" "}
                            {issue(item.reason)}
                        </li>
                    ))}
                    {data.excludedHumanReviews.map((review, index) => (
                        <li key={index}>
                            Revisión sin vínculo verificado · {review.reference}
                        </li>
                    ))}
                </ul>
            </details>
            <details>
                <summary>Inspeccionar muestras nativas</summary>
                <label>
                    Temporalidad
                    <select
                        value={frame}
                        onChange={(event) =>
                            setFrame(event.target.value as Frame)
                        }
                    >
                        {frames.map((value) => (
                            <option key={value}>{value}</option>
                        ))}
                    </select>
                </label>
                <div className="receipt-cut-table">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Apertura nativa</th>
                                <th scope="col">Primer recibo utilizable</th>
                                <th scope="col">Valores OHLC</th>
                                <th scope="col">Revisiones posteriores</th>
                            </tr>
                        </thead>
                        <tbody>
                            {samples.map((sample) => (
                                <tr key={sampleKey(sample)}>
                                    <th scope="row">
                                        {sample.openTimeNative.display}
                                        <br />
                                        {sample.openTimeNative.raw}
                                    </th>
                                    <td>
                                        {
                                            sample.firstUsableHistorical.receipt
                                                .sequence
                                        }{" "}
                                        · posición{" "}
                                        {
                                            sample.firstUsableHistorical
                                                .sourcePosition
                                        }
                                        <br />
                                        {sample.localReceiptClockLabel ??
                                            "Sin etiqueta local"}
                                        {sample.spansInterruption &&
                                            " · atraviesa una interrupción"}
                                        <details>
                                            <summary>
                                                Hashes de procedencia
                                            </summary>
                                            <div>
                                                Recibo{" "}
                                                <code>
                                                    {
                                                        sample
                                                            .firstUsableHistorical
                                                            .receipt
                                                            .recordSha256
                                                    }
                                                </code>
                                            </div>
                                            <div>
                                                Sonda{" "}
                                                <code>
                                                    {
                                                        sample
                                                            .firstUsableHistorical
                                                            .receipt.probeSha256
                                                    }
                                                </code>
                                            </div>
                                        </details>
                                    </td>
                                    <td>
                                        {Object.values(
                                            sample.firstUsableHistorical.values,
                                        ).join(" / ")}
                                    </td>
                                    <td>{sample.laterHistoricalRevisions}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </details>
        </article>
    );
}
