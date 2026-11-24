import { sessionConfiguration, sessionOverlays } from "./sessionOverlays";

export function SessionOverlayLegend({
    sessions,
    bands,
    levels,
    h4,
    onBands,
    onLevels,
}: {
    sessions: ReturnType<typeof sessionOverlays>;
    bands: boolean;
    levels: boolean;
    h4: boolean;
    onBands: (enabled: boolean) => void;
    onLevels: (enabled: boolean) => void;
}) {
    const time = new Intl.DateTimeFormat("es-CO", {
        timeZone: "America/Bogota",
        day: "2-digit",
        month: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        hourCycle: "h23",
    });
    const status = {
        MappingUnavailable: "Mapeo UTC H1 sin verificar",
        SessionOpen: "Sesión aún no finalizada al corte",
        Complete: "",
        Incomplete: "Cobertura H1 incompleta",
    } as const;
    const names = {
        AH: "Asia High",
        AL: "Asia Low",
        LH: "London High",
        LL: "London Low",
    };

    return (
        <fieldset className="integrated-session-overlays">
            <legend>Sesiones Asia y Londres</legend>
            <div className="integrated-controls">
                <label>
                    <input
                        type="checkbox"
                        checked={bands}
                        disabled={h4}
                        onChange={(e) => onBands(e.currentTarget.checked)}
                    />{" "}
                    Bandas de sesión
                </label>
                <label>
                    <input
                        type="checkbox"
                        checked={levels}
                        onChange={(e) => onLevels(e.currentTarget.checked)}
                    />{" "}
                    Niveles High/Low
                </label>
            </div>
            <p>
                <code>{sessionConfiguration}</code> · America/Bogota · [inicio,
                fin)
            </p>
            <p>
                Referencias documentales H1; no representan una toma de liquidez
                confirmada.
            </p>
            {h4 && (
                <p>
                    En H4 se proyectan los niveles H1. Las bandas requieren H1,
                    M5 o M1.
                </p>
            )}
            <ul className="integrated-session-list">
                {sessions.map((session) => (
                    <li key={session.name}>
                        <span
                            className="integrated-session-swatch"
                            style={{ backgroundColor: session.color }}
                        />
                        <strong>{session.name}</strong>{" "}
                        <span>
                            {time.format(new Date(session.startUtc))} →{" "}
                            {time.format(new Date(session.endUtc))}
                        </span>
                        {" · "}
                        {session.observed}/{session.expected} H1
                        {session.status !== "Complete" && (
                            <span> · {status[session.status]}</span>
                        )}
                        {levels && session.levels.length > 0 && (
                            <ul>
                                {session.levels.map((level) => (
                                    <li
                                        key={level.code}
                                        data-documentary-level={level.code}
                                    >
                                        <details>
                                            <summary>
                                                {names[level.code]} (
                                                {level.code}){" "}
                                                <b>{level.price}</b>
                                            </summary>
                                            <div>
                                                {session.provider} ·{" "}
                                                {session.instrument} ·{" "}
                                                {session.operatingDate}
                                            </div>
                                            <div>
                                                Corte {session.cutoffUtc} ·
                                                SHA-256 H1{" "}
                                                <code>
                                                    {session.sourceSha256}
                                                </code>
                                            </div>
                                            <ul>
                                                {level.sources.map((bar) => (
                                                    <li key={bar.openUtc}>
                                                        Nativa {bar.nativeOpen}{" "}
                                                        · {bar.openUtc} →{" "}
                                                        {bar.closeUtc}
                                                    </li>
                                                ))}
                                            </ul>
                                        </details>
                                    </li>
                                ))}
                            </ul>
                        )}
                    </li>
                ))}
            </ul>
        </fieldset>
    );
}
