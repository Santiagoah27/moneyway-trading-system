import { useState } from "react";
import { HistoricalEvidencePackagePanel } from "./HistoricalEvidencePackagePanel";
import { ProspectivePreparationPanel } from "./ProspectivePreparationPanel";
import { StandardCanonicalDiagnosticPanel } from "./StandardCanonicalDiagnosticPanel";
import type {
    CanonicalDiagnosticReference,
    StandardDiagnosticContext,
} from "./StandardCanonicalDiagnosticPanel";

interface EvidenceSelection {
    recordId: string;
    revision: number;
    revisionHash: string;
    sessionId: string;
    tradingDay: string;
    providerId: string | null;
    symbol: string;
    scenario: string | null;
    canonicalDiagnostic?: CanonicalDiagnosticReference | null;
}
interface SessionEvent {
    revision: number;
    registeredAtUtc: string;
    command: {
        action: string;
        reviewer: string;
        sourceReference: string;
        closureKind: string | null;
        reason: string | null;
        execution: {
            recordId: string;
            revision: number;
            revisionHash: string;
        } | null;
    };
}
export interface SupervisedSessionState {
    sessionKey: string;
    sessionId: string;
    path: string;
    status: string;
    revision: number;
    revisionHash: string;
    identity: {
        tradingDay: string;
        providerId: { value: string };
        symbol: { value: string };
    };
    terminal: boolean;
    localEntryIntentPermitted: boolean;
    localGuardReason: string;
    activeSetup: string | null;
    executionConfirmation: SessionEvent | null;
    closureConfirmation: SessionEvent | null;
    events: SessionEvent[];
}
export function SupervisedSessionPanel({
    apiBaseUrl,
    evidence,
    onAssociate,
}: {
    apiBaseUrl: string;
    evidence: EvidenceSelection | null;
    onAssociate?: (context: StandardDiagnosticContext) => void;
}) {
    const [scope, setScope] = useState({
        sessionId: "",
        tradingDay: "",
        providerId: "",
        symbol: "US100",
        path: "Standard",
    });
    const [reviewer, setReviewer] = useState("");
    const [source, setSource] = useState("");
    const [setup, setSetup] = useState("");
    const [ownReference, setOwnReference] = useState<string | null>(null);
    const [closedReference, setClosedReference] = useState<string | null>(null);
    const [kind, setKind] = useState("OtherEvidencedExit");
    const [reason, setReason] = useState("");
    const [session, setSession] = useState<SupervisedSessionState | null>(null);
    const [diagnostic, setDiagnostic] =
        useState<CanonicalDiagnosticReference | null>(null);
    const confirmationReference =
        session && evidence
            ? JSON.stringify([
                  session.sessionKey,
                  evidence.recordId,
                  evidence.revision,
                  evidence.revisionHash,
              ])
            : null;
    const own =
        confirmationReference !== null &&
        ownReference === confirmationReference;
    const closed =
        confirmationReference !== null &&
        closedReference === confirmationReference;
    const [list, setList] = useState<SupervisedSessionState[]>([]);
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState("");
    const [pending, setPending] = useState<{
        path: string;
        body: unknown;
    } | null>(null);
    async function request<T>(path: string, body?: unknown): Promise<T> {
        const response = await fetch(
            `${apiBaseUrl}/api/nasdaq/supervised-sessions${path}`,
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
                `${p.code ?? response.status}: ${p.detail ?? (p.errors ? Object.values(p.errors).flat().join(" ") : "No se pudo recuperar la sesión")}`,
            );
        }
        return (await response.json()) as T;
    }
    async function work(f: () => Promise<void>) {
        setBusy(true);
        setMessage("");
        try {
            await f();
        } catch (e) {
            setMessage(e instanceof Error ? e.message : "Error de sesión");
        } finally {
            setBusy(false);
        }
    }
    async function send(path: string, body: unknown) {
        await work(async () => {
            const operation = pending ?? { path, body };
            setPending(operation);
            const value = await request<SupervisedSessionState>(
                operation.path,
                operation.body,
            );
            setSession(value);
            setPending(null);
            setMessage(
                value.terminal
                    ? "Sesión terminal: no reentrada, nuevo setup ni segunda operación. Evidencia documental sigue disponible."
                    : "Estado operativo registrado. No autoriza órdenes ni modifica el replay.",
            );
        });
    }
    function action(name: string) {
        if (!session) return;
        void send(`/${session.sessionKey}/actions`, {
            submissionId: crypto.randomUUID(),
            action: name,
            reviewer,
            sourceReference: source,
            ...(name === "EntryIntent" &&
            diagnostic?.sessionKey === session.sessionKey
                ? { canonicalDiagnostic: diagnostic }
                : {}),
            ...(name === "ActivateSetup" ||
            name === "InvalidateSetup" ||
            name === "EntryIntent"
                ? { setupReference: setup }
                : {}),
            ...(name === "ConfirmExecution" || name === "ConfirmClosure"
                ? {
                      execution: evidence
                          ? {
                                recordId: evidence.recordId,
                                revision: evidence.revision,
                                revisionHash: evidence.revisionHash,
                            }
                          : null,
                  }
                : {}),
            ...(name === "ConfirmExecution"
                ? { moneyWayOwnedExecutionConfirmed: own }
                : {}),
            ...(name === "ConfirmClosure"
                ? {
                      moneyWayTradeClosedConfirmed: closed,
                      closureKind: kind,
                      reason,
                  }
                : {}),
        });
    }
    return (
        <section
            aria-label="Sesión operativa supervisada"
            className="execution-capture"
        >
            <h3>Sesión Nasdaq supervisada · una operación real</h3>
            <HistoricalEvidencePackagePanel
                apiBaseUrl={apiBaseUrl}
                onScope={(p) => {
                    setScope({
                        sessionId: p.sessionId,
                        tradingDay: p.tradingDay,
                        providerId: p.providerId,
                        symbol: p.originalSymbol,
                        path: "Standard",
                    });
                    setSession(null);
                    setDiagnostic(null);
                    setMessage(
                        "Identidad documental seleccionada. El paquete no autoriza replay ni operación; abre o recupera la sesión mediante su acción explícita.",
                    );
                }}
            />
            <p>
                Después del cierre confirmado de la primera operación propia de
                MoneyWay: sin reentrada, nuevo setup ni segunda operación en esa
                sesión. El mentor, el P&amp;L reportado y el SL protegido no
                cierran esta sesión automáticamente.
            </p>
            <p>
                Ámbito local por proveedor/instrumento/fecha Bogotá; cuenta
                desconocida. Sin equivalencia USTEC/US100 ni bloqueo global de
                otras cuentas. No envía órdenes, no verifica broker y no
                convierte declaraciones en autorización histórica.
            </p>
            <button
                type="button"
                disabled={!evidence || busy || !!pending}
                onClick={() => {
                    if (evidence) {
                        setScope({
                            sessionId: evidence.sessionId,
                            tradingDay: evidence.tradingDay,
                            providerId: evidence.providerId ?? "",
                            symbol: evidence.symbol,
                            path:
                                evidence.scenario === "WickfillScenario2"
                                    ? "WickfillScenario2"
                                    : "Standard",
                        });
                        setSession(null);
                    }
                }}
            >
                Usar ámbito del registro recuperado
            </button>
            <fieldset disabled={busy || !!pending}>
                <legend>Identidad y procedencia operativas</legend>
                <div className="capture-grid">
                    <label>
                        Sesión: identificador declarado
                        <input
                            value={scope.sessionId}
                            onChange={(e) =>
                                setScope({
                                    ...scope,
                                    sessionId: e.target.value,
                                })
                            }
                        />
                    </label>
                    <label>
                        Sesión: fecha America/Bogota
                        <input
                            type="date"
                            value={scope.tradingDay}
                            onChange={(e) =>
                                setScope({
                                    ...scope,
                                    tradingDay: e.target.value,
                                })
                            }
                        />
                    </label>
                    <label>
                        Sesión: proveedor conocido declarado
                        <input
                            value={scope.providerId}
                            onChange={(e) =>
                                setScope({
                                    ...scope,
                                    providerId: e.target.value,
                                })
                            }
                        />
                    </label>
                    <label>
                        Sesión: instrumento
                        <select
                            value={scope.symbol}
                            onChange={(e) =>
                                setScope({ ...scope, symbol: e.target.value })
                            }
                        >
                            <option>US100</option>
                            <option>USTEC</option>
                        </select>
                    </label>
                    <label>
                        Sesión: ruta
                        <select
                            value={scope.path}
                            onChange={(e) =>
                                setScope({ ...scope, path: e.target.value })
                            }
                        >
                            <option>Standard</option>
                            <option>WickfillScenario2</option>
                        </select>
                    </label>
                    <label>
                        Sesión: operador declarante
                        <input
                            value={reviewer}
                            onChange={(e) => setReviewer(e.target.value)}
                        />
                    </label>
                    <label>
                        Sesión: fuente operativa (note: o https://)
                        <input
                            value={source}
                            onChange={(e) => setSource(e.target.value)}
                        />
                    </label>
                </div>
                <button
                    type="button"
                    onClick={() =>
                        void send("", {
                            submissionId: crypto.randomUUID(),
                            session: {
                                ...scope,
                                reviewer,
                                sourceReference: source,
                            },
                        })
                    }
                >
                    Abrir o recuperar ámbito de sesión
                </button>
                <button
                    type="button"
                    disabled={!scope.tradingDay}
                    onClick={() =>
                        void work(async () =>
                            setList(
                                await request<SupervisedSessionState[]>(
                                    `?tradingDay=${scope.tradingDay}`,
                                ),
                            ),
                        )
                    }
                >
                    Listar sesiones de la fecha
                </button>
            </fieldset>
            <ul>
                {list.map((s) => (
                    <li key={s.sessionKey}>
                        <button
                            disabled={busy}
                            onClick={() =>
                                void work(async () =>
                                    setSession(
                                        await request<SupervisedSessionState>(
                                            `/${s.sessionKey}`,
                                        ),
                                    ),
                                )
                            }
                        >
                            {s.sessionId} · {s.identity.tradingDay} ·{" "}
                            {s.identity.providerId.value}/
                            {s.identity.symbol.value} · {s.status}
                        </button>
                    </li>
                ))}
            </ul>
            {session && (
                <section aria-label="Estado de sesión supervisada">
                    {session.path === "Standard" && (
                        <ProspectivePreparationPanel
                            key={session.sessionKey}
                            apiBaseUrl={apiBaseUrl}
                            session={session}
                        />
                    )}
                    {session.path === "Standard" && (
                        <StandardCanonicalDiagnosticPanel
                            key={session.sessionKey}
                            apiBaseUrl={apiBaseUrl}
                            session={session}
                            savedReference={evidence?.canonicalDiagnostic}
                            onReference={setDiagnostic}
                            onAssociate={onAssociate}
                        />
                    )}
                    <h4>
                        {session.terminal
                            ? "TERMINAL · SIN REENTRADA"
                            : session.status}
                    </h4>
                    <p>
                        {session.identity.tradingDay} · America/Bogota ·{" "}
                        {session.path} · {session.identity.providerId.value}/
                        {session.identity.symbol.value}. Sesión:{" "}
                        {session.sessionId}. Revisión: {session.revision} ·{" "}
                        {session.revisionHash}.
                    </p>
                    <p>
                        Guardia local: {session.localGuardReason}. Ventana de
                        entrada implementada y en disputa: [08:30, 11:00)
                        Bogotá; la instrucción humana actual establece
                        08:30–11:30. No se altera el runtime ni se verifica
                        autorización por asumir un cutoff. Calendario,
                        preparación y demás reglas siguen siendo requisitos
                        independientes. Pasar esta guardia no equivale a Ready.
                    </p>
                    <p>
                        Operación MoneyWay registrada:{" "}
                        {session.executionConfirmation
                            ? "Confirmada por operador; no verificada por broker"
                            : "No confirmada"}
                        . Cierre propio:{" "}
                        {session.closureConfirmation
                            ? "Confirmado por operador"
                            : "No confirmado"}
                        .
                    </p>
                    {session.closureConfirmation && (
                        <p>
                            Cierre:{" "}
                            {session.closureConfirmation.command.closureKind} ·
                            Motivo: {session.closureConfirmation.command.reason}{" "}
                            · Operador:{" "}
                            {session.closureConfirmation.command.reviewer} ·
                            Fuente:{" "}
                            {
                                session.closureConfirmation.command
                                    .sourceReference
                            }{" "}
                            · Decisión operativa registrada:{" "}
                            {session.closureConfirmation.registeredAtUtc}. No es
                            un instante histórico exacto de ejecución.
                        </p>
                    )}
                    <button
                        disabled={busy}
                        onClick={() =>
                            void work(async () =>
                                setSession(
                                    await request<SupervisedSessionState>(
                                        `/${session.sessionKey}`,
                                    ),
                                ),
                            )
                        }
                    >
                        Recuperar estado persistido
                    </button>
                    <fieldset disabled={busy || !!pending}>
                        <legend>Acciones supervisadas, sin órdenes</legend>
                        <label>
                            Sesión: referencia de setup supervisado
                            <input
                                value={setup}
                                onChange={(e) => setSetup(e.target.value)}
                            />
                        </label>
                        <button
                            disabled={session.terminal || !setup}
                            onClick={() => action("ActivateSetup")}
                        >
                            Registrar setup supervisado
                        </button>
                        <button
                            disabled={session.terminal || !setup}
                            onClick={() => action("InvalidateSetup")}
                        >
                            Invalidar setup sin operación
                        </button>
                        <button
                            disabled={session.terminal || !setup}
                            onClick={() => action("EntryIntent")}
                        >
                            Registrar intento de entrada sin orden
                        </button>
                        {session.terminal && (
                            <button
                                disabled={!setup}
                                onClick={() => action("EntryIntent")}
                            >
                                Comprobar rechazo de reentrada
                            </button>
                        )}
                        <p>
                            Evidencia seleccionada:{" "}
                            {evidence
                                ? `${evidence.recordId} · revisión ${evidence.revision}`
                                : "Recupera primero un registro de ejecución"}
                            . El registro documental por sí solo no confirma
                            propiedad MoneyWay.
                        </p>
                        <label>
                            <input
                                type="checkbox"
                                checked={own}
                                onChange={(e) =>
                                    setOwnReference(
                                        e.target.checked
                                            ? confirmationReference
                                            : null,
                                    )
                                }
                            />
                            Confirmo que MoneyWay ejecutó su propia operación
                            supervisada, según la fuente declarada
                        </label>
                        <button
                            disabled={
                                session.terminal ||
                                !!session.executionConfirmation ||
                                !own ||
                                !evidence
                            }
                            onClick={() => action("ConfirmExecution")}
                        >
                            Registrar ejecución propia ya realizada
                        </button>
                        <label>
                            <input
                                type="checkbox"
                                checked={closed}
                                onChange={(e) =>
                                    setClosedReference(
                                        e.target.checked
                                            ? confirmationReference
                                            : null,
                                    )
                                }
                            />
                            Confirmo que la operación propia está cerrada, no
                            solo protegida ni con P&amp;L reportado
                        </label>
                        <label>
                            Sesión: tipo de cierre
                            <select
                                value={kind}
                                onChange={(e) => setKind(e.target.value)}
                            >
                                <option value="TakeProfit">
                                    Take Profit reportado
                                </option>
                                <option value="StopLoss">
                                    Stop Loss reportado
                                </option>
                                <option value="BreakEven">
                                    Break-even reportado
                                </option>
                                <option value="ProtectedOneToOne">
                                    Salida protegida 1:1 reportada; retorno no
                                    calculado
                                </option>
                                <option value="OtherEvidencedExit">
                                    Otro cierre con evidencia
                                </option>
                            </select>
                        </label>
                        <label>
                            Sesión: motivo de terminación
                            <input
                                value={reason}
                                onChange={(e) => setReason(e.target.value)}
                            />
                        </label>
                        <button
                            disabled={
                                session.terminal ||
                                !session.executionConfirmation ||
                                !closed ||
                                !evidence ||
                                !reason
                            }
                            onClick={() => action("ConfirmClosure")}
                        >
                            Confirmar cierre propio y terminar sesión
                        </button>
                    </fieldset>
                    <details>
                        <summary>Eventos y evidencia originales</summary>
                        <pre>{JSON.stringify(session.events, null, 2)}</pre>
                    </details>
                    <p>
                        La evidencia histórica y sus correcciones siguen
                        disponibles aunque la sesión sea terminal. No hay
                        reapertura automática ni botón de desbloqueo.
                    </p>
                </section>
            )}
            {pending && !busy && (
                <button onClick={() => void send(pending.path, pending.body)}>
                    Reintentar misma acción de sesión
                </button>
            )}
            {message && <p role="status">{message}</p>}
        </section>
    );
}
