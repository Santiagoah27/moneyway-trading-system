import { formatTime } from "../report";
import type { ContextData } from "./model";
import {
    londonAsiaAt,
    type InteractionObservation,
} from "./londonAsiaInteraction";

export function LondonAsiaSummary({
    context,
    cutoff,
}: {
    context: ContextData;
    cutoff: string;
}) {
    let result: ReturnType<typeof londonAsiaAt>;
    try {
        result = londonAsiaAt(context, cutoff);
    } catch (error) {
        return (
            <p role="alert">
                {error instanceof Error
                    ? error.message
                    : "Interacción no disponible."}
            </p>
        );
    }
    const first = (o: InteractionObservation | null) =>
        o
            ? `${formatTime(o.candle.openTimeUtc)} → ${formatTime(o.candle.closeTimeUtc)}`
            : "No observado en este prefijo";
    return (
        <section aria-label="Interacción London–Asia documental">
            <h2>London frente a Asia · OHLC descriptivo H1</h2>
            {!result ? (
                <p>Sin prefijo disponible.</p>
            ) : (
                <>
                    <p>
                        Estado: {result.status}. Clasificación completa:{" "}
                        {result.completedClassification ??
                            "No disponible; sesión parcial o datos incompletos"}
                        .
                    </p>
                    <div className="lab-chart-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th>Referencia</th>
                                    <th>Magnitud observada</th>
                                    <th>Primer contacto exacto del extremo</th>
                                    <th>Primera penetración estricta</th>
                                    <th>Primer cierre más allá</th>
                                </tr>
                            </thead>
                            <tbody>
                                {[
                                    {
                                        name: "Asia High",
                                        magnitude:
                                            result.observedUpperMagnitude,
                                        contact: result.firstUpperExactContact,
                                        penetration:
                                            result.firstUpperPenetration,
                                        close: result.firstUpperCloseBeyond,
                                    },
                                    {
                                        name: "Asia Low",
                                        magnitude:
                                            result.observedLowerMagnitude,
                                        contact: result.firstLowerExactContact,
                                        penetration:
                                            result.firstLowerPenetration,
                                        close: result.firstLowerCloseBeyond,
                                    },
                                ].map((s) => (
                                    <tr key={s.name}>
                                        <td>{s.name}</td>
                                        <td>
                                            {s.magnitude ?? "No disponible"}
                                        </td>
                                        <td>{first(s.contact)}</td>
                                        <td>{first(s.penetration)}</td>
                                        <td>{first(s.close)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                    <p>
                        Orden de primeras penetraciones:{" "}
                        {result.firstPenetrationOrdering}. Una misma H1 no
                        establece orden intrabar.
                    </p>
                    <details>
                        <summary>
                            Velas fuente y observaciones independientes
                        </summary>
                        <div className="lab-chart-scroll">
                            <table>
                                <thead>
                                    <tr>
                                        <th>Intervalo H1 UTC</th>
                                        <th>O / H / L / C</th>
                                        <th>Asia High</th>
                                        <th>Asia Low</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {result.observations.map((o) => (
                                        <tr key={o.candle.openTimeUtc}>
                                            <td>
                                                {formatTime(
                                                    o.candle.openTimeUtc,
                                                )}{" "}
                                                →{" "}
                                                {formatTime(
                                                    o.candle.closeTimeUtc,
                                                )}
                                            </td>
                                            <td>
                                                {o.candle.open} /{" "}
                                                {o.candle.high} / {o.candle.low}{" "}
                                                / {o.candle.close}
                                            </td>
                                            {[o.asiaHigh, o.asiaLow].map(
                                                (s, i) => (
                                                    <td key={i}>
                                                        Referencia{" "}
                                                        {s.referencePrice};
                                                        contacto{" "}
                                                        {String(
                                                            s.exactExtremeContact,
                                                        )}
                                                        ; penetración{" "}
                                                        {String(
                                                            s.strictPenetration,
                                                        )}
                                                        ; cierre más allá{" "}
                                                        {String(s.closeBeyond)};
                                                        penetración sin cierre
                                                        más allá{" "}
                                                        {String(
                                                            s.penetrationWithoutCloseBeyond,
                                                        )}
                                                        ; apertura más allá{" "}
                                                        {String(
                                                            s.opensBeyondReference,
                                                        )}
                                                        .
                                                    </td>
                                                ),
                                            )}
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </details>
                    <p>{result.reason}</p>
                    <p>
                        Prefijo evaluado {formatTime(result.cutoffUtc)}.
                        Disponibilidad documental; no autentica publicación ni
                        momento exacto de contacto o trayectoria de cruce.
                    </p>
                </>
            )}
            <p>
                Sin dirección BUY/SELL, relevancia, toma válida, señal M5 ni
                elegibilidad inferidas. CanonicalAdmissionPermitted=false ·
                OperationalSignal=false.
            </p>
        </section>
    );
}
